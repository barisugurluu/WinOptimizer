using Microsoft.Extensions.Logging;
using WinOptimizer.Core;

namespace WinOptimizer.Modules.BenchmarkEngine;

/// <summary>
/// BenchmarkEngine — önce/sonra performans ölçümü (master plan Bölüm 13).
/// Analyze = önce ölçüm, Execute = son ölçüm + karşılaştırma raporu. Risk: None (salt okunur).
/// </summary>
public sealed class BenchmarkEngineModule : IOptimizationModule
{
    public string Id => "BenchmarkEngine";
    public string DisplayName => "Performans Benchmark";
    public RiskLevel Risk => RiskLevel.None;

    private readonly BenchmarkMeasurer _measurer = new();
    private readonly ILogger<BenchmarkEngineModule> _logger;

    /// <summary>En son ölçüm (optimize öncesi referans).</summary>
    public BenchmarkSnapshot? LastSnapshot { get; private set; }

    public BenchmarkEngineModule(ILogger<BenchmarkEngineModule> logger) => _logger = logger;

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        // "Önce" ölçümü — referans noktası
        LastSnapshot = _measurer.Measure();
        _logger.LogInformation("Benchmark (önce): {Summary}", LastSnapshot.ToSummary());

        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id,
            Summary = "Önce: " + LastSnapshot.ToSummary(),
            Details = new()
            {
                ["before"] = LastSnapshot,
                ["note"] = "Optimizasyondan sonra 'Uygula' ile 'son' ölçüm alın ve karşılaştırın."
            }
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
                    Description = "Son ölçümü al ve önce/sonra karşılaştırma raporu üret",
                    Risk = RiskLevel.None,
                    Target = "CompareReport"
                }
            },
            IsDryRun = true
        });
    }

    public Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        progress.Report(new ProgressInfo { ModuleId = Id, Percent = 50, Message = "Son ölçüm alınıyor…", Current = 1, Total = 2 });

        var before = LastSnapshot ?? _measurer.Measure();
        var after = _measurer.Measure();
        var delta = BenchmarkMeasurer.Diff(before, after);
        LastSnapshot = after;

        progress.Report(new ProgressInfo { ModuleId = Id, Percent = 100, Message = "Rapor üretildi.", Current = 2, Total = 2 });

        string report = delta.ToReport(before, after);
        _logger.LogInformation("Benchmark raporu:\n{Report}", report);

        return Task.FromResult(new ExecutionResult
        {
            ModuleId = Id,
            Succeeded = 1,
            Changes = new[]
            {
                new ChangeRecord
                {
                    Module = Id,
                    Operation = ChangeOperationType.Other,
                    Target = "BenchmarkReport",
                    PreviousValue = before.ToSummary(),
                    NewValue = after.ToSummary(),
                    Note = report
                }
            }
        });
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id, ChangeId = change.Id, IsSuccess = true,
            Error = "Benchmark ölçümü geri alınmaz (salt okunur)."
        });
    }
}
