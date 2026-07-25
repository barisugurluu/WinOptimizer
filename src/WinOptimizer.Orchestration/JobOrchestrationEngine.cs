using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Orchestration;

/// <summary>
/// JobOrchestrationEngine — "Tek Tıkla En İyi Hale Getir" akışını koordine eder.
/// Birden çok modülü sırayla analiz eder, önizleme üretir ve seçilen modülleri
/// paralel/iptal edilebilir şekilde uygular. (Master plan Bölüm 2.1 & 12.3 Akış A.)
/// </summary>
public sealed class JobOrchestrationEngine
{
    private readonly ModuleRegistry _registry;
    private readonly SafetyNet _safety;
    private readonly ILogger<JobOrchestrationEngine> _logger;

    public JobOrchestrationEngine(
        ModuleRegistry registry, SafetyNet safety, ILogger<JobOrchestrationEngine> logger)
    {
        _registry = registry;
        _safety = safety;
        _logger = logger;
    }

    /// <summary>Tüm kayıtlı modülleri analiz eder; birleştirilmiş analiz raporu döndürür.</summary>
    public async Task<IReadOnlyDictionary<string, AnalysisResult>> AnalyzeAllAsync(
        CancellationToken ct = default)
    {
        var results = new Dictionary<string, AnalysisResult>();
        foreach (var module in _registry.Modules)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var analysis = await module.AnalyzeAsync(ct);
                results[module.Id] = analysis;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Modül analizi başarısız: {Id}", module.Id);
            }
        }
        return results;
    }

    /// <summary>
    /// Analizleri önizlemeye dönüştürür. Kullanıcı bu noktada ne uygulanacağını görür.
    /// </summary>
    public async Task<IReadOnlyList<PreviewResult>> PreviewAllAsync(
        IReadOnlyDictionary<string, AnalysisResult> analyses, CancellationToken ct = default)
    {
        var previews = new List<PreviewResult>();
        foreach (var module in _registry.Modules)
        {
            ct.ThrowIfCancellationRequested();
            if (analyses.TryGetValue(module.Id, out var analysis))
            {
                previews.Add(await module.PreviewAsync(analysis, ct));
            }
        }
        return previews;
    }

    /// <summary>
    /// Uygulama öncesi güvenlik hazırlığı: sistem geri yükleme noktası alır.
    /// </summary>
    public Task PrepareSafetyAsync(string description) => _safety.PrepareAsync(description);

    /// <summary>
    /// Belirli modülleri sırayla (veya paralel) uygular. İptal edilebilir, ilerleme raporlar.
    /// </summary>
    /// <param name="moduleIds">Uygulanacak modüllerin kimlikleri; boşsa tüm modüller.</param>
    public async Task<IReadOnlyList<ExecutionResult>> ExecuteAsync(
        IEnumerable<string>? moduleIds,
        IProgress<ProgressInfo>? progress,
        CancellationToken ct = default)
    {
        var targets = (moduleIds is null
                ? _registry.Modules
                : _registry.Modules.Where(m => moduleIds.Contains(m.Id, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        _logger.LogInformation("Optimizasyon çalıştırması başlıyor: {Count} modül ({Modules})",
            targets.Count, string.Join(", ", targets.Select(m => m.Id)));

        var results = new List<ExecutionResult>(targets.Count);
        foreach (var module in targets)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var analysis = await module.AnalyzeAsync(ct);
                var preview = await module.PreviewAsync(analysis, ct);
                var exec = await module.ExecuteAsync(preview, progress ?? new Progress<ProgressInfo>(), ct);
                results.Add(exec);
                _logger.LogInformation("{Id}: {S} başarılı, {K} atlandı, {F} başarısız, {B} bayt kazanç",
                    module.Id, exec.Succeeded, exec.Skipped, exec.Failed, exec.GainBytes);
                LogChanges(exec);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Modül yürütme başarısız: {Id}", module.Id);
                results.Add(new ExecutionResult
                {
                    ModuleId = module.Id,
                    Failed = 1,
                    Errors = new[] { ex.Message }
                });
            }
        }

        _logger.LogInformation("Optimizasyon çalıştırması bitti: {S} başarılı, {F} başarısız, {B} bayt kazanç",
            results.Sum(r => r.Succeeded), results.Sum(r => r.Failed), results.Sum(r => r.GainBytes));
        return results;
    }

    /// <summary>
    /// Yapılan her değişikliği denetim izi olarak günlükler.
    /// Change journal geri alma için kayıt tutar; günlük ise kullanıcının dışa aktardığı teşhis
    /// paketinde "bu araç sistemimde tam olarak neyi değiştirdi?" sorusunu yanıtlar.
    /// Tek yerde yapılır — her modüle kopyalanmaz, böylece zamanla tutarsızlaşamaz.
    /// </summary>
    private void LogChanges(ExecutionResult exec)
    {
        foreach (var c in exec.Changes)
        {
            _logger.LogInformation(
                "Değişiklik {ChangeId}: {Module}/{Operation} → {Target} ({Previous} → {New})",
                c.Id, c.Module, c.Operation, c.Target, c.PreviousValue ?? "(yok)", c.NewValue ?? "(yok)");
        }
    }
}
