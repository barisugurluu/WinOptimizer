using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Core;
using WinOptimizer.Orchestration;
using WinOptimizer.Safety;
using Xunit;

namespace WinOptimizer.Modules.Tests;

/// <summary>
/// RollbackService — change journal kaydını, kaydı üreten modülün geri alma uygulamasına
/// yönlendiren katman (master plan hedef G3: "tüm tweak'ler tek tıkla geri alınabilir").
///
/// Bu katman eklenmeden önce geri alma erişilemezdi: modüller RollbackAsync uyguluyordu ve
/// journal değişiklikleri kaydediyordu, ancak hiçbir kod ikisini birbirine bağlamıyordu.
/// </summary>
public class RollbackServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly ChangeJournal _journal;
    private readonly ModuleRegistry _registry;
    private readonly RollbackService _service;

    public RollbackServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "WinOptimizerRollbackTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _journal = new ChangeJournal(_dir, NullLogger<ChangeJournal>.Instance);
        _registry = new ModuleRegistry(NullLogger<ModuleRegistry>.Instance);
        _service = new RollbackService(_registry, _journal, NullLogger<RollbackService>.Instance);
    }

    [Fact]
    public async Task Rollback_routes_the_record_to_the_module_that_created_it()
    {
        var target = new FakeModule("Alpha", succeeds: true);
        var other = new FakeModule("Beta", succeeds: true);
        _registry.Register(target).Register(other);
        var change = new ChangeRecord { Module = "Alpha", Target = @"HKLM\Test", NewValue = "2", PreviousValue = "1" };

        var outcome = await _service.RollbackAsync(change);

        outcome.IsSuccess.Should().BeTrue();
        outcome.ModuleId.Should().Be("Alpha");
        target.ReceivedChanges.Should().ContainSingle().Which.Id.Should().Be(change.Id);
        other.ReceivedChanges.Should().BeEmpty("kayıt yalnızca sahibi modüle gitmeli");
    }

    [Fact]
    public async Task Rollback_of_an_unregistered_module_fails_without_throwing()
    {
        var change = new ChangeRecord { Module = "KayitliDegil", Target = "x" };

        var outcome = await _service.RollbackAsync(change);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Message.Should().Contain("KayitliDegil");
    }

    [Fact]
    public async Task Failed_rollback_is_reported_with_the_module_reason()
    {
        _registry.Register(new FakeModule("Alpha", succeeds: false, error: "Desteklenmeyen işlem."));
        var change = new ChangeRecord { Module = "Alpha", Target = "x" };

        var outcome = await _service.RollbackAsync(change);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Message.Should().Be("Desteklenmeyen işlem.");
    }

    [Fact]
    public async Task Successful_rollback_is_itself_written_to_the_journal()
    {
        _registry.Register(new FakeModule("Alpha", succeeds: true));
        var change = new ChangeRecord
        {
            Module = "Alpha",
            Target = @"HKLM\Test\Value",
            PreviousValue = "1",
            NewValue = "2"
        };

        await _service.RollbackAsync(change);

        var records = await _journal.ReadDayAsync(DateTime.UtcNow);
        var undo = records.Should().ContainSingle().Subject;
        undo.Module.Should().Be("Alpha");
        undo.Target.Should().Be(@"HKLM\Test\Value");
        undo.PreviousValue.Should().Be("2", "geri alma yön değiştirir: yeni değer artık öncekidir");
        undo.NewValue.Should().Be("1");
        undo.Note.Should().Contain(change.Id, "hangi kaydın geri alındığı izlenebilmeli");
    }

    [Fact]
    public async Task Failed_rollback_is_not_written_to_the_journal()
    {
        _registry.Register(new FakeModule("Alpha", succeeds: false, error: "olmadı"));

        await _service.RollbackAsync(new ChangeRecord { Module = "Alpha", Target = "x" });

        (await _journal.ReadDayAsync(DateTime.UtcNow)).Should().BeEmpty(
            "başarısız geri alma geçmişe başarılıymış gibi yazılmamalı");
    }

    [Fact]
    public async Task RollbackById_finds_the_record_in_the_journal()
    {
        _registry.Register(new FakeModule("Alpha", succeeds: true));
        var change = new ChangeRecord { Module = "Alpha", Target = "hedef" };
        await _journal.WriteAsync(change);

        var outcome = await _service.RollbackByIdAsync(change.Id);

        outcome.IsSuccess.Should().BeTrue();
        outcome.ChangeId.Should().Be(change.Id);
    }

    [Fact]
    public async Task RollbackById_reports_a_missing_record_instead_of_throwing()
    {
        var outcome = await _service.RollbackByIdAsync("yokboyle");

        outcome.IsSuccess.Should().BeFalse();
        outcome.Message.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task RollbackLast_undoes_the_most_recent_change_first()
    {
        var module = new FakeModule("Alpha", succeeds: true);
        _registry.Register(module);
        var older = new ChangeRecord { Module = "Alpha", Target = "eski", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10) };
        var newer = new ChangeRecord { Module = "Alpha", Target = "yeni", Timestamp = DateTimeOffset.UtcNow };
        await _journal.WriteAsync(older);
        await _journal.WriteAsync(newer);

        var outcomes = await _service.RollbackLastAsync(1);

        outcomes.Should().ContainSingle();
        module.ReceivedChanges.Should().ContainSingle().Which.Target.Should().Be("yeni");
    }

    [Fact]
    public async Task RollbackLast_returns_empty_when_there_is_nothing_to_undo()
    {
        var outcomes = await _service.RollbackLastAsync();

        outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadRecent_returns_newest_first()
    {
        await _journal.WriteAsync(new ChangeRecord { Module = "A", Target = "eski", Timestamp = DateTimeOffset.UtcNow.AddHours(-2) });
        await _journal.WriteAsync(new ChangeRecord { Module = "A", Target = "yeni", Timestamp = DateTimeOffset.UtcNow });

        var records = await _service.ReadRecentAsync();

        records.Select(r => r.Target).Should().ContainInOrder("yeni", "eski");
    }

    [Fact]
    public async Task Cancellation_stops_a_multi_record_rollback()
    {
        _registry.Register(new FakeModule("Alpha", succeeds: true));
        await _journal.WriteAsync(new ChangeRecord { Module = "Alpha", Target = "a" });
        await _journal.WriteAsync(new ChangeRecord { Module = "Alpha", Target = "b" });
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await _service.RollbackLastAsync(2, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Geri alma yönlendirmesini gözlemlemek için sahte modül — sisteme dokunmaz.</summary>
    private sealed class FakeModule : IOptimizationModule
    {
        private readonly bool _succeeds;
        private readonly string? _error;

        public FakeModule(string id, bool succeeds, string? error = null)
        {
            Id = id;
            _succeeds = succeeds;
            _error = error;
        }

        public List<ChangeRecord> ReceivedChanges { get; } = new();

        public string Id { get; }
        public string DisplayName => Id;
        public RiskLevel Risk => RiskLevel.Low;

        public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default) =>
            Task.FromResult(new AnalysisResult { ModuleId = Id });

        public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default) =>
            Task.FromResult(new PreviewResult { ModuleId = Id });

        public Task<ExecutionResult> ExecuteAsync(
            PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default) =>
            Task.FromResult(new ExecutionResult { ModuleId = Id });

        public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
        {
            ReceivedChanges.Add(change);
            return Task.FromResult(new RollbackResult
            {
                ModuleId = Id,
                ChangeId = change.Id,
                IsSuccess = _succeeds,
                Error = _error
            });
        }
    }
}
