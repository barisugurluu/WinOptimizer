using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.UpdateEngine;

/// <summary>
/// UpdateEngine — Windows Update yönetimi. SoftwareDistribution sıfırlama,
/// bekleyen güncelleme kontrolü. /ResetBase ayrı onay ister. Risk: Low/Medium.
/// (Master plan Bölüm 3.13.)
/// </summary>
public sealed class UpdateEngineModule : IOptimizationModule
{
    public string Id => "UpdateEngine";
    public string DisplayName => "Windows Update";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly ProcessRunner _runner;
    private readonly ILogger<UpdateEngineModule> _logger;

    public UpdateEngineModule(ProcessRunner runner, ILogger<UpdateEngineModule> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = 2,
            Summary = "Güncelleme kontrolü başlatılabilir; SoftwareDistribution önbelleği temizlenebilir."
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        return Task.FromResult(new PreviewResult
        {
            ModuleId = Id,
            Actions = new[]
            {
                new PreviewAction { Description = "Güncelleme tara (usoclient /StartScan)",
                    Risk = RiskLevel.None, Target = "Scan" },
                new PreviewAction { Description = "WU önbelleğini sıfırla (servis durdur + Download temizle)",
                    Risk = RiskLevel.Medium, Target = "ResetWU", RequiresExtraConfirmation = true }
            },
            IsDryRun = true
        });
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
                ModuleId = Id,
                Percent = idx * 100 / total,
                Message = action.Description,
                Current = idx,
                Total = total
            });

            try
            {
                if (action.Target == "Scan")
                {
                    int code = await _runner.RunAsync("usoclient.exe", "StartScan", null, ct);
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.CommandRun,
                        Target = "usoclient /StartScan",
                        NewValue = $"exit={code}",
                        Note = "Güncelleme taraması"
                    });
                }
                else if (action.Target == "ResetWU")
                {
                    // WU servisini durdur, Download klasörünü temizle, yeniden başlat
                    await _runner.RunAsync("net.exe", "stop wuauserv", null, ct);
                    await _runner.RunAsync("net.exe", "stop bits", null, ct);
                    var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    await _runner.RunAsync("cmd.exe", new[] { "/c", "rd", "/s", "/q", Path.Combine(win, "SoftwareDistribution", "Download") }, null, ct);
                    await _runner.RunAsync("net.exe", "start bits", null, ct);
                    await _runner.RunAsync("net.exe", "start wuauserv", null, ct);
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.CommandRun,
                        Target = "SoftwareDistribution\\Download",
                        NewValue = "cleared",
                        Note = "WU önbelleği sıfırlandı"
                    });
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "WU işlemi başarısız: {Target}", action.Target); failed++; }
        }

        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Failed = failed, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default) =>
        Task.FromResult(new RollbackResult
        {
            ModuleId = Id,
            ChangeId = change.Id,
            IsSuccess = true,
            Error = "WU sıfırlama geçicidir; Windows önbelleği yeniden oluşturur."
        });
}
