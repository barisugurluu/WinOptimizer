using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using WinOptimizer.App.Infrastructure;
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

    private Serilog.ILogger? _serilogLogger;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Veri dizini (journal, backups, settings, logs)
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WinOptimizer");
        Directory.CreateDirectory(baseDir);

        // Serilog yapılandırılmış dosya günlüğü (master plan Bölüm 8.3).
        _serilogLogger = LoggingBootstrap.CreateLogger(baseDir);
        Log.Logger = _serilogLogger;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
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

        // Safety katmanı (Faz 0)
        services.AddSingleton<RestorePointService>();
        services.AddSingleton<ChangeJournal>(_ =>
            new ChangeJournal(baseDir, _.GetRequiredService<ILogger<ChangeJournal>>()));
        services.AddSingleton<RegistryBackup>(_ =>
            new RegistryBackup(baseDir, _.GetRequiredService<ILogger<RegistryBackup>>()));
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

        // Orchestration
        services.AddSingleton<ModuleRegistry>();
        services.AddSingleton<JobOrchestrationEngine>();
        services.AddSingleton<RollbackService>();
        services.AddSingleton<SchedulerService>();

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
        services.AddSingleton<ManagementViewModel>();

        // Sayfalar + sekmeler
        services.AddTransient<DashboardPage>();
        services.AddTransient<ModulePage>();
        services.AddTransient<RollbackPage>();
        services.AddTransient<OverviewTab>();
        services.AddTransient<SettingsTab>();
        services.AddTransient<SchedulerTab>();
        services.AddTransient<ManagementPage>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        // Ayarları hemen yükle ve yoklama aralığını belirle.
        var settings = Services.GetRequiredService<SettingsService>();
        PollingIntervalSeconds = Math.Clamp(settings.Current.MetricsPollSeconds, 1, 60);

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

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "İşlenmemiş UI istisnası.");
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Fatal(ex, "İşlenmemiş uygulama etki alanı istisnası.");
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
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
        Log.CloseAndFlush();
    }

    /// <summary>Çözücüden bir servis alır (XAML/code-behind için kolaylık).</summary>
    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();
}
