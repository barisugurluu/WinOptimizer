using FluentAssertions;
using WinOptimizer.Modules.BenchmarkEngine;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// BenchmarkEngine — snapshot/delta/rapor mantığı testleri (master plan Bölüm 13).
/// </summary>
public class BenchmarkEngineTests
{
    [Fact]
    public void Measurer_returns_snapshot_with_some_metrics()
    {
        var measurer = new BenchmarkMeasurer();
        var snap = measurer.Measure();

        snap.Should().NotBeNull();
        // Disk ve RAM genellikle ölçülebilir; CPU yükü de.
        snap.DiskFreeGb.Should().BeGreaterThan(0);
        snap.SecurityScore.Should().BeInRange(0, 100);
        snap.ToSummary().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Diff_calculates_correct_delta()
    {
        var before = new BenchmarkSnapshot { BootSec = 38.2, FreeRamMb = 4200, DiskFreeGb = 11.2, SecurityScore = 72 };
        var after = new BenchmarkSnapshot { BootSec = 27.4, FreeRamMb = 6800, DiskFreeGb = 13.5, SecurityScore = 88 };

        var delta = BenchmarkMeasurer.Diff(before, after);

        delta.BootSec.Should().BeApproximately(-10.8, 0.001);   // iyileşme (azaldı)
        delta.FreeRamMb.Should().Be(2600);                        // arttı
        delta.DiskFreeGb.Should().BeApproximately(2.3, 0.001);    // arttı
        delta.SecurityScore.Should().Be(16);                      // arttı
    }

    [Fact]
    public void Diff_returns_null_when_metric_missing()
    {
        var before = new BenchmarkSnapshot { BootSec = 10 };
        var after = new BenchmarkSnapshot { FreeRamMb = 5000 };

        var delta = BenchmarkMeasurer.Diff(before, after);

        delta.BootSec.Should().BeNull();
        delta.FreeRamMb.Should().BeNull();
        delta.DiskFreeGb.Should().BeNull();
        delta.SecurityScore.Should().BeNull();
    }

    [Fact]
    public void Report_contains_all_improved_metrics()
    {
        var before = new BenchmarkSnapshot { BootSec = 38.2, FreeRamMb = 4200, DiskFreeGb = 11.2, SecurityScore = 72 };
        var after = new BenchmarkSnapshot { BootSec = 27.4, FreeRamMb = 6800, DiskFreeGb = 13.5, SecurityScore = 88 };
        var delta = BenchmarkMeasurer.Diff(before, after);

        var report = delta.ToReport(before, after);

        report.Should().Contain("Boot süresi");
        report.Should().Contain("Boş RAM");
        report.Should().Contain("Boş disk");
        report.Should().Contain("Güvenlik skoru");
        report.Should().Contain("▲"); // iyileşme oku
    }
}
