using System.Globalization;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Orchestration;

/// <summary>RealtimeGuard servisinin görülebilir durumları.</summary>
public enum GuardServiceState
{
    /// <summary>Servis hiç kurulu değil.</summary>
    NotInstalled,

    /// <summary>Kurulu, durdurulmuş.</summary>
    Stopped,

    /// <summary>Başlatılıyor.</summary>
    StartPending,

    /// <summary>Durduruluyor.</summary>
    StopPending,

    /// <summary>Çalışıyor.</summary>
    Running,

    /// <summary>Durum okunamadı (yetki/WMI/hizmet yöneticisi hatası).</summary>
    Unknown,
}

/// <summary>
/// RealtimeGuard servisini uygulama içinden yönetir: durum okuma, başlat/durdur,
/// kur/kaldır/onar.
/// </summary>
/// <remarks>
/// <para><b>Tek tanım ilkesi:</b> kurma/kaldırma işini kendisi yapmaz —
/// <c>WinOptimizer.Service.exe install-service|uninstall-service</c> verb'lerini çağırır.
/// Servis tanımı (ad, açıklama, başlangıç türü, kurtarma eylemleri) yalnızca
/// <c>ServiceInstaller</c> içinde yaşar; kurulum sihirbazı da aynı verb'ü çağırır.
/// Daha önce tanım üç yerde kopyalıydı ve senkronsuz kaldığı için kurulum donuyordu.</para>
/// <para>Durum/başlat/durdur için <see cref="ServiceController"/> kullanılır:
/// <c>sc query</c> çıktısını ayrıştırmak TR/EN Windows'ta kırılgandır.</para>
/// <para>Her mutasyon change journal'a yazılır (CLAUDE.md §3.2) — böylece işlem
/// Geri Al çizelgesinde ve teşhis paketinde görünür.</para>
/// </remarks>
public sealed class GuardServiceController
{
    /// <summary>Servisin sistemdeki adı. <c>ServiceInstaller.ServiceName</c> ile aynı olmalı.</summary>
    public const string ServiceName = "WinOptimizerGuard";

    private const string ServiceExeName = "WinOptimizer.Service.exe";
    private const string InstallVerb = "install-service";
    private const string UninstallVerb = "uninstall-service";

    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(30);

    private readonly ProcessRunner _runner;
    private readonly SafetyNet _safety;
    private readonly ILogger<GuardServiceController> _logger;

    public GuardServiceController(
        ProcessRunner runner, SafetyNet safety, ILogger<GuardServiceController> logger)
    {
        _runner = runner;
        _safety = safety;
        _logger = logger;
    }

