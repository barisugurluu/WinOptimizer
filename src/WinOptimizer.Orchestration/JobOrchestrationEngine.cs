using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Orchestration.Confirmation;
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
    private readonly SettingsService _settings;
    private readonly IActionConfirmation _confirmation;
    private readonly ILogger<JobOrchestrationEngine> _logger;

    public JobOrchestrationEngine(
        ModuleRegistry registry,
        SafetyNet safety,
        SettingsService settings,
        IActionConfirmation confirmation,
        ILogger<JobOrchestrationEngine> logger)
    {
        _registry = registry;
        _safety = safety;
        _settings = settings;
        _confirmation = confirmation;
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
    /// Uygulama öncesi güvenlik hazırlığı: <b>ayarda açıksa</b> sistem geri yükleme noktası alır.
    /// (<c>SafetyNet.PrepareAsync</c> bayrağı zaten alıyordu; buradan geçirilmediği için
    /// "otomatik geri yükleme noktası" ayarının hiçbir etkisi yoktu.)
    /// </summary>
    public Task PrepareSafetyAsync(string description) =>
        _safety.PrepareAsync(description, _settings.Current.SafetyNet.AutoRestorePoint);

    /// <summary>
    /// <b>Kayıtlı tüm modülleri</b> uygular — ileri seviye/açık istek yolu.
    /// Tek tıkla akışı bunu KULLANMAZ (bkz. <see cref="ExecuteAsync"/>).
    /// </summary>
    public Task<IReadOnlyList<ExecutionResult>> ExecuteAllAsync(
        IProgress<ProgressInfo>? progress, CancellationToken ct = default) =>
        ExecuteCoreAsync(_registry.Modules.Select(m => m.Id).ToList(), progress, ct);

    /// <summary>
    /// Belirli modülleri sırayla uygular. İptal edilebilir, ilerleme raporlar.
    /// </summary>
    /// <param name="moduleIds">
    /// Uygulanacak modül kimlikleri. <c>null</c> ise <b>ayarlardaki etkin modüller</b>
    /// (<see cref="AppSettings.EnabledModules"/>) kullanılır — eskiden "tüm modüller"
    /// anlamına geliyordu ve tek tıkla, Hyper-V etkinleştirme dahil 16 modülü tek bir
    /// genel onayla çalıştırıyordu.
    /// </param>
    public Task<IReadOnlyList<ExecutionResult>> ExecuteAsync(
        IEnumerable<string>? moduleIds,
        IProgress<ProgressInfo>? progress,
        CancellationToken ct = default) =>
        ExecuteCoreAsync(moduleIds?.ToList() ?? _settings.Current.EnabledModules, progress, ct);

    private async Task<IReadOnlyList<ExecutionResult>> ExecuteCoreAsync(
        IReadOnlyCollection<string> moduleIds,
        IProgress<ProgressInfo>? progress,
        CancellationToken ct)
    {
        var targets = _registry.Modules
            .Where(m => moduleIds.Contains(m.Id, StringComparer.OrdinalIgnoreCase))
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

                // ONAY KAPISI — bu tek nokta hem Panosu'ndaki tek-tık'ı hem CLI'nin
                // optimize/clean komutlarını kapsar. Onaylanmayan eylemler listeden
                // düşürülür; modül o adımı kendiliğinden atlar.
                if (ConfirmationGate.RequiresConfirmation(module, preview, _settings.Current))
                {
                    var approved = await _confirmation.ConfirmAsync(
                        new ConfirmationRequest(module.Id, module.DisplayName, module.Risk, preview.Actions),
                        ct);

                    if (approved.Count == 0)
                    {
                        _logger.LogInformation("{Id}: kullanıcı onaylamadı, modül atlandı.", module.Id);
                        results.Add(new ExecutionResult
                        {
                            ModuleId = module.Id,
                            Skipped = preview.Actions.Count,
                        });
                        continue;
                    }

                    if (approved.Count < preview.Actions.Count)
                    {
                        _logger.LogInformation("{Id}: {Approved}/{Total} eylem onaylandı.",
                            module.Id, approved.Count, preview.Actions.Count);
                    }

                    preview = ConfirmationGate.WithActions(preview, approved);
                }

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
