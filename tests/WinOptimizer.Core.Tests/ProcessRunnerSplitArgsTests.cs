using FluentAssertions;
using WinOptimizer.Safety;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// ProcessRunner.SplitToArgs — komut enjeksiyonu savunmasının birim testleri.
/// E2 sertleştirmesini (master plan §17.5) doğrular: kabuklar tek argüman
/// korur, diğerleri boşluktan bölünür — dinamik veri asla tek string'de birleşmez.
/// </summary>
public class ProcessRunnerSplitArgsTests
{
    [Theory]
    [InlineData("cmd.exe")]      // cmd /c <tek metin>
    [InlineData("CMD.EXE")]      // büyük/küçük harf duyarsız
    [InlineData("powershell.exe")]
    [InlineData("pwsh.exe")]
    public void SplitToArgs_keeps_shell_command_as_single_arg(string shell)
    {
        // Kabuklar tek bir argüman bekler; boşluklardan bölünmemeli (enjeksiyon yüzeyi).
        var args = ProcessRunner.SplitToArgs(shell, "/c echo hello & del important.file");

        args.Should().ContainSingle()
            .Which.Should().Be("/c echo hello & del important.file");
    }

    [Theory]
    [InlineData("reg.exe", "export HKLM\\SOFTWARE out.reg /y", 4)]
    [InlineData("sfc", "/scannow", 1)]
    [InlineData("net.exe", "stop wuauserv", 2)]
    public void SplitToArgs_splits_non_shell_on_spaces(string file, string args, int expectedCount)
    {
        var result = ProcessRunner.SplitToArgs(file, args);

        result.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void SplitToArgs_preserves_argument_order_for_powercfg()
    {
        var args = ProcessRunner.SplitToArgs("powercfg.exe", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");

        args.Should().Equal("/setactive", "381b4222-f694-41f0-9685-ff5bb260df2e");
    }

    [Fact]
    public void SplitToArgs_treats_full_path_shell_as_shell()
    {
        // Tam yollar da dosya adından tanınmalı (C:\Windows\System32\cmd.exe).
        var args = ProcessRunner.SplitToArgs(@"C:\Windows\System32\cmd.exe", "/c dir");

        args.Should().ContainSingle().Which.Should().Be("/c dir");
    }

    [Fact]
    public void SplitToArgs_empty_args_yields_empty_for_non_shell()
    {
        var args = ProcessRunner.SplitToArgs("reg.exe", "");
        args.Should().BeEmpty();
    }
}
