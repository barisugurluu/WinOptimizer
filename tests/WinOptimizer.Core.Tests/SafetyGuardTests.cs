using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Safety;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// SafetyGuard beyaz liste korumaları — kritik servislere/yollara asla dokunulmaması.
/// (Master plan Bölüm 3.5 & 8.1.)
/// </summary>
public class SafetyGuardTests
{
    private static SafetyGuard Create() =>
        new(NullLogger<SafetyGuard>.Instance);

    [Theory]
    [InlineData("WinDefend")]   // Windows Defender
    [InlineData("RpcSs")]       // RPC
    [InlineData("EventLog")]    // Olay günlüğü
    [InlineData("PlugPlay")]    // Tak-Çalıştır
    public void IsCriticalService_recognizes_protected_services(string service)
    {
        var guard = Create();
        guard.IsCriticalService(service).Should().BeTrue();
    }

    [Theory]
    [InlineData("DiagTrack")]   // Telemetri — optimize edilebilir (kritik değil)
    [InlineData("SysMain")]     // Superfetch
    public void IsCriticalService_allows_non_critical_services(string service)
    {
        var guard = Create();
        guard.IsCriticalService(service).Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_blocks_critical_service_with_reason()
    {
        var guard = Create();
        var ok = guard.IsAllowed("WinDefend", out var reason);

        ok.Should().BeFalse();
        reason.Should().NotBeNullOrEmpty();
        reason.Should().Contain("WinDefend");
    }

    [Fact]
    public void IsAllowed_allows_normal_target()
    {
        var guard = Create();
        var ok = guard.IsAllowed("DiagTrack", out var reason);

        ok.Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void IsProtectedPath_blocks_system32()
    {
        var guard = Create();
        guard.IsProtectedPath(@"C:\Windows\System32\drivers").Should().BeTrue();
    }

    [Fact]
    public void IsProtectedPath_allows_temp()
    {
        var guard = Create();
        guard.IsProtectedPath(Path.GetTempPath()).Should().BeFalse();
    }
}
