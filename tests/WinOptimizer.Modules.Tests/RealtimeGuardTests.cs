using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Safety;
using WinOptimizer.Service;
using Xunit;

namespace WinOptimizer.Modules.Tests;

/// <summary>
/// RealtimeGuard eşik değerlendirme mantığı (master plan Bölüm 3.17).
/// Servis barındırma ve IPC katmanı kapsam dışı — burada yalnızca saf karar mantığı test edilir:
/// hangi metrik hangi uyarıyı üretir ve hangi uyarı OTOMATİK müdahaleye izin verir.
/// </summary>
public class ThresholdEngineTests
{
    private static readonly GuardThresholds Defaults = new();

    private static GuardMetric Healthy() => new(
        Timestamp: DateTimeOffset.UtcNow,
        RamUsagePercent: 40,
        RamFreeBytes: 8L * 1024 * 1024 * 1024,
        CpuUsagePercent: 10,
        CDriveFreePercent: 50,
        CDriveFreeBytes: 200L * 1024 * 1024 * 1024,
        SmartFailurePredicted: false,
        CpuTemperatureC: 45,
        BatteryPercent: 90,
        DefenderSignatureAgeDays: 1);

    [Fact]
    public void Healthy_system_produces_no_alerts()
    {
        var alerts = new ThresholdEngine(Defaults).Evaluate(Healthy());

        alerts.Should().BeEmpty();
    }

    [Fact]
    public void High_ram_usage_raises_an_auto_remediable_warning()
    {
        var metric = Healthy() with { RamUsagePercent = 90 };

        var alerts = new ThresholdEngine(Defaults).Evaluate(metric);

        var ram = alerts.Single(a => a.Metric == "RAM");
        ram.Severity.Should().Be(AlertSeverity.Warning);
        ram.CanAutoRemediate.Should().BeTrue();
    }

    [Fact]
    public void Ram_usage_exactly_at_the_threshold_still_alerts()
    {
        var metric = Healthy() with { RamUsagePercent = Defaults.RamUsagePercent };

        var alerts = new ThresholdEngine(Defaults).Evaluate(metric);

        alerts.Should().ContainSingle(a => a.Metric == "RAM");
    }

    [Fact]
    public void Critically_low_disk_space_is_critical_and_auto_remediable()
    {
        var metric = Healthy() with { CDriveFreePercent = 3 };

        var alerts = new ThresholdEngine(Defaults).Evaluate(metric);

        var disk = alerts.Single(a => a.Metric == "Disk");
        disk.Severity.Should().Be(AlertSeverity.Critical);
        disk.CanAutoRemediate.Should().BeTrue();
    }

    [Fact]
    public void Low_but_not_critical_disk_space_warns_without_auto_remediation()
    {
        var metric = Healthy() with { CDriveFreePercent = 10 };

        var alerts = new ThresholdEngine(Defaults).Evaluate(metric);

        var disk = alerts.Single(a => a.Metric == "Disk");
        disk.Severity.Should().Be(AlertSeverity.Warning);
        disk.CanAutoRemediate.Should().BeFalse("yalnızca kritik doluluk otomatik temizlik tetikler");
    }

    [Fact]
    public void Disk_alert_is_never_raised_twice_for_the_same_sample()
    {
        var metric = Healthy() with { CDriveFreePercent = 2 };

        var alerts = new ThresholdEngine(Defaults).Evaluate(metric);

        alerts.Count(a => a.Metric == "Disk").Should().Be(1, "uyarı ve kritik eşik birbirini dışlamalı");
    }

    /// <summary>
    /// Donanım arızası ve batarya/sıcaklık uyarıları ASLA otomatik müdahale ettirmemeli —
    /// bunlar kullanıcı kararı gerektirir (risk matrisi: yanlış otomatik eylem zararlı olabilir).
    /// </summary>
    [Fact]
    public void Hardware_alerts_are_never_auto_remediable()
    {
        var metric = Healthy() with
        {
            SmartFailurePredicted = true,
            BatteryPercent = 10,
            CpuTemperatureC = 95
        };

        var alerts = new ThresholdEngine(Defaults).Evaluate(metric);

        alerts.Should().Contain(a => a.Metric == "SMART" && a.Severity == AlertSeverity.Critical);
        alerts.Where(a => a.Metric is "SMART" or "Battery" or "Temp")
              .Should().OnlyContain(a => !a.CanAutoRemediate);
    }

    [Fact]
    public void Stale_defender_signature_raises_an_auto_remediable_warning()
    {
        var metric = Healthy() with { DefenderSignatureAgeDays = Defaults.DefenderSignatureMaxAgeDays + 1 };

        var alerts = new ThresholdEngine(Defaults).Evaluate(metric);

        alerts.Single(a => a.Metric == "Defender").CanAutoRemediate.Should().BeTrue();
    }

    [Fact]
    public void Missing_optional_sensors_do_not_produce_alerts()
    {
        // Masaüstünde batarya, sensörsüz sistemde sıcaklık okunamaz (null).
        var metric = Healthy() with { BatteryPercent = null, CpuTemperatureC = null };

        var alerts = new ThresholdEngine(Defaults).Evaluate(metric);

        alerts.Should().NotContain(a => a.Metric == "Battery" || a.Metric == "Temp");
    }

    [Fact]
    public void Custom_thresholds_are_respected()
    {
        var strict = new GuardThresholds { RamUsagePercent = 50 };
        var metric = Healthy() with { RamUsagePercent = 60 };

        new ThresholdEngine(strict).Evaluate(metric).Should().ContainSingle(a => a.Metric == "RAM");
        new ThresholdEngine(Defaults).Evaluate(metric).Should().BeEmpty();
    }
}

/// <summary>
/// Otomatik müdahale kapısı — yalnızca <c>CanAutoRemediate</c> işaretli uyarılar eyleme dönüşür.
/// (Bu testler sistemi değiştirmez: müdahale edilemez uyarılar hiçbir eylem tetiklemez.)
/// </summary>
public class RemediationEngineTests
{
    private static RemediationEngine Create() =>
        new(new ProcessRunner(NullLogger<ProcessRunner>.Instance),
            NullLogger<RemediationEngine>.Instance);

    [Fact]
    public async Task No_alerts_means_no_action()
    {
        var applied = await Create().ApplyAsync(Array.Empty<GuardAlert>());

        applied.Should().Be(0);
    }

    [Fact]
    public async Task Alerts_that_are_not_auto_remediable_never_trigger_an_action()
    {
        var alerts = new[]
        {
            new GuardAlert(AlertSeverity.Critical, "SMART", "Disk arızası", "Yedek alın", CanAutoRemediate: false),
            new GuardAlert(AlertSeverity.Warning, "Battery", "Batarya düşük", "Güç profili", CanAutoRemediate: false),
            new GuardAlert(AlertSeverity.Warning, "Temp", "Sıcaklık yüksek", "Fan kontrolü", CanAutoRemediate: false)
        };

        var applied = await Create().ApplyAsync(alerts);

        applied.Should().Be(0, "donanım uyarıları otomatik eylem tetiklememeli");
    }

    [Fact]
    public async Task Unknown_metric_names_are_ignored_even_when_marked_auto_remediable()
    {
        var alerts = new[]
        {
            new GuardAlert(AlertSeverity.Warning, "BilinmeyenMetrik", "?", "?", CanAutoRemediate: true)
        };

        var applied = await Create().ApplyAsync(alerts);

        applied.Should().Be(0);
    }
}
