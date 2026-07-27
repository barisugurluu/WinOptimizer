using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Core;
using WinOptimizer.Safety;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// SafetyNet facade — journal kaydının ve registry yedek yetkilendirmesinin
/// doğru alt bileşenlere yönlendirildiğini doğrular. Gerçek WMI/process çağrısı
/// yapmaz; ChangeJournal geçici dizine yazar.
/// </summary>
public class SafetyNetFacadeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SafetyNet _safety;

    public SafetyNetFacadeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wo-facade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _safety = new SafetyNet(
            new RestorePointService(NullLogger<RestorePointService>.Instance),
            new ChangeJournal(_tempDir, NullLogger<ChangeJournal>.Instance),
            new RegistryBackup(_tempDir, NullLogger<RegistryBackup>.Instance),
            new SafetyGuard(NullLogger<SafetyGuard>.Instance),
            NullLogger<SafetyNet>.Instance);
    }

    [Fact]
    public async Task RecordAsync_persists_change_to_journal()
    {
        var record = new ChangeRecord
        {
            Module = "SystemTweaker",
            Operation = ChangeOperationType.RegistrySetValue,
            Target = @"HKLM\SOFTWARE\Test",
            PreviousValue = "0",
            NewValue = "1"
        };

        await _safety.RecordAsync(record);

        var read = await _safety.Journal.ReadDayAsync(DateTime.UtcNow);
        read.Should().ContainSingle()
            .Which.Target.Should().Be(record.Target);
    }

    [Fact]
    public void Exposes_underlying_components_for_orchestration()
    {
        // JobEngine/RollbackService journal, guard ve restorePoint'e erişir.
        _safety.Journal.Should().NotBeNull();
        _safety.Guard.Should().NotBeNull();
        _safety.RegistryBackup.Should().NotBeNull();
        _safety.RestorePoint.Should().NotBeNull();
    }

    [Fact]
    public async Task PrepareAsync_completes_without_throwing_when_restore_unavailable()
    {
        // Gerçek makinede restore point WMA ile alınır; test ortamında muhtemelen
        // başarısız olur ama facade istisna fırlatmamalı (sadece günlükler).
        Func<Task> act = () => _safety.PrepareAsync("test", createRestorePoint: false);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Guard_blocks_critical_service_through_facade()
    {
        // Facade üzerinden SafetyGuard erişimi — kritik servis koruması aktif.
        var ok = _safety.Guard.IsAllowed("WinDefend", out var reason);

        ok.Should().BeFalse("Defender kritisine dokunulamaz");
        reason.Should().NotBeNullOrEmpty();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }
}
