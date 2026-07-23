using System.Diagnostics;
using System.Management;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.CpuEngine;

/// <summary>
/// CpuEngine — Otomatik servisler ve yüksek CPU tespiti.
/// Kritik servislere dokunulmaz (SafetyGuard). Risk: Low.
/// (Master plan Bölüm 3.5.)
/// </summary>
public sealed class CpuEngineModule : IOptimizationModule
{
    public string Id => "CpuEngine";
    public string DisplayName => "CPU & Başlangıç Optimizasyonu";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly SafetyNet _safety;
    private readonly ILogger<CpuEngineModule> _logger;

    public CpuEngineModule(SafetyNet safety, ILogger<CpuEngineModule> logger)
    {
        _safety = safety;
        _logger = logger;
    }

    /// <summary>Optimize edilebilir (kritik olmayan) otomatik servisler.</summary>
    private static readonly string[] OptimizableServices =
    {
        "DiagTrack", "dmwappushservice", "SysMain", "WSearch", "Fax", "fhsvc", "RetailDemo"
    };

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        int autoServices = 0;
        var serviceNames = new List<string>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, StartMode, State FROM Win32_Service WHERE StartMode='Auto'");
            foreach (var mo in searcher.Get().Cast<ManagementObject>())
            {
                ct.ThrowIfCancellationRequested();
                string? name = mo["Name"] as string;
                if (name is null) continue;
                if (OptimizableServices.Contains(name, StringComparer.OrdinalIgnoreCase) &&
                    !_safety.Guard.IsCriticalService(name))
                {
                    autoServices++;
                    serviceNames.Add(name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Servis listesi alınamadı.");
        }

        int highCpu = CountHighCpuProcesses();

        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = autoServices + highCpu,
            Summary = $"{autoServices} gereksiz otomatik servis, {highCpu} yüksek CPU süreci.",
            Details = new() { ["OptimizableServices"] = serviceNames }
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = new List<PreviewAction>();
        if (analysis.Details.TryGetValue("OptimizableServices", out var box) && box is List<string> list)
        {
            foreach (var svc in list)
            {
                actions.Add(new PreviewAction
                {
                    Description = $"{svc} servisini Manuel başlangıca çevir",
                    Risk = RiskLevel.Low,
                    Target = svc
                });
            }
        }
        return Task.FromResult(new PreviewResult { ModuleId = Id, Actions = actions, IsDryRun = true });
    }

    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        var changes = new List<ChangeRecord>();
        int succeeded = 0, skipped = 0, failed = 0;
        int total = preview.Actions.Count;
        int idx = 0;

        foreach (var action in preview.Actions)
        {
            ct.ThrowIfCancellationRequested();
            idx++;
            progress.Report(new ProgressInfo
            {
                ModuleId = Id, Percent = idx * 100 / Math.Max(1, total),
                Message = action.Description, Current = idx, Total = total
            });

            var svc = action.Target!;
            if (!_safety.Guard.IsAllowed(svc, out var reason))
            {
                _logger.LogWarning("Atlandı: {Reason}", reason);
                skipped++;
                continue;
            }

            try
            {
                using var p = Process.Start(new ProcessStartInfo("sc.exe", $"config {svc} start= demand")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
                });
                p?.WaitForExit();
                if (p?.ExitCode == 0)
                {
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.ServiceStartType,
                        Target = svc,
                        PreviousValue = "Automatic",
                        NewValue = "Manual",
                        Note = action.Description
                    });
                }
                else { failed++; }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Servis değiştirilemedi: {Svc}", svc);
                failed++;
            }
        }

        await _safety.Journal.WriteRangeAsync(changes, ct);
        return new ExecutionResult
        {
            ModuleId = Id, Succeeded = succeeded, Skipped = skipped, Failed = failed,
            Changes = changes
        };
    }

    public async Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        if (change.Operation != ChangeOperationType.ServiceStartType)
        {
            return new RollbackResult { ModuleId = Id, ChangeId = change.Id, IsSuccess = false, Error = "Desteklenmeyen işlem." };
        }

        try
        {
            using var p = Process.Start(new ProcessStartInfo("sc.exe",
                $"config {change.Target} start= {change.PreviousValue ?? "Auto"}")
            {
                UseShellExecute = false, CreateNoWindow = true
            });
            p?.WaitForExit();
            bool ok = p?.ExitCode == 0;
            return await Task.FromResult(new RollbackResult { ModuleId = Id, ChangeId = change.Id, IsSuccess = ok });
        }
        catch (Exception ex)
        {
            return new RollbackResult { ModuleId = Id, ChangeId = change.Id, IsSuccess = false, Error = ex.Message };
        }
    }

    /// <summary>CPU'su %50'den yüksek olan süreç sayısını sayar (anlık).</summary>
    private static int CountHighCpuProcesses()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PercentProcessorTime FROM Win32_PerfFormattedData_PerfProc_Process");
            int count = 0;
            foreach (var mo in searcher.Get().Cast<ManagementObject>())
            {
                if (Convert.ToUInt32(mo["PercentProcessorTime"]) >= 50) count++;
            }
            return count;
        }
        catch { return 0; }
    }
}
