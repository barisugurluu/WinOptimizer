using System.Management;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.SecurityHardening;

/// <summary>
/// SecurityHardening — Defender, ASR kuralları, Controlled Folder Access, PUA, HVCI/VBS.
/// KURAL: WinOptimizer Defender'ı ASLA kapatmaz (master plan Bölüm 3.14). Risk: Low.
/// </summary>
public sealed class SecurityHardeningModule : IOptimizationModule
{
    public string Id => "SecurityHardening";
    public string DisplayName => "Güvenlik Sertleştirme";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly ProcessRunner _runner;
    private readonly ILogger<SecurityHardeningModule> _logger;

    /// <summary>Önerilen güvenli ASR kuralları (Bölüm 3.14 beyaz liste).</summary>
    private static readonly (string Id, string Name)[] RecommendedAsrRules = new[]
    {
        ("D4F940AB-401B-4EFC-AADC-AD5F3C50688A", "Office alt süreç oluşturma engeli"),
        ("3B576869-A4EC-4529-8536-B80A7769E899", "Office Win32 API çağrı engeli"),
        ("9E6C4E1F-7D60-472F-BA1A-A39EF669E4B2", "Çalınan kimlik bilgileri kötüye kullanım engeli")
    };

    public SecurityHardeningModule(ProcessRunner runner, ILogger<SecurityHardeningModule> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        var details = new Dictionary<string, object>();
        var warnings = new List<string>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Defender",
                "SELECT * FROM MSFT_MpComputerStatus");
            var status = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (status is not null)
            {
                bool rtp = Convert.ToBoolean(status["RealTimeProtectionEnabled"]);
                details["RealTimeProtection"] = rtp;
                if (!rtp) warnings.Add("⚠ Gerçek zamanlı koruma KAPALI.");
                details["AntivirusSignatureAge"] = status["AntivirusSignatureAge"];
                if (Convert.ToUInt32(status["AntivirusSignatureAge"]) > 7)
                    warnings.Add("Defender imzası 7 günden eski.");
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Defender durumu sorgulanamadı."); }

        details["HvciEnabled"] = IsHvciEnabled();

        string summary = warnings.Count > 0 ? "⚠ " + string.Join(" | ", warnings) : "Güvenlik durumu sağlıklı.";
        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id, ItemCount = RecommendedAsrRules.Length + 1, Summary = summary, Details = details
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = new List<PreviewAction>
        {
            new() { Description = "Defender imza güncellemesi (Update-MpSignature)",
                Risk = RiskLevel.None, Target = "UpdateSignature" },
            new() { Description = "PUA (İstenmeyen Uygulama) korumasını aç",
                Risk = RiskLevel.Low, Target = "PuaProtection" }
        };
        foreach (var rule in RecommendedAsrRules)
        {
            actions.Add(new PreviewAction
            {
                Description = $"ASR kuralı: {rule.Name} (önce AuditMode)",
                Risk = RiskLevel.Low,
                Target = "Asr:" + rule.Id
            });
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
                ModuleId = Id, Percent = idx * 100 / total, Message = action.Description, Current = idx, Total = total
            });

            try
            {
                bool ok = false;
                if (action.Target == "UpdateSignature")
                {
                    int code = await _runner.RunAsync("powershell.exe",
                        "-NoProfile -Command Update-MpSignature", null, ct);
                    ok = code == 0;
                }
                else if (action.Target == "PuaProtection")
                {
                    int code = await _runner.RunAsync("powershell.exe",
                        "-NoProfile -Command Set-MpPreference -PUAProtection Enabled", null, ct);
                    ok = code == 0;
                }
                else if (action.Target?.StartsWith("Asr:", StringComparison.Ordinal) == true)
                {
                    var ruleId = action.Target[4..];
                    // ASR kuralları önce AuditMode'da denenir (master plan 3.14)
                    int code = await _runner.RunAsync("powershell.exe",
                        $"-NoProfile -Command Add-MpPreference -AttackSurfaceReductionRules_Ids {ruleId} " +
                        "-AttackSurfaceReductionRules_Actions AuditMode", null, ct);
                    ok = code == 0;
                }

                if (ok)
                {
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id, Operation = ChangeOperationType.CommandRun,
                        Target = action.Target ?? string.Empty, NewValue = "enabled", Note = action.Description
                    });
                }
                else failed++;
            }
            catch (Exception ex) { _logger.LogError(ex, "Güvenlik işlemi başarısız: {Target}", action.Target); failed++; }
        }

        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Failed = failed, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        // Defender güçlendirme geri alınmaz (zaten güvenliği artırır).
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id, ChangeId = change.Id, IsSuccess = true,
            Error = "Güvenlik sertleştirme geri alınmaz; zaten sistemi korur."
        });
    }

    /// <summary>HVCI (Memory Integrity) etkin mi? (DeviceGuard registry).</summary>
    private static bool IsHvciEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
            return key?.GetValue("Enabled") is int v && v == 1;
        }
        catch { return false; }
    }
}
