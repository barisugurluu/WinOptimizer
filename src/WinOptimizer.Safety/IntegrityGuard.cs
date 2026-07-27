using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Safety;

/// <summary>
/// Bütünlük koruyucu — journal ve registry yedek dosyalarına HMAC-SHA256 yan
/// dosyası (<c>&lt;file&gt;.hmac</c>) üretir ve geri alma öncesi doğrular
/// (master plan §17.4 — "Journal &amp; Backup Bütünlük Doğrulaması").
///
/// <para>İmza anahtarı kuruluma özeldir (<see cref="IntegrityKeyStore"/> ile DPAPI
/// korumalı olarak üretilir/saklanır). Aynı kullanıcı/makine dışında kimse geçerli
/// bir HMAC üretemez; dosya kurcalandığında doğrulama başarısız olur ve geri alma
/// güvenli biçimde engellenir ("güven aşıldı — geri alma devre dışı").</para>
/// </summary>
public sealed class IntegrityGuard
{
    private readonly byte[] _key;
    private readonly ILogger<IntegrityGuard> _logger;

    /// <summary>Belirtilen HMAC anahtarı ve günlükçü ile bütünlük koruyucu oluşturur.</summary>
    /// <param name="key">HMAC anahtarı (32 bayt önerilir). <see cref="IntegrityKeyStore.LoadOrCreate"/>.</param>
    /// <param name="logger">Günlükçü.</param>
    public IntegrityGuard(byte[] key, ILogger<IntegrityGuard> logger)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _logger = logger;
    }

    /// <summary>Bir dosyanın HMAC yan dosyasını (<c>&lt;path&gt;.hmac</c>) yazar.</summary>
    public async Task SignFileAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            return;
        }

        byte[] hash = await ComputeHmacAsync(path, ct).ConfigureAwait(false);
        string sidecar = SidecarPath(path);
        await File.WriteAllTextAsync(sidecar, ToHex(hash), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Bir dosyanın içeriğini yan HMAC dosyasıyla karşılaştırır.
    /// Yan dosya yoksa veya uyuşmazsa <c>false</c> döner (kurcalanmış/bozuksa).
    /// </summary>
    public async Task<bool> VerifyFileAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        string sidecar = SidecarPath(path);
        if (!File.Exists(sidecar))
        {
            _logger.LogWarning("Bütünlük yan dosyası yok (imzalanmamış?): {Path}", path);
            return false;
        }

        byte[] actual = await ComputeHmacAsync(path, ct).ConfigureAwait(false);
        string expected = await File.ReadAllTextAsync(sidecar, ct).ConfigureAwait(false);

        bool ok = FixedTimeEquals(ToHex(actual), expected);
        if (!ok)
        {
            _logger.LogError("Bütünlük doğrulaması BAŞARISIZ — dosya kurcalanmış olabilir: {Path}", path);
        }

        return ok;
    }

    /// <summary>Dosya içeriği üzerinden HMAC-SHA256 hesaplar.</summary>
    private async Task<byte[]> ComputeHmacAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        var hmac = new HMACSHA256(_key);
        return await hmac.ComputeHashAsync(stream, ct).ConfigureAwait(false);
    }

    private static string SidecarPath(string path) => path + ".hmac";

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>Zamanlama saldırısına karşı sabit süreli karşılaştırma.</summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }
}
