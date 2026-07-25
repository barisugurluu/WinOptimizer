using System.Security.Cryptography;
using WinOptimizer.Native;

namespace WinOptimizer.Updater;

/// <summary>
/// Güncelleme paketinin bütünlüğünü (SHA-256) ve — Windows'ta — Authenticode imzasını doğrular
/// (master plan Bölüm 17.2/20.6). SHA-256 zorunlu; imza Windows'ta best-effort öneridir.
/// </summary>
public sealed class UpdateVerifier
{
    /// <summary>Paketin SHA-256 ve (Windows'ta) Authenticode imzasını doğrular.</summary>
    public VerifyResult Verify(string packagePath, string expectedSha256)
    {
        bool hashOk = VerifySha256(packagePath, expectedSha256);
        bool sigOk = OperatingSystem.IsWindows() && WinTrust.VerifyEmbeddedSignature(packagePath);
        return new VerifyResult(hashOk, sigOk);
    }

    /// <summary>Dosya SHA-256'sı beklenen değerle eşleşiyor mu? Beklenen boşsa true (best-effort atla).</summary>
    public static bool VerifySha256(string path, string expected)
    {
        if (string.IsNullOrEmpty(expected))
            return true;
        if (!File.Exists(path))
            return false;
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        string actual = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        return string.Equals(actual, expected.ToLowerInvariant(), StringComparison.Ordinal);
    }
}

/// <summary>Doğrulama sonucu.</summary>
/// <param name="HashOk">SHA-256 eşleşti (veya beklenen boş).</param>
/// <param name="SignatureOk">Authenticode imzası geçerli (yalnızca Windows).</param>
public sealed record VerifyResult(bool HashOk, bool SignatureOk)
{
    /// <summary>Paket kurulabilir mi? Hash geçerli olmalı; imza uyarı olarak kabul edilir (uygulama imzasızsa).</summary>
    public bool IsInstallable => HashOk;
}
