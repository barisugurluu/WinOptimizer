using System.Text.Json.Serialization;

namespace WinOptimizer.App.Infrastructure;

/// <summary>
/// Canlı sistem metrikleri — RealtimeGuard servisinden (Named Pipe) alınan anlık görüntü.
/// <see cref="WinOptimizer.Service.GuardMetric"/> ile aynı JSON şekline sahiptir; App,
/// Service projesine bağımlı olmadan pipe çıktısını bu DTO'ya deserileştirir.
/// (Servis yoksa, <see cref="LiveMetricsProvider"/> WMI ile yerel fallback üretir.)
/// </summary>
public sealed class LiveMetric
{
    /// <summary>Ölçüm zaman damgası (UTC).</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>RAM kullanım yüzdesi (0–100).</summary>
    public int RamUsagePercent { get; init; }

    /// <summary>Boş RAM (bayt).</summary>
    public long RamFreeBytes { get; init; }

    /// <summary>CPU kullanım yüzdesi (0–100).</summary>
    public int CpuUsagePercent { get; init; }

    /// <summary>C: sürücüsü boş alan yüzdesi.</summary>
    [JsonPropertyName("CDriveFreePercent")]
    public int CDriveFreePercent { get; init; }

    /// <summary>C: sürücüsü boş alan (bayt).</summary>
    [JsonPropertyName("CDriveFreeBytes")]
    public long CDriveFreeBytes { get; init; }

    /// <summary>SMART arıza tahmini var mı?</summary>
    public bool SmartFailurePredicted { get; init; }

    /// <summary>CPU sıcaklığı (°C); bilinmiyorsa null.</summary>
    public int? CpuTemperatureC { get; init; }

    /// <summary>Batarya yüzdesi; bilinmiyorsa null.</summary>
    public int? BatteryPercent { get; init; }

    /// <summary>Defender imza eskime süresi (gün).</summary>
    public int DefenderSignatureAgeDays { get; init; }
}
