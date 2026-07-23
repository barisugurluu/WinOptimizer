using System.Management;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.StorageOptimizer;

/// <summary>
/// StorageOptimizer — SSD'de TRIM, HDD'de defrag. Disk türü otomatik algılanır.
/// SSD'de defrag YASAKTIR. Risk: Low. (Master plan Bölüm 3.6.)
/// </summary>
public sealed class StorageOptimizerModule : IOptimizationModule
{
    public string Id => "StorageOptimizer";
    public string DisplayName => "Disk Optimizasyonu (TRIM/Defrag)";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly ProcessRunner _runner;
    private readonly ILogger<StorageOptimizerModule> _logger;

    public StorageOptimizerModule(ProcessRunner runner, ILogger<StorageOptimizerModule> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        var drives = new List<object>();
        int optimizable = 0;
        foreach (var di in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
        {
            bool isSsd = IsSsd(di.Name[0]);
            if (isSsd) optimizable++;
            drives.Add(new
            {
                Drive = di.Name,
                Type = isSsd ? "SSD" : "HDD",
                FreeGB = Math.Round(di.TotalFreeSpace / 1e9, 1),
                TotalGB = Math.Round(di.TotalSize / 1e9, 1)
            });
        }
        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id, ItemCount = optimizable,
            Summary = $"{drives.Count} sabit disk, {optimizable} SSD TRIM için uygun.",
            Details = new() { ["Drives"] = drives }
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = new List<PreviewAction>();
        if (analysis.Details.TryGetValue("Drives", out var box) && box is List<object> list)
        {
            foreach (dynamic d in list)
            {
                string op = d.Type == "SSD" ? "TRIM/retrim" : "parçalanma analizi";
                actions.Add(new PreviewAction
                {
                    Description = $"{d.Drive} {d.Type} — {op}",
                    Risk = RiskLevel.Low, Target = (string)d.Drive
                });
            }
        }
        return Task.FromResult(new PreviewResult { ModuleId = Id, Actions = actions, IsDryRun = true });
    }

    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        var changes = new List<ChangeRecord>();
        int succeeded = 0, failed = 0;
        int total = preview.Actions.Count, idx = 0;

        foreach (var action in preview.Actions)
        {
            ct.ThrowIfCancellationRequested();
            idx++;
            progress.Report(new ProgressInfo
            {
                ModuleId = Id, Percent = idx * 100 / Math.Max(1, total),
                Message = action.Description, Current = idx, Total = total
            });

            char drive = action.Target![0];
            bool isSsd = IsSsd(drive);
            string verb = isSsd ? "ReTrim" : "Defrag";

            try
            {
                int code = await _runner.RunAsync("powershell.exe",
                    $"-NoProfile -Command Optimize-Volume -DriveLetter {drive} -{verb}", null, ct);
                if (code == 0)
                {
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id, Operation = ChangeOperationType.CommandRun,
                        Target = $"{drive}: ({verb})", NewValue = "optimized", Note = action.Description
                    });
                }
                else failed++;
            }
            catch (Exception ex) { _logger.LogError(ex, "Disk optimize edilemedi: {Drive}", drive); failed++; }
        }

        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Failed = failed, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default) =>
        Task.FromResult(new RollbackResult
        {
            ModuleId = Id, ChangeId = change.Id, IsSuccess = true,
            Error = "TRIM/defrag geri alınmaz; diski iyileştirir."
        });

    /// <summary>Sürücünün SSD olup olmadığını algılar (WMI; başarısızsa varsayılan SSD).</summary>
    private bool IsSsd(char driveLetter)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Storage",
                "SELECT MediaType FROM MSFT_PhysicalDisk");
            foreach (var mo in searcher.Get().Cast<ManagementObject>())
            {
                // MediaType 3 = SSD
                if (Convert.ToUInt16(mo["MediaType"]) == 3) return true;
            }
            return false;
        }
        catch { return true; } // SSD varsayılan (TRIM HDD'ye zarar vermez)
    }
}
