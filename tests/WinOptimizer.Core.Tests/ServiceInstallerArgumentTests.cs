using FluentAssertions;
using WinOptimizer.Service;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// <see cref="ServiceInstaller"/>'ın ürettiği sc.exe argüman dizilerini doğrular.
/// Kritik nokta: sc.exe'nin <c>option= value</c> sözdizimi gereği <c>binPath=</c>,
/// <c>start=</c>, <c>DisplayName=</c> ile değerleri <b>ayrı</b> argümanlar olmalıdır.
/// Tek argümana birleştirilirse sc.exe sessizce yanlış servis oluşturur. Gerçek servis
/// kurulumu unit test edilmez (yönetici + sistem durumu gerektirir).
/// </summary>
public class ServiceInstallerArgumentTests
{
    private const string ExePath = @"C:\Program Files\WinOptimizer\WinOptimizer.Service.exe";

    [Fact]
    public void BuildCreateArgs_matches_golden_sc_create_sequence()
    {
        string[] args = ServiceInstaller.BuildCreateArgs(ExePath);

        args.Should().Equal(
            "create", "WinOptimizerGuard",
            "binPath=", ExePath,
            "start=", "auto",
            "DisplayName=", "WinOptimizer RealtimeGuard");
    }

    [Fact]
    public void BuildConfigArgs_keeps_same_values_as_create()
    {
        string[] create = ServiceInstaller.BuildCreateArgs(ExePath);
        string[] config = ServiceInstaller.BuildConfigArgs(ExePath);

        // Idempotent kurulum: 'config' fiil dışında 'create' ile birebir aynı olmalı,
        // aksi halde ikinci kurulum servisi farklı ayarlarla bırakır.
        config[0].Should().Be("config");
        config.Skip(1).Should().Equal(create.Skip(1));
    }

    [Fact]
    public void BuildCreateArgs_never_merges_option_key_with_its_value()
    {
        string[] args = ServiceInstaller.BuildCreateArgs(ExePath);

        foreach (string arg in args.Where(a => a.EndsWith('=')))
        {
            arg.Should().MatchRegex("^[A-Za-z]+=$",
                "sc.exe 'option=' ile degeri ayri argumanlar olarak bekler");
        }
    }

    [Fact]
    public void BuildDescriptionArgs_has_name_and_description()
    {
        ServiceInstaller.BuildDescriptionArgs().Should().Equal(
            "description", "WinOptimizerGuard", "WinOptimizer gerçek zamanlı koruma servisi");
    }

    [Fact]
    public void BuildFailureArgs_restarts_twice_after_crash()
    {
        ServiceInstaller.BuildFailureArgs().Should().Equal(
            "failure", "WinOptimizerGuard",
            "reset=", "86400",
            "actions=", "restart/60000/restart/60000//");
    }

    [Fact]
    public void BuildSimpleArgs_emits_verb_and_service_name()
    {
        ServiceInstaller.BuildSimpleArgs("delete").Should().Equal("delete", "WinOptimizerGuard");
    }

    [Theory]
    [InlineData("optimize")]
    [InlineData("install")]      // eski, hatalı verb — artık tanınmamalı (donma nedeni)
    [InlineData("uninstall")]
    [InlineData("--urls")]
    public async Task TryHandleAsync_returns_null_for_unknown_argument(string arg)
    {
        int? result = await ServiceInstaller.TryHandleAsync([arg]);

        result.Should().BeNull(
            "verb taninmazsa servis normal worker olarak calismali; " +
            "eski 'install' argumani sessizce worker baslatip kurulumu donduruyordu");
    }

    [Fact]
    public async Task TryHandleAsync_returns_null_without_arguments()
    {
        (await ServiceInstaller.TryHandleAsync([])).Should().BeNull();
    }
}
