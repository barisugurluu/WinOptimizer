using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Orchestration;
using WinOptimizer.Orchestration.Preflight;
using WinOptimizer.Safety;
using Xunit;

namespace WinOptimizer.E2E.Tests;

/// <summary>
/// Gereksinim kontrolünün gerçek sistemde çalıştığını ve <b>hiçbir koşulda patlamadığını</b>
/// doğrular. Kritik davranış: bozuk bir alt sistem (WMI, disk, servis yöneticisi) yalnızca
/// ilgili maddeyi uyarıya düşürmeli; gereksinim ekranının kendisi asla istisna atmamalı —
/// aksi halde "uygulama açılmıyor" sorununu teşhis eden ekran da açılmaz.
/// </summary>
public class SystemRequirementsScenarios : IDisposable
{
    private readonly string _dataDir = Path.Combine(
        Path.GetTempPath(), "winopt-req-" + Guid.NewGuid().ToString("N")[..8]);

    private SystemRequirementsChecker CreateChecker()
    {
        var runner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        var restorePoint = new RestorePointService(NullLogger<RestorePointService>.Instance);
        var journal = new ChangeJournal(_dataDir, NullLogger<ChangeJournal>.Instance,
            new IntegrityGuard(IntegrityKeyStore.LoadOrCreate(_dataDir),
                NullLogger<IntegrityGuard>.Instance));
        var registryBackup = new RegistryBackup(_dataDir, NullLogger<RegistryBackup>.Instance,
            new IntegrityGuard(IntegrityKeyStore.LoadOrCreate(_dataDir),
                NullLogger<IntegrityGuard>.Instance));
        var safety = new SafetyNet(restorePoint, journal, registryBackup,
            new SafetyGuard(NullLogger<SafetyGuard>.Instance), NullLogger<SafetyNet>.Instance);
        var guard = new GuardServiceController(runner, safety,
            NullLogger<GuardServiceController>.Instance);

        return new SystemRequirementsChecker(
            _dataDir, guard, restorePoint, NullLogger<SystemRequirementsChecker>.Instance);
    }

    [Fact]
    public async Task Requirements_check_covers_every_documented_id()
    {
        var report = await CreateChecker().RunAsync();

        var ids = report.Checks.Select(c => c.Id).ToList();
        ids.Should().Contain([
            "Os.Architecture", "Os.Build", "Os.Edition", "Process.Elevated",
            "Wmi.Cimv2", "Wmi.SystemRestore", "Disk.SystemDriveFree",
            "Data.Writable", "Service.Guard"
        ]);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Every_failing_check_explains_what_to_do()
    {
        var report = await CreateChecker().RunAsync();

        // Engelleyen bir madde varsa kullanıcının yapabileceği bir şey söylenmeli;
        // "olmuyor" demek yeterli değil.
        report.Checks
            .Where(c => c.Severity == RequirementSeverity.Blocking)
            .Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.RemedyHint));

        // Her maddede ölçülen değer bulunmalı (destek konuşmasının veri kaynağı).
        report.Checks.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Detail));
    }

    [Fact]
    public async Task Data_directory_check_passes_for_a_writable_directory()
    {
        var report = await CreateChecker().RunAsync();

        var writable = report.Checks.Single(c => c.Id == "Data.Writable");
        writable.Severity.Should().Be(RequirementSeverity.Ok,
            "geçici klasör yazılabilir olmalı");

        // Yazma testi dosyası ARDINDA BIRAKILMAMALI.
        Directory.EnumerateFiles(_dataDir, ".yazma-testi-*").Should().BeEmpty();
    }

    [Fact]
    public async Task Elevation_check_reflects_the_current_process()
    {
        var report = await CreateChecker().RunAsync();

        var elevation = report.Checks.Single(c => c.Id == "Process.Elevated");
        var expected = Elevation.IsAdministrator()
            ? RequirementSeverity.Ok
            : RequirementSeverity.Blocking;
        elevation.Severity.Should().Be(expected);
    }

    [Fact]
    public async Task Report_text_is_shareable_and_lists_every_check()
    {
        var report = await CreateChecker().RunAsync();
        string text = report.ToPlainText();

        text.Should().Contain("Sistem Gereksinim Kontrolü");
        foreach (var check in report.Checks)
        {
            text.Should().Contain(check.Id);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataDir))
            {
                Directory.Delete(_dataDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Geçici klasör silinemezse test sonucu etkilenmez.
        }
        GC.SuppressFinalize(this);
    }
}
