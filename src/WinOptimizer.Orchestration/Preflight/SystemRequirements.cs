using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core.Compatibility;
using WinOptimizer.Safety;

namespace WinOptimizer.Orchestration.Preflight;

/// <summary>Bir gereksinimin sonucunun ağırlığı.</summary>
public enum RequirementSeverity
{
    /// <summary>Karşılanıyor.</summary>
    Ok,

    /// <summary>Karşılanmıyor ama uygulama çalışabilir (bazı özellikler kısıtlı).</summary>
    Warning,

    /// <summary>Karşılanmıyor ve uygulama anlamlı biçimde çalışamaz.</summary>
    Blocking,
}

/// <summary>Tek bir gereksinim kontrolünün sonucu.</summary>
/// <param name="Id">Kararlı kimlik (ör. <c>Wmi.Cimv2</c>) — destek konuşmalarında referans.</param>
/// <param name="Title">Kullanıcıya gösterilen kısa ad.</param>
/// <param name="Severity">Sonucun ağırlığı.</param>
/// <param name="Detail">Ölçülen değer / hata metni.</param>
/// <param name="RemedyHint">Kullanıcının ne yapabileceği (varsa).</param>
public sealed record RequirementCheck(
    string Id,
    string Title,
    RequirementSeverity Severity,
    string Detail,
    string? RemedyHint = null);

/// <summary>Gereksinim kontrolünün tamamı.</summary>
public sealed record RequirementsReport(IReadOnlyList<RequirementCheck> Checks)
{
    /// <summary>Uygulamanın çalışmasını engelleyen bir madde var mı?</summary>
    public bool HasBlocking => Checks.Any(c => c.Severity == RequirementSeverity.Blocking);

    /// <summary>Kısıtlama uyarısı var mı?</summary>
    public bool HasWarnings => Checks.Any(c => c.Severity == RequirementSeverity.Warning);

    /// <summary>Teşhis paketine konan düz metin biçimi.</summary>
    public string ToPlainText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("WinOptimizer — Sistem Gereksinim Kontrolü");
        sb.AppendLine("========================================");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Oluşturulma: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        foreach (var check in Checks)
        {
            string mark = check.Severity switch
            {
                RequirementSeverity.Ok => "[ OK ]",
                RequirementSeverity.Warning => "[UYARI]",
                _ => "[ENGEL]",
            };
            sb.AppendLine(CultureInfo.InvariantCulture, $"{mark} {check.Id} — {check.Title}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {check.Detail}");
            if (!string.IsNullOrEmpty(check.RemedyHint))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        → {check.RemedyHint}");
            }
        }

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Engelleyen madde: {(HasBlocking ? "VAR" : "yok")} · Uyarı: {(HasWarnings ? "var" : "yok")}");
        return sb.ToString();
    }
}

/// <summary>
/// Uygulamanın gerçekten çalışabileceğini önden denetler ve sonucu kullanıcıya
/// <b>okunabilir</b> biçimde sunar.
/// </summary>
/// <remarks>
/// <para><b>Neden Orchestration katmanında?</b> Orchestration hem App hem Cli tarafından
/// referanslanır ama hiçbir modül ve Safety onu görmez — yani katmanlama kuralı (Modules ve
/// Safety asla App'e bakmaz) yeni bir yukarı kenar oluşmadan korunur.</para>
/// <para><b>Her probe kendi try/catch'inde:</b> bozuk bir WMI yığını gereksinim ekranının
/// KENDİSİNİ patlatmamalı — probe hatası ilgili maddeyi uyarıya düşürür, o kadar.</para>
/// </remarks>
public sealed class SystemRequirementsChecker
{
    private const long WarnFreeBytes = 2L * 1024 * 1024 * 1024;   // 2 GB
    private const long BlockFreeBytes = 512L * 1024 * 1024;       // 512 MB

    private readonly ILogger<SystemRequirementsChecker> _logger;
    private readonly GuardServiceController _guardService;
    private readonly RestorePointService _restorePoint;
    private readonly string _dataDir;

    public SystemRequirementsChecker(
        string dataDir,
        GuardServiceController guardService,
        RestorePointService restorePoint,
        ILogger<SystemRequirementsChecker> logger)
    {
        _dataDir = dataDir;
        _guardService = guardService;
        _restorePoint = restorePoint;
        _logger = logger;
    }

