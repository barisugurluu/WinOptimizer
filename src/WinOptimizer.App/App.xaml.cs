using System.IO;
using WinOptimizer.Native;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using WinOptimizer.App.Infrastructure;
using WinOptimizer.Safety.Diagnostics;
using WinOptimizer.App.ViewModels;
using WinOptimizer.App.Views;
using WinOptimizer.App.Views.Management;
using WinOptimizer.Modules.AppManager;
using WinOptimizer.Modules.BackupRestore;
using WinOptimizer.Modules.BootOptimizer;
using WinOptimizer.Modules.CleanEngine;
using WinOptimizer.Modules.CpuEngine;
using WinOptimizer.Modules.DevEnvironment;
using WinOptimizer.Modules.GpuOptimizer;
using WinOptimizer.Modules.HardwareMonitor;
using WinOptimizer.Modules.MemoryEngine;
using WinOptimizer.Modules.NetworkOptimizer;
using WinOptimizer.Modules.PrivacyGuard;
using WinOptimizer.Modules.RepairEngine;
using WinOptimizer.Modules.SecurityHardening;
using WinOptimizer.Modules.StorageOptimizer;
using WinOptimizer.Modules.SystemTweaker;
using WinOptimizer.Modules.UpdateEngine;
using WinOptimizer.Orchestration;
using WinOptimizer.Orchestration.Confirmation;
using WinOptimizer.Orchestration.Preflight;
using WinOptimizer.Safety;

namespace WinOptimizer.App;

