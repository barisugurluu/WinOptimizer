using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.CleanEngine;

/// <summary>
/// CleanEngine — Disk &amp; önbellek temizliği modülü. Risk: Low.
/// (Master plan Bölüm 3.1.)
/// </summary>
public sealed class CleanEngineModule : IOptimizationModule
{
    public string Id => "CleanEngine";
    public string DisplayName => "Disk & Önbellek Temizliği";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly DiskCleaner _cleaner;
    private readonly SafetyNet _safety;
    private readonly ILogger<CleanEngineModule> _logger;

    public CleanEngineModule(SafetyNet safety, ILogger<CleanEngineModule> logger)
    {
        _safety = safety;
        _logger = logger;
        _cleaner = new DiskCleaner(logger);
    }

    private sealed record CleanStep(string Name, string Folder, Func<FileInfo, bool>? Predicate,
        SearchOption Option, RiskLevel Risk, string Description);

    private CleanStep[] BuildSteps() => new[]
    {
        new CleanStep("Temp", CleanTargets.TempFolders[0],
            fi => DiskCleaner.IsOlderThan(fi, CleanTargets.MinFileAgeHours),
            SearchOption.AllDirectories, RiskLevel.Low, "%TEMP% klasörü (24 saatten eski)"),
        new CleanStep("SystemTemp", CleanTargets.TempFolders[1],
            fi => DiskCleaner.IsOlderThan(fi, CleanTargets.MinFileAgeHours),
            SearchOption.AllDirectories, RiskLevel.Low, "C:\\Windows\\Temp (24 saatten eski)"),
        new CleanStep("Prefetch", CleanTargets.PrefetchFolder,
            fi => fi.Extension.Equals(".pf", StringComparison.OrdinalIgnoreCase),
            SearchOption.TopDirectoryOnly, RiskLevel.Low, "Prefetch önbelleği"),
        new CleanStep("WUDownload", CleanTargets.WindowsUpdateDownload,
            null, SearchOption.AllDirectories, RiskLevel.Low, "Windows Update indirilenler"),
        new CleanStep("DeliveryOptimization", CleanTargets.DeliveryOptimization,
            null, SearchOption.AllDirectories, RiskLevel.Low, "Delivery Optimization önbelleği"),
        new CleanStep("Logs", CleanTargets.LogFolders[0],
            fi => CleanTargets.LogExtensions.Contains(fi.Extension),
            SearchOption.AllDirectories, RiskLevel.Low, "Sistem günlükleri (.log/.cab)"),
        new CleanStep("CBS", CleanTargets.LogFolders[1],
            fi => CleanTargets.LogExtensions.Contains(fi.Extension),
            SearchOption.AllDirectories, RiskLevel.Low, "CBS günlükleri"),
        new CleanStep("WER", CleanTargets.LogFolders[2],
            null, SearchOption.AllDirectories, RiskLevel.Low, "Windows Error Reporting")
    };
    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        long total = 0;
        int items = 0;
        var details = new Dictionary<string, object>();

        foreach (var step in BuildSteps())
        {
            ct.ThrowIfCancellationRequested();
            var (count, bytes) = _cleaner.AnalyzeFolder(step.Folder, step.Predicate, step.Option);
            total += bytes;
            items += count;
            details[step.Name] = new { Files = count, Bytes = bytes, Path = step.Folder };
        }

        foreach (var ud in CleanTargets.GetBrowserUserdataFolders())
        {
            ct.ThrowIfCancellationRequested();
            var (bCount, bBytes) = AnalyzeBrowserCache(ud);
            total += bBytes;
            items += bCount;
            details[$"Browser:{Path.GetFileName(Path.GetDirectoryName(ud))}"] =
                new { Files = bCount, Bytes = bBytes };
        }

        // Firefox profilleri (Bölüm 3.1 — profiles.ini çözümleme)
        foreach (var ffProfile in CleanTargets.GetFirefoxProfilePaths())
        {
            ct.ThrowIfCancellationRequested();
            var (fCount, fBytes) = AnalyzeFirefoxCache(ffProfile);
            total += fBytes;
            items += fCount;
            details[$"Firefox:{Path.GetFileName(ffProfile)}"] = new { Files = fCount, Bytes = fBytes };
        }

