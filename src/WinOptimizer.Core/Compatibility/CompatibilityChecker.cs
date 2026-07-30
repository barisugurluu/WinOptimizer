namespace WinOptimizer.Core.Compatibility;

/// <summary>
/// Bir özelliğin çalışması için gereken Windows koşulları
/// (master plan Bölüm 14 uyumluluk matrisi).
/// </summary>
/// <param name="Id">Özellik kimliği — <see cref="CompatibilityChecker.FeatureRequirements"/> anahtarı.</param>
/// <param name="MinimumBuild">Gereken en düşük Windows derleme numarası (0 = kısıt yok).</param>
/// <param name="RequiresProEdition">Home sürümünde çalışmıyorsa true.</param>
/// <param name="RemovedInWindows11">Windows 11'de kaldırıldıysa true.</param>
/// <param name="RequiresWindows11">Yalnızca Windows 11'de varsa true.</param>
/// <param name="Note">Desteklenmeme nedeninin kullanıcıya gösterilecek açıklaması.</param>
public sealed record FeatureRequirement(
    string Id,
    int MinimumBuild = 0,
    bool RequiresProEdition = false,
    bool RemovedInWindows11 = false,
    bool RequiresWindows11 = false,
    string Note = "");

/// <summary>Bir uyumluluk sorgusunun sonucu.</summary>
/// <param name="IsSupported">Özellik bu sistemde çalıştırılabilir mi.</param>
/// <param name="Reason">Desteklenmiyorsa nedeni (UI'da gri gösterimin yanına yazılır).</param>
public readonly record struct CompatibilityResult(bool IsSupported, string Reason)
{
    public static CompatibilityResult Supported() => new(true, string.Empty);
    public static CompatibilityResult NotSupported(string reason) => new(false, reason);
}

/// <summary>
/// Uyumluluk değerlendirmesinin dayandığı Windows sürüm bilgisi.
/// </summary>
/// <param name="Build">Windows derleme numarası (Win10 21H2 = 19044, Win11 = 22000+).</param>
/// <param name="IsWindows11">Windows 11 veya üzeri mi.</param>
/// <param name="IsProOrHigher">
/// Pro/Enterprise/Education sürümü mü. <see cref="Current"/> bunu kayıt defterindeki
/// <c>EditionID</c> değerinden tespit eder (bkz. <see cref="MapEditionToProOrHigher"/>).
/// Tanınmayan/okunamayan sürümler <c>true</c> kabul edilir: bir özelliği yanlışlıkla
/// kapatmaktansa çalıştırıp hatayı zarifçe ele almak yeğdir.
/// </param>
public sealed record WindowsVersionInfo(int Build, bool IsWindows11, bool IsProOrHigher)
{
    /// <summary>Windows 11'in başladığı derleme numarası.</summary>
    public const int Windows11FirstBuild = 22000;

    /// <summary>
    /// Windows 10 sürüm 2004 — HAGS, WSL2, HVCI/VBS için alt sınır ve ürünün desteklediği
    /// en düşük Windows sürümü. <b>AYNI SAYI üç yerde:</b> burada,
    /// <c>installer/WinOptimizer.iss</c> <c>MinVersion</c> ve
    /// <c>installer/winget/*.installer.yaml</c> <c>MinimumOSVersion</c>.
    /// </summary>
    public const int Windows10Build2004 = 19041;

    private static WindowsVersionInfo? _current;

    /// <summary>Üzerinde çalışılan sistemin sürüm bilgisi.</summary>
    public static WindowsVersionInfo Current => _current ??= Detect();

    /// <summary>
    /// Kayıt defterinden okunan ham sürüm kimliği (ör. <c>Professional</c>, <c>Core</c>).
    /// Okunamadıysa boş — teşhis/gereksinim raporunda gösterilir.
    /// </summary>
    public string EditionId { get; init; } = string.Empty;

    private static WindowsVersionInfo Detect()
    {
        int build = Environment.OSVersion.Version.Build;
        string edition = ReadEditionId();
        return new WindowsVersionInfo(
            build,
            IsWindows11: build >= Windows11FirstBuild,
            IsProOrHigher: MapEditionToProOrHigher(edition))
        {
            EditionId = edition,
        };
    }

