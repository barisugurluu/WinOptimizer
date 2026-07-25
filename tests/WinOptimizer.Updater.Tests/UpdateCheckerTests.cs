using WinOptimizer.Updater;
using Xunit;

namespace WinOptimizer.Updater.Tests;

/// <summary>
/// UpdateChecker statik yardımcılarının birim testleri.
/// Ağ bağımlı CheckAsync bu testlerin dışında (gerçek GitHub API gerektirir).
/// </summary>
public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3-beta.1", "1.2.3")]
    [InlineData("v0.1.0", "0.1.0")]
    [InlineData("", "0.0.0.0")]
    [InlineData("garbage", "0.0.0.0")]
    public void ParseVersion_strips_prefix_and_prerelease(string tag, string expected)
        => Assert.Equal(new Version(expected), UpdateChecker.ParseVersion(tag));

    [Fact]
    public void ParseVersion_handles_four_part_versions()
        => Assert.Equal(new Version(1, 2, 3, 4), UpdateChecker.ParseVersion("v1.2.3.4"));

    [Fact]
    public void UpdateOptions_defaults_are_sensible()
    {
        var o = new UpdateOptions();
        Assert.Equal("WinOptimizer", o.Owner);
        Assert.Equal(UpdateChannel.Stable, o.Channel);
        Assert.Equal(TimeSpan.FromSeconds(30), o.TimeoutValue);
        Assert.Equal(new Version(0, 0, 0, 0), o.CurrentVersionValue);
    }

    [Fact]
    public void UpdateOptions_honors_explicit_values()
    {
        var o = new UpdateOptions(
            Owner: "foo", Repo: "bar", Channel: UpdateChannel.Beta,
            CurrentVersion: new Version(2, 0), Timeout: TimeSpan.FromSeconds(5));
        Assert.Equal("foo", o.Owner);
        Assert.Equal(UpdateChannel.Beta, o.Channel);
        Assert.Equal(TimeSpan.FromSeconds(5), o.TimeoutValue);
        Assert.Equal(new Version(2, 0), o.CurrentVersionValue);
    }
}

/// <summary>UpdateVerifier bütünlük doğrulama testleri (imza doğrulama Windows+gerçek imzalı dosya gerektirir).</summary>
public class UpdateVerifierTests
{
    [Fact]
    public void VerifySha256_empty_expected_skips_check()
        => Assert.True(UpdateVerifier.VerifySha256("nonexistent.msi", string.Empty));

    [Fact]
    public void VerifySha256_missing_file_fails()
        => Assert.False(UpdateVerifier.VerifySha256("definitely-not-here.msi", "deadbeef"));

    [Fact]
    public void VerifyResult_installable_requires_hash_only()
    {
        Assert.True(new VerifyResult(HashOk: true, SignatureOk: false).IsInstallable);
        Assert.False(new VerifyResult(HashOk: false, SignatureOk: true).IsInstallable);
    }

    [Fact]
    public void VerifySha256_matches_known_content()
    {
        // "abc" için SHA-256 (bilgi intihar testi).
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "abc");
            const string expected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
            Assert.True(UpdateVerifier.VerifySha256(tmp, expected));
            Assert.False(UpdateVerifier.VerifySha256(tmp, "00"));
        }
        finally { File.Delete(tmp); }
    }
}
