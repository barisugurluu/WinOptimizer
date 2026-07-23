using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Native;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.MemoryEngine;

/// <summary>
/// MemoryEngine — Boştaki süreçlerin working set'ini EmptyWorkingSet ile boşaltır.
/// Aktif pencere atlanır (master plan Bölüm 3.4). Risk: Low.
/// </summary>
public sealed class MemoryEngineModule : IOptimizationModule
{
    public string Id => "MemoryEngine";
    public string DisplayName => "Bellek Optimizasyonu (RAM)";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly SafetyNet _safety;
    private readonly ILogger<MemoryEngineModule> _logger;
    private readonly ProcessMemory _processMemory = new();

    private static readonly TimeSpan MinIdleTime = TimeSpan.FromMinutes(5);

    public MemoryEngineModule(SafetyNet safety, ILogger<MemoryEngineModule> logger)
    {
        _safety = safety;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        long reclaimable = 0;
        int candidates = 0;
        var ids = new uint[2048];
        if (!PsapiNative.EnumProcesses(ids, ids.Length * sizeof(uint), out int returned))
        {
            return Task.FromResult(new AnalysisResult { ModuleId = Id, Summary = "Süreçler listelenemedi." });
        }

        int count = returned / sizeof(uint);
        for (int i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var p = Process.GetProcessById((int)ids[i]);
                if (p.MainWindowHandle != IntPtr.Zero &&
                    p.MainWindowHandle == Kernel32.GetForegroundWindow())
                {
                    continue;
                }
                if (DateTime.UtcNow - p.StartTime.ToUniversalTime() <= MinIdleTime)
                {
                    continue;
                }
                candidates++;
                reclaimable += p.WorkingSet64;
            }
            catch { /* erişim engelli atlanır */ }
        }

        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = candidates,
            TotalBytes = reclaimable,
            Summary = $"{candidates} boştaki süreç, ~{FormatBytes(reclaimable)} RAM boşaltılabilir."
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        return Task.FromResult(new PreviewResult
        {
            ModuleId = Id,
            Actions = new[]
            {
                new PreviewAction
                {
                    Description = $"{analysis.ItemCount} boştaki sürecin çalışma kümesi boşaltılacak " +
                                  $"(~{FormatBytes(analysis.TotalBytes)} potansiyel kazanç)",
                    Risk = RiskLevel.Low,
                    Target = "Idle processes"
                }
            },
            EstimatedGainBytes = analysis.TotalBytes,
            IsDryRun = true
        });
    }

    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        progress.Report(new ProgressInfo
        {
            ModuleId = Id, Percent = 10, Message = "Boştaki süreçler taranıyor…", Current = 0, Total = 1
        });

        long before = SumAllWorkingSets();
        int trimmed = _processMemory.TrimIdleProcesses(MinIdleTime);
        long after = SumAllWorkingSets();
        long gained = before > after ? before - after : 0;

        var change = new ChangeRecord
        {
            Module = Id,
            Operation = ChangeOperationType.ProcessOptimize,
            Target = "Idle processes",
            PreviousValue = before.ToString(),
            NewValue = after.ToString(),
            Note = $"{trimmed} süreç working set'i boşaltıldı"
        };
        await _safety.RecordAsync(change, ct);

        progress.Report(new ProgressInfo { ModuleId = Id, Percent = 100, Message = "Tamamlandı.", Current = 1, Total = 1 });

        return new ExecutionResult
        {
            ModuleId = Id,
            Succeeded = trimmed,
            GainBytes = gained,
            Changes = new[] { change }
        };
    }

    /// <inheritdoc/>
    /// <remarks>Working set boşaltma geçicidir; süreçler tekrar doldurur.</remarks>
    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        _logger.LogDebug("MemoryEngine geri alma gereksiz (working set geçici).");
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id,
            ChangeId = change.Id,
            IsSuccess = true,
            Error = "Working set boşaltma geçicidir; süreçler ihtiyaç oldukça RAM'i yeniden kullanır."
        });
    }

    /// <summary>Tüm süreçlerin working set toplamı (kazanç ölçümü için).</summary>
    private static long SumAllWorkingSets()
    {
        long sum = 0;
        foreach (var p in Process.GetProcesses())
        {
            try { sum += p.WorkingSet64; } catch { }
        }
        return sum;
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F0} KB",
        _ => $"{bytes} B"
    };
}
