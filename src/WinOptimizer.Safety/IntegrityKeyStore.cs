using System.Security.Cryptography;
using System.Text;

namespace WinOptimizer.Safety;

/// <summary>
/// HMAC imzalama anahtarını DPAPI ile koruyarak üretir/saklar
/// (<see cref="IntegrityGuard"/> için). Anahtar kuruluma özeldir; aynı Windows
/// kullanıcısı dışında kimse okuyamaz (master plan §17.4).
/// </summary>
public static class IntegrityKeyStore
{
    private const string KeyFileName = "integrity.key";

    /// <summary>
    /// Veri dizinindeki anahtarı yükler; yoksa güvenli rastgele anahtar üretip
    /// DPAPI (CurrentUser) ile şifreleyerek saklar ve döndürür.
    /// </summary>
    public static byte[] LoadOrCreate(string baseDir)
    {
        Directory.CreateDirectory(baseDir);
        string keyFile = Path.Combine(baseDir, KeyFileName);

        if (File.Exists(keyFile))
        {
            byte[] protectedKey = File.ReadAllBytes(keyFile);
            try
            {
                return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                // Anahtar bu kullanıcı/makine için geçersiz (taşınmış olabilir): yenisini üret.
            }
        }

        byte[] fresh = RandomNumberGenerator.GetBytes(32);
        byte[] protectedFresh = ProtectedData.Protect(fresh, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(keyFile, protectedFresh);
        return fresh;
    }
}

/// <summary>
/// DPAPI (Data Protection API) sarmalayıcısı — opsiyonel gizli değerleri
/// (API belirteci vb.) Windows kullanıcısı bazında şifreler (master plan §17).
/// Ayar dosyasına düz metin yerine <c>ProtectedValues</c> sözlüğü içinde base64
/// olarak konur; yalnızca aynı kullanıcı çözebilir.
/// </summary>
public static class SecretProtector
{
    /// <summary>Düz metni DPAPI ile şifreleyip base64 döndürür.</summary>
    public static string Protect(string plain)
    {
        byte[] blob = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plain ?? string.Empty), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(blob);
    }

    /// <summary>base64 DPAPI kabını çözüp düz metni döndürür.</summary>
    public static string Unprotect(string protectedBase64)
    {
        byte[] blob = Convert.FromBase64String(protectedBase64);
        byte[] plain = ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }
}
