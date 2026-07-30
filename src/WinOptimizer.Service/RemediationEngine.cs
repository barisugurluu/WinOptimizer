using System.Globalization;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Native;
using WinOptimizer.Safety;

namespace WinOptimizer.Service;

/// <summary>
/// RemediationEngine — eşik aşımlarında otomatik müdahale uygular (master plan Bölüm 3.17).
/// </summary>
/// <remarks>
/// <para><b>Her eylem açık izin ister.</b> Bu motor LocalSystem yetkisiyle çalışır; kullanıcıya
/// sormadan dosya silmesi kabul edilemez. Ana anahtar <c>AutoRemediate</c> ve eylem başına
/// izinler <b>varsayılan olarak kapalıdır</b> (yalnız Defender imza güncellemesi hariç —
/// hiçbir şey silmez). İzinler Guard sekmesinden yönetilir.</para>
/// <para><b>Her müdahale journal'a yazılır</b> (CLAUDE.md §3.2), böylece Geri Al çizelgesinde
/// ve teşhis paketinde görünür — daha önce hiçbir kayıt tutulmuyordu.</para>
/// <para>Kaldırılan davranış: <c>Shell32.EmptyRecycleBin()</c>. LocalSystem bağlamında
/// <c>SHEmptyRecycleBin(NULL,…)</c> <b>SYSTEM'in</b> geri dönüşüm kutusunu
/// (<c>S-1-5-18</c>) boşaltır, kullanıcınınkini değil: pratikte hiç yer kazandırmaz ama
/// geri alınamaz ve onaysızdır. Saf zarar olduğu için silindi.</para>
/// </remarks>
public sealed class RemediationEngine
{
    /// <summary>Silinebilecek dosyaların en düşük yaşı. 24 saat fazla agresifti.</summary>
    private static readonly TimeSpan MinFileAge = TimeSpan.FromDays(7);

    /// <summary>Tek çalıştırmada silinecek en fazla dosya (kaçak temizliği sınırlar).</summary>
    private const int MaxDeletionsPerRun = 500;

    private static readonly TimeSpan MinIdleTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    private readonly ProcessRunner _runner;
    private readonly GuardSettingsProvider _settings;
    private readonly ChangeJournal _journal;
    private readonly ILogger<RemediationEngine> _logger;
    private readonly ProcessMemory _processMemory = new();

    // Aynı eylemin sürekli tekrar etmesini önlemek için son uygulama zamanları
    private DateTimeOffset _lastRamTrim = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDiskClean = DateTimeOffset.MinValue;
    private DateTimeOffset _lastSigUpdate = DateTimeOffset.MinValue;

    public RemediationEngine(
        ProcessRunner runner,
        GuardSettingsProvider settings,
        ChangeJournal journal,
        ILogger<RemediationEngine> logger)
    {
        _runner = runner;
        _settings = settings;
        _journal = journal;
        _logger = logger;
    }

