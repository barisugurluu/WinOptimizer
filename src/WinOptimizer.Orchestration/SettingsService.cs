using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Orchestration;

/// <summary>
/// Ayar modeli (master plan Bölüm 16.1 — settings.json). Sürüm 2.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Ayar şeması sürümü. v1→v2: EnabledModules + canlı metrik alanları eklendi.</summary>
    public int SchemaVersion { get; set; } = 2;
    public string Language { get; set; } = "tr-TR";
    public string Theme { get; set; } = "dark";
    public string ActiveProfile { get; set; } = "balanced";
    public SafetyNetSettings SafetyNet { get; set; } = new();
    public RealtimeGuardSettings RealtimeGuard { get; set; } = new();
    public SchedulerSettings Scheduler { get; set; } = new();

    /// <summary>
    /// "Tek Tıkla" kapsamında etkin modül kimlikleri. Boşliste = tüm kayıtlı modüller.
    /// (Bölüm 16.1 — modül aç/kapa kalıcılığı.)
    /// </summary>
    public List<string> EnabledModules { get; set; } = new();

    /// <summary>Genel Bakış sekmesinde canlı metrikler gösterilsin mi?</summary>
    public bool DashboardLiveMetrics { get; set; } = true;

    /// <summary>Canlı metrik yenileme aralığı (saniye).</summary>
    public int MetricsPollSeconds { get; set; } = 3;
}

public sealed class SafetyNetSettings
{
    public bool AutoRestorePoint { get; set; } = true;
    public bool AutoRegistryBackup { get; set; } = true;
    public bool RequireConfirmationForHighRisk { get; set; } = true;
}

public sealed class RealtimeGuardSettings
{
    public bool Enabled { get; set; } = true;
    public GuardThresholdValues Thresholds { get; set; } = new();
}

public sealed class GuardThresholdValues
{
    public int RamUsagePercent { get; set; } = 85;
    public int DiskFreePercent { get; set; } = 15;
    public int DiskFreeCriticalPercent { get; set; } = 5;
    public int CpuPerProcessPercent { get; set; } = 80;
    public int TempCelsius { get; set; } = 85;
}

public sealed class SchedulerSettings
{
    public WeeklyOptimizeSettings WeeklyOptimize { get; set; } = new();
}

public sealed class WeeklyOptimizeSettings
{
    public bool Enabled { get; set; } = true;
    public string Day { get; set; } = "Sunday";
    public string Time { get; set; } = "03:00";
}

/// <summary>
/// SettingsService — ayarları JSON olarak yükler/kaydeder (master plan Bölüm 16.1).
/// Kaydetme sonrası <see cref="SettingsChanged"/> olayını tetikler; UI güncellemek için kullanılır.
/// </summary>
public sealed class SettingsService
{
    private readonly string _filePath;
    private readonly ILogger<SettingsService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettings Current { get; private set; } = new();

    /// <summary>Ayarlar diske kaydedildiğinde/validasyon sonrası tetiklenir.</summary>
    public event EventHandler? SettingsChanged;

    public SettingsService(string baseDir, ILogger<SettingsService> logger)
    {
        _filePath = Path.Combine(baseDir, "settings.json");
        _logger = logger;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
                Migrate(Current);
                _logger.LogDebug("Ayarlar yüklendi: {Path} (şema v{Ver})", _filePath, Current.SchemaVersion);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ayarlar yüklenemedi, varsayılan kullanılıyor.");
            Current = new AppSettings();
        }
    }

    /// <summary>Eski şema sürümlerini geçerli sürüme yükseltir (şimdilik v1→v2).</summary>
    private static void Migrate(AppSettings s)
    {
        if (s.SchemaVersion < 2)
        {
            // v1'de bulunmayan alanlar varsayılanlarla doldu (zaten new() boş liste/true).
            s.EnabledModules ??= new List<string>();
            s.DashboardLiveMetrics = true;
            s.MetricsPollSeconds = 3;
            s.SchemaVersion = 2;
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOpts);
            File.WriteAllText(_filePath, json);
            _logger.LogInformation("Ayarlar kaydedildi: {Path}", _filePath);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ayarlar kaydedilemedi.");
        }
    }
}
