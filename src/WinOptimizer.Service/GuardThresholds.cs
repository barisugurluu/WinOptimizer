namespace WinOptimizer.Service;

/// <summary>
/// RealtimeGuard eşik ayarları (master plan Bölüm 3.17 & 16.1).
/// Her metriğin tetik eşiği ve uygulanacak eylem.
/// </summary>
public sealed class GuardThresholds
{
    /// <summary>RAM kullanımı %'si (sürekli 60 sn).</summary>
    public int RamUsagePercent { get; init; } = 85;

    /// <summary>C: boş alan %'si — uyarı eşiği.</summary>
    public int DiskFreePercent { get; init; } = 15;

    /// <summary>C: boş alan %'si — kritik (otomatik temizlik).</summary>
    public int DiskFreeCriticalPercent { get; init; } = 5;

    /// <summary>Tek süreç CPU %'si (5 dk) — uyarı.</summary>
    public int CpuPerProcessPercent { get; init; } = 80;

    /// <summary>CPU sıcaklığı (°C).</summary>
    public int TempCelsius { get; init; } = 85;

    /// <summary>Defender imza eskime süresi (gün).</summary>
    public int DefenderSignatureMaxAgeDays { get; init; } = 7;
}

/// <summary>
/// Bir metrik örneği — MetricsCollector tarafından poll edilir.
/// </summary>
public sealed record GuardMetric(
    DateTimeOffset Timestamp,
    int RamUsagePercent,
    long RamFreeBytes,
    int CpuUsagePercent,
    int CDriveFreePercent,
    long CDriveFreeBytes,
    bool SmartFailurePredicted,
    int? CpuTemperatureC,
    int? BatteryPercent,
    int DefenderSignatureAgeDays);
