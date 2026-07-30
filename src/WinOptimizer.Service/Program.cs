using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WinOptimizer.Safety;
using WinOptimizer.Safety.Diagnostics;
using WinOptimizer.Service;

// Servis kurulumu (master plan Bölüm 3.17 & Faz 7) artık koddadır — bkz. ServiceInstaller:
//   WinOptimizer.Service.exe install-service     (oluştur/yeniden yapılandır + başlat)
//   WinOptimizer.Service.exe uninstall-service   (durdur + sil)
//   WinOptimizer.Service.exe service-status
//
// Kurulum sihirbazı ve (Faz 2) uygulama içindeki Guard sekmesi aynı verb'leri çağırır;
// servis tanımının tek kaynağı ServiceInstaller'dır. Verb yoksa normal servis/worker
// modunda çalışır: LocalSystem, otomatik başlangıç.
if (await ServiceInstaller.TryHandleAsync(args) is int verbExitCode)
{
    return verbExitCode;
}

var builder = Host.CreateApplicationBuilder(args);

// Dosya günlüğü: %ProgramData%\WinOptimizer\logs\service-*.log
// Servis LocalSystem olarak çalıştığı için host varsayılanı EventLog'dur; teşhis paketi ise
// yalnızca logs\*.log topluyor. Bu sink olmadan "servis çalışmıyor" raporunda servise dair
// hiçbir kayıt bulunmuyordu. EventLog sink'i KALIR (dosya sink'inin kendisi patlarsa gerekir).
string serviceDataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinOptimizer");
builder.Logging.AddSerilog(
    LoggingBootstrap.CreateLogger(serviceDataDir, LoggingBootstrap.ServicePrefix),
    dispose: true);

// Ayarlar settings.json'dan okunur (5 sn'de bir stat kontrolü). Eşikler ARTIK sabit
// init-varsayılanları değil: arayüzden değiştirilebilir ve guard kapatılabilir.
builder.Services.AddSingleton<GuardSettingsProvider>();
builder.Services.AddSingleton<GuardThresholds>(sp =>
{
    var settings = sp.GetRequiredService<GuardSettingsProvider>();
    settings.RefreshIfChanged();
    return settings.Thresholds;
});
builder.Services.AddSingleton<MetricsCollector>();
builder.Services.AddSingleton<ThresholdEngine>();
builder.Services.AddSingleton<GuardState>();
builder.Services.AddSingleton<ProcessRunner>();

// Otomatik müdahaleler change journal'a yazılır (CLAUDE.md §3.2).
builder.Services.AddSingleton<IntegrityGuard>(sp => new IntegrityGuard(
    IntegrityKeyStore.LoadOrCreate(serviceDataDir),
    sp.GetRequiredService<ILogger<IntegrityGuard>>()));
builder.Services.AddSingleton<ChangeJournal>(sp => new ChangeJournal(
    serviceDataDir,
    sp.GetRequiredService<ILogger<ChangeJournal>>(),
    sp.GetRequiredService<IntegrityGuard>()));
builder.Services.AddSingleton<RemediationEngine>();

builder.Services.AddSingleton<GuardIpcServer>(sp =>
{
    var state = sp.GetRequiredService<GuardState>();
    var settings = sp.GetRequiredService<GuardSettingsProvider>();
    var logger = sp.GetRequiredService<ILogger<GuardIpcServer>>();
    return new GuardIpcServer(
        state.GetMetric,
        state.GetAlerts,
        () => new
        {
            enabled = settings.Enabled,
            autoRemediate = settings.AutoRemediate,
            autoTrimRam = settings.AutoTrimRam,
            autoCleanDiskCritical = settings.AutoCleanDiskCritical,
            autoUpdateDefenderSignatures = settings.AutoUpdateDefenderSignatures,
            thresholds = settings.Thresholds,
        },
        logger);
});

builder.Services.AddHostedService<RealtimeGuardWorker>();

// Windows servisi olarak çalıştır (konsolda değilse otomatik servis moduna geçer).
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "WinOptimizerGuard";
});

var host = builder.Build();
await host.RunAsync();
return 0;
