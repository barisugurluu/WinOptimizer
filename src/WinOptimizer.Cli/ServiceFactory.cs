using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using WinOptimizer.Modules.CleanEngine;
using WinOptimizer.Modules.MemoryEngine;
using WinOptimizer.Modules.RepairEngine;
using WinOptimizer.Modules.SystemTweaker;
using WinOptimizer.Orchestration;
using WinOptimizer.Orchestration.Confirmation;
using WinOptimizer.Orchestration.Preflight;
using WinOptimizer.Safety;
using WinOptimizer.Safety.Diagnostics;

namespace WinOptimizer.Cli;

/// <summary>
/// CLI için bağımlılık enjeksiyonu kapsayıcısını kurar.
/// Temizlik/onarım/tweak modüllerini ve SafetyNet'i hazırlar.
/// </summary>
internal static class ServiceFactory
{
    /// <summary>CLI için bağımlılık kapsayıcısını kurar.</summary>
    /// <param name="confirmation">
    /// Onay mercii — CLI'da bayrak tabanlıdır (<c>--yes</c> / <c>--allow-risky</c>).
    /// Bayraklar komut satırından geldiği için DI'ya dışarıdan verilir.
    /// </param>
    public static IServiceProvider Build(IActionConfirmation confirmation)
    {
        var services = new ServiceCollection();

        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinOptimizer");
        EnsureDataDirectory(baseDir);

        services.AddLogging(b =>
        {
            b.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
            // Dosya günlüğü: zamanlanmış (03:00) gözetimsiz çalışmaların da izi kalsın —
            // konsol çıktısı hiç kimsenin görmediği bir yere gidiyor.
            b.AddSerilog(LoggingBootstrap.CreateLogger(baseDir, LoggingBootstrap.CliPrefix), dispose: true);
        });

        // Safety katmanı — bütünlük koruyucu önce (§17.4)
        services.AddSingleton<IntegrityGuard>(_ => new IntegrityGuard(
            IntegrityKeyStore.LoadOrCreate(baseDir), _.GetRequiredService<ILogger<IntegrityGuard>>()));
        services.AddSingleton<RestorePointService>();
        services.AddSingleton<ChangeJournal>(_ =>
            new(baseDir, _.GetRequiredService<ILogger<ChangeJournal>>(), _.GetRequiredService<IntegrityGuard>()));
        services.AddSingleton<RegistryBackup>(_ =>
            new(baseDir, _.GetRequiredService<ILogger<RegistryBackup>>(), _.GetRequiredService<IntegrityGuard>()));
        services.AddSingleton<SafetyGuard>();
        services.AddSingleton<SafetyNet>();
        services.AddSingleton<ProcessRunner>();

        // Modüller
        services.AddSingleton<CleanEngineModule>();
        services.AddSingleton<MemoryEngineModule>();
        services.AddSingleton<RepairEngineModule>();
        services.AddSingleton<SystemTweakerModule>();

        // Orchestration
        services.AddSingleton(confirmation);
        services.AddSingleton<SettingsService>(_ =>
            new SettingsService(baseDir, _.GetRequiredService<ILogger<SettingsService>>()));
        services.AddSingleton<ModuleRegistry>();
        services.AddSingleton<JobOrchestrationEngine>();
        services.AddSingleton<RollbackService>();

        var sp = services.BuildServiceProvider();

        // CLI de arayüzle aynı ayarlara uyar: gözetimsiz çalışmada "otomatik kayıt defteri
        // yedeği" kapalıysa burada da kapalıdır (Safety, SettingsService'i göremez).
        sp.GetRequiredService<SafetyNet>().AutoRegistryBackup =
            sp.GetRequiredService<SettingsService>().Current.SafetyNet.AutoRegistryBackup;

        var reg = sp.GetRequiredService<ModuleRegistry>();
        reg.Register(sp.GetRequiredService<CleanEngineModule>())
           .Register(sp.GetRequiredService<MemoryEngineModule>())
           .Register(sp.GetRequiredService<RepairEngineModule>())
           .Register(sp.GetRequiredService<SystemTweakerModule>());
        return sp;
    }

    /// <summary>
    /// Veri dizinini oluşturur; başarısız olursa çıplak <see cref="UnauthorizedAccessException"/>
    /// yerine ne yapılacağını söyleyen bir <see cref="PreflightException"/> atar.
    /// </summary>
    private static void EnsureDataDirectory(string baseDir)
    {
        try
        {
            Directory.CreateDirectory(baseDir);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new PreflightException(
                $"Veri dizini oluşturulamadı: {baseDir}{Environment.NewLine}" +
                $"Sebep: {ex.Message}{Environment.NewLine}" +
                "Bu komutu yönetici olarak açtığınız bir terminalde çalıştırın.", ex);
        }
    }
}
