using Microsoft.Extensions.Logging;
using WinOptimizer.Orchestration;

namespace WinOptimizer.Service;

/// <summary>
/// Servisin <c>settings.json</c>'ı okumasını sağlar: guard açık/kapalı, eşikler ve
/// otomatik müdahale izinleri.
/// </summary>
/// <remarks>
/// <para><b>Neden anket (poll), FileSystemWatcher değil?</b> <see cref="RealtimeGuardWorker"/>
/// zaten 5 saniyede bir tikliyor. ~1 KB'lık bir dosyanın <c>LastWriteTimeUtc</c>'sini tik
/// başına okumak bedavadır, yeni iş parçacığı ve yeni hata modu getirmez.
/// <c>FileSystemWatcher</c> ise tek bir yazma için 2-4 olay üretir, iç arabellek taşmasında
/// olayları sessizce düşürür, kendi debounce + <c>Error</c> işleyicisi + dispose'unu ister ve
/// guard durumunu ikinci bir iş parçacığından değiştirirdi. Eşikler için 5 sn gecikme önemsiz.</para>
/// <para><b>Neden Orchestration'daki AppSettings yeniden kullanılıyor?</b> Servise ayrı bir DTO
/// yazmak, arayüzün yazdığı ile servisin okuduğu şemanın zamanla ayrışması demekti —
/// bu proje tam olarak o tür sürüklenmeden zarar gördü. Şema tek yerde tanımlıdır.</para>
/// </remarks>
public sealed class GuardSettingsProvider
{
    private readonly string _settingsPath;
    private readonly ILogger<GuardSettingsProvider> _logger;
    private DateTime _lastWriteUtc = DateTime.MinValue;
    private bool _loadedOnce;

    public GuardSettingsProvider(ILogger<GuardSettingsProvider> logger)
    {
        _logger = logger;
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinOptimizer");
        _settingsPath = Path.Combine(baseDir, "settings.json");
        BaseDirectory = baseDir;
    }

    /// <summary>Veri dizini (journal ve günlükler için).</summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// Gerçek zamanlı koruma etkin mi. <b>Kapalıyken servis çalışmaya devam eder</b> ama
    /// metrik toplamaz/müdahale etmez — böylece arayüz "guard kapalı" ile "servis yok"
    /// durumlarını ayırt edebilir.
    /// </summary>
    public bool Enabled { get; private set; } = true;

    /// <summary>Otomatik müdahale ana anahtarı. <b>Varsayılan kapalı.</b></summary>
    public bool AutoRemediate { get; private set; }

    /// <summary>RAM eşiği aşıldığında boştaki süreçlerin belleğini boşalt.</summary>
    public bool AutoTrimRam { get; private set; }

    /// <summary>Disk kritik seviyeye düştüğünde geçici dosyaları temizle.</summary>
    public bool AutoCleanDiskCritical { get; private set; }

    /// <summary>Defender imzaları eskidiğinde güncelle (güvenli — varsayılan açık).</summary>
    public bool AutoUpdateDefenderSignatures { get; private set; } = true;

    /// <summary>Etkin eşikler.</summary>
    public GuardThresholds Thresholds { get; private set; } = new();

    /// <summary>
    /// Ayar dosyası değiştiyse yeniden okur.
    /// </summary>
    /// <returns>Değerler güncellendiyse <c>true</c>.</returns>
    public bool RefreshIfChanged()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                // Ayar dosyası henüz yok (uygulama hiç açılmamış): varsayılanlarla devam.
                return !_loadedOnce && MarkLoaded();
            }

            DateTime writeUtc = File.GetLastWriteTimeUtc(_settingsPath);
            if (_loadedOnce && writeUtc == _lastWriteUtc)
            {
                return false;
            }

            _lastWriteUtc = writeUtc;

            // SettingsService dosyayı okur, şema göçünü yapar ve varsayılanları doldurur;
            // servis kendi JSON ayrıştırmasını yapmaz.
            var settings = new SettingsService(BaseDirectory, new SettingsLoggerAdapter(_logger)).Current;

            bool wasEnabled = Enabled;
            Enabled = settings.RealtimeGuard.Enabled;
            AutoRemediate = settings.RealtimeGuard.AutoRemediate;
            AutoTrimRam = settings.RealtimeGuard.AutoTrimRam;
            AutoCleanDiskCritical = settings.RealtimeGuard.AutoCleanDiskCritical;
            AutoUpdateDefenderSignatures = settings.RealtimeGuard.AutoUpdateDefenderSignatures;

            var t = settings.RealtimeGuard.Thresholds;
            Thresholds = new GuardThresholds
            {
                RamUsagePercent = t.RamUsagePercent,
                DiskFreePercent = t.DiskFreePercent,
                DiskFreeCriticalPercent = t.DiskFreeCriticalPercent,
                CpuPerProcessPercent = t.CpuPerProcessPercent,
                TempCelsius = t.TempCelsius,
            };

            _logger.LogInformation(
                "Ayarlar okundu: guard={Enabled}, otomatik müdahale={Auto} " +
                "(ram={Ram}, disk={Disk}, defender={Def}), RAM eşiği=%{RamPct}, disk kritik=%{DiskPct}",
                Enabled, AutoRemediate, AutoTrimRam, AutoCleanDiskCritical,
                AutoUpdateDefenderSignatures, Thresholds.RamUsagePercent,
                Thresholds.DiskFreeCriticalPercent);

            if (_loadedOnce && wasEnabled != Enabled)
            {
                // Sabit şablon (CA2254): değişken kısım parametreye taşınır.
                _logger.LogInformation(
                    "Gerçek zamanlı koruma durumu değişti: {State} (servis çalışmaya devam ediyor).",
                    Enabled ? "AÇIK" : "KAPALI");
            }

            return MarkLoaded();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ayar okunamazsa mevcut değerlerle devam edilir; servis durmaz.
            _logger.LogWarning(ex, "Ayarlar okunamadı, önceki değerlerle devam ediliyor.");
            return false;
        }
    }

    private bool MarkLoaded()
    {
        _loadedOnce = true;
        return true;
    }

    /// <summary>
    /// <see cref="SettingsService"/> kendi günlükleyici türünü ister; servis tarafında
    /// tek bir kaydediciyi yeniden kullanmak için ince bir köprü.
    /// </summary>
    private sealed class SettingsLoggerAdapter : ILogger<SettingsService>
    {
        private readonly ILogger _inner;

        public SettingsLoggerAdapter(ILogger inner) => _inner = inner;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            _inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
