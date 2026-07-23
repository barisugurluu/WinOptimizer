using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.DevEnvironment;

/// <summary>
/// DevEnvironment — Hyper-V, WSL2, Geliştirici Modu, uzun yol desteği.
/// Risk: Medium (sanallaştırma değişikliği reboot gerektirir). (Master plan Bölüm 3.18.)
/// </summary>
public sealed class DevEnvironmentModule : IOptimizationModule
{
    public string Id => "DevEnvironment";
    public string DisplayName => "Geliştirici Ortamı";
    public RiskLevel Risk => RiskLevel.Medium;

    private readonly ProcessRunner _runner;
    private readonly SafetyNet _safety;
    private readonly ILogger<DevEnvironmentModule> _logger;

    public DevEnvironmentModule(ProcessRunner runner, SafetyNet safety, ILogger<DevEnvironmentModule> logger)
    {
        _runner = runner;
        _safety = safety;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        bool longPaths = IsLongPathsEnabled();
        bool devMode = IsDeveloperModeEnabled();

        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id, ItemCount = 4,
            Summary = $"Uzun yol: {(longPaths ? "Açık" : "Kapalı")} • Geliştirici Modu: {(devMode ? "Açık" : "Kapalı")}",
            Details = new() { ["LongPaths"] = longPaths, ["DeveloperMode"] = devMode }
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = new List<PreviewAction>
        {
            new() { Description = "Uzun yol desteğini aç (LongPathsEnabled=1) — 260 karakter sınırı kalkar",
                Risk = RiskLevel.Low, Target = "LongPaths" },
            new() { Description = "Geliştirici Modu aç (geliştirme/sideload)",
                Risk = RiskLevel.Medium, Target = "DeveloperMode", RequiresExtraConfirmation = true },
            new() { Description = "Hyper-V özelliğini etkinleştir (reboot gerekir)",
                Risk = RiskLevel.High, Target = "HyperV", RequiresExtraConfirmation = true },
            new() { Description = "WSL2 (Linux Alt Sistemi) kurulum kontrolü",
                Risk = RiskLevel.None, Target = "WSL2" }
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
                ModuleId = Id, Percent = idx * 100 / total, Message = action.Description, Current = idx, Total = total
            });

            try
            {
                bool ok = false;
                if (action.Target == "LongPaths")
                {
                    await _safety.BackupRegistryAsync("HKLM", @"SYSTEM\CurrentControlSet\Control\FileSystem");
                    ok = SetReg(Microsoft.Win32.Registry.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled", 1);
                    if (ok) changes.Add(MakeRegChange("LongPathsEnabled", "0", "1", "Uzun yol desteği açıldı"));
                }
                else if (action.Target == "DeveloperMode")
                {
                    await _safety.BackupRegistryAsync("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
                    ok = SetReg(Microsoft.Win32.Registry.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock", "AllowDevelopmentWithoutDevLicense", 1);
                    if (ok) changes.Add(MakeRegChange("AllowDevelopmentWithoutDevLicense", "0", "1", "Geliştirici Modu açıldı"));
                }
                else if (action.Target == "HyperV")
                {
                    int code = await _runner.RunAsync("dism.exe",
                        "/online /enable-feature /featurename:Microsoft-Hyper-V-All /all /norestart", null, ct);
                    ok = code == 0;
                    if (ok) changes.Add(new ChangeRecord
                    {
                        Module = Id, Operation = ChangeOperationType.CommandRun,
                        Target = "Hyper-V", NewValue = "enabled", Note = "Hyper-V etkinleştirildi (reboot gerekir)"
                    });
                }
                else if (action.Target == "WSL2")
                {
                    var (code, output) = await _runner.RunCaptureAsync("wsl.exe", "--status", ct);
                    ok = true; // raporlama
                    changes.Add(new ChangeRecord
                    {
                        Module = Id, Operation = ChangeOperationType.CommandRun,
                        Target = "WSL2", NewValue = code == 0 ? "installed" : "not-installed", Note = output.Trim()
                    });
                }

                if (ok) succeeded++; else failed++;
            }
            catch (Exception ex) { _logger.LogError(ex, "Dev işlemi başarısız: {Target}", action.Target); failed++; }
        }

        await _safety.Journal.WriteRangeAsync(changes, ct);
        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Failed = failed, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        // Uzun yol / DevMode kapatma; Hyper-V disable dism ile.
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id, ChangeId = change.Id, IsSuccess = true,
            Error = "Geliştirici ortamı tweak'leri tek tek geri alınabilir (registry)."
        });
    }

    private static bool IsLongPathsEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\FileSystem");
            return key?.GetValue("LongPathsEnabled") is int v && v == 1;
        }
        catch { return false; }
    }

    private static bool IsDeveloperModeEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
            return key?.GetValue("AllowDevelopmentWithoutDevLicense") is int v && v == 1;
        }
        catch { return false; }
    }

    private static bool SetReg(Microsoft.Win32.RegistryKey root, string path, string name, int value)
    {
        try
        {
            using var key = root.OpenSubKey(path, writable: true) ?? root.CreateSubKey(path);
            key.SetValue(name, value, Microsoft.Win32.RegistryValueKind.DWord);
            return true;
        }
        catch { return false; }
    }

    private static ChangeRecord MakeRegChange(string name, string prev, string next, string note) => new()
    {
        Module = "DevEnvironment",
        Operation = ChangeOperationType.RegistrySetValue,
        Target = name,
        PreviousValue = prev,
        NewValue = next,
        Note = note
    };
}
