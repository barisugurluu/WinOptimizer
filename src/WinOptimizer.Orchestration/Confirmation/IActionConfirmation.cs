using WinOptimizer.Core;

namespace WinOptimizer.Orchestration.Confirmation;

/// <summary>Kullanıcıdan onay istenirken sunulan bağlam.</summary>
/// <param name="ModuleId">Modül kimliği.</param>
/// <param name="ModuleDisplayName">Kullanıcıya gösterilen modül adı.</param>
/// <param name="ModuleRisk">Modülün bildirdiği risk düzeyi.</param>
/// <param name="Actions">Onaya sunulan eylemler.</param>
public sealed record ConfirmationRequest(
    string ModuleId,
    string ModuleDisplayName,
    RiskLevel ModuleRisk,
    IReadOnlyList<PreviewAction> Actions);

/// <summary>
/// Riskli eylemler için onay mercii. Arayüz bir diyalog gösterir, CLI bayraklara bakar,
/// testler hepsini onaylar.
/// </summary>
public interface IActionConfirmation
{
    /// <summary>
    /// Onaylanan eylem <b>alt kümesini</b> döndürür. Boş liste = bu modülü tamamen atla.
    /// </summary>
    Task<IReadOnlyList<PreviewAction>> ConfirmAsync(
        ConfirmationRequest request, CancellationToken ct = default);
}

/// <summary>
/// Onay politikası — tek predicate, tek filtre. Hem tek-tık/CLI hattı
/// (<see cref="JobOrchestrationEngine"/>) hem de modül sayfası aynı kuralı kullanır.
/// </summary>
/// <remarks>
/// <para><b>Neden modülü iptal etmek yerine eylem filtreleniyor?</b> Her modülün
/// <c>ExecuteAsync</c>'i <c>preview.Actions</c> üzerinde geziyor — örneğin CleanEngine
/// geri dönüşüm boşaltmayı <c>Actions.Any(a =&gt; a.Target == "RecycleBin")</c> ile
/// anahtarlıyor. Onaylanmayan eylemi listeden düşürmek modülü tam o adımı atlatır;
/// modül kodu değişmez ve sözleşme (Analyze→Preview→Execute→Rollback) kırılmaz.</para>
/// <para><b>Neden modül Risk'i tek başına yeterli değil?</b> Modül metadata'sı fazla kaba:
/// CleanEngine <c>Low</c> ama geri dönüşüm kutusunu boşaltıyor (geri alınamaz),
/// BackupRestore <c>Low</c> ama vssadmin çalıştırıyor, GpuOptimizer <c>Low</c> ama HAGS
/// çeviriyor. Bu yüzden karar <b>eylem düzeyinde</b> de verilir.</para>
/// </remarks>
public static class ConfirmationGate
{
    /// <summary>
    /// Bu önizleme için kullanıcı onayı gerekiyor mu?
    /// Ayar kapalıysa (<c>RequireConfirmationForHighRisk = false</c>) hiçbir zaman sorulmaz.
    /// </summary>
    public static bool RequiresConfirmation(
        IOptimizationModule module, PreviewResult preview, AppSettings settings)
    {
        if (!settings.SafetyNet.RequireConfirmationForHighRisk)
        {
            return false;
        }

        return module.Risk >= RiskLevel.Medium ||
               preview.Actions.Any(a => a.RequiresExtraConfirmation || a.Risk >= RiskLevel.High);
    }

    /// <summary>
    /// Onaylanan eylemlerle yeni bir önizleme üretir (özgün nesne değiştirilmez).
    /// </summary>
    public static PreviewResult WithActions(
        PreviewResult preview, IReadOnlyList<PreviewAction> approved) =>
        new()
        {
            ModuleId = preview.ModuleId,
            Actions = approved,
            IsDryRun = preview.IsDryRun,
            // Kazanç tahmini onaylanan eylemlere göre yeniden hesaplanır; aksi halde
            // kullanıcı reddettiği eylemlerin kazancını da rapor edilmiş görür.
            EstimatedGainBytes = approved.Sum(a => a.Bytes),
        };

    /// <summary>
    /// Ek onay gerektiren (yani varsayılan olarak <b>işaretsiz</b> sunulacak) eylemler.
    /// </summary>
    public static bool NeedsExplicitOptIn(PreviewAction action) =>
        action.RequiresExtraConfirmation || action.Risk >= RiskLevel.High;
}

/// <summary>
/// Her şeyi onaylayan uygulama — testler ve etkileşimsiz (headless) senaryolar için.
/// </summary>
/// <remarks>
/// Üretimde <b>kullanılmaz</b>: gerçek çağıranlar arayüz diyalogunu veya CLI'nın bayrak
/// tabanlı uygulamasını kullanır.
/// </remarks>
public sealed class AutoApproveConfirmation : IActionConfirmation
{
    public Task<IReadOnlyList<PreviewAction>> ConfirmAsync(
        ConfirmationRequest request, CancellationToken ct = default) =>
        Task.FromResult(request.Actions);
}
