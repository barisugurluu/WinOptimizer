using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.SystemTweaker;

/// <summary>
/// SystemTweaker — Registry ince ayarlarını uygular/geri alır.
/// Her tweak için önce registry yedeği alınır, sonra change journal'a yazılır.
/// Riskli tweak'ler ayrı onay ister. (Master plan Bölüm 3.9.)
/// </summary>
public sealed class SystemTweakerModule : IOptimizationModule
{
    public string Id => "SystemTweaker";
    public string DisplayName => "Sistem İnce Ayarları";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly SafetyNet _safety;
    private readonly ILogger<SystemTweakerModule> _logger;
    private readonly RegistryTweakApplier _applier = new();

    public SystemTweakerModule(SafetyNet safety, ILogger<SystemTweakerModule> logger)
    {
        _safety = safety;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        int available = 0;
        var details = new Dictionary<string, object>();
        foreach (var t in TweakCatalog.All)
        {
            bool alreadyOn = _applier.IsEnabled(t);
            if (!alreadyOn) available++;
            details[t.Id] = new { Enabled = alreadyOn, Risk = t.Risk.ToString(), t.Description };
        }
        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = available,
            Summary = $"{available} tweak uygulanabilir ({TweakCatalog.All.Count} toplam).",
            Details = details
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = new List<PreviewAction>();
        foreach (var t in TweakCatalog.All)
        {
            if (!_applier.IsEnabled(t))
            {
                actions.Add(new PreviewAction
                {
                    Description = $"{t.DisplayName} — {t.Description}",
                    Risk = t.Risk,
                    Target = t.Id,
                    RequiresExtraConfirmation = t.Risk >= RiskLevel.Medium
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
        int total = preview.Actions.Count;
        int idx = 0;

        foreach (var action in preview.Actions)
        {
            ct.ThrowIfCancellationRequested();
            idx++;
            progress.Report(new ProgressInfo
            {
                ModuleId = Id,
                Percent = idx * 100 / Math.Max(1, total),
                Message = action.Description,
                Current = idx,
                Total = total
            });

            var tweak = TweakCatalog.All.FirstOrDefault(t => t.Id == action.Target);
            if (tweak is null) { failed++; continue; }

            try
            {
                // Registry yedeği al (geri alma için)
                string hive = tweak.Hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";
                string? backup = await _safety.BackupRegistryAsync(hive, tweak.Path);

                var (ok, previous) = _applier.SetValue(tweak);
                if (ok)
                {
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.RegistrySetValue,
                        Target = $"{hive}\\{tweak.Path}\\{tweak.ValueName}",
                        PreviousValue = previous?.ToString() ?? "(yok)",
                        NewValue = tweak.EnabledValue.ToString(),
                        Backup = backup,
                        Note = tweak.DisplayName
                    });
                }
                else { failed++; }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tweak uygulanamadı: {Id}", tweak.Id);
                failed++;
            }
        }

        await _safety.Journal.WriteRangeAsync(changes, ct);
        return new ExecutionResult
        {
            ModuleId = Id,
            Succeeded = succeeded,
            Failed = failed,
            Changes = changes
        };
    }

    public async Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        // target: HKLM\path\value ayrıştır
        var parts = change.Target.Split('\\');
        if (parts.Length < 2)
        {
            return new RollbackResult { ModuleId = Id, ChangeId = change.Id, IsSuccess = false, Error = "Geçersiz hedef." };
        }

        var hive = parts[0] == "HKCU" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
        string valueName = parts[^1];
        string path = string.Join('\\', parts[1..^1]);

        var tweak = TweakCatalog.All.FirstOrDefault(t =>
            t.Hive == hive && t.ValueName == valueName && t.Path == path);
        if (tweak is null)
        {
            return new RollbackResult { ModuleId = Id, ChangeId = change.Id, IsSuccess = false, Error = "Tweak bulunamadı." };
        }

        object? prev = change.PreviousValue == "(yok)" ? null : change.PreviousValue;
        bool ok = _applier.RevertValue(tweak, prev);
        return await Task.FromResult(new RollbackResult { ModuleId = Id, ChangeId = change.Id, IsSuccess = ok });
    }
}
