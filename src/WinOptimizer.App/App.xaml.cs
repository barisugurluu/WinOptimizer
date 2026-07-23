using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinOptimizer.App.ViewModels;
using WinOptimizer.App.Views;
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

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();
        });

        // Veri dizini (journal, backups, settings)
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WinOptimizer");
        Directory.CreateDirectory(baseDir);

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

        // UI ViewModel + pencere
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ModulePageViewModel>();
        services.AddSingleton<RollbackViewModel>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<ModulePage>();
        services.AddTransient<RollbackPage>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

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

    private void OnExit(object sender, ExitEventArgs e)
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>Çözücüden bir servis alır (XAML/code-behind için kolaylık).</summary>
    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();
}
