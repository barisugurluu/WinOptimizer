namespace WinOptimizer.Service;

/// <summary>Bildirim önem derecesi (master plan Bölüm 12.4 InfoBar).</summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>Bir eşik aşımı sonucu üretilen uyarı/eylem önerisi.</summary>
public sealed record GuardAlert(
    AlertSeverity Severity,
    string Metric,
    string Message,
    string RecommendedAction,
    bool CanAutoRemediate);

/// <summary>
/// ThresholdEngine — metrikleri eşiklerle karşılaştırır, uyarı/eylem üretir.
/// (Master plan Bölüm 3.17 — eşik tabanlı otomatik müdahale.)
/// </summary>
public sealed class ThresholdEngine
{
    private readonly GuardThresholds _thresholds;

    public ThresholdEngine(GuardThresholds thresholds) => _thresholds = thresholds;

    /// <summary>Bir metrik örneğini değerlendirir; üretilen uyarı listesini döndürür.</summary>
    public IReadOnlyList<GuardAlert> Evaluate(GuardMetric m)
    {
        var alerts = new List<GuardAlert>();

        // RAM yüksek
        if (m.RamUsagePercent >= _thresholds.RamUsagePercent)
        {
            alerts.Add(new GuardAlert(
                AlertSeverity.Warning, "RAM",
                $"RAM kullanımı yüksek (%{m.RamUsagePercent}).",
                "Boştaki süreçlerin çalışma kümesi boşaltılabilir (EmptyWorkingSet).",
                CanAutoRemediate: true));
        }

        // C: disk doluluk
        if (m.CDriveFreePercent <= _thresholds.DiskFreeCriticalPercent)
        {
            alerts.Add(new GuardAlert(
                AlertSeverity.Critical, "Disk",
                $"C: sürücüsü KRİTİK boş alan (%{m.CDriveFreePercent}).",
                "TEMP + Geri Dönüşüm kutusu otomatik temizlenmeli.",
                CanAutoRemediate: true));
        }
        else if (m.CDriveFreePercent <= _thresholds.DiskFreePercent)
        {
            alerts.Add(new GuardAlert(
                AlertSeverity.Warning, "Disk",
                $"C: sürücüsü boş alan düşük (%{m.CDriveFreePercent}).",
                "Hızlı temizlik öneriliyor.", CanAutoRemediate: false));
        }

        // SMART arıza tahmini
        if (m.SmartFailurePredicted)
        {
            alerts.Add(new GuardAlert(
                AlertSeverity.Critical, "SMART",
                "Disk arıza tahmini (SMART FailurePredict=True)!",
                "Verilerinizi yedekleyin; diski değiştirin.", CanAutoRemediate: false));
        }

        // Batarya kritik
        if (m.BatteryPercent is int bat && bat <= 15)
        {
            alerts.Add(new GuardAlert(
                AlertSeverity.Warning, "Battery",
                $"Batarya kritik (%{bat}).",
                "Güç tasarrufu profilini etkinleştirin.", CanAutoRemediate: false));
        }

        // CPU sıcaklık
        if (m.CpuTemperatureC is int temp && temp >= _thresholds.TempCelsius)
        {
            alerts.Add(new GuardAlert(
                AlertSeverity.Warning, "Temp",
                $"CPU sıcaklığı yüksek ({temp}°C).",
                "Fan/soğutma kontrolü öneriliyor.", CanAutoRemediate: false));
        }

        // Defender imzası eski
        if (m.DefenderSignatureAgeDays > _thresholds.DefenderSignatureMaxAgeDays)
        {
            alerts.Add(new GuardAlert(
                AlertSeverity.Warning, "Defender",
                $"Defender imzası eski ({m.DefenderSignatureAgeDays} gün).",
                "İmza güncellemesi (Update-MpSignature) öneriliyor.",
                CanAutoRemediate: true));
        }

        return alerts;
    }
}