    /// <summary>Tüm gereksinimleri denetler.</summary>
    public async Task<RequirementsReport> RunAsync(CancellationToken ct = default)
    {
        var checks = new List<RequirementCheck>
        {
            CheckArchitecture(),
            CheckOsBuild(),
            CheckEdition(),
            CheckElevation(),
            await CheckWmiAsync(ct).ConfigureAwait(false),
            CheckSystemRestore(),
            CheckSystemDriveSpace(),
            CheckDataDirectoryWritable(),
            CheckGuardService(),
        };

        var report = new RequirementsReport(checks);
        _logger.LogInformation(
            "Gereksinim kontrolü: {Total} madde, engelleyen={Blocking}, uyarı={Warning}",
            checks.Count, report.HasBlocking, report.HasWarnings);
        return report;
    }

    private static RequirementCheck CheckArchitecture()
    {
        bool ok = Environment.Is64BitOperatingSystem;
        return new RequirementCheck(
            "Os.Architecture",
            "64-bit Windows",
            ok ? RequirementSeverity.Ok : RequirementSeverity.Blocking,
            $"{RuntimeInformation.OSArchitecture} ({(Environment.Is64BitProcess ? "64-bit süreç" : "32-bit süreç")})",
            ok ? null : "WinOptimizer yalnızca 64-bit (x64) Windows üzerinde çalışır.");
    }

    private static RequirementCheck CheckOsBuild()
    {
        int build = Environment.OSVersion.Version.Build;
        bool ok = build >= WindowsVersionInfo.Windows10Build2004;
        return new RequirementCheck(
            "Os.Build",
            "Windows sürümü",
            ok ? RequirementSeverity.Ok : RequirementSeverity.Blocking,
            $"Derleme {build.ToString(CultureInfo.InvariantCulture)}" +
            (WindowsVersionInfo.Current.IsWindows11 ? " (Windows 11)" : " (Windows 10)"),
            ok ? null : $"En az derleme {WindowsVersionInfo.Windows10Build2004} (Windows 10 sürüm 2004) gerekir.");
    }

    private static RequirementCheck CheckEdition()
    {
        var version = WindowsVersionInfo.Current;
        string edition = string.IsNullOrEmpty(version.EditionId) ? "(okunamadı)" : version.EditionId;
        return version.IsProOrHigher
            ? new RequirementCheck("Os.Edition", "Windows sürümü (edition)",
                RequirementSeverity.Ok, edition)
            : new RequirementCheck("Os.Edition", "Windows sürümü (edition)",
                RequirementSeverity.Warning,
                $"{edition} — Home ailesi",
                "Pro'ya özel özellikler gizlenir: sistem görüntüsü yedeği (wbadmin), " +
                "Hyper-V, bazı güvenlik politikaları. Diğer tüm işlevler çalışır.");
    }

    private static RequirementCheck CheckElevation()
    {
        bool ok = Elevation.IsAdministrator();
        return new RequirementCheck(
            "Process.Elevated",
            "Yönetici ayrıcalığı",
            ok ? RequirementSeverity.Ok : RequirementSeverity.Blocking,
            ok ? "Yükseltilmiş çalışıyor" : "Yükseltilmemiş",
            ok ? null : "Uygulamayı kapatıp kısayola sağ tıklayarak 'Yönetici olarak çalıştır' seçin.");
    }

    private async Task<RequirementCheck> CheckWmiAsync(CancellationToken ct)
    {
        try
        {
            // 5 sn'lik tavan: bozuk bir WMI deposunda sorgu süresiz asılı kalabilir ve
            // gereksinim ekranı hiç açılmaz.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            string caption = await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Caption FROM Win32_OperatingSystem");
                using var results = searcher.Get();
                foreach (ManagementObject os in results.Cast<ManagementObject>())
                {
                    using (os)
                    {
                        return os["Caption"]?.ToString() ?? string.Empty;
                    }
                }
                return string.Empty;
            }, cts.Token).ConfigureAwait(false);

