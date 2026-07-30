using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Orchestration;

/// <summary>
/// Ayar modeli (master plan Bölüm 16.1 — settings.json). Sürüm 3.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Geçerli şema sürümü. Yeni alan eklerken burayı ve <c>Migrate</c>'i güncelle.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>
    /// Ayar şeması sürümü. v1→v2: EnabledModules + canlı metrik alanları eklendi.
    /// v2→v3: FirstRunCompletedVersion eklendi.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// İlk açılış sihirbazının tamamlandığı uygulama sürümü (<c>null</c> = hiç gösterilmedi).
    /// Sürüm karşılaştırıldığı için sihirbaz büyük bir yükseltmeden sonra bir kez daha
    /// görünür — "ne değişti" bilgisinin doğal yeri orasıdır.
    /// </summary>
    public string? FirstRunCompletedVersion { get; set; }
    public string Language { get; set; } = "tr-TR";
    public string Theme { get; set; } = "dark";
    public string ActiveProfile { get; set; } = "balanced";
    public SafetyNetSettings SafetyNet { get; set; } = new();
    public RealtimeGuardSettings RealtimeGuard { get; set; } = new();
    public SchedulerSettings Scheduler { get; set; } = new();

    /// <summary>
    /// Tek tıkla optimizasyonun <b>güvenli varsayılan</b> modül listesi.
    /// </summary>
    /// <remarks>
    /// <para>Küratörlü bir liste; risk seviyesi filtresi <b>değil</b>. Modül metadata'sındaki
    /// <c>Risk</c> fazla kaba: CleanEngine <c>Low</c> ama geri dönüşüm kutusunu boşaltıyor,
    /// SecurityHardening <c>Low</c>, BackupRestore <c>Low</c> ama vssadmin çalıştırıyor,
    /// GpuOptimizer <c>Low</c> ama HAGS çeviriyor. "Low olanları çalıştır" kuralı bunların
    /// hepsini kapsardı.</para>
    /// <para>Dışarıda bırakma gerekçeleri: RepairEngine (SFC/DISM 20+ dk, "hızlı" bir
    /// işlemde şaşırtıcı) · NetworkOptimizer (winsock sıfırlama reboot ister, VPN bozar) ·
    /// SystemTweaker/PrivacyGuard/SecurityHardening (kullanıcının seçmesi gereken
    /// kayıt defteri/politika değişiklikleri) · BootOptimizer/CpuEngine/GpuOptimizer ·
    /// AppManager (uygulama kaldırır) · BackupRestore (vssadmin) · DevEnvironment
    /// (Hyper-V/Geliştirici Modu, reboot) · HardwareMonitor (salt okunur, anlamsız).</para>
    /// </remarks>
    public static readonly IReadOnlyList<string> DefaultOneClickModules =
        ["CleanEngine", "MemoryEngine", "StorageOptimizer", "UpdateEngine"];

    /// <summary>
    /// "Tek Tıkla" kapsamında etkin modül kimlikleri.
    /// </summary>
    /// <remarks>
    /// <b>Boş liste artık "tüm modüller" DEMEK DEĞİLDİR.</b> Eski davranışta tek tıkla,
    /// kayıtlı 16 modülün tamamını tek bir genel "Devam?" sorusuyla çalıştırıyordu —
    /// Hyper-V etkinleştirme ve geri dönüşüm boşaltma dahil. Varsayılan artık
    /// <see cref="DefaultOneClickModules"/>'dur; tümünü çalıştırmak için
    /// <see cref="JobOrchestrationEngine.ExecuteAllAsync"/> açıkça çağrılır.
    /// </remarks>
    public List<string> EnabledModules { get; set; } = [.. DefaultOneClickModules];

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
    /// <summary>
    /// Gerçek zamanlı izleme etkin mi. Kapalıyken servis çalışmaya devam eder ama metrik
    /// toplamaz — böylece arayüz "guard kapalı" ile "servis kurulu değil"i ayırt edebilir.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Otomatik müdahale ana anahtarı. <b>Varsayılan KAPALI</b> (CLAUDE.md §3.4 güvenli
    /// varsayılanlar): LocalSystem yetkisiyle çalışan bir servisin kullanıcıya sormadan
    /// dosya silmesi, açık onay gerektirir.
    /// </summary>
    public bool AutoRemediate { get; set; }

    /// <summary>RAM eşiği aşılınca boştaki süreçlerin working set'ini boşalt.</summary>
    public bool AutoTrimRam { get; set; }

    /// <summary>Disk kritik seviyeye inince geçici dosyaları temizle (geri alınamaz).</summary>
    public bool AutoCleanDiskCritical { get; set; }

    /// <summary>
    /// Defender imzalarını güncelle. Varsayılan olarak açık tutulabilen tek otomatik
    /// eylem: hiçbir şey silmez, geri alınacak bir değişiklik üretmez.
    /// </summary>
    public bool AutoUpdateDefenderSignatures { get; set; } = true;

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

    /// <summary>Eski şema sürümlerini geçerli sürüme yükseltir.</summary>
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

        if (s.SchemaVersion < 3)
        {
            // v2 kullanıcıları uygulamayı zaten kullanıyor: ilk açılış sihirbazı onlara
            // gösterilmez. null bırakılırsa mevcut kullanıcılara "hoş geldiniz" ekranı
            // açılır ki yanıltıcı olur.
            s.FirstRunCompletedVersion ??= "0.0.0";

            // v2'de boş liste "tüm modüller" anlamına geliyordu. Sessizce v3 semantiğine
            // geçirmek (boş = hiçbiri) tek tıkla'yı işlevsiz bırakır; bunun yerine güvenli
            // varsayılan YAZILIR — böylece kimse farkında olmadan "16 modül"de kalmaz.
            if (s.EnabledModules is null || s.EnabledModules.Count == 0)
            {
                s.EnabledModules = [.. AppSettings.DefaultOneClickModules];
            }

            s.SchemaVersion = 3;
        }
    }

    /// <summary>
    /// Ayarları diske yazar.
    /// </summary>
    /// <returns>
    /// Yazma başarılıysa <c>true</c>. <b>Çağıran bu değeri kontrol etmek zorundadır:</b>
    /// eskiden hata yutuluyor, arayüz ise her koşulda "kaydedildi" diyordu — kalıcılık
    /// konusunda yalan söyleyen bir ayar ekranı, az seçeneği olandan kötüdür.
    /// </returns>
    public bool Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOpts);
            File.WriteAllText(_filePath, json);
            _logger.LogInformation("Ayarlar kaydedildi: {Path}", _filePath);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ayarlar kaydedilemedi.");
            return false;
        }
    }
}
