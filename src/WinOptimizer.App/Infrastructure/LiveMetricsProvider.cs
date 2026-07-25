using System.Management;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.App.Infrastructure;

/// <summary>
/// LiveMetricsProvider — Genel Bakış sekmesi için canlı sistem metriklerini sağlar.
/// Öncelikle RealtimeGuard servisinden (pipe) alır; servis yoksa doğrudan WMI sorgular
/// (yerel fallback). Böylece uygulama, servis kurulu olmasa da metrik gösterebilir.
/// (Master plan Bölüm 3.6/3.7 — canlı CPU/RAM/disk grafiği.)
/// </summary>
public sealed class LiveMetricsProvider
{
    private readonly GuardPipeClient _pipe;
    private readonly ILogger<LiveMetricsProvider> _logger;

    public LiveMetricsProvider(GuardPipeClient pipe, ILogger<LiveMetricsProvider> logger)
    {
        _pipe = pipe;
        _logger = logger;
    }

    /// <summary>RealtimeGuard servisi çalışıyor mu?</summary>
    public Task<bool> IsServiceRunningAsync(CancellationToken ct = default) =>
        _pipe.IsServiceRunningAsync(ct);

    /// <summary>
    /// Bir metrik örneği toplar: önce servisten, yoksa WMI'dan. Hiçbiri başarısızsa null.
    /// </summary>
    public async Task<LiveMetric?> GetAsync(CancellationToken ct = default)
    {
        var fromService = await _pipe.GetMetricsAsync(ct);
        if (fromService is not null) return fromService;

        try { return CollectFromWmi(); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WMI fallback metrik toplama başarısız.");
            return null;
        }
    }

    private static LiveMetric CollectFromWmi()
    {
        int ramPct = 0;
        long ramFree = 0;
        int cpuPct = 0;
        int cFreePct = 0;
        long cFree = 0;

        // RAM
        try
        {
            using var os = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            var m = os.Get().Cast<ManagementObject>().FirstOrDefault();
            if (m is not null)
            {
                double total = Convert.ToDouble(m["TotalVisibleMemorySize"]);
                double free = Convert.ToDouble(m["FreePhysicalMemory"]);
                if (total > 0) ramPct = (int)(100 - free / total * 100);
                ramFree = (long)(free * 1024);
            }
        }
        catch { /* bazı makinelerde WMI sınırlı */ }

        // CPU
        try
        {
            using var cpu = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            var m = cpu.Get().Cast<ManagementObject>().FirstOrDefault();
            if (m is not null) cpuPct = Convert.ToInt32(m["LoadPercentage"]);
        }
        catch { }

        // C: disk
        try
        {
            using var ld = new ManagementObjectSearcher(
                "SELECT Size, FreeSpace FROM Win32_LogicalDisk WHERE DeviceID='C:'");
            var m = ld.Get().Cast<ManagementObject>().FirstOrDefault();
            if (m is not null)
            {
                double size = Convert.ToDouble(m["Size"]);
                double free = Convert.ToDouble(m["FreeSpace"]);
                cFree = (long)free;
                cFreePct = size > 0 ? (int)(free / size * 100) : 0;
            }
        }
        catch { }

        return new LiveMetric
        {
            Timestamp = DateTimeOffset.UtcNow,
            RamUsagePercent = ramPct,
            RamFreeBytes = ramFree,
            CpuUsagePercent = cpuPct,
            CDriveFreePercent = cFreePct,
            CDriveFreeBytes = cFree
        };
    }
}