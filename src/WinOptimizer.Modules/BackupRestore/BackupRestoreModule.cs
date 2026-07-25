using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.BackupRestore;

/// <summary>
/// BackupRestore — wbadmin sistem görüntüsü (BMR), sistem durumu, birim gölge kopyası.
/// C: sürücüsüne yedek ALINMAZ. wbadmin Home'da sınırlı (master plan Bölüm 3.15).
/// Risk: Low (yedekleme).
/// </summary>
public sealed class BackupRestoreModule : IOptimizationModule
{
    public string Id => "BackupRestore";
    public string DisplayName => "Yedekleme & Geri Yükleme";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly ProcessRunner _runner;
    private readonly ILogger<BackupRestoreModule> _logger;

    public BackupRestoreModule(ProcessRunner runner, ILogger<BackupRestoreModule> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        string versions = "(sorgulanamadı)";
        int versionCount = 0;
        try
        {
            var (code, output) = await _runner.RunCaptureAsync("wbadmin.exe", "get versions", ct);
            if (code == 0)
            {
                versionCount = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Count(l => l.Contains("Version identifier", StringComparison.OrdinalIgnoreCase));
                versions = versionCount > 0 ? $"{versionCount} yedek sürümü mevcut." : "Yedek yok.";
            }
            else
            {
                versions = "wbadmin bu sürümde sınırlı (Home olabilir). vssadmin alternatifi mevcut.";
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "wbadmin sorgulanamadı."); }

        return new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = versionCount,
            Summary = versions,
            Details = new() { ["wbadminAvailable"] = versionCount >= 0 }
        };
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = new List<PreviewAction>
        {
            new() { Description = "Sistem görüntüsü yedeği (BMR) — hedef sürücü seçilmeli",
                Risk = RiskLevel.Low, Target = "BmrBackup", RequiresExtraConfirmation = true },
            new() { Description = "Sistem durumu yedeği (kayıt defteri, boot)",
                Risk = RiskLevel.Low, Target = "SystemState", RequiresExtraConfirmation = true },
            new() { Description = "Birim gölge kopyası oluştur (vssadmin — Home alternatifi)",
                Risk = RiskLevel.Low, Target = "ShadowCopy" },
            new() { Description = "Yedek sürümlerini listele",
                Risk = RiskLevel.None, Target = "ListVersions" }
        };
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
                ModuleId = Id,
                Percent = idx * 100 / total,
                Message = action.Description,
                Current = idx,
                Total = total
            });

            try
            {
                bool ok = false;
                string note = action.Description;
                if (action.Target == "ListVersions")
                {
                    var (code, output) = await _runner.RunCaptureAsync("wbadmin.exe", "get versions", ct);
                    ok = code == 0;
                    note = $"Yedek sürümleri: {output.Split('\n').Length} satır";
                }
                else if (action.Target == "ShadowCopy")
                {
                    // C: sürücüsü için gölge kopya (Home uyumlu)
                    int code = await _runner.RunAsync("vssadmin.exe", "create shadow /for=C:", null, ct);
                    ok = code == 0;
                    note = "C: birim gölge kopyası oluşturuldu";
                }
                // BMR / SystemState: hedef sürücü gerektirir; UI'dan hedef seçimiyle çağrılır.

                if (ok)
                {
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.CommandRun,
                        Target = action.Target ?? string.Empty,
                        NewValue = "ok",
                        Note = note
                    });
                }
                else failed++;
            }
            catch (Exception ex) { _logger.LogError(ex, "Yedek işlemi başarısız: {Target}", action.Target); failed++; }
        }

        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Failed = failed, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        // Yedek oluşturma geri alınmaz (wbadmin delete catalog son çare, tehlikeli).
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id,
            ChangeId = change.Id,
            IsSuccess = true,
            Error = "Yedek oluşturma geri alınmaz; gereksiz yedek elle silinebilir."
        });
    }

    /// <summary>BMR yedeğini belirli hedefe alır (UI'dan hedef seçimiyle çağrılır).
    /// C: sürücüsüne yedek alınamaz (master plan Bölüm 3.15).</summary>
    public async Task<bool> CreateSystemImageBackupAsync(string targetDrive, CancellationToken ct = default)
    {
        if (targetDrive.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("C: sürücüsüne yedek alınamaz.");
            return false;
        }
        int code = await _runner.RunAsync("wbadmin.exe",
            $"start backup -backupTarget:{targetDrive} -include:C: -allCritical -quiet", null, ct);
        return code == 0;
    }
}
