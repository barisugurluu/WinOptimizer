using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Core;
using WinOptimizer.Safety;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// ChangeJournal — yazılan her değişikliğin geri okunabilmesi ve
/// geri almada bulunulabilmesi (master plan Bölüm 16.3 & 8.1).
/// </summary>
public class ChangeJournalTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ChangeJournal _journal;

    public ChangeJournalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wo-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _journal = new ChangeJournal(_tempDir, NullLogger<ChangeJournal>.Instance);
    }

    [Fact]
    public async Task Write_then_ReadDay_returns_same_record()
    {
        var record = new ChangeRecord
        {
            Module = "SystemTweaker",
            Operation = ChangeOperationType.RegistrySetValue,
            Target = @"HKLM\SOFTWARE\Test\Value",
            PreviousValue = "0",
            NewValue = "1"
        };

        await _journal.WriteAsync(record);
        var read = await _journal.ReadDayAsync(DateTime.UtcNow);

        read.Should().ContainSingle();
        read[0].Target.Should().Be(record.Target);
        read[0].NewValue.Should().Be("1");
        read[0].Operation.Should().Be(ChangeOperationType.RegistrySetValue);
    }

    [Fact]
    public async Task Find_returns_record_by_id()
    {
        var record = new ChangeRecord
        {
            Module = "CleanEngine",
            Operation = ChangeOperationType.FileDelete,
            Target = @"C:\Windows\Temp"
        };
        await _journal.WriteAsync(record);

        var found = await _journal.FindAsync(record.Id);

        found.Should().NotBeNull();
        found!.Id.Should().Be(record.Id);
    }

    [Fact]
    public async Task Find_returns_null_when_not_found()
    {
        var found = await _journal.FindAsync("nonexistent-id");
        found.Should().BeNull();
    }

    [Fact]
    public async Task WriteRange_writes_multiple_in_order()
    {
        var records = Enumerable.Range(0, 5).Select(i => new ChangeRecord
        {
            Module = "Test",
            Target = $"item-{i}"
        });

        await _journal.WriteRangeAsync(records);
        var read = await _journal.ReadDayAsync(DateTime.UtcNow);

        read.Should().HaveCount(5);
        read.Select(r => r.Target).Should().BeEquivalentTo(
            new[] { "item-0", "item-1", "item-2", "item-3", "item-4" });
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }
}
