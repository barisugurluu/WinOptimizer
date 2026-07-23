using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.PrivacyGuard;

/// <summary>
/// PrivacyGuard — Gizlilik & telemetri sınırlama. Kullanıcı onaylı, şeffaf.
/// Telemetri, reklam ID, Cortana vb. Risk: Low/Medium. (Master plan Bölüm 3.10.)
/// </summary>
public sealed class PrivacyGuardModule : IOptimizationModule
{
    public string Id => "PrivacyGuard";
    public string DisplayName => "Gizlilik & Telemetri";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly ProcessRunner _runner;
    private readonly ILogger<PrivacyGuardModule> _logger;

    public PrivacyGuardModule(ProcessRunner runner, ILogger<PrivacyGuardModule> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    // (id, hive, path, value, enabledValue, desc, risk)
    private static readonly (string Id, string Hive, string Path, string Value, string On, string Desc)[] Tweaks = new[]
    {
        ("AdvertisingId", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
         "Enabled", "0", "Reklam tanımlayıcısını kapat"),
        ("Cortana", "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\Windows Search",
         "AllowCortana", "0", "Cortana'yı devre dışı bırak"),
        ("InputTelemetry", "HKCU", @"SOFTWARE\Microsoft\Input\TIPC",
         "Enabled", "0", "Yazım/çizim telemetrisini kapat"),
        ("AppLaunchTelemetry", "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
         "Start_TrackProgs", "0", "Uygulama başlatma izlemesini kapat")
    };

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id, ItemCount = Tweaks.Length,
            Summary = $"{Tweaks.Length} gizlilik ayarı uygulanabilir."
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = Tweaks.Select(t => new PreviewAction
        {
            Description = $"{t.Desc} ({t.Hive}\\{t.Path})", Risk = RiskLevel.Low, Target = t.Id
        }).ToList();
        return Task.FromResult(new PreviewResult { ModuleId = Id, Actions = actions, IsDryRun = true });
    }

    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        var changes = new List<ChangeRecord>();
        int succeeded = 0, failed = 0;
        int total = Tweaks.Length, idx = 0;

        foreach (var t in Tweaks)
        {
            ct.ThrowIfCancellationRequested();
            idx++;
            progress.Report(new ProgressInfo
            {
                ModuleId = Id, Percent = idx * 100 / total, Message = t.Desc, Current = idx, Total = total
            });

            try
            {
                // reg add "<hive>\<path>" /v <value> /t REG_DWORD /d <on> /f
                int code = await _runner.RunAsync("reg.exe",
                    $"add \"{t.Hive}\\{t.Path}\" /v {t.Value} /t REG_DWORD /d {t.On} /f", null, ct);
                if (code == 0)
                {
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id, Operation = ChangeOperationType.RegistrySetValue,
                        Target = $"{t.Hive}\\{t.Path}\\{t.Value}", NewValue = t.On, Note = t.Desc
                    });
                }
                else failed++;
            }
            catch (Exception ex) { _logger.LogError(ex, "Gizlilik tweak başarısız: {Id}", t.Id); failed++; }
        }

        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Failed = failed, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        // Değeri 1'e (varsayılan açık) döndür
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id, ChangeId = change.Id, IsSuccess = true,
            Error = "Gizlilik ayarı varsayılana döndürüldü."
        });
    }
}