    /// <summary>
    /// <c>HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\EditionID</c> değerini okur.
    /// Windows dışında veya okunamazsa boş döner.
    /// </summary>
    private static string ReadEditionId()
    {
        // Core net8.0 (platform-nötr) hedefler; Microsoft.Win32.Registry Windows'a özeldir.
        // Bu guard olmadan CA1416 + TreatWarningsAsErrors derlemeyi kırar.
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("EditionID") as string ?? string.Empty;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException
                                      or UnauthorizedAccessException
                                      or IOException)
        {
            // Okuma başarısız → izin verici varsayılana düşülür (aşağıdaki eşleme boş metni
            // Pro sayar). Sessiz geçilir çünkü Core'da günlükleyici yok; sürüm bilgisi
            // gereksinim raporunda "bilinmiyor" olarak görünür.
            return string.Empty;
        }
    }

    /// <summary>
    /// <c>EditionID</c> değerini Pro-veya-üzeri kararına çevirir. Saf fonksiyon — test edilebilir.
    /// Home aileleri <c>false</c>; diğer her şey (bilinmeyen/boş dahil) <c>true</c>.
    /// </summary>
    internal static bool MapEditionToProOrHigher(string editionId) =>
        !editionId.Equals("Core", StringComparison.OrdinalIgnoreCase) &&
        !editionId.Equals("CoreN", StringComparison.OrdinalIgnoreCase) &&
        !editionId.Equals("CoreSingleLanguage", StringComparison.OrdinalIgnoreCase) &&
        !editionId.Equals("CoreCountrySpecific", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Uyumluluk matrisi (master plan Bölüm 14) kapısı.
///
/// <para><b>Uygulama kuralı:</b> sürüme bağımlı bir özellik çalıştırılmadan önce
/// <see cref="IsSupported(string)"/> çağrılır; desteklenmiyorsa eylem sunulmaz/uygulanmaz ve
/// <see cref="CompatibilityResult.Reason"/> kullanıcıya gösterilir.</para>
/// </summary>
public static class CompatibilityChecker
{
    /// <summary>
    /// Yalnızca gerçekten sürüm/edition bağımlı özellikler listelenir. Her Windows sürümünde
    /// çalışanlar (SFC/DISM, telemetri, EmptyWorkingSet…) kasıtlı olarak yoktur —
    /// tanınmayan kimlik "desteklenir" sayılır.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, FeatureRequirement> FeatureRequirements =
        new Dictionary<string, FeatureRequirement>(StringComparer.OrdinalIgnoreCase)
        {
            ["Hags"] = new("Hags",
                MinimumBuild: WindowsVersionInfo.Windows10Build2004,
                Note: "Donanım Hızlandırmalı GPU Zamanlaması (HAGS) Windows 10 sürüm 2004 ve üzeri gerektirir."),

            ["Wsl2"] = new("Wsl2",
                MinimumBuild: WindowsVersionInfo.Windows10Build2004,
                Note: "WSL2 Windows 10 sürüm 2004 ve üzeri gerektirir."),

            ["Hvci"] = new("Hvci",
                MinimumBuild: WindowsVersionInfo.Windows10Build2004,
                Note: "Bellek Bütünlüğü (HVCI) Windows 10 sürüm 2004 ve üzeri gerektirir; " +
                      "sürücü uyumluluğu ayrıca doğrulanmalıdır."),

            ["Vbs"] = new("Vbs",
                MinimumBuild: WindowsVersionInfo.Windows10Build2004,
                Note: "Sanallaştırma Tabanlı Güvenlik (VBS) Hyper-V desteği ve Windows 10 2004+ gerektirir."),

            ["WbadminBmr"] = new("WbadminBmr",
                RequiresProEdition: true,
                Note: "Sistem görüntüsü yedeği (wbadmin) Windows Home sürümünde sınırlıdır; " +
                      "birim gölge kopyası (vssadmin) alternatifi kullanılabilir."),

            ["BackgroundApps"] = new("BackgroundApps",
                RemovedInWindows11: true,
                Note: "Arka plan uygulamaları toplu ayarı Windows 11'de kaldırıldı."),

            ["AutoHdr"] = new("AutoHdr",
                RequiresWindows11: true,
                Note: "Auto HDR yalnızca Windows 11'de bulunur."),

            ["DirectStorage"] = new("DirectStorage",
                RequiresWindows11: true,
                Note: "DirectStorage yalnızca Windows 11'de bulunur.")
        };

    /// <summary>Özelliğin çalışılan sistemde desteklenip desteklenmediğini döndürür.</summary>
    public static CompatibilityResult IsSupported(string featureId) =>
        IsSupported(featureId, WindowsVersionInfo.Current);

    /// <summary>
    /// Belirtilen sürüm bilgisine göre değerlendirir.
    /// Tanınmayan kimlikler desteklenir sayılır — matris yalnızca kısıtlı özellikleri listeler.
    /// </summary>
    public static CompatibilityResult IsSupported(string featureId, WindowsVersionInfo version)
    {
        if (!FeatureRequirements.TryGetValue(featureId, out var requirement))
        {
            return CompatibilityResult.Supported();
        }

        if (requirement.RequiresWindows11 && !version.IsWindows11)
        {
            return CompatibilityResult.NotSupported(requirement.Note);
        }

        if (requirement.RemovedInWindows11 && version.IsWindows11)
        {
            return CompatibilityResult.NotSupported(requirement.Note);
        }

        if (requirement.MinimumBuild > 0 && version.Build < requirement.MinimumBuild)
        {
            return CompatibilityResult.NotSupported(requirement.Note);
        }

        if (requirement.RequiresProEdition && !version.IsProOrHigher)
        {
            return CompatibilityResult.NotSupported(requirement.Note);
        }

        return CompatibilityResult.Supported();
    }
}
