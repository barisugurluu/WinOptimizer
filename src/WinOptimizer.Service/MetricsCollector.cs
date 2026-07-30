using System.Diagnostics;
using System.Management;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Service;

/// <summary>
/// MetricsCollector — WMI üzerinden sistem metriklerini poll eder.
/// RAM, CPU, disk doluluk, SMART, batarya, Defender imza yaşı.
/// (Master plan Bölüm 3.17 — WMI Poller.)
/// </summary>
public sealed class MetricsCollector
{
    private readonly ILogger<MetricsCollector> _logger;

    // İSTEĞE BAĞLI YOKLAMALAR — bir kez başarısız olursa bir daha DENENMEZ.
    // Gerçek makinede görüldü: SMART (root\wmi) çoğu tüketici donanımında desteklenmiyor,
    // her 5 sn'lik tikte istisna atıyor ve yığın iziyle günlüğe yazılıyordu — 247 satırın
    // 40'ı, günde ~17.000 kayıt. Ayrıca desteklenmeyen bir WMI sorgusunu saniyede bir
    // tekrarlamak "<%1 CPU" bütçesine de aykırı. Donanım/namespace desteği çalışma
    // sırasında değişmez, bu yüzden ilk hatada kalıcı olarak devre dışı bırakılır.
    private bool _smartUnsupported;
    private bool _batteryUnsupported;
    private bool _defenderUnsupported;

    public MetricsCollector(ILogger<MetricsCollector> logger) => _logger = logger;

    /// <summary>
    /// İsteğe bağlı bir yoklamayı kalıcı olarak devre dışı bırakır ve sebebini BİR KEZ
    /// günlüğe yazar.
    /// </summary>
    private void DisableProbe(ref bool flag, string probeName, Exception ex)
    {
        flag = true;
        _logger.LogInformation(
            "{Probe} bu makinede desteklenmiyor ({Reason}); bir daha denenmeyecek.",
            probeName, ex.Message.Trim());
    }

    /// <summary>Yeni bir metrik örneği toplar.</summary>
    public GuardMetric Collect()
    {
        int ramPct = 0;
        long ramFree = 0;
        int cpuPct = 0;
        int cFreePct = 0;
        long cFree = 0;
        bool smartFail = false;
        int? cpuTemp = null;
        int? battery = null;
        int sigAge = 0;

        // RAM
        try
        {
            using var os = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            var mo = os.Get().Cast<ManagementObject>().FirstOrDefault();
            if (mo is not null)
            {
                double total = Convert.ToDouble(mo["TotalVisibleMemorySize"]);
                double free = Convert.ToDouble(mo["FreePhysicalMemory"]);
                if (total > 0) ramPct = (int)(100 - free / total * 100);
                ramFree = (long)(free * 1024);
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "RAM metrik hatası."); }

        // CPU yükü
        try
        {
            using var cpu = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            var mo = cpu.Get().Cast<ManagementObject>().FirstOrDefault();
            if (mo is not null) cpuPct = Convert.ToInt32(mo["LoadPercentage"]);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "CPU metrik hatası."); }

        // Sistem sürücüsü doluluk. C: SABİT DEĞİL: Windows başka bir sürücüde kurulu
        // olabilir; sabitken o makinelerde disk metrikleri sessizce 0 raporlanıyordu.
        // (Alan adları geçmişe dönük uyumluluk için CDriveFree* olarak kaldı.)
        try
        {
            string systemDrive = Path.GetPathRoot(Environment.SystemDirectory)!.TrimEnd('\\');
            using var ld = new ManagementObjectSearcher(
                $"SELECT DeviceID, Size, FreeSpace FROM Win32_LogicalDisk WHERE DeviceID='{systemDrive}'");
            var mo = ld.Get().Cast<ManagementObject>().FirstOrDefault();
            if (mo is not null)
            {
                double size = Convert.ToDouble(mo["Size"]);
                double free = Convert.ToDouble(mo["FreeSpace"]);
                cFree = (long)free;
                cFreePct = size > 0 ? (int)(free / size * 100) : 0;
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Disk metrik hatası."); }

        // SMART arıza tahmini (isteğe bağlı — çoğu tüketici donanımında yok)
        if (!_smartUnsupported)
        {
            try
            {
                using var smart = new ManagementObjectSearcher(
                    @"\\.\root\wmi", "SELECT PredictFailure FROM MSStorageDriver_FailurePredictStatus");
                foreach (var mo in smart.Get().Cast<ManagementObject>())
                {
                    if (Convert.ToBoolean(mo["PredictFailure"])) { smartFail = true; break; }
                }
            }
            catch (Exception ex) { DisableProbe(ref _smartUnsupported, "SMART arıza tahmini", ex); }
        }

        // Batarya (isteğe bağlı — masaüstlerinde yok)
        if (!_batteryUnsupported)
        {
            try
            {
                using var bat = new ManagementObjectSearcher(
                    "SELECT EstimatedChargeRemaining FROM Win32_Battery");
                var mo = bat.Get().Cast<ManagementObject>().FirstOrDefault();
                if (mo is not null) battery = Convert.ToInt32(mo["EstimatedChargeRemaining"]);
            }
            catch (Exception ex) { DisableProbe(ref _batteryUnsupported, "Batarya durumu", ex); }
        }

        // Defender imza yaşı (isteğe bağlı — namespace bulunmayabilir)
        if (!_defenderUnsupported)
        {
            try
            {
                using var def = new ManagementObjectSearcher(
                    @"\\.\root\Microsoft\Windows\Defender", "SELECT AntivirusSignatureAge FROM MSFT_MpComputerStatus");
                var mo = def.Get().Cast<ManagementObject>().FirstOrDefault();
                if (mo is not null) sigAge = Convert.ToInt32(mo["AntivirusSignatureAge"]);
            }
            catch (Exception ex) { DisableProbe(ref _defenderUnsupported, "Defender durumu", ex); }
        }

        return new GuardMetric(
            DateTimeOffset.UtcNow, ramPct, ramFree, cpuPct,
            cFreePct, cFree, smartFail, cpuTemp, battery, sigAge);
    }
}
