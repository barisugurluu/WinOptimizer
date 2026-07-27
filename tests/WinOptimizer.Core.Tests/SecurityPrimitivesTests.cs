using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Core;
using WinOptimizer.Safety;
using Xunit;

namespace WinOptimizer.Core.Tests;

/// <summary>
/// Güvenlik ve dayanıklılık ilkeleri — HMAC bütünlük (§17.4), DPAPI (§17),
/// Polly geçici-hata yeniden deneme (§19) ve dosya boyutu biçimlendirici (CA1305/DRY).
/// </summary>
public class SecurityPrimitivesTests : IDisposable
{
    private readonly string _tempDir;

    public SecurityPrimitivesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wo-sec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    // --- HMAC bütünlük koruyucu (IntegrityGuard) ---

    [Fact]
    public async Task IntegrityGuard_sign_then_verify_roundtrip_passes()
    {
        string path = Path.Combine(_tempDir, "journal.jsonl");
        await File.WriteAllTextAsync(path, "line1\nline2\n");
        var guard = new IntegrityGuard(new byte[32], NullLogger<IntegrityGuard>.Instance);

        await guard.SignFileAsync(path);
        File.Exists(path + ".hmac").Should().BeTrue();

        (await guard.VerifyFileAsync(path)).Should().BeTrue();
    }

    [Fact]
    public async Task IntegrityGuard_verify_fails_when_file_tampered()
    {
        string path = Path.Combine(_tempDir, "data.reg");
        await File.WriteAllTextAsync(path, "Windows Registry Editor Version 5.00");
        var guard = new IntegrityGuard(new byte[32], NullLogger<IntegrityGuard>.Instance);

        await guard.SignFileAsync(path);
        await File.AppendAllTextAsync(path, "[HACK]");

        (await guard.VerifyFileAsync(path)).Should().BeFalse();
    }

    [Fact]
    public async Task IntegrityGuard_verify_fails_without_sidecar()
    {
        string path = Path.Combine(_tempDir, "unsigned.txt");
        await File.WriteAllTextAsync(path, "payload");
        var guard = new IntegrityGuard(new byte[32], NullLogger<IntegrityGuard>.Instance);

        (await guard.VerifyFileAsync(path)).Should().BeFalse();
    }

    [Fact]
    public void IntegrityKeyStore_load_or_create_returns_stable_key()
    {
        byte[] k1 = IntegrityKeyStore.LoadOrCreate(_tempDir);
        byte[] k2 = IntegrityKeyStore.LoadOrCreate(_tempDir);

        k1.Should().HaveCount(32);
        k2.Should().Equal(k1);   // ikinci çağrı aynı anahtarı döndürür (kalıcı)
    }

    // --- DPAPI gizli koruyucu (SecretProtector) ---

    [Fact]
    public void SecretProtector_protect_unprotect_roundtrip()
    {
        const string secret = "github-token-abcdef123456";

        string blob = SecretProtector.Protect(secret);

        blob.Should().NotBe(secret);                       // gerçekten şifreli
        SecretProtector.Unprotect(blob).Should().Be(secret); // round-trip
    }

    // --- Polly dayanıklılık (Resilience) ---

    [Fact]
    public async Task Resilience_retries_transient_failures_then_succeeds()
    {
        int attempts = 0;

        int result = await Resilience.ExecuteAsync(_ =>
        {
            attempts++;
            if (attempts < 3) throw new IOException("RPC sunucusu meşgul");
            return Task.FromResult(42);
        }, CancellationToken.None);

        result.Should().Be(42);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task Resilience_propagates_exception_after_retries_exhausted()
    {
        Func<Task> act = () => Resilience.ExecuteAsync<int>(
            _ => throw new IOException("kalıcı hata"), CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
    }

    // --- Dosya boyutu biçimlendirici (InvariantCulture, DRY) ---

    [Theory]
    [InlineData(1L << 30, "1.00 GB")]
    [InlineData(1L << 20, "1.0 MB")]
    [InlineData(1L << 10, "1 KB")]
    [InlineData(512, "512 B")]
    public void FileSizeFormatter_uses_invariant_decimal_dot(long bytes, string expected)
    {
        FileSizeFormatter.Format(bytes).Should().Be(expected);
    }

    [Fact]
    public void FileSizeFormatter_output_has_no_culture_decimal_comma()
    {
        string formatted = FileSizeFormatter.Format(1_500_000_000);
        formatted.Should().NotContain(",");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }
}
