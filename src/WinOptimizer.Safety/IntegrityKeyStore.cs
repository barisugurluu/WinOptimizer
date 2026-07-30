using System.Security.Cryptography;
using System.Text;

namespace WinOptimizer.Safety;

/// <summary>
/// HMAC imzalama anahtarını DPAPI ile koruyarak üretir/saklar
/// (<see cref="IntegrityGuard"/> için) — master plan §17.4.
/// </summary>
/// <remarks>
/// <para><b>Kapsam MAKİNE'dir (LocalMachine), kullanıcı değil.</b> Anahtar
/// <c>%ProgramData%\WinOptimizer</c> altında, yani makine geneli ve yalnız yöneticinin
/// yazabildiği bir dizinde durur. Aynı journal'ı doğrulaması gerekenler: arayüzü çalıştıran
/// kullanıcı, <b>LocalSystem</b> olarak çalışan RealtimeGuard servisi ve SYSTEM olarak
/// çalışan zamanlanmış görev. <c>CurrentUser</c> kapsamı bunların hiçbirinde işe yaramıyordu:
/// ikinci bir Windows kullanıcısı açtığında çözme başarısız oluyor, anahtar sessizce yeniden
/// üretiliyor ve o ana kadarki TÜM <c>.hmac</c> imzaları doğrulanamaz hale geliyordu.</para>
/// <para><b>Ödünleşim (bilinçli):</b> <c>LocalMachine</c> kapsamında aynı makinedeki herhangi
/// bir süreç anahtarı çözebilir. Tehdit modeli gizlilik değil, <b>kurcalama tespiti</b>dir:
/// amaç, yönetici-yazılabilir bir dizindeki journal dosyalarının fark edilmeden
/// değiştirilememesidir. Anahtar dosyasının kendisi zaten yönetici korumasındadır.</para>
/// </remarks>
public static class IntegrityKeyStore
{
    private const string KeyFileName = "integrity.key";

    /// <summary>
    /// Veri dizinindeki anahtarı yükler; yoksa güvenli rastgele anahtar üretip
    /// DPAPI (LocalMachine) ile şifreleyerek saklar ve döndürür.
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
                return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.LocalMachine);
            }
            catch (CryptographicException)
            {
                // Eski sürümlerden kalma CurrentUser kapsamlı anahtar olabilir: göç dene.
            }

            try
            {
                byte[] legacy = ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
                // Aynı anahtarı LocalMachine kapsamıyla yeniden yaz: mevcut .hmac imzaları
                // GEÇERLİ KALIR (anahtar değişmiyor, yalnız koruma kapsamı değişiyor).
                File.WriteAllBytes(keyFile,
                    ProtectedData.Protect(legacy, null, DataProtectionScope.LocalMachine));
                return legacy;
            }
            catch (CryptographicException)
            {
                // Anahtar bu makine için de çözülemiyor (dosya kopyalanmış/bozulmuş):
                // yenisi üretilir. Bu noktada eski .hmac dosyaları doğrulanamaz — çağıran
                // (IntegrityGuard) bunu doğrulama hatası olarak raporlar.
            }
        }

        byte[] fresh = RandomNumberGenerator.GetBytes(32);
        byte[] protectedFresh = ProtectedData.Protect(fresh, null, DataProtectionScope.LocalMachine);
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
