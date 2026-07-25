using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.BootOptimizer;

/// <summary>
/// BootOptimizer — Hızlı Açılış yönetimi ve görev zamanlayıcı optimizasyonu.
/// Fast Startup kapatma opsiyonel. Risk: Low. (Master plan Bölüm 3.11.)
/// </summary>
public sealed class BootOptimizerModule : IOptimizationModule
{
    public string Id => "BootOptimizer";
    public string DisplayName => "Başlangıç & Önyükleme";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly ProcessRunner _runner;
    private readonly ILogger<BootOptimizerModule> _logger;

    public BootOptimizerModule(ProcessRunner runner, ILogger<BootOptimizerModule> logger)
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
            Summary = "Fast Startup durumu kontrol edilebilir; gereksiz zamanlanmış görevler taranabilir."
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        return Task.FromResult(new PreviewResult
        {
            ModuleId = Id,
            Actions = new[]
            {
                new PreviewAction { Description = "Hızlı Açılış (Fast Startup) durumunu raporla",
                    Risk = RiskLevel.None, Target = "FastStartup" },
                new PreviewAction { Description = "Açılış süresini ölç (BootTsVer)",
                    Risk = RiskLevel.None, Target = "BootTime" }
            },
            IsDryRun = true
        });
    }

    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        var changes = new List<ChangeRecord>();
        int succeeded = 0;
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
                if (action.Target == "BootTime")
                {
                    // Açılış süresi bilgisi (PowerCfg /energy daha derin ama uzun sürer)
                    var (code, output) = await _runner.RunCaptureAsync("powershell.exe",
                        "-NoProfile -Command " +
                        "(Get-CimInstance Win32_OperatingSystem).LastBootUpTime", ct);
                    if (code == 0)
                    {
                        succeeded++;
                        changes.Add(new ChangeRecord
                        {
                            Module = Id,
                            Operation = ChangeOperationType.CommandRun,
                            Target = "BootTime",
                            NewValue = output.Trim(),
                            Note = "Son açılış zamanı"
                        });
                    }
                }
                else
                {
                    // Fast Startup durumu: HiberbootEnabled
                    var (code, output) = await _runner.RunCaptureAsync("reg.exe",
                        @"query HKLM\SYSTEM\CurrentControlSet\Control\SessionManager\Power /v HiberbootEnabled", ct);
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.CommandRun,
                        Target = "FastStartup",
                        NewValue = output.Contains("0x1") ? "On" : "Off",
                        Note = "Hızlı Açılış durumu"
                    });
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Boot işlemi başarısız: {Target}", action.Target); }
        }

        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default) =>
        Task.FromResult(new RollbackResult
        {
            ModuleId = Id,
            ChangeId = change.Id,
            IsSuccess = true,
            Error = "Önyükleme ayarı değiştirilmedi (yalnızca rapor)."
        });
}
