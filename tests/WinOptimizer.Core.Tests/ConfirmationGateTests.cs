using FluentAssertions;
using WinOptimizer.Core;
using WinOptimizer.Orchestration;
using WinOptimizer.Orchestration.Confirmation;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// Onay politikasının testleri. Bu kapı, gözetimsiz (03:00 zamanlanmış) çalışmaların
/// geri alınamaz işlemleri sessizce yapmasını engelleyen tek mekanizmadır.
/// </summary>
public class ConfirmationGateTests
{
    private static AppSettings SettingsWithConfirmation(bool required) =>
        new() { SafetyNet = new SafetyNetSettings { RequireConfirmationForHighRisk = required } };

    private sealed class FakeModule : IOptimizationModule
    {
        public string Id => "Fake";
        public string DisplayName => "Sahte Modül";
        public RiskLevel Risk { get; init; } = RiskLevel.Low;

        public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default) =>
            Task.FromResult(new AnalysisResult { ModuleId = Id });

        public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default) =>
            Task.FromResult(new PreviewResult { ModuleId = Id });

        public Task<ExecutionResult> ExecuteAsync(
            PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default) =>
            Task.FromResult(new ExecutionResult { ModuleId = Id });

        public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default) =>
            Task.FromResult(new RollbackResult { ModuleId = Id, ChangeId = change.Id, IsSuccess = true });
    }

    private static PreviewResult PreviewWith(params PreviewAction[] actions) =>
        new() { ModuleId = "Fake", Actions = actions };

    private static PreviewAction Action(
        string description, RiskLevel risk = RiskLevel.Low,
        bool extraConfirmation = false, long bytes = 0) =>
        new()
        {
            Description = description,
            Risk = risk,
            RequiresExtraConfirmation = extraConfirmation,
            Bytes = bytes,
        };

    [Fact]
    public void RequiresConfirmation_is_false_when_the_setting_is_off()
    {
        var module = new FakeModule { Risk = RiskLevel.High };
        var preview = PreviewWith(Action("tehlikeli", RiskLevel.High, extraConfirmation: true));

        ConfirmationGate.RequiresConfirmation(module, preview, SettingsWithConfirmation(false))
            .Should().BeFalse("kullanıcı onay istemeyi kapatabilmeli");
    }

    [Fact]
    public void RequiresConfirmation_triggers_on_medium_risk_module()
    {
        var module = new FakeModule { Risk = RiskLevel.Medium };

        ConfirmationGate.RequiresConfirmation(module, PreviewWith(Action("sıradan")), SettingsWithConfirmation(true))
            .Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_triggers_on_action_flag_even_for_a_low_risk_module()
    {
        // KRİTİK: CleanEngine Risk=Low ama geri dönüşüm kutusunu boşaltıyor (geri alınamaz).
        // Yalnız modül riskine bakan bir kapı bu eylemi sessizce uygulardı.
        var module = new FakeModule { Risk = RiskLevel.Low };
        var preview = PreviewWith(
            Action("geçici dosyaları sil"),
            Action("Geri Dönüşüm kutusunu boşalt", RiskLevel.Low, extraConfirmation: true));

        ConfirmationGate.RequiresConfirmation(module, preview, SettingsWithConfirmation(true))
            .Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_triggers_on_high_risk_action_without_the_flag()
    {
        var module = new FakeModule { Risk = RiskLevel.Low };
        var preview = PreviewWith(Action("Hyper-V etkinleştir", RiskLevel.High));

        ConfirmationGate.RequiresConfirmation(module, preview, SettingsWithConfirmation(true))
            .Should().BeTrue();
    }

    [Fact]
    public void RequiresConfirmation_is_false_for_a_plain_low_risk_run()
    {
        var module = new FakeModule { Risk = RiskLevel.Low };
        var preview = PreviewWith(Action("önbellek temizle"), Action("günlükleri sil"));

        ConfirmationGate.RequiresConfirmation(module, preview, SettingsWithConfirmation(true))
            .Should().BeFalse("her çalıştırmada soru sormak onayı anlamsızlaştırır");
    }

    [Theory]
    [InlineData(RiskLevel.Low, false, false)]
    [InlineData(RiskLevel.Medium, false, false)]
    [InlineData(RiskLevel.High, false, true)]
    [InlineData(RiskLevel.Low, true, true)]
    public void NeedsExplicitOptIn_matches_high_risk_or_flagged_actions(
        RiskLevel risk, bool flagged, bool expected)
        => ConfirmationGate.NeedsExplicitOptIn(Action("x", risk, flagged)).Should().Be(expected);

    [Fact]
    public void WithActions_keeps_identity_and_recomputes_the_estimate()
    {
        var preview = new PreviewResult
        {
            ModuleId = "Fake",
            IsDryRun = true,
            EstimatedGainBytes = 5000,
            Actions = [Action("a", bytes: 1000), Action("b", bytes: 4000)],
        };

        var filtered = ConfirmationGate.WithActions(preview, [preview.Actions[0]]);

        filtered.ModuleId.Should().Be("Fake");
        filtered.IsDryRun.Should().BeTrue();
        filtered.Actions.Should().HaveCount(1);
        filtered.EstimatedGainBytes.Should().Be(1000,
            "reddedilen eylemin kazancı raporlanmamalı");
        preview.Actions.Should().HaveCount(2, "özgün önizleme değiştirilmemeli");
    }

    [Fact]
    public void DefaultOneClickModules_excludes_slow_and_reboot_requiring_modules()
    {
        var defaults = AppSettings.DefaultOneClickModules;

        defaults.Should().BeEquivalentTo(
            ["CleanEngine", "MemoryEngine", "StorageOptimizer", "UpdateEngine"]);

        // Bu modüllerin varsayılan tek-tık kapsamında OLMAMASI bilinçli bir güvenlik
        // kararıdır; listeye eklenirlerse bu test kasıtlı olarak kırılır.
        defaults.Should().NotContain("RepairEngine", "SFC/DISM 20+ dakika sürer");
        defaults.Should().NotContain("NetworkOptimizer", "winsock sıfırlama reboot ister");
        defaults.Should().NotContain("DevEnvironment", "Hyper-V etkinleştirme reboot ister");
        defaults.Should().NotContain("AppManager", "uygulama kaldırır");
        defaults.Should().NotContain("BackupRestore", "vssadmin çalıştırır");
        defaults.Should().NotContain("GpuOptimizer", "HAGS çevirir");
        defaults.Should().NotContain("SystemTweaker");
        defaults.Should().NotContain("PrivacyGuard");
        defaults.Should().NotContain("SecurityHardening");
    }

    [Fact]
    public void New_settings_start_with_the_safe_one_click_scope()
    {
        // Boş liste ARTIK "tüm modüller" demek değil; yeni kurulum güvenli listeyle başlar.
        new AppSettings().EnabledModules
            .Should().BeEquivalentTo(AppSettings.DefaultOneClickModules);
    }

    [Fact]
    public async Task AutoApproveConfirmation_returns_every_action()
    {
        var actions = new[] { Action("a"), Action("b", RiskLevel.High, extraConfirmation: true) };
        var request = new ConfirmationRequest("Fake", "Sahte", RiskLevel.Low, actions);

        var approved = await new AutoApproveConfirmation().ConfirmAsync(request);

        approved.Should().BeEquivalentTo(actions);
    }
}