    /// <summary>
    /// Bir uyarı listesindeki otomatik-uygulanabilir eylemleri yürütür.
    /// </summary>
    /// <returns>Uygulanan eylem sayısı.</returns>
    public async Task<int> ApplyAsync(IReadOnlyList<GuardAlert> alerts, CancellationToken ct = default)
    {
        if (!_settings.AutoRemediate)
        {
            // Ana anahtar kapalı: hiçbir şey yapılmaz. (Daha önce böyle bir anahtar yoktu;
            // "RealtimeGuard'ı kapat" ayarı da okunmadığı için motor durdurulamıyordu.)
            return 0;
        }

        int applied = 0;
        foreach (var alert in alerts.Where(a => a.CanAutoRemediate))
        {
            try
            {
                bool done = alert.Metric switch
                {
                    "RAM" when _settings.AutoTrimRam && Expired(_lastRamTrim) =>
                        await TryTrimRamAsync(ct),
                    "Disk" when _settings.AutoCleanDiskCritical && Expired(_lastDiskClean) =>
                        await TryCleanDiskCriticalAsync(ct),
                    "Defender" when _settings.AutoUpdateDefenderSignatures && Expired(_lastSigUpdate) =>
                        await TryUpdateSignatureAsync(ct),
                    _ => false,
                };

                if (done)
                {
                    applied++;
                    _logger.LogInformation("[Otomatik müdahale] {Metric}: {Action}",
                        alert.Metric, alert.RecommendedAction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Otomatik müdahale başarısız: {Metric}", alert.Metric);
            }
        }

        return applied;
    }

    private static bool Expired(DateTimeOffset last) => DateTimeOffset.UtcNow - last > Cooldown;

    /// <summary>Boştaki süreçlerin working set'ini boşaltır.</summary>
    private async Task<bool> TryTrimRamAsync(CancellationToken ct)
    {
        int trimmed = _processMemory.TrimIdleProcesses(MinIdleTime);
        _lastRamTrim = DateTimeOffset.UtcNow;
        _logger.LogInformation("Otomatik RAM boşaltma: {N} süreç working set'i boşaltıldı.", trimmed);

        if (trimmed > 0)
        {
            await JournalAsync(ChangeOperationType.ProcessOptimize, "IdleProcesses",
                string.Format(CultureInfo.InvariantCulture, "{0} süreç", trimmed), ct);
        }

        return trimmed > 0;
    }

    /// <summary>
    /// Kritik disk durumunda geçici dosyaları temizler.
    /// </summary>
    /// <remarks>
    /// <c>Path.GetTempPath()</c> <b>kullanılmaz</b>: LocalSystem için bu <c>C:\Windows\Temp</c>
    /// demektir ve orada uçuş halindeki MSI/CBS/sürücü kurulum dosyaları bulunur. Bunun yerine
    /// belgelenmiş bir izin listesi, 7 günlük yaş sınırı ve silme adedi tavanı kullanılır.
    /// </remarks>
    private async Task<bool> TryCleanDiskCriticalAsync(CancellationToken ct)
    {
        int deleted = 0;
        long freedBytes = 0;

        foreach (string dir in GetCleanableDirectories())
        {
            if (!Directory.Exists(dir) || deleted >= MaxDeletionsPerRun)
            {
                continue;
            }

            foreach (var file in EnumerateSafely(dir))
            {
                ct.ThrowIfCancellationRequested();
                if (deleted >= MaxDeletionsPerRun)
                {
                    break;
                }

                try
                {
                    if (DateTime.UtcNow - file.LastWriteTimeUtc <= MinFileAge)
                    {
                        continue;
                    }

                    long size = file.Length;
                    file.Delete();
                    deleted++;
                    freedBytes += size;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Kilitli/erişilemez dosya atlanır — ama sessizce değil (RCS1075).
                    _logger.LogDebug(ex, "Geçici dosya silinemedi (atlandı): {File}", file.FullName);
                }
            }
        }

        _lastDiskClean = DateTimeOffset.UtcNow;
        _logger.LogInformation(
            "Otomatik kritik disk temizliği: {N} dosya, {Bytes} bayt boşaltıldı.", deleted, freedBytes);

        if (deleted > 0)
        {
            await JournalAsync(ChangeOperationType.FileDelete, "TempFiles",
                string.Format(CultureInfo.InvariantCulture, "{0} dosya / {1} bayt", deleted, freedBytes), ct);
        }

        return deleted > 0;
    }

    /// <summary>
    /// Temizlenebilir dizinlerin izin listesi. Sistem klasörleri ve kullanıcı verisi
    /// bilinçli olarak dışarıdadır.
    /// </summary>
    private static IEnumerable<string> GetCleanableDirectories()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Path.Combine(windows, "Temp");
        yield return Path.Combine(windows, "Prefetch");
    }

    private IEnumerable<FileInfo> EnumerateSafely(string dir)
    {
        // AllDirectories, erişilemeyen tek bir alt klasörde tüm listelemeyi patlatır;
        // üst düzey dosyalarla sınırlı kalmak hem güvenli hem yeterli.
        try
        {
            return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Dizin listelenemedi: {Dir}", dir);
            return [];
        }
    }

    /// <summary>Defender imza güncellemesi.</summary>
    private async Task<bool> TryUpdateSignatureAsync(CancellationToken ct)
    {
        int code = await _runner.RunAsync("powershell.exe",
            "-NoProfile -Command Update-MpSignature", null, ct);
        _lastSigUpdate = DateTimeOffset.UtcNow;
        return code == 0;
    }

    /// <summary>
    /// Müdahaleyi change journal'a yazar. Servis Safety'yi zaten referanslıyor;
    /// bu sayede otomatik işlemler Geri Al çizelgesinde ve teşhis paketinde görünür.
    /// </summary>
    private Task JournalAsync(
        ChangeOperationType operation, string target, string detail, CancellationToken ct) =>
        _journal.WriteAsync(new ChangeRecord
        {
            Module = "RealtimeGuard",
            Operation = operation,
            Target = target,
            NewValue = detail,
            Note = "otomatik müdahale (RealtimeGuard)",
        }, ct);
}
