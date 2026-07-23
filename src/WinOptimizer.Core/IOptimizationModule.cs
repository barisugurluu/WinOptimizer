using System.ComponentModel;

namespace WinOptimizer.Core;

/// <summary>
/// Tüm optimizasyon modüllerinin uyguladığı ortak sözleşme.
/// Modüler mimarinin temel taşı (master plan Bölüm 2.1 & 3):
/// <c>AnalyzeAsync</c> → <c>PreviewAsync</c> → <c>ExecuteAsync</c> → <c>RollbackAsync</c>.
/// </summary>
public interface IOptimizationModule
{
    /// <summary>Modülün benzersiz, sabit tanımlayıcısı (ör. "CleanEngine").</summary>
    string Id { get; }

    /// <summary>Kullanıcıya gösterilen yerelleştirilmiş ad.</summary>
    string DisplayName { get; }

    /// <summary>Modülün varsayılan risk düzeyi.</summary>
    RiskLevel Risk { get; }

    /// <summary>
    /// Sistemi tarar; ne kadar alan/kazanç sağlanabileceğini hesaplar.
    /// Hiçbir değişiklik yapmaz — yalnızca analiz.
    /// </summary>
    Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default);

    /// <summary>
    /// Analizi somut bir uygulama planına dönüştürür (öncesi/sonrası).
    /// Bu çıktı kullanıcıya "Önizleme" olarak gösterilir.
    /// </summary>
    Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default);

    /// <summary>
    /// Önizlemeyi uygular. Uzun işlemler ilerleme raporlar ve iptal edilebilir.
    /// Yapılan her değişiklik change journal'a yazılır.
    /// </summary>
    Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview,
        IProgress<ProgressInfo> progress,
        CancellationToken ct = default);

    /// <summary>
    /// Tek bir değişiklik kaydını geri alır (simetrik ters işlem).
    /// </summary>
    Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default);
}

/// <summary>
/// Modülün analiz aşamasında topladığı özet bilgi.
/// Kullanıcıya "X GB temizlenebilir" özetini üretir.
/// </summary>
public sealed class AnalysisResult
{
    /// <summary>Modül kimliği (hangi modülden geldi).</summary>
    public required string ModuleId { get; init; }

    /// <summary>Keşfedilen öğe sayısı (ör. silinebilir dosya adedi).</summary>
    public int ItemCount { get; init; }

    /// <summary>Toplam etkilenen boyut (bayt). Bilinmiyorsa 0.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Analize ilişkin insan-okur özet (TR/EN).</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Modüle özel ek veri (ör. kilitli dosya listesi).</summary>
    public Dictionary<string, object> Details { get; init; } = new();
}

/// <summary>
/// Uygulama öncesi kullanıcıya gösterilen önizleme planı.
/// </summary>
public sealed class PreviewResult
{
    public required string ModuleId { get; init; }

    /// <summary>Uygulanacak eylemlerin listesi (her biri ayrı onaylanabilir).</summary>
    public IReadOnlyList<PreviewAction> Actions { get; init; } = Array.Empty<PreviewAction>();

    /// <summary>Toplam elde edilecek kazanç (bayt).</summary>
    public long EstimatedGainBytes { get; init; }

    /// <summary>Dry-run modunda mı üretildi (hiçbir şey değiştirilmedi).</summary>
    public bool IsDryRun { get; init; }
}

/// <summary>
/// Önizlemedeki tek bir eylem (ör. "C:\Windows\Temp\foo.tmp sil").
/// </summary>
[ImmutableObject(true)]
public sealed record PreviewAction
{
    public required string Description { get; init; }
    public required RiskLevel Risk { get; init; }
    public string? Target { get; init; }
    public long Bytes { get; init; }
    public bool RequiresExtraConfirmation { get; init; }
}

/// <summary>
/// Uygulama sonucu — ne yapıldığının özeti + change journal kayıtları.
/// </summary>
public sealed class ExecutionResult
{
    public required string ModuleId { get; init; }

    /// <summary>Başarıyla uygulanan eylem sayısı.</summary>
    public int Succeeded { get; init; }

    /// <summary>Atlanan (kilitli/erişilemez) eylem sayısı.</summary>
    public int Skipped { get; init; }

    /// <summary>Başarısız olan eylem sayısı.</summary>
    public int Failed { get; init; }

    /// <summary>Elde edilen toplam kazanç (bayt).</summary>
    public long GainBytes { get; init; }

    /// <summary>Geri almak için change journal'a yazılan kayıtlar.</summary>
    public IReadOnlyList<ChangeRecord> Changes { get; init; } = Array.Empty<ChangeRecord>();

    /// <summary>Varsa oluşan hatalar.</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool IsSuccess => Failed == 0;
}

/// <summary>
/// Geri alma sonucu.
/// </summary>
public sealed class RollbackResult
{
    public required string ModuleId { get; init; }
    public required string ChangeId { get; init; }
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Uzun işlemler sırasında ilerleme bildirimi (IProgress&lt;T&gt;).
/// </summary>
public sealed class ProgressInfo
{
    public required string ModuleId { get; init; }

    /// <summary>0–100 arası tamamlanma yüzdesi.</summary>
    public int Percent { get; init; }

    /// <summary>Şu an yapılan işin açıklaması.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>İşlenen öğe / toplam öğe.</summary>
    public int Current { get; init; }
    public int Total { get; init; }
}
