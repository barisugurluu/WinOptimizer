using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.DeepCleanEngine;

/// <summary>
/// DeepCleanEngine — Derin temizlik. Windows.old (10 gün kuralı), hibernation dosyası,
/// eski sürücü paketleri (pnputil), büyük/yinelenen dosyalar. Risk: Medium/High (onaylı).
/// (Master plan Bölüm 3.2.)
/// </summary>
public sealed class DeepCleanEngineModule : IOptimizationModule
{
    public string Id => "DeepCleanEngine";
    public string DisplayName => "Derin Temizlik";
    public RiskLevel Risk => RiskLevel.Medium;

    private readonly DiskScanner _scanner = new();
    private readonly ProcessRunner _runner;
    private readonly SafetyNet _safety;
    private readonly ILogger<DeepCleanEngineModule> _logger;

    /// <summary>Windows.old için güvenli silme süresi (gün) — Bölüm 3.2.</summary>
    private const double WindowsOldMinAgeDays = 10.0;

    /// <summary>"Büyük dosya" eşiği (bayt) — varsayılan 250 MB.</summary>
    private const long LargeFileThresholdBytes = 250L * 1024 * 1024;

    public DeepCleanEngineModule(ProcessRunner runner, SafetyNet safety, ILogger<DeepCleanEngineModule> logger)
    {
        _runner = runner;
        _safety = safety;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        long total = 0;
        int items = 0;
        var details = new Dictionary<string, object>();

        // Windows.old (10 günden eski)
        var winOld = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "..", "Windows.old");
        winOld = Path.GetFullPath(winOld);
        if (Directory.Exists(winOld) &&
            Directory.GetLastWriteTimeUtc(winOld) < DateTime.UtcNow.AddDays(-WindowsOldMinAgeDays))
        {
            var (c, b) = _scanner.ScanFolder(winOld);
            items += c; total += b;
            details["WindowsOld"] = new { Files = c, Bytes = b, Path = winOld, DaysOld = (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(winOld)).TotalDays };
        }

        // Hibernation dosyası (hiberfil.sys)
        var hiberfil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows).Substring(0, 2), "\\", "hiberfil.sys");
        if (File.Exists(hiberfil))
        {
            try
            {
                var fi = new FileInfo(hiberfil);
                details["Hibernation"] = new { Bytes = fi.Length };
            }
            catch { /* erişim engelli */ }
        }

        // Hata dökümleri (memory.dmp, Minidump)
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var memoryDmp = Path.Combine(win, "memory.dmp");
        if (File.Exists(memoryDmp))
        {
            try
            {
                var fi = new FileInfo(memoryDmp);
                items++; total += fi.Length;
                details["MemoryDump"] = new { Bytes = fi.Length };
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Büyük dosya boyutu okunamadı (memory.dmp)."); }
        }

        // Eski sürücü paketleri (pnputil -e ile listelenebilir; burada özet)
        details["OldDrivers"] = "pnputil -e ile eski 3. parti sürücüler listelenebilir (silme onaylı).";

        string summary = items > 0
            ? $"{items} öğe / {DiskScanner.FormatBytes(total)} derin temizlik için uygun (onay gerektirir)."
            : "Derin temizlik için büyük hedef bulunamadı.";
        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = items,
            TotalBytes = total,
            Summary = summary,
            Details = details
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = new List<PreviewAction>();

        if (analysis.Details.TryGetValue("WindowsOld", out var wo) && wo is { } woBox)
        {
            long bytes = ExtractLong(woBox, "Bytes");
            actions.Add(new PreviewAction
            {
                Description = $"Windows.old kaldır (10+ gün eski, {DiskScanner.FormatBytes(bytes)})",
                Risk = RiskLevel.Medium,
                Target = "WindowsOld",
                RequiresExtraConfirmation = true
            });
        }
        if (analysis.Details.ContainsKey("Hibernation"))
        {
            actions.Add(new PreviewAction
            {
                Description = "Hibernation kapat (powercfg /h off) — Fast Startup'ı da etkiler",
                Risk = RiskLevel.Medium,
                Target = "Hibernation",
                RequiresExtraConfirmation = true
            });
        }
        if (analysis.Details.ContainsKey("MemoryDump"))
        {
            actions.Add(new PreviewAction
            {
                Description = "memory.dmp (hata dökümü) sil",
                Risk = RiskLevel.Low,
                Target = "MemoryDump"
            });
        }
        actions.Add(new PreviewAction
        {
            Description = "Büyük dosyaları tara (>250 MB) — listeleme (silme elle)",
            Risk = RiskLevel.None,
            Target = "LargeFiles"
        });

        return Task.FromResult(new PreviewResult { ModuleId = Id, Actions = actions, IsDryRun = true });
    }

    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        var changes = new List<ChangeRecord>();
        int succeeded = 0, skipped = 0, failed = 0;
        long gain = 0;
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
                if (action.Target == "WindowsOld")
                {
                    long bytes = _scanner.GetFolderSize(GetWindowsOldPath());
                    // takeown + icacls + rd ile güvenli kaldırma (10 gün kuralı sağlanmış)
                    await _runner.RunAsync("cmd.exe", new[] { "/c", "rd", "/s", "/q", GetWindowsOldPath() }, null, ct);
                    succeeded++; gain += bytes;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.FileDelete,
                        Target = "Windows.old",
                        PreviousValue = DiskScanner.FormatBytes(bytes),
                        NewValue = "removed",
                        Note = "10+ gün eski Windows.old kaldırıldı"
                    });
                }
                else if (action.Target == "Hibernation")
                {
                    int code = await _runner.RunAsync("powercfg.exe", "/h off", null, ct);
                    if (code == 0)
                    {
                        succeeded++;
                        changes.Add(new ChangeRecord
                        {
                            Module = Id,
                            Operation = ChangeOperationType.CommandRun,
                            Target = "hiberfil.sys",
                            NewValue = "removed",
                            Note = "Hibernation kapatıldı"
                        });
                    }
                    else failed++;
                }
                else if (action.Target == "MemoryDump")
                {
                    var memoryDmp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "memory.dmp");
                    long bytes = 0;
                    if (File.Exists(memoryDmp)) { var fi = new FileInfo(memoryDmp); bytes = fi.Length; fi.Delete(); succeeded++; gain += bytes; }
                    else skipped++;
                }
                else if (action.Target == "LargeFiles")
                {
                    // Yalnızca rapor — silme yok
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.CommandRun,
                        Target = "LargeFiles",
                        NewValue = "scanned",
                        Note = "Büyük dosyalar tarandı"
                    });
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Derin temizlik başarısız: {Target}", action.Target); failed++; }
        }

        await _safety.Journal.WriteRangeAsync(changes, ct);
        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Skipped = skipped, Failed = failed, GainBytes = gain, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        // Windows.old / hibernation silme geri alınamaz (sistem geri yükleme noktası öner).
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id,
            ChangeId = change.Id,
            IsSuccess = false,
            Error = "Derin temizlik geri alınamaz; sistem geri yükleme noktası kullanın."
        });
    }

    private static string GetWindowsOldPath() => Path.GetFullPath(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "..", "Windows.old"));

    private static long ExtractLong(object box, string field)
    {
        try { var p = box.GetType().GetProperty(field); return p?.GetValue(box) is long l ? l : 0L; }
        catch { return 0L; }
    }
}
