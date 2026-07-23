using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinOptimizer.Modules.CleanEngine;
using WinOptimizer.Modules.MemoryEngine;
using WinOptimizer.Modules.RepairEngine;
using WinOptimizer.Modules.SystemTweaker;
using WinOptimizer.Orchestration;
using WinOptimizer.Safety;

namespace WinOptimizer.Cli;

/// <summary>
/// CLI için bağımlılık enjeksiyonu kapsayıcısını kurar.
/// Temizlik/onarım/tweak modüllerini ve SafetyNet'i hazırlar.
/// </summary>
internal static class ServiceFactory
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }));

        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinOptimizer");
        Directory.CreateDirectory(baseDir);

        // Safety katmanı
        services.AddSingleton<RestorePointService>();
        services.AddSingleton<ChangeJournal>(_ =>
            new(baseDir, _.GetRequiredService<ILogger<ChangeJournal>>()));
        services.AddSingleton<RegistryBackup>(_ =>
            new(baseDir, _.GetRequiredService<ILogger<RegistryBackup>>()));
        services.AddSingleton<SafetyGuard>();
        services.AddSingleton<SafetyNet>();
        services.AddSingleton<ProcessRunner>();

        // Modüller
        services.AddSingleton<CleanEngineModule>();
        services.AddSingleton<MemoryEngineModule>();
        services.AddSingleton<RepairEngineModule>();
        services.AddSingleton<SystemTweakerModule>();

        // Orchestration
        services.AddSingleton<ModuleRegistry>();
        services.AddSingleton<JobOrchestrationEngine>();

        var sp = services.BuildServiceProvider();
        var reg = sp.GetRequiredService<ModuleRegistry>();
        reg.Register(sp.GetRequiredService<CleanEngineModule>())
           .Register(sp.GetRequiredService<MemoryEngineModule>())
           .Register(sp.GetRequiredService<RepairEngineModule>())
           .Register(sp.GetRequiredService<SystemTweakerModule>());
        return sp;
    }
}
