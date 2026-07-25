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
/// Pro/Enterprise/Education sürümü mü. <see cref="Current"/> bunu <c>true</c> kabul eder:
/// Core platform-nötr olduğundan sürüm (edition) okumaz. Home/Pro ayrımına gerçekten duyarlı
/// bir çağıran, edition'ı kendi tespit edip <see cref="CompatibilityChecker.IsSupported(string, WindowsVersionInfo)"/>
/// aşırı yüklemesine geçirmelidir. Varsayılanın izin verici olması bilinçlidir — bir özelliği
/// yanlışlıkla kapatmaktansa çalıştırıp hatayı zarifçe ele almak yeğdir.
/// </param>
public sealed record WindowsVersionInfo(int Build, bool IsWindows11, bool IsProOrHigher)
{
    /// <summary>Windows 11'in başladığı derleme numarası.</summary>
    public const int Windows11FirstBuild = 22000;

    /// <summary>Windows 10 sürüm 2004 — HAGS, WSL2, HVCI/VBS için alt sınır.</summary>
    public const int Windows10Build2004 = 19041;

    private static WindowsVersionInfo? _current;

    /// <summary>Üzerinde çalışılan sistemin sürüm bilgisi.</summary>
    public static WindowsVersionInfo Current => _current ??= Detect();

    private static WindowsVersionInfo Detect()
    {
        int build = Environment.OSVersion.Version.Build;
        return new WindowsVersionInfo(build, IsWindows11: build >= Windows11FirstBuild, IsProOrHigher: true);
    }
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
