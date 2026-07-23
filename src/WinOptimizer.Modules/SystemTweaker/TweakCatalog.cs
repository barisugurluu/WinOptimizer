using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.SystemTweaker;

/// <summary>
/// Tweak kataloğu — master plan Bölüm 3.9'daki güvenli tweak'ler.
/// Riskli olanlar (8.3, pagefile) varsayılan KAPALI; ayrı onay ister.
/// </summary>
public static class TweakCatalog
{
    public static IReadOnlyList<RegistryTweak> All { get; } = new[]
    {
        // --- Dosya Sistemi (NTFS) — Bölüm 3.9.B ---
        new RegistryTweak(
            "NtfsDisable8dot3", "NTFS 8.3 kısa isim oluşturmayı kapat",
            RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem",
            "NtfsDisable8dot3NameCreation", 1, 0, RegistryValueKind.DWord, RiskLevel.Medium,
            "Dizin tarama hızını artırır (eski 16-bit uygulamaları etkileyebilir)."),
        new RegistryTweak(
            "NtfsDisableLastAccess", "NTFS LastAccess güncellemesini kapat",
            RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem",
            "NtfsDisableLastAccessUpdate", 1, 0, RegistryValueKind.DWord, RiskLevel.Low,
            "Yazma I/O'sunu azaltır."),
        new RegistryTweak(
            "NtfsMemoryUsage", "NTFS bellek kullanımını artır",
            RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem",
            "NtfsMemoryUsage", 2, 1, RegistryValueKind.DWord, RiskLevel.Low,
            "Önbellek boyutunu artırır."),

        // --- Görsel / UX — Bölüm 3.9.C ---
        new RegistryTweak(
            "MenuShowDelay", "Menü gösterim gecikmesini sıfırla",
            RegistryHive.CurrentUser, @"Control Panel\Desktop",
            "MenuShowDelay", "0", "400", RegistryValueKind.String, RiskLevel.Low,
            "Menüler anında açılır."),
        new RegistryTweak(
            "SystemUsesTransparency", "Pencere şeffaflığını kapat",
            RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "EnableTransparency", 0, 1, RegistryValueKind.DWord, RiskLevel.Low,
            "CPU/GPU tasarrufu."),

        // --- Güç — Bölüm 3.9.A (Ultimate Performance powercfg ile ayrı) ---
        new RegistryTweak(
            "TelemetryAllow", "Telemetri düzeyini en aza indir",
            RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            "AllowTelemetry", 0, null, RegistryValueKind.DWord, RiskLevel.Low,
            "Microsoft'a giden tanılama verisini en aza indirir.")
    };
}

/// <summary>
/// PowerPlanManager — powercfg ile güç planı yönetimi (Bölüm 3.9.A / Faz 4).
/// Ultimate Performance planını oluşturur ve aktifleştirir.
/// </summary>
public sealed class PowerPlanManager
{
    private readonly ProcessRunner _runner;
    private readonly ILogger<PowerPlanManager> _logger;

    /// <summary>Ultimate Performance plan GUID'si (master plan Bölüm 4.3).</summary>
    public const string UltimatePerformanceGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public PowerPlanManager(ProcessRunner runner, ILogger<PowerPlanManager> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    /// <summary>Ultimate Performance planını kopyalar (yoksa oluşturur) ve aktifleştirir.</summary>
    public async Task<bool> EnableUltimatePerformanceAsync()
    {
        try
        {
            // powercfg -duplicatescheme <guid>
            await _runner.RunAsync("powercfg.exe", $"-duplicatescheme {UltimatePerformanceGuid}", null);
            // Aktif plan yap (kopyalanan GUID dinamik; en son oluşturulan Ultimate Performance'ı bul)
            var (code, output) = await _runner.RunCaptureAsync("powercfg.exe", "/list");
            if (code != 0) return false;

            // Çıktıdaki Ultimate Performance satırından GUID'i çıkar
            var match = System.Text.RegularExpressions.Regex.Match(
                output, @"([0-9a-fA-F]{8}-[0-9a-fA-F-]{27}).*Ultimate Performance",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string guid = match.Groups[1].Value;
                int setActiveCode = await _runner.RunAsync("powercfg.exe", $"/setactive {guid}", null);
                _logger.LogInformation("Ultimate Performance planı aktifleştirildi: {Guid}", guid);
                return setActiveCode == 0;
            }
            _logger.LogWarning("Ultimate Performance plan GUID'i bulunamadı.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ultimate Performance etkinleştirilemedi.");
            return false;
        }
    }
}