        // Geri Dönüşüm kutusu (Bölüm 3.1 / 11.5 — SHEmptyRecycleBin)
        try
        {
            long recycleBytes = WinOptimizer.Native.Shell32.GetRecycleBinSize();
            if (recycleBytes > 0)
            {
                total += recycleBytes;
                details["RecycleBin"] = new { Files = 0, Bytes = recycleBytes, Note = "Geri Dönüşüm kutusu" };
            }
        }
        catch { /* erişim engelli */ }

        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = items,
            TotalBytes = total,
            Summary = $"{items:N0} dosya / {FormatBytes(total)} temizlenebilir.",
            Details = details
        });
    }

    /// <inheritdoc/>
    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = new List<PreviewAction>();
        foreach (var step in BuildSteps())
        {
            if (analysis.Details.TryGetValue(step.Name, out var d) && d is { } box)
            {
                long bytes = ExtractLong(box, "Bytes");
                int files = ExtractInt(box, "Files");
                if (files > 0)
                {
                    actions.Add(new PreviewAction
                    {
                        Description = $"{step.Description}: {files:N0} dosya ({FormatBytes(bytes)})",
                        Risk = step.Risk,
                        Target = step.Folder,
                        Bytes = bytes
                    });
                }
            }
        }

        // Geri Dönüşüm kutusu önizleme (kullanıcıya önceden sorulur — Bölüm 3.1)
        if (analysis.Details.TryGetValue("RecycleBin", out var rb) && rb is { } rbBox)
        {
            long rbBytes = ExtractLong(rbBox, "Bytes");
            if (rbBytes > 0)
            {
                actions.Add(new PreviewAction
                {
                    Description = $"Geri Dönüşüm kutusunu boşalt ({FormatBytes(rbBytes)})",
                    Risk = RiskLevel.Low,
                    Target = "RecycleBin",
                    Bytes = rbBytes,
                    RequiresExtraConfirmation = true
                });
            }
        }

        return Task.FromResult(new PreviewResult
        {
            ModuleId = Id,
            Actions = actions,
            EstimatedGainBytes = analysis.TotalBytes,
            IsDryRun = true
        });
    }

    /// <inheritdoc/>
    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        var changes = new List<ChangeRecord>();
        var errors = new List<string>();
        int succeeded = 0, skipped = 0, failed = 0;
        long gain = 0;

        var steps = BuildSteps().ToList();
        int totalSteps = steps.Count;
        int stepIndex = 0;

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            stepIndex++;
            progress.Report(new ProgressInfo
            {
                ModuleId = Id,
                Percent = (int)(stepIndex * 100.0 / totalSteps),
                Message = step.Description,
                Current = stepIndex,
                Total = totalSteps
            });

            var (deleted, sk, bytes) = _cleaner.CleanFolder(
                step.Folder, step.Predicate, toRecycle: false, step.Option);
            succeeded += deleted;
            skipped += sk;
            gain += bytes;

            if (deleted > 0)
            {
                changes.Add(new ChangeRecord
                {
                    Module = Id,
                    Operation = ChangeOperationType.FileDelete,
                    Target = step.Folder,
                    PreviousValue = deleted.ToString(),
                    NewValue = "0",
                    Note = $"{step.Description} — {deleted} dosya silindi"
                });
            }
        }

        // Geri Dönüşüm kutusu boşaltma (Bölüm 11.5 — SHEmptyRecycleBin)
        if (preview.Actions.Any(a => a.Target == "RecycleBin"))
        {
            stepIndex++;
            progress.Report(new ProgressInfo
            {
                ModuleId = Id,
                Percent = 100,
                Message = "Geri Dönüşüm kutusu boşaltılıyor…",
                Current = stepIndex,
                Total = totalSteps + 1
            });
            try
            {
                long beforeSize = WinOptimizer.Native.Shell32.GetRecycleBinSize();
                bool ok = WinOptimizer.Native.Shell32.EmptyRecycleBin();
                if (ok && beforeSize > 0)
                {
                    gain += beforeSize;
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.RecycleBinEmpty,
                        Target = "RecycleBin",
                        PreviousValue = FormatBytes(beforeSize),
                        NewValue = "0",
                        Note = "Geri Dönüşüm kutusu boşaltıldı"
                    });
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Geri Dönüşüm kutusu boşaltılamadı."); }
        }

        await _safety.RecordRangeAsync(changes, ct);

        return new ExecutionResult
        {
            ModuleId = Id,
            Succeeded = succeeded,
            Skipped = skipped,
            Failed = failed,
            GainBytes = gain,
            Changes = changes,
            Errors = errors
        };
    }

    /// <inheritdoc/>
    /// <remarks>Dosya silme doğası gereği geri alınamaz; sistem geri yükleme noktası önerilir.</remarks>
    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        _logger.LogWarning("CleanEngine dosya silme geri alınamaz (target: {Target}). " +
            "Sistem geri yükleme noktasından yararlanın.", change.Target);
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id,
            ChangeId = change.Id,
            IsSuccess = false,
            Error = "Silinen geçici/önbellek dosyaları geri alınamaz; sistem geri yükleme noktası kullanın."
        });
    }

    // --- Tarayıcı önbellek analizi (korumalı çerez/şifre dosyaları hariç) ---
    private (int Count, long Bytes) AnalyzeBrowserCache(string userData)
    {
        int count = 0;
        long bytes = 0;
        foreach (var profile in Directory.EnumerateDirectories(userData, "*", SearchOption.TopDirectoryOnly))
        {
            foreach (var cache in CleanTargets.ChromiumCacheSubdirs)
            {
                var cacheDir = Path.Combine(profile, cache);
                if (!Directory.Exists(cacheDir)) continue;
                var (c, b) = _cleaner.AnalyzeFolder(cacheDir,
                    fi => !CleanTargets.BrowserProtectedFiles.Contains(fi.Name));
                count += c;
                bytes += b;
            }
        }
        return (count, bytes);
    }

    // --- Firefox önbellek analizi (cache2, startupCache — Bölüm 3.1) ---
    private (int Count, long Bytes) AnalyzeFirefoxCache(string profilePath)
    {
        int count = 0;
        long bytes = 0;
        foreach (var cache in CleanTargets.FirefoxCacheSubdirs)
        {
            var cacheDir = Path.Combine(profilePath, cache);
            if (!Directory.Exists(cacheDir)) continue;
            var (c, b) = _cleaner.AnalyzeFolder(cacheDir);
            count += c;
            bytes += b;
        }
        return (count, bytes);
    }

    // --- Anonim nesneden alan okuma yardımcıları (analiz details) ---
    private static long ExtractLong(object box, string field)
    {
        try
        {
            var p = box.GetType().GetProperty(field);
            return p?.GetValue(box) is long l ? l : 0L;
        }
        catch { return 0L; }
    }

    private static int ExtractInt(object box, string field)
    {
        try
        {
            var p = box.GetType().GetProperty(field);
            return p?.GetValue(box) is int i ? i : 0;
        }
        catch { return 0; }
    }

    public static string FormatBytes(long bytes) => FileSizeFormatter.Format(bytes);
}