/// <summary>
/// Uygulama giriş noktası. Bağımlılık enjeksiyonu kapsayıcısını kurar,
/// tüm modülleri kaydeder ve MainWindow'u başlatır.
/// (Master plan Bölüm 2.1 — katmanlı mimari, Faz 0/1.)
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Genel Bakış sekmesi canlı metrik yoklama aralığı (saniye) — ayarlardan okunur.</summary>
    public static int PollingIntervalSeconds { get; private set; } = 3;

    private static string? _dataDir;

    /// <summary>
    /// Tek örnek kilidi. Adı <c>installer/WinOptimizer.iss</c> içindeki <c>AppMutex=</c>
    /// ile <b>birebir</b> aynı olmalıdır: kurulum sihirbazı çalışan bir örneği bu mutex
    /// üzerinden görüp kapatmayı teklif eder, aksi halde yükseltme kilitli dosyalarla
    /// başarısız olur. Süreç ömrü boyunca canlı tutulması için statik alanda tutulur.
    /// </summary>
    private static Mutex? _singleInstanceMutex;

    private const string SingleInstanceMutexName = @"Global\WinOptimizerAppSingleInstance";

    /// <summary>
    /// Ana pencere gösterildi mi. Bundan önceki bir UI istisnası ölümcüldür (yutulursa
    /// arayüzsüz orphan süreç kalır); sonrasındaki ise kurtarılabilir.
    /// </summary>
    private bool _uiReady;

    private Serilog.ILogger? _serilogLogger;

    /// <summary>
    /// Uygulama girişi. <b>Tek işi</b>: istisna yakalayıcılarını her şeyden önce kurmak ve
    /// <see cref="Bootstrap"/>'ı korumalı çağırmak.
    /// </summary>
    /// <remarks>
    /// Eskiden tüm başlatma mantığı burada, try/catch OLMADAN duruyordu ve
    /// <c>OnDispatcherUnhandledException</c> istisnayı sessizce yutuyordu (<c>e.Handled = true</c>,
    /// dialog yok). <c>MainWindow.Show()</c>'a hiç gelinmediği için sonuç: <b>arayüzü olmayan,
    /// hata mesajı vermeyen, görev yöneticisinde duran bir orphan süreç</b>. Kullanıcının tek
    /// ipucu adını bilmediği bir günlük dosyasıydı. Handler'ların Bootstrap'tan ÖNCE kurulması,
    /// veri dizini oluşturma ve günlükleyici kurulumu dahil her adımı kapsar.
    /// </remarks>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            Bootstrap();
        }
        catch (Exception ex)
        {
            ShowStartupFailure(ex);
            Shutdown(1);
        }
    }

    private void Bootstrap()
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // İkinci örnek: aynı %ProgramData% dosyalarına iki elevated süreç yazmasın.
            MessageBox.Show(
                "WinOptimizer zaten çalışıyor.",
                "WinOptimizer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        // Veri dizini (journal, backups, settings, logs)
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WinOptimizer");
        Directory.CreateDirectory(baseDir);
        _dataDir = baseDir;

        // Serilog yapılandırılmış dosya günlüğü (master plan Bölüm 8.3).
        _serilogLogger = LoggingBootstrap.CreateLogger(baseDir, LoggingBootstrap.AppPrefix);
        Log.Logger = _serilogLogger;
        Log.Information("WinOptimizer başlatılıyor. Veri dizini: {BaseDir}", baseDir);

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddSerilog(_serilogLogger, dispose: false);
        });

        // Ayarlar (ilk yüklenecek — diğer kayıtlar Current okuyabilir).
        services.AddSingleton<SettingsService>(_ =>
            new SettingsService(baseDir, _.GetRequiredService<ILogger<SettingsService>>()));

        // Safety katmanı (Faz 0) — bütünlük koruyucu önce kurulur (§17.4).
        services.AddSingleton<IntegrityGuard>(_ => new IntegrityGuard(
            IntegrityKeyStore.LoadOrCreate(baseDir), _.GetRequiredService<ILogger<IntegrityGuard>>()));
        services.AddSingleton<RestorePointService>();
        services.AddSingleton<ChangeJournal>(_ =>
            new ChangeJournal(baseDir, _.GetRequiredService<ILogger<ChangeJournal>>(), _.GetRequiredService<IntegrityGuard>()));
        services.AddSingleton<RegistryBackup>(_ =>
            new RegistryBackup(baseDir, _.GetRequiredService<ILogger<RegistryBackup>>(), _.GetRequiredService<IntegrityGuard>()));
        services.AddSingleton<SafetyGuard>();
        services.AddSingleton<SafetyNet>();
        services.AddSingleton<ProcessRunner>();

        // Modüller (Faz 1–5)
        services.AddSingleton<CleanEngineModule>();
        services.AddSingleton<MemoryEngineModule>();
        services.AddSingleton<CpuEngineModule>();
        services.AddSingleton<RepairEngineModule>();
        services.AddSingleton<SystemTweakerModule>();
        services.AddSingleton<HardwareMonitorModule>();
        services.AddSingleton<StorageOptimizerModule>();
        services.AddSingleton<PrivacyGuardModule>();
        services.AddSingleton<NetworkOptimizerModule>();
        services.AddSingleton<BootOptimizerModule>();
        services.AddSingleton<AppManagerModule>();
        services.AddSingleton<UpdateEngineModule>();
        services.AddSingleton<SecurityHardeningModule>();
        services.AddSingleton<BackupRestoreModule>();
        services.AddSingleton<GpuOptimizerModule>();
        services.AddSingleton<DevEnvironmentModule>();

        // Onay mercii — riskli eylemler için tek politika (Orchestration.ConfirmationGate),
        // iki çağıran: JobOrchestrationEngine (tek-tık) ve ModulePageViewModel (modül sayfası).
        services.AddSingleton<IActionConfirmation, DialogActionConfirmation>();

        // Orchestration
        services.AddSingleton<ModuleRegistry>();
        services.AddSingleton<JobOrchestrationEngine>();
        services.AddSingleton<RollbackService>();
        services.AddSingleton<SchedulerService>();
        // RealtimeGuard hizmetini uygulama içinden kurma/başlatma/durdurma.
        services.AddSingleton<GuardServiceController>();
        services.AddSingleton<SystemRequirementsChecker>(_ => new SystemRequirementsChecker(
            baseDir,
            _.GetRequiredService<GuardServiceController>(),
            _.GetRequiredService<RestorePointService>(),
            _.GetRequiredService<ILogger<SystemRequirementsChecker>>()));
        // Teşhis paketi gereksinim raporunu da içersin diye checker'a delege ile bağlanır
        // (tersi olsaydı DiagnosticsPackageBuilder Preflight'a bağımlı olurdu).
        services.AddSingleton<DiagnosticsPackageBuilder>(sp =>
            new DiagnosticsPackageBuilder(
                baseDir,
                sp.GetRequiredService<ILogger<DiagnosticsPackageBuilder>>(),
                () => sp.GetRequiredService<SystemRequirementsChecker>()
                        .RunAsync().GetAwaiter().GetResult().ToPlainText()));

        // Yönetim merkezi altyapısı
        services.AddSingleton<GuardPipeClient>();
        services.AddSingleton<LiveMetricsProvider>();
        services.AddSingleton<Wpf.Ui.IPageService>(sp => new PageService(sp));

        // UI ViewModel'ları
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ModulePageViewModel>();
        services.AddSingleton<RollbackViewModel>();
        services.AddSingleton<OverviewViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SchedulerViewModel>();
        services.AddSingleton<GuardViewModel>();
        services.AddSingleton<SystemDataViewModel>();
        services.AddSingleton<ModulesViewModel>();
        services.AddTransient<FirstRunViewModel>();
        services.AddSingleton<ManagementViewModel>();

        // Sayfalar + sekmeler
        services.AddTransient<DashboardPage>();
        services.AddTransient<ModulePage>();
        services.AddTransient<RollbackPage>();
        services.AddTransient<OverviewTab>();
        services.AddTransient<SettingsTab>();
        services.AddTransient<SchedulerTab>();
        services.AddTransient<GuardTab>();
        services.AddTransient<SystemDataTab>();
        services.AddTransient<ModulesTab>();
        services.AddTransient<ManagementPage>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        // Ayarları hemen yükle ve yoklama aralığını belirle.
        var settings = Services.GetRequiredService<SettingsService>();
        PollingIntervalSeconds = Math.Clamp(settings.Current.MetricsPollSeconds, 1, 60);

        // Dil ve tema: HER İKİSİ DE hiçbir pencere oluşturulmadan ÖNCE uygulanmalı.
        // Dil, XAML x:Static bağları çözülmeden önce ayarlanmazsa hiçbir etkisi olmaz.
        CultureBootstrap.Apply(settings.Current.Language);
        ThemeBootstrap.Apply(settings.Current.Theme);

        // SafetyNet ayarı aşağı doğru itilir (Safety, Orchestration'daki SettingsService'i
        // göremez — katmanlama). Başlangıçta ve her kaydetmede güncellenir.
        var safetyNet = Services.GetRequiredService<SafetyNet>();
        safetyNet.AutoRegistryBackup = settings.Current.SafetyNet.AutoRegistryBackup;

        // Ayar değişince yoklama aralığı ve SafetyNet bayrağı yeniden uygulansın
        // (eskiden yalnız başlangıçta okunuyordu).
        settings.SettingsChanged += (_, _) =>
        {
            PollingIntervalSeconds = Math.Clamp(settings.Current.MetricsPollSeconds, 1, 60);
            safetyNet.AutoRegistryBackup = settings.Current.SafetyNet.AutoRegistryBackup;
        };

        // Modülleri registry'e kaydet (tüm modüller — kullanıcıya tam kontrol)
        var registry = Services.GetRequiredService<ModuleRegistry>();
        registry
            .Register(Services.GetRequiredService<CleanEngineModule>())
            .Register(Services.GetRequiredService<MemoryEngineModule>())
            .Register(Services.GetRequiredService<HardwareMonitorModule>())
            .Register(Services.GetRequiredService<SystemTweakerModule>())
            .Register(Services.GetRequiredService<StorageOptimizerModule>())
            .Register(Services.GetRequiredService<NetworkOptimizerModule>())
            .Register(Services.GetRequiredService<UpdateEngineModule>())
            .Register(Services.GetRequiredService<SecurityHardeningModule>())
            .Register(Services.GetRequiredService<RepairEngineModule>())
            .Register(Services.GetRequiredService<CpuEngineModule>())
            .Register(Services.GetRequiredService<PrivacyGuardModule>())
            .Register(Services.GetRequiredService<BootOptimizerModule>())
            .Register(Services.GetRequiredService<AppManagerModule>())
            .Register(Services.GetRequiredService<BackupRestoreModule>())
            .Register(Services.GetRequiredService<GpuOptimizerModule>())
            .Register(Services.GetRequiredService<DevEnvironmentModule>());

        // İlk açılış / gereksinim kontrolü — ana pencereden ÖNCE.
        if (!ShowFirstRunIfNeeded(settings, baseDir))
        {
            // Engelleyen gereksinim veya kullanıcı vazgeçti: ana pencere açılmaz.
            Shutdown(2);
            return;
        }

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        _uiReady = true;
    }

    /// <summary>
    /// Gerekiyorsa ilk açılış penceresini gösterir.
    /// </summary>
    /// <returns>Ana pencere açılmalı mı.</returns>
    private static bool ShowFirstRunIfNeeded(SettingsService settings, string baseDir)
    {
        string currentVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        if (string.Equals(settings.Current.FirstRunCompletedVersion, currentVersion, StringComparison.Ordinal))
        {
            return true;
        }

        var viewModel = Services.GetRequiredService<FirstRunViewModel>();
        var window = new FirstRunWindow(viewModel, baseDir);
        window.ShowDialog();
        return window.Continued;
    }

    /// <summary>
    /// UI istisnası. <b>Pencere açılmadan önce</b> gelen istisna ölümcüldür: yutulursa
    /// görünmez bir orphan süreç kalır. Pencere açıldıktan sonrakiler ise uygulamayı
    /// kapatmaz ama asla sessizce yutulmaz.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (!_uiReady)
        {
            Log.Fatal(e.Exception, "Başlangıç sırasında işlenmemiş UI istisnası.");
            e.Handled = true;
            ShowStartupFailure(e.Exception);
            Shutdown(1);
            return;
        }

        Log.Error(e.Exception, "İşlenmemiş UI istisnası.");
        e.Handled = true;
        ShowRuntimeFailure(e.Exception);
    }

    /// <summary>
    /// Başlangıç hatasını kullanıcıya <b>görünür</b> biçimde bildirir.
    /// </summary>
    /// <remarks>
    /// Serilog'a ve DI'ya bağlı OLAMAZ: <see cref="Bootstrap"/> günlükleyiciyi kurmadan önce
    /// de patlayabilir (o noktada <c>Log.Logger</c> Serilog'un SilentLogger'ıdır ve
    /// <c>App.Services</c> null'dır). Bu yüzden dialogdan önce <c>%LOCALAPPDATA%</c>'ya düz
    /// metin yazılır — patlayan şey <c>%ProgramData%</c> erişiminin kendisi olabilir.
    /// </remarks>
    private void ShowStartupFailure(Exception ex)
    {
        string fallbackPath = WriteFallbackReport(ex);

        try
        {
            var dialog = new ErrorDialog(ex, _dataDir, fallbackPath, isFatal: true);
            dialog.ShowDialog();
        }
        catch (Exception dialogEx)
        {
            // Dialog bile açılamıyorsa (ör. WPF tema sözlüğü bozuk) son çare: MessageBox.
            MessageBox.Show(
                $"WinOptimizer başlatılamadı.{Environment.NewLine}{Environment.NewLine}" +
                $"{ex.GetType().Name}: {ex.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"Ayrıntılı rapor: {fallbackPath}{Environment.NewLine}" +
                $"(Hata penceresi de açılamadı: {dialogEx.Message})",
                "WinOptimizer — Başlatma Hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>Çalışma sırasındaki (ölümcül olmayan) istisnayı bildirir; uygulama açık kalır.</summary>
    private void ShowRuntimeFailure(Exception ex)
    {
        try
        {
            var dialog = new ErrorDialog(ex, _dataDir, fallbackReportPath: null, isFatal: false);
            dialog.ShowDialog();
        }
        catch (Exception dialogEx)
        {
            Log.Error(dialogEx, "Hata penceresi gösterilemedi.");
        }
    }

    /// <summary>
    /// Günlükleyici çalışmıyor olabileceği için hatayı <c>%LOCALAPPDATA%</c>'ya düz metin yazar.
    /// </summary>
    /// <returns>Yazılan dosyanın yolu; yazılamadıysa boş metin.</returns>
    private static string WriteFallbackReport(Exception ex)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinOptimizer");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "startup-error.txt");
            File.WriteAllText(path,
                $"WinOptimizer başlatma hatası{Environment.NewLine}" +
                $"Zaman   : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
                $"Sürüm   : {typeof(App).Assembly.GetName().Version}{Environment.NewLine}" +
                $"Windows : {Environment.OSVersion.VersionString}{Environment.NewLine}" +
                $"64-bit  : {Environment.Is64BitOperatingSystem}{Environment.NewLine}" +
                Environment.NewLine + ex);
            return path;
        }
        catch (Exception writeEx) when (writeEx is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "İşlenmemiş uygulama etki alanı istisnası.");
            if (_dataDir is not null)
                CrashDumper.Write(Path.Combine(_dataDir, "dumps"), ex);
        }
        else
            Log.Fatal("İşlenmemiş etki alanı istisnası (nesne): {Obj}", e.ExceptionObject);
        Log.CloseAndFlush();
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Gözlemlenmemiş Task istisnası.");
        e.SetObserved();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        Log.Information("WinOptimizer kapatılıyor.");
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
        Log.CloseAndFlush();
    }

    /// <summary>Çözücüden bir servis alır (XAML/code-behind için kolaylık).</summary>
    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();
}