    /// <summary>
    /// Servis exe'sinin tam yolu; <b>dosya yoksa <c>null</c></b>. Çağıran bu durumda
    /// düğmeleri devre dışı bırakıp yolu göstermelidir — "en iyi tahmin" döndürüp
    /// sonra başarı bildirmek (eski <c>SchedulerViewModel</c> hatası) yasak.
    /// </summary>
    public static string? ResolveServiceExePath()
    {
        string path = Path.Combine(AppContext.BaseDirectory, ServiceExeName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Servisin anlık durumu.</summary>
    public GuardServiceState GetState()
    {
        try
        {
            if (!ServiceExists())
            {
                return GuardServiceState.NotInstalled;
            }

            using var controller = new ServiceController(ServiceName);
            return controller.Status switch
            {
                ServiceControllerStatus.Running => GuardServiceState.Running,
                ServiceControllerStatus.Stopped => GuardServiceState.Stopped,
                ServiceControllerStatus.StartPending => GuardServiceState.StartPending,
                ServiceControllerStatus.StopPending => GuardServiceState.StopPending,
                _ => GuardServiceState.Unknown,
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                     or System.ComponentModel.Win32Exception)
        {
            _logger.LogDebug(ex, "Servis durumu okunamadı.");
            return GuardServiceState.Unknown;
        }
    }

    /// <summary>Servisi kurar (veya varsa yeniden yapılandırır) ve başlatır.</summary>
    public Task<bool> InstallAsync(CancellationToken ct = default) => RunVerbAsync(InstallVerb, ct);

    /// <summary>
    /// Servisi onarır. <see cref="InstallAsync"/> ile aynı verb'dür: verb idempotenttir
    /// (varsa <c>sc config</c>, yoksa <c>sc create</c>), bu yüzden "Onar" ayrı bir kod
    /// yolu gerektirmez.
    /// </summary>
    public Task<bool> RepairAsync(CancellationToken ct = default) => RunVerbAsync(InstallVerb, ct);

    /// <summary>Servisi durdurur ve kaldırır.</summary>
    public Task<bool> UninstallAsync(CancellationToken ct = default) => RunVerbAsync(UninstallVerb, ct);

    /// <summary>Kurulu servisi başlatır.</summary>
    public Task<bool> StartAsync(CancellationToken ct = default) =>
        ChangeStateAsync(ServiceControllerStatus.Running, ct);

    /// <summary>Çalışan servisi durdurur.</summary>
    public Task<bool> StopAsync(CancellationToken ct = default) =>
        ChangeStateAsync(ServiceControllerStatus.Stopped, ct);

    private async Task<bool> RunVerbAsync(string verb, CancellationToken ct)
    {
        string? exe = ResolveServiceExePath();
        if (exe is null)
        {
            _logger.LogError(
                "Servis dosyası bulunamadı: {Path} — kurulum eksik veya bozuk olabilir.",
                Path.Combine(AppContext.BaseDirectory, ServiceExeName));
            return false;
        }

        GuardServiceState before = GetState();
        var (code, output) = await _runner.RunCaptureAsync(exe, [verb], ct).ConfigureAwait(false);
        GuardServiceState after = GetState();

        if (code != 0)
        {
            _logger.LogError(
                "Servis işlemi başarısız: {Verb} → exit {Code}{NewLine}{Output}",
                verb, code, Environment.NewLine, output.Trim());
        }
        else
        {
            _logger.LogInformation("Servis işlemi tamam: {Verb} ({Before} → {After})", verb, before, after);
        }

        await JournalAsync(verb, before, after, code, ct).ConfigureAwait(false);
        return code == 0;
    }

    private async Task<bool> ChangeStateAsync(ServiceControllerStatus target, CancellationToken ct)
    {
        if (!ServiceExists())
        {
            _logger.LogWarning("Servis kurulu değil; önce 'Kur' işlemini uygulayın.");
            return false;
        }

        GuardServiceState before = GetState();
        try
        {
            using var controller = new ServiceController(ServiceName);
            if (target == ServiceControllerStatus.Running)
            {
                if (controller.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
                {
                    return true;
                }

                controller.Start();
            }
            else
            {
                if (controller.Status == ServiceControllerStatus.Stopped)
                {
                    return true;
                }

                if (!controller.CanStop)
                {
                    _logger.LogWarning("Servis şu anda durdurulamıyor (CanStop=false).");
                    return false;
                }

                controller.Stop();
            }

            // Bloklamamak için arka planda beklenir: WaitForStatus senkrondur.
            await Task.Run(() => controller.WaitForStatus(target, StatusTimeout), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                     or System.ComponentModel.Win32Exception
                                     or System.ServiceProcess.TimeoutException)
        {
            _logger.LogError(ex, "Servis durumu değiştirilemedi (hedef: {Target}).", target);
            return false;
        }

        GuardServiceState after = GetState();
        await JournalAsync(
            target == ServiceControllerStatus.Running ? "start" : "stop",
            before, after, exitCode: 0, ct).ConfigureAwait(false);
        return true;
    }

    private Task JournalAsync(
        string verb, GuardServiceState before, GuardServiceState after, int exitCode, CancellationToken ct)
    {
        // Durum değişmediyse kayıt tutulmaz: journal gürültüsü geri alma listesini okunmaz yapar.
        if (before == after && exitCode == 0)
        {
            return Task.CompletedTask;
        }

        return _safety.RecordAsync(new ChangeRecord
        {
            Module = "GuardService",
            Operation = ChangeOperationType.ServiceStartType,
            Target = ServiceName,
            PreviousValue = before.ToString(),
            NewValue = after.ToString(),
            Note = string.Format(
                CultureInfo.InvariantCulture, "{0} (exit {1})", verb, exitCode),
        }, ct);
    }

    /// <summary>Servis kayıtlı mı? Yerelleştirmeden bağımsız sorgu.</summary>
    private static bool ServiceExists()
    {
        ServiceController[] services = ServiceController.GetServices();
        try
        {
            return services.Any(s =>
                s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (ServiceController service in services)
            {
                service.Dispose();
            }
        }
    }
}
