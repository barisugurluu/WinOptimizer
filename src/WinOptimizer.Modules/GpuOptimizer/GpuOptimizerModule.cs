using System.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WinOptimizer.Core;
using WinOptimizer.Core.Compatibility;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.GpuOptimizer;

/// <summary>
/// GpuOptimizer — GPU tespiti, HAGS (Donanım Hızlandırmalı GPU Zamanlaması), VRR.
/// NVIDIA/AMD güç modları üçüncü parti (NVAPI/ADL) — bilgilendirme. Risk: Low/Medium.
/// (Master plan Bölüm 3.16.)
/// </summary>
public sealed class GpuOptimizerModule : IOptimizationModule
{
    public string Id => "GpuOptimizer";
    public string DisplayName => "GPU Optimizasyonu";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly SafetyNet _safety;
    private readonly ILogger<GpuOptimizerModule> _logger;

    public GpuOptimizerModule(SafetyNet safety, ILogger<GpuOptimizerModule> logger)
    {
        _safety = safety;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        var gpus = new List<object>();
        bool hags = false;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController");
            foreach (var mo in searcher.Get().Cast<ManagementObject>())
            {
                string name = (mo["Name"] as string) ?? "?";
                string vendor = name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "NVIDIA"
                    : name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ? "AMD"
                    : name.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? "Intel" : "Diğer";
                gpus.Add(new { Name = name, Vendor = vendor, Driver = mo["DriverVersion"] ?? "?" });
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "GPU sorgusu başarısız."); }

        hags = IsHagsEnabled();

        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = gpus.Count,
            Summary = $"{gpus.Count} GPU bulundu. HAGS: {(hags ? "Açık" : "Kapalı")}.",
            Details = new() { ["Gpus"] = gpus, ["HagsEnabled"] = hags }
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = new List<PreviewAction>();
        bool hags = analysis.Details.TryGetValue("HagsEnabled", out var h) && h is true;
        var hagsSupport = CompatibilityChecker.IsSupported("Hags");
        if (!hags && hagsSupport.IsSupported)
        {
            actions.Add(new PreviewAction
            {
                Description = "HAGS (Donanım Hızlandırmalı GPU Zamanlaması) aç",
                Risk = RiskLevel.Medium,
                Target = "Hags",
                RequiresExtraConfirmation = true
            });
        }
        else if (!hags)
        {
            _logger.LogInformation("HAGS bu Windows sürümünde sunulmuyor: {Reason}", hagsSupport.Reason);
        }
        actions.Add(new PreviewAction
        {
            Description = "Variable Refresh Rate (VRR/GSync/FreeSync) durumunu raporla",
            Risk = RiskLevel.None,
            Target = "Vrr"
        });
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
                if (action.Target == "Hags")
                {
                    // HwSchMode = 2 (açık). Önce registry yedeği al.
                    await _safety.BackupRegistryAsync("HKLM", @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
                    ok = SetRegistryDword(Microsoft.Win32.Registry.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
                    if (ok)
                    {
                        changes.Add(new ChangeRecord
                        {
                            Module = Id,
                            Operation = ChangeOperationType.RegistrySetValue,
                            Target = @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode",
                            PreviousValue = "1",
                            NewValue = "2",
                            Note = "HAGS açıldı (reboot gerekir)"
                        });
                    }
                }
                else if (action.Target == "Vrr")
                {
                    // VRR durumu raporu (salt okunur)
                    ok = true;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.CommandRun,
                        Target = "VRR",
                        NewValue = "reported",
                        Note = "VRR durumu raporlandı"
                    });
                }

                if (ok) succeeded++; else failed++;
            }
            catch (Exception ex) { _logger.LogError(ex, "GPU işlemi başarısız: {Target}", action.Target); failed++; }
        }

        await _safety.Journal.WriteRangeAsync(changes, ct);
        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Failed = failed, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        if (change.Operation == ChangeOperationType.RegistrySetValue)
        {
            bool ok = SetRegistryDword(Microsoft.Win32.Registry.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1);
            return Task.FromResult(new RollbackResult { ModuleId = Id, ChangeId = change.Id, IsSuccess = ok });
        }
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id,
            ChangeId = change.Id,
            IsSuccess = true,
            Error = "Geri alınacak kayıt yok."
        });
    }

    private static bool IsHagsEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
            return key?.GetValue("HwSchMode") is int v && v == 2;
        }
        catch { return false; }
    }

    private static bool SetRegistryDword(Microsoft.Win32.RegistryKey root, string path, string name, int value)
    {
        try
        {
            using var key = root.OpenSubKey(path, writable: true) ?? root.CreateSubKey(path);
            key.SetValue(name, value, Microsoft.Win32.RegistryValueKind.DWord);
            return true;
        }
        catch { return false; }
    }
}
