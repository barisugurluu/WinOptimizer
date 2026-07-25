using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Core;
using WinOptimizer.Safety;
using Xunit;

namespace WinOptimizer.Modules.Tests;

/// <summary>
/// Analyze → Preview → Execute → Rollback sözleşmesinin tüm yüksek riskli modüllerde
/// tutarlı olduğunu doğrular (master plan Bölüm 22.2 DoD: simetri şartı).
///
/// Bu testler sistemi DEĞİŞTİRMEZ: yalnızca saf (I/O'suz) Analyze/Preview/Rollback yolları ve
/// boş önizlemeli Execute çağrılır. Gerçek komut çalıştıran yollar SafetyGuard testlerinde,
/// dış süreç başlatmadan, engelleme davranışı üzerinden doğrulanır.
/// </summary>
public class ModuleContractTests
{
    public static TheoryData<IOptimizationModule> AllModules() => new()
    {
        Factories.AppManager(),
        Factories.BackupRestore(),
        Factories.BootOptimizer(),
        Factories.CpuEngine(),
        Factories.SecurityHardening(),
        Factories.SystemTweaker(),
        Factories.UpdateEngine()
    };

    [Theory]
    [MemberData(nameof(AllModules))]
    public void Module_exposes_stable_identity(IOptimizationModule module)
    {
        module.Id.Should().NotBeNullOrWhiteSpace();
        module.DisplayName.Should().NotBeNullOrWhiteSpace();
        module.Id.Should().Be(module.Id, "Id sabit olmalı — journal kayıtları buna bağlanır");
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public async Task Preview_is_dry_run_and_carries_module_id(IOptimizationModule module)
    {
        var analysis = new AnalysisResult { ModuleId = module.Id };
        var preview = await module.PreviewAsync(analysis);

        preview.ModuleId.Should().Be(module.Id);
        preview.IsDryRun.Should().BeTrue("önizleme hiçbir şeyi değiştirmemeli");
        // Boş analizde eylem üretmeyen modüller de geçerlidir; üretilenlerin açıklaması olmalı.
        preview.Actions.Should().NotContain(a => string.IsNullOrWhiteSpace(a.Description));
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public async Task Execute_with_empty_preview_changes_nothing(IOptimizationModule module)
    {
        var preview = new PreviewResult { ModuleId = module.Id, Actions = Array.Empty<PreviewAction>() };
        var progress = new CollectingProgress();

        var result = await module.ExecuteAsync(preview, progress);

        result.ModuleId.Should().Be(module.Id);
        result.Succeeded.Should().Be(0);
        result.Changes.Should().BeEmpty();
        progress.Reports.Should().BeEmpty("hiç eylem yokken ilerleme raporlanmamalı");
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public async Task Rollback_always_reports_the_change_id(IOptimizationModule module)
    {
        var change = new ChangeRecord { Module = module.Id, Target = "test-hedefi" };

        var result = await module.RollbackAsync(change);

        result.ModuleId.Should().Be(module.Id);
        result.ChangeId.Should().Be(change.Id, "geri alma sonucu hangi kaydı işaret ettiğini bildirmeli");
    }

    /// <summary>
    /// Ek onay gerektiren eylemler risk düzeyiyle tutarlı olmalı: Medium/High riskli hiçbir
    /// eylem sessizce uygulanmamalı (master plan Bölüm 1.3 — güvenli varsayılanlar).
    /// </summary>
    [Theory]
    [MemberData(nameof(AllModules))]
    public async Task Risky_actions_require_extra_confirmation(IOptimizationModule module)
    {
        var preview = await module.PreviewAsync(new AnalysisResult { ModuleId = module.Id });

        // NotContain kullanılıyor: riskli eylemi hiç olmayan modüller de bu kuralı sağlar.
        preview.Actions.Should().NotContain(
            a => a.Risk >= RiskLevel.Medium && !a.RequiresExtraConfirmation,
            "orta/yüksek riskli eylemler ayrıca onaylanmalı");
    }

    private sealed class CollectingProgress : IProgress<ProgressInfo>
    {
        public List<ProgressInfo> Reports { get; } = new();
        public void Report(ProgressInfo value) => Reports.Add(value);
    }
}

/// <summary>Testler için modül örnekleri — gerçek sistem bağımlılıkları olmadan kurulur.</summary>
internal static class Factories
{
    public static ProcessRunner Runner() => new(NullLogger<ProcessRunner>.Instance);

    public static SafetyGuard Guard() => new(NullLogger<SafetyGuard>.Instance);

    /// <summary>Journal'ı geçici bir dizine yazan SafetyNet (gerçek sisteme dokunmaz).</summary>
    public static SafetyNet Safety(string? journalDir = null)
    {
        var dir = journalDir ?? Path.Combine(Path.GetTempPath(), "WinOptimizerTests", Guid.NewGuid().ToString("N"));
        return new SafetyNet(
            new RestorePointService(NullLogger<RestorePointService>.Instance),
            new ChangeJournal(dir, NullLogger<ChangeJournal>.Instance),
            new RegistryBackup(dir, NullLogger<RegistryBackup>.Instance),
            Guard(),
            NullLogger<SafetyNet>.Instance);
    }

    public static AppManager.AppManagerModule AppManager() =>
        new(Runner(), NullLogger<AppManager.AppManagerModule>.Instance);

    public static BackupRestore.BackupRestoreModule BackupRestore() =>
        new(Runner(), NullLogger<BackupRestore.BackupRestoreModule>.Instance);

    public static BootOptimizer.BootOptimizerModule BootOptimizer() =>
        new(Runner(), NullLogger<BootOptimizer.BootOptimizerModule>.Instance);

    public static CpuEngine.CpuEngineModule CpuEngine(SafetyNet? safety = null) =>
        new(safety ?? Safety(), NullLogger<CpuEngine.CpuEngineModule>.Instance);

    public static SecurityHardening.SecurityHardeningModule SecurityHardening() =>
        new(Runner(), NullLogger<SecurityHardening.SecurityHardeningModule>.Instance);

    public static SystemTweaker.SystemTweakerModule SystemTweaker(SafetyNet? safety = null) =>
        new(safety ?? Safety(), NullLogger<SystemTweaker.SystemTweakerModule>.Instance);

    public static UpdateEngine.UpdateEngineModule UpdateEngine() =>
        new(Runner(), NullLogger<UpdateEngine.UpdateEngineModule>.Instance);
}
