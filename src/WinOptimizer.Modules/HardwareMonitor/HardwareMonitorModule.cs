using System.Management;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;

namespace WinOptimizer.Modules.HardwareMonitor;

/// <summary>
/// HardwareMonitor — Donanım izleme (salt okunur). CPU/RAM/disk SMART/doluluk.
/// Risk: None. (Master plan Bölüm 3.7.)
/// </summary>
public sealed class HardwareMonitorModule : IOptimizationModule
{
    public string Id => "HardwareMonitor";
    public string DisplayName => "Donanım İzleme & Teşhis";
    public RiskLevel Risk => RiskLevel.None;

    private readonly ILogger<HardwareMonitorModule> _logger;
    public HardwareMonitorModule(ILogger<HardwareMonitorModule> logger) => _logger = logger;

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        var details = new Dictionary<string, object>();
        var warnings = new List<string>();

        Query("SELECT Name,LoadPercentage,NumberOfCores FROM Win32_Processor", mo =>
        {
            details["CpuName"] = mo["Name"] ?? "?";
            details["CpuLoad"] = mo["LoadPercentage"] ?? 0;
            details["CpuCores"] = mo["NumberOfCores"] ?? 0;
        });

        try
        {
            using var mem = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");
            var m = mem.Get().Cast<ManagementObject>().FirstOrDefault();
            if (m is not null)
            {
                double totalKb = Convert.ToDouble(m["TotalVisibleMemorySize"]);
                double freeKb = Convert.ToDouble(m["FreePhysicalMemory"]);
                details["RamTotalGB"] = Math.Round(totalKb / 1048576.0, 1);
                details["RamFreeGB"] = Math.Round(freeKb / 1048576.0, 1);
                details["RamUsedPct"] = totalKb > 0 ? (int)(100 - freeKb / totalKb * 100) : 0;
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "RAM sorgusu başarısız."); }

        try
        {
            using var ld = new ManagementObjectSearcher(
                "SELECT DeviceID,Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3");
            var drives = new List<object>();
            foreach (var d in ld.Get().Cast<ManagementObject>())
            {
                double size = Convert.ToDouble(d["Size"]);
                double free = Convert.ToDouble(d["FreeSpace"]);
                int pct = size > 0 ? (int)(free / size * 100) : 0;
                if (pct < 15) warnings.Add($"Disk {d["DeviceID"]} boş alan düşük (%{pct}).");
                drives.Add(new { Drive = d["DeviceID"], FreePercent = pct });
            }
            details["Drives"] = drives;
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Disk sorgusu başarısız."); }

        string summary = warnings.Count > 0 ? "⚠ " + string.Join(" | ", warnings) : "Donanım sağlığı iyi görünüyor.";
        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id, ItemCount = details.Count, Summary = summary, Details = details
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default) =>
        Task.FromResult(new PreviewResult { ModuleId = Id, Actions = Array.Empty<PreviewAction>(), IsDryRun = true });

    public Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        progress.Report(new ProgressInfo { ModuleId = Id, Percent = 100, Message = "Salt okunur modül.", Current = 1, Total = 1 });
        return Task.FromResult(new ExecutionResult { ModuleId = Id });
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default) =>
        Task.FromResult(new RollbackResult
        {
            ModuleId = Id, ChangeId = change.Id, IsSuccess = true,
            Error = "Salt okunur modül — geri alınacak işlem yok."
        });

    private void Query(string wql, Action<ManagementObject> action)
    {
        try
        {
            using var s = new ManagementObjectSearcher(wql);
            var obj = s.Get().Cast<ManagementObject>().FirstOrDefault();
            if (obj is not null) action(obj);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "WMI sorgusu başarısız: {Wql}", wql); }
    }
}
