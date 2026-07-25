using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Core;
using Xunit;

namespace WinOptimizer.Modules.Tests;

/// <summary>
/// Risk matrisi (master plan Bölüm 9) azaltmalarının regresyon testleri.
///
/// Bu testlerin değeri şu: her biri, korumanın devreye girdiği için dış sürecin
/// (sc.exe / powershell.exe / wbadmin.exe) HİÇ başlatılmadığı bir yolu doğrular.
/// Koruma kaldırılırsa test gerçek sistem komutunu çalıştırmaya kalkar ve kırılır.
/// </summary>
public class SafetyGuardIntegrationTests
{
    private static readonly IProgress<ProgressInfo> NoProgress = new Progress<ProgressInfo>();

    /// <summary>
    /// Risk 9.2 — kritik sistem servisine asla dokunulmaz.
    /// CpuEngine, SafetyGuard beyaz listesindeki bir servisi sc.exe çağırmadan atlamalı.
    /// </summary>
    [Theory]
    [InlineData("WinDefend")]
    [InlineData("RpcSs")]
    [InlineData("EventLog")]
    [InlineData("Winmgmt")]
    public async Task CpuEngine_never_reconfigures_a_critical_service(string criticalService)
    {
        var module = Factories.CpuEngine();
        var preview = new PreviewResult
        {
            ModuleId = module.Id,
            Actions = new[]
            {
                new PreviewAction
                {
                    Description = $"{criticalService} servisini Manuel başlangıca çevir",
                    Risk = RiskLevel.Low,
                    Target = criticalService
                }
            }
        };

        var result = await module.ExecuteAsync(preview, NoProgress);

        result.Skipped.Should().Be(1, "kritik servis SafetyGuard tarafından engellenmeli");
        result.Succeeded.Should().Be(0);
        result.Changes.Should().BeEmpty("engellenen eylem journal'a yazılmamalı");
    }

    /// <summary>
    /// Risk 9.11 — sistem UWP framework paketleri kaldırılamaz (kaldırılırsa uygulamalar bozulur).
    /// AppManager, korunan paket için PowerShell çağırmadan atlamalı.
    /// </summary>
    [Theory]
    [InlineData("Microsoft.VCLibs")]
    [InlineData("Microsoft.NET.Native.Framework")]
    [InlineData("Microsoft.UI.Xaml")]
    [InlineData("Microsoft.WindowsAppRuntime")]
    public async Task AppManager_never_removes_a_protected_system_package(string protectedPackage)
    {
        var module = Factories.AppManager();
        var preview = new PreviewResult
        {
            ModuleId = module.Id,
            Actions = new[]
            {
                new PreviewAction
                {
                    Description = $"Yüklüyse kaldır: {protectedPackage}",
                    Risk = RiskLevel.Medium,
                    Target = protectedPackage,
                    RequiresExtraConfirmation = true
                }
            }
        };

        var result = await module.ExecuteAsync(preview, NoProgress);

        result.Skipped.Should().Be(1, "korunan sistem paketi atlanmalı");
        result.Succeeded.Should().Be(0);
        result.Changes.Should().BeEmpty();
    }

    /// <summary>
    /// Master plan Bölüm 3.15 — sistem yedeği C: sürücüsünün kendisine alınamaz.
    /// wbadmin hiç çalıştırılmadan reddedilmeli.
    /// </summary>
    [Theory]
    [InlineData(@"C:\")]
    [InlineData("C:")]
    [InlineData(@"c:\Yedekler")]
    public async Task BackupRestore_refuses_to_back_up_onto_the_system_drive(string target)
    {
        var module = Factories.BackupRestore();

        var ok = await module.CreateSystemImageBackupAsync(target);

        ok.Should().BeFalse("C: sürücüsüne yedek alınamaz");
    }

    /// <summary>
    /// Master plan Bölüm 3.14 — WinOptimizer Defender'ı ASLA kapatmaz.
    /// Önizlemedeki hiçbir eylem koruma kapatmaya yönelik olmamalı.
    /// </summary>
    [Fact]
    public async Task SecurityHardening_never_offers_to_disable_defender()
    {
        var module = Factories.SecurityHardening();

        var preview = await module.PreviewAsync(new AnalysisResult { ModuleId = module.Id });

        preview.Actions.Should().NotBeEmpty();
        preview.Actions.Should().OnlyContain(
            a => !a.Description.Contains("kapat", StringComparison.OrdinalIgnoreCase)
              && !a.Description.Contains("disable", StringComparison.OrdinalIgnoreCase)
              && !a.Description.Contains("devre dışı", StringComparison.OrdinalIgnoreCase),
            "güvenlik sertleştirme yalnızca korumayı artırır");
    }

    /// <summary>
    /// ASR kuralları önce AuditMode'da denenmeli (Bölüm 3.14) — doğrudan Block'a geçilmemeli,
    /// aksi halde meşru uygulamalar engellenir (risk matrisi 9.14).
    /// </summary>
    [Fact]
    public async Task SecurityHardening_introduces_asr_rules_in_audit_mode_first()
    {
        var module = Factories.SecurityHardening();

        var preview = await module.PreviewAsync(new AnalysisResult { ModuleId = module.Id });

        var asrActions = preview.Actions
            .Where(a => a.Target?.StartsWith("Asr:", StringComparison.Ordinal) == true)
            .ToList();

        asrActions.Should().NotBeEmpty("önerilen ASR kuralları sunulmalı");
        asrActions.Should().OnlyContain(a => a.Description.Contains("AuditMode", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Windows Update önbelleği sıfırlama yıkıcı olabilir — ek onay istemeli (Bölüm 3.13).
    /// </summary>
    [Fact]
    public async Task UpdateEngine_requires_confirmation_before_resetting_the_update_cache()
    {
        var module = Factories.UpdateEngine();

        var preview = await module.PreviewAsync(new AnalysisResult { ModuleId = module.Id });

        var reset = preview.Actions.Single(a => a.Target == "ResetWU");
        reset.RequiresExtraConfirmation.Should().BeTrue();
        reset.Risk.Should().BeOneOf(RiskLevel.Medium, RiskLevel.High);
    }

    /// <summary>
    /// İptal edilen bir işlem yarıda kalmamalı: CancellationToken hemen dikkate alınmalı.
    /// </summary>
    [Fact]
    public async Task CpuEngine_honours_cancellation_before_touching_anything()
    {
        var module = Factories.CpuEngine();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var preview = new PreviewResult
        {
            ModuleId = module.Id,
            Actions = new[]
            {
                new PreviewAction { Description = "DiagTrack", Risk = RiskLevel.Low, Target = "DiagTrack" }
            }
        };

        var act = async () => await module.ExecuteAsync(preview, NoProgress, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
