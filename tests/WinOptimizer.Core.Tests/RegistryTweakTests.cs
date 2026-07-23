using FluentAssertions;
using Microsoft.Win32;
using WinOptimizer.Modules.SystemTweaker;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// Registry tweak "uygula → geri al" simetri/idempotentlik testleri.
/// (Master plan Bölüm 8.1 — her tweak uygula/geri al simetrik olmalı.)
/// Testler HKCU altında izole bir test anahtarı kullanır (yönetici gerektirmez).
/// </summary>
public class RegistryTweakTests : IDisposable
{
    private const string TestKeyPath = @"SOFTWARE\WinOptimizerTest";
    private readonly RegistryTweakApplier _applier = new();

    public RegistryTweakTests()
    {
        // Test öncesi anahtarı temizle
        Registry.CurrentUser.DeleteSubKeyTree(TestKeyPath, throwOnMissingSubKey: false);
    }

    [Fact]
    public void SetValue_then_RevertValue_restores_previous()
    {
        var tweak = MakeTweak("TestValue", 1, 0);

        // Önce başlangıç değeri 0 olsun
        using (var key = Registry.CurrentUser.CreateSubKey(TestKeyPath))
        {
            key.SetValue("TestValue", 0, RegistryValueKind.DWord);
        }

        // Uygula → önceki (0) dönmeli
        var (ok, previous) = _applier.SetValue(tweak);
        ok.Should().BeTrue();
        previous.Should().Be(0);
        _applier.IsEnabled(tweak).Should().BeTrue("değer EnabledValue=1'e eşit olmalı");

        // Geri al → 0'a dönmeli
        _applier.RevertValue(tweak, previous).Should().BeTrue();
        _applier.IsEnabled(tweak).Should().BeFalse("geri al sonrası kapalı olmalı");
    }

    [Fact]
    public void SetValue_returns_null_previous_when_key_did_not_exist()
    {
        var tweak = MakeTweak("BrandNewValue", 1, 0);
        var (ok, previous) = _applier.SetValue(tweak);

        ok.Should().BeTrue();
        previous.Should().BeNull("anahtar daha önce yoktu");
    }

    [Fact]
    public void IsEnabled_false_when_key_missing()
    {
        var tweak = MakeTweak("DoesNotExist", 1, 0);
        _applier.IsEnabled(tweak).Should().BeFalse();
    }

    [Fact]
    public void TweakCatalog_has_entries()
    {
        TweakCatalog.All.Should().NotBeEmpty();
        TweakCatalog.All.Should().OnlyContain(t => !string.IsNullOrEmpty(t.Id));
        // Aynı Id iki kez olmamalı
        TweakCatalog.All.Select(t => t.Id).Should().OnlyHaveUniqueItems();
    }

    private static RegistryTweak MakeTweak(string valueName, object on, object off) =>
        new("Test_" + valueName, "Test tweak",
            RegistryHive.CurrentUser, TestKeyPath, valueName, on, off,
            RegistryValueKind.DWord, Core.RiskLevel.Low, "Test açıklaması");

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(TestKeyPath, throwOnMissingSubKey: false);
    }
}
