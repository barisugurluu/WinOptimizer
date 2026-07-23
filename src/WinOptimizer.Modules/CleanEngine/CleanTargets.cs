namespace WinOptimizer.Modules.CleanEngine;

/// <summary>
/// Temizlik hedefleri — master plan Bölüm 3.1'deki CleanEngine alt görev listesi.
/// Her hedef güvenli (kritik veri içermeyen) geçici/önbellek dosyalarını işaret eder.
/// </summary>
internal static class CleanTargets
{
    /// <summary>"Kilitli dosyalar atlanır" kuralı için dosya yaşı eşiği (24 saat).</summary>
    public const double MinFileAgeHours = 24.0;

    /// <summary>Genel geçici dizinler. Kullanıcı TEMP ve Sistem TEMP.</summary>
    public static IReadOnlyList<string> TempFolders => new[]
    {
        Path.GetTempPath(),                       // %TEMP%
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") // C:\Windows\Temp
    };

    /// <summary>Prefetch önbelleği (Windows yeniden oluşturur).</summary>
    public static string PrefetchFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");

    /// <summary>Windows Update indirilen yüklemeler (yalnızca Download alt klasörü).</summary>
    public static string WindowsUpdateDownload =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SoftwareDistribution", "Download");

    /// <summary>Delivery Optimization önbelleği.</summary>
    public static string DeliveryOptimization =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SoftwareDistribution", "DeliveryOptimization");

    /// <summary>Sistem günlük dizinleri (.log, .cab, dökümler).</summary>
    public static IReadOnlyList<string> LogFolders
    {
        get
        {
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return new[]
            {
                Path.Combine(win, "Logs"),
                Path.Combine(win, "Logs", "CBS"),
                Path.Combine(progData, "Microsoft", "Windows", "WER"), // Windows Error Reporting
                Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(progData, "Microsoft", "Windows", "WER", "ReportQueue")
            };
        }
    }

    /// <summary>Silinebilir günlük dosya uzantıları.</summary>
    public static IReadOnlySet<string> LogExtensions => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".cab", ".old", ".tmp", ".etl", ".dmp"
    };

    /// <summary>
    /// Chromium tabanlı tarayıcı önbellek alt dizinleri.
    /// NOT: Çerez/şifre/oturuma DOKUNULMAZ (yalnızca Cache aileleri).
    /// </summary>
    public static IReadOnlyList<string> ChromiumCacheSubdirs => new[]
    {
        "Cache", "Code Cache", "GPUCache", "Crashpad", "Service Worker"
    };

    /// <summary>
    /// Çerez/oturum dosyaları — KESİNLİKLE silinmez (beyaz liste).
    /// </summary>
    public static IReadOnlySet<string> BrowserProtectedFiles => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Cookies", "Login Data", "Web Data", "History", "Bookmarks",
        "Preferences", "Local State", "Sessions", "Network\\Cookies"
    };

    /// <summary>
    /// Tarayıcı "User Data" kök dizinlerini (yüklüyse) döndürür.
    /// Chrome, Edge, Brave profilleri taranır.
    /// </summary>
    public static IEnumerable<string> GetBrowserUserdataFolders()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(local, "Google", "Chrome", "User Data"),
            Path.Combine(local, "Microsoft", "Edge", "User Data"),
            Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data"),
            Path.Combine(local, "Vivaldi", "User Data")
        };
        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) yield return c;
        }
    }

    /// <summary>
    /// Firefox profillerini döndürür (Bölüm 3.1 — profiles.ini çözümleme).
    /// Mozilla\Firefox\Profiles altındaki her profili tarar.
    /// </summary>
    public static IEnumerable<string> GetFirefoxProfilePaths()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var profilesRoot = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
        if (!Directory.Exists(profilesRoot)) yield break;
        foreach (var dir in Directory.EnumerateDirectories(profilesRoot, "*", SearchOption.TopDirectoryOnly))
        {
            yield return dir;
        }
    }

    /// <summary>Firefox önbellek alt dizinleri (cache2, startupCache).</summary>
    public static IReadOnlyList<string> FirefoxCacheSubdirs => new[]
    {
        "cache2", "startupCache", "shader-cache", "thumbnails"
    };
}
