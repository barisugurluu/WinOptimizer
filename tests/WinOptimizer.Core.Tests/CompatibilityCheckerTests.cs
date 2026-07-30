using FluentAssertions;
using WinOptimizer.Core.Compatibility;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// Uyumluluk matrisi kapısı (master plan Bölüm 14).
/// Desteklenmeyen sürümde özellik sunulmamalı; desteklenmeme nedeni kullanıcıya gösterilebilmeli.
/// </summary>
public class CompatibilityCheckerTests
{
    private static readonly WindowsVersionInfo Windows10_1909 = new(18363, IsWindows11: false, IsProOrHigher: true);
    private static readonly WindowsVersionInfo Windows10_21H2 = new(19044, IsWindows11: false, IsProOrHigher: true);
    private static readonly WindowsVersionInfo Windows11 = new(22631, IsWindows11: true, IsProOrHigher: true);
    private static readonly WindowsVersionInfo Windows11Home = new(22631, IsWindows11: true, IsProOrHigher: false);

    [Fact]
    public void Unknown_feature_is_treated_as_supported()
    {
        // Matris yalnızca kısıtlı özellikleri listeler — her sürümde çalışanlar (SFC, telemetri…) yok.
        CompatibilityChecker.IsSupported("SfcScan", Windows10_1909).IsSupported.Should().BeTrue();
    }

    [Theory]
    [InlineData("Hags")]
    [InlineData("Wsl2")]
    [InlineData("Hvci")]
    [InlineData("Vbs")]
    public void Build_gated_features_are_blocked_before_windows10_2004(string featureId)
    {
        var result = CompatibilityChecker.IsSupported(featureId, Windows10_1909);

        result.IsSupported.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace("kullanıcıya neden gösterilmeli");
    }

    [Theory]
    [InlineData("Hags")]
    [InlineData("Wsl2")]
    [InlineData("Hvci")]
    [InlineData("Vbs")]
    public void Build_gated_features_are_allowed_from_windows10_2004_onwards(string featureId)
    {
        CompatibilityChecker.IsSupported(featureId, Windows10_21H2).IsSupported.Should().BeTrue();
        CompatibilityChecker.IsSupported(featureId, Windows11).IsSupported.Should().BeTrue();
    }

    [Fact]
    public void Feature_exactly_at_the_minimum_build_is_supported()
    {
        var exactly2004 = new WindowsVersionInfo(
            WindowsVersionInfo.Windows10Build2004, IsWindows11: false, IsProOrHigher: true);

        CompatibilityChecker.IsSupported("Hags", exactly2004).IsSupported.Should().BeTrue();
    }

    [Fact]
    public void Features_removed_in_windows11_are_blocked_there_but_allowed_on_windows10()
    {
        CompatibilityChecker.IsSupported("BackgroundApps", Windows10_21H2).IsSupported.Should().BeTrue();

        var onWin11 = CompatibilityChecker.IsSupported("BackgroundApps", Windows11);
        onWin11.IsSupported.Should().BeFalse();
        onWin11.Reason.Should().Contain("Windows 11");
    }

    [Theory]
    [InlineData("AutoHdr")]
    [InlineData("DirectStorage")]
    public void Windows11_only_features_are_blocked_on_windows10(string featureId)
    {
        CompatibilityChecker.IsSupported(featureId, Windows10_21H2).IsSupported.Should().BeFalse();
        CompatibilityChecker.IsSupported(featureId, Windows11).IsSupported.Should().BeTrue();
    }

    [Fact]
    public void Pro_only_features_are_blocked_on_home_editions()
    {
        CompatibilityChecker.IsSupported("WbadminBmr", Windows11).IsSupported.Should().BeTrue();

        var onHome = CompatibilityChecker.IsSupported("WbadminBmr", Windows11Home);
        onHome.IsSupported.Should().BeFalse();
        onHome.Reason.Should().Contain("vssadmin", "kullanıcıya alternatif önerilmeli");
    }

    [Fact]
    public void Feature_ids_are_matched_case_insensitively()
    {
        CompatibilityChecker.IsSupported("HAGS", Windows10_1909).IsSupported.Should().BeFalse();
        CompatibilityChecker.IsSupported("hags", Windows10_1909).IsSupported.Should().BeFalse();
    }

    [Fact]
    public void Every_requirement_explains_itself()
    {
        CompatibilityChecker.FeatureRequirements.Values
            .Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Note),
                "desteklenmeme nedeni UI'da gösterilir");
    }

    [Fact]
    public void Current_reports_the_running_system()
    {
        var current = WindowsVersionInfo.Current;

        current.Build.Should().Be(Environment.OSVersion.Version.Build);
        current.IsWindows11.Should().Be(current.Build >= WindowsVersionInfo.Windows11FirstBuild);
    }

    [Theory]
    // Home aileleri -> Pro-only özellikler (wbadmin BMR) sunulmaz.
    [InlineData("Core", false)]
    [InlineData("CoreN", false)]
    [InlineData("CoreSingleLanguage", false)]
    [InlineData("CoreCountrySpecific", false)]
    [InlineData("core", false)]                 // kayıt defteri değeri büyük/küçük harf duyarsız okunur
    // Pro ve üzeri.
    [InlineData("Professional", true)]
    [InlineData("Enterprise", true)]
    [InlineData("Education", true)]
    [InlineData("ServerStandard", true)]
    // Bilinmeyen/okunamayan -> İZİN VERİCİ. Bir özelliği yanlışlıkla kapatmaktansa
    // çalıştırıp hatayı zarifçe ele almak yeğdir (belgelenmiş varsayılan).
    [InlineData("", true)]
    [InlineData("SomeFutureEdition", true)]
    public void MapEditionToProOrHigher_treats_only_home_families_as_limited(string editionId, bool expected)
        => WindowsVersionInfo.MapEditionToProOrHigher(editionId).Should().Be(expected);

    [Fact]
    public void Current_reports_the_real_edition_not_a_hardcoded_true()
    {
        var current = WindowsVersionInfo.Current;

        // EditionID okunabildiyse IsProOrHigher onunla tutarlı olmalı. Eskiden IsProOrHigher
        // sabit true'ydu; Home makinelerde wbadmin/Hyper-V yolları sunulup Process.Start
        // seviyesinde patlıyordu.
        if (!string.IsNullOrEmpty(current.EditionId))
        {
            current.IsProOrHigher.Should()
                .Be(WindowsVersionInfo.MapEditionToProOrHigher(current.EditionId));
        }
    }
}