            return string.IsNullOrEmpty(caption)
                ? new RequirementCheck("Wmi.Cimv2", "WMI erişimi", RequirementSeverity.Blocking,
                    "Sorgu boş sonuç döndürdü",
                    "WMI deposu bozuk olabilir. Yönetici komut isteminde: winmgmt /verifyrepository")
                : new RequirementCheck("Wmi.Cimv2", "WMI erişimi", RequirementSeverity.Ok, caption);
        }
        catch (OperationCanceledException)
        {
            return new RequirementCheck("Wmi.Cimv2", "WMI erişimi", RequirementSeverity.Blocking,
                "Sorgu 5 saniyede yanıt vermedi",
                "WMI hizmeti (Winmgmt) yanıt vermiyor. Bilgisayarı yeniden başlatmayı deneyin.");
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
        {
            // Donanım metrikleri, geri yükleme noktası ve modüllerin çoğu WMI'ye dayanır.
            return new RequirementCheck("Wmi.Cimv2", "WMI erişimi", RequirementSeverity.Blocking,
                $"{ex.GetType().Name}: {ex.Message}",
                "WMI olmadan canlı metrikler ve modüllerin çoğu çalışmaz.");
        }
    }

    private RequirementCheck CheckSystemRestore()
    {
        try
        {
            bool enabled = _restorePoint.IsEnabled();
            return enabled
                ? new RequirementCheck("Wmi.SystemRestore", "Sistem Geri Yükleme",
                    RequirementSeverity.Ok, "Etkin")
                : new RequirementCheck("Wmi.SystemRestore", "Sistem Geri Yükleme",
                    RequirementSeverity.Warning, "Kapalı veya kullanılamıyor",
                    "İşlem öncesi geri yükleme noktası alınamayacak. Değişiklikler yine de " +
                    "değişiklik günlüğünden (journal) geri alınabilir.");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Sistem Geri Yükleme durumu okunamadı.");
            return new RequirementCheck("Wmi.SystemRestore", "Sistem Geri Yükleme",
                RequirementSeverity.Warning, $"Sorgulanamadı: {ex.Message}");
        }
    }

    private static RequirementCheck CheckSystemDriveSpace()
    {
        try
        {
            // C: sabit DEĞİL: Windows başka bir sürücüde kurulu olabilir.
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            long free = drive.AvailableFreeSpace;
            string detail = string.Format(
                CultureInfo.InvariantCulture,
                "{0} sürücüsünde {1:N1} GB boş", drive.Name, free / 1024.0 / 1024 / 1024);

            if (free < BlockFreeBytes)
            {
                return new RequirementCheck("Disk.SystemDriveFree", "Sistem sürücüsü boş alanı",
                    RequirementSeverity.Blocking, detail,
                    "Geri yükleme noktası ve yedek alınamaz. En az 512 MB boşaltın.");
            }

            return free < WarnFreeBytes
                ? new RequirementCheck("Disk.SystemDriveFree", "Sistem sürücüsü boş alanı",
                    RequirementSeverity.Warning, detail,
                    "2 GB'ın altında; geri yükleme noktası alınması başarısız olabilir.")
                : new RequirementCheck("Disk.SystemDriveFree", "Sistem sürücüsü boş alanı",
                    RequirementSeverity.Ok, detail);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new RequirementCheck("Disk.SystemDriveFree", "Sistem sürücüsü boş alanı",
                RequirementSeverity.Warning, $"Okunamadı: {ex.Message}");
        }
    }

    private RequirementCheck CheckDataDirectoryWritable()
    {
        string probe = Path.Combine(_dataDir, $".yazma-testi-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(_dataDir);
            File.WriteAllText(probe, "test");
            File.Delete(probe);
            return new RequirementCheck("Data.Writable", "Veri dizinine yazma",
                RequirementSeverity.Ok, _dataDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ayarlar, günlükler ve geri alma günlüğü buraya yazılır: yazılamıyorsa
            // uygulama hiçbir şeyi güvenli biçimde yapamaz.
            return new RequirementCheck("Data.Writable", "Veri dizinine yazma",
                RequirementSeverity.Blocking, $"{_dataDir} — {ex.Message}",
                "Bu klasöre yazma izni gerekir; uygulamayı yönetici olarak çalıştırın.");
        }
        finally
        {
            try
            {
                if (File.Exists(probe))
                {
                    File.Delete(probe);
                }
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Yazma testi dosyası silinemedi: {Path}", probe);
            }
        }
    }

    private RequirementCheck CheckGuardService()
    {
        GuardServiceState state = _guardService.GetState();
        return state switch
        {
            GuardServiceState.Running => new RequirementCheck("Service.Guard",
                "RealtimeGuard hizmeti", RequirementSeverity.Ok, "Çalışıyor"),
            GuardServiceState.NotInstalled => new RequirementCheck("Service.Guard",
                "RealtimeGuard hizmeti", RequirementSeverity.Warning, "Kurulu değil",
                "İsteğe bağlıdır. Canlı metrikler yerel WMI ile okunur; " +
                "arka plan izleme istiyorsanız Guard sekmesinden kurabilirsiniz."),
            _ => new RequirementCheck("Service.Guard",
                "RealtimeGuard hizmeti", RequirementSeverity.Warning, state.ToString(),
                "Guard sekmesinden başlatabilir veya onarabilirsiniz."),
        };
    }
}
