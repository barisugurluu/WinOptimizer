using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Orchestration;

/// <summary>
/// Ayar modeli (master plan Bölüm 16.1 — settings.json).
/// </summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string Language { get; set; } = "tr-TR";
    public string Theme { get; set; } = "dark";
    public string ActiveProfile { get; set; } = "balanced";
    public SafetyNetSettings SafetyNet { get; set; } = new();
    public RealtimeGuardSettings RealtimeGuard { get; set; } = new();
    public SchedulerSettings Scheduler { get; set; } = new();
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
/// </summary>
public sealed class SettingsService
{
    private readonly string _filePath;
    private readonly ILogger<SettingsService> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettings Current { get; private set; } = new();

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
                _logger.LogDebug("Ayarlar yüklendi: {Path}", _filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ayarlar yüklenemedi, varsayılan kullanılıyor.");
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOpts);
            File.WriteAllText(_filePath, json);
            _logger.LogInformation("Ayarlar kaydedildi: {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ayarlar kaydedilemedi.");
        }
    }
}
