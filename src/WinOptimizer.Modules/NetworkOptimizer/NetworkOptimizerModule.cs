using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.NetworkOptimizer;

/// <summary>
/// NetworkOptimizer — DNS önbellek temizliği, DHCP yenileme, Winsock/TCP sıfırlama.
/// Riskli reset'ler yalnızca teşhis modunda, kullanıcı onayıyla. Risk: Low/Medium.
/// (Master plan Bölüm 3.8.)
/// </summary>
public sealed class NetworkOptimizerModule : IOptimizationModule
{
    public string Id => "NetworkOptimizer";
    public string DisplayName => "Ağ Optimizasyonu";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly ProcessRunner _runner;
    private readonly ILogger<NetworkOptimizerModule> _logger;

    public NetworkOptimizerModule(ProcessRunner runner, ILogger<NetworkOptimizerModule> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    private static readonly (string Id, string File, string Args, string Desc, RiskLevel Risk)[] SafeSteps = new[]
    {
        ("FlushDns", "ipconfig", "/flushdns", "DNS önbelleğini temizle", RiskLevel.Low),
        ("ReleaseRenew", "ipconfig", "/release && ipconfig /renew", "DHCP IP yenile", RiskLevel.Low)
    };

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = SafeSteps.Length,
            Summary = $"{SafeSteps.Length} güvenli ağ işlemi mevcut (DNS, DHCP)."
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = SafeSteps.Select(s => new PreviewAction
        {
            Description = $"{s.Desc} ({s.File} {s.Args})",
            Risk = s.Risk,
            Target = s.Id
        }).ToList();
        return Task.FromResult(new PreviewResult { ModuleId = Id, Actions = actions, IsDryRun = true });
    }

    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        var changes = new List<ChangeRecord>();
        int succeeded = 0, failed = 0;
        int total = SafeSteps.Length, idx = 0;

        foreach (var step in SafeSteps)
        {
            ct.ThrowIfCancellationRequested();
            idx++;
            progress.Report(new ProgressInfo
            {
                ModuleId = Id,
                Percent = idx * 100 / total,
                Message = step.Desc,
                Current = idx,
                Total = total
            });

            try
            {
                // "&&" içeren args'ı cmd üzerinden çalıştır
                string file = step.File;
                string args = step.Args;
                if (args.Contains("&&"))
                {
                    file = "cmd.exe";
                    args = $"/c {step.File} {step.Args}";
                }

                int code = await _runner.RunAsync(file, args, null, ct);
                if (code == 0)
                {
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.CommandRun,
                        Target = $"{step.File} {step.Args}",
                        NewValue = "ok",
                        Note = step.Desc
                    });
                }
                else failed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ağ işlemi başarısız: {Id}", step.Id);
                failed++;
            }
        }

        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Failed = failed, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default) =>
        Task.FromResult(new RollbackResult
        {
            ModuleId = Id,
            ChangeId = change.Id,
            IsSuccess = true,
            Error = "DNS/DHCP sıfırlama geçicidir; sistem yeniden doldurur."
        });
}
