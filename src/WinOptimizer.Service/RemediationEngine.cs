using Microsoft.Extensions.Logging;
using WinOptimizer.Native;
using WinOptimizer.Safety;

namespace WinOptimizer.Service;

/// <summary>
/// RemediationEngine — eşik aşımlarında OTOMATIK müdahale uygular.
/// Yalnızca güvenli, düşük riskli eylemler uygulanır (master plan Bölüm 3.17):
/// - RAM > %85 (sürekli) → EmptyWorkingSet boşta süreçlere
/// - Disk &lt; %5 (kritik) → otomatik TEMP + Geri Dönüşüm temizliği
/// - Defender imzası > 7 gün → Update-MpSignature
/// Riskli eylemler yalnızca bildirim (toast) üretir — otomatik yapılmaz.
/// </summary>
public sealed class RemediationEngine
{
    private readonly ProcessRunner _runner;
    private readonly ILogger<RemediationEngine> _logger;
    private readonly ProcessMemory _processMemory = new();
    private static readonly TimeSpan MinIdleTime = TimeSpan.FromMinutes(5);

    // Aynı eylemin sürekli tekrar etmesini önlemek için son uygulama zamanları
    private DateTimeOffset _lastRamTrim = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDiskClean = DateTimeOffset.MinValue;
    private DateTimeOffset _lastSigUpdate = DateTimeOffset.MinValue;
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    public RemediationEngine(ProcessRunner runner, ILogger<RemediationEngine> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    /// <summary>
    /// Bir uyarı listesindeki otomatik-uygulanabilir eylemleri yürütür.
    /// Döndürür: uygulanan eylem sayısı.
    /// </summary>
    public async Task<int> ApplyAsync(IReadOnlyList<GuardAlert> alerts)
    {
        int applied = 0;
        foreach (var alert in alerts.Where(a => a.CanAutoRemediate))
        {
            try
            {
                bool done = alert.Metric switch
                {
                    "RAM" when DateTime.UtcNow - _lastRamTrim > Cooldown => await TryTrimRamAsync(),
                    "Disk" when DateTime.UtcNow - _lastDiskClean > Cooldown => await TryCleanDiskCriticalAsync(),
                    "Defender" when DateTime.UtcNow - _lastSigUpdate > Cooldown => await TryUpdateSignatureAsync(),
                    _ => false
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

    /// <summary>Boştaki süreçlerin working set'ini boşaltır (RAM > %85).</summary>
    private Task<bool> TryTrimRamAsync()
    {
        int trimmed = _processMemory.TrimIdleProcesses(MinIdleTime);
        _lastRamTrim = DateTimeOffset.UtcNow;
        _logger.LogInformation("Otomatik RAM boşaltma: {N} süreç working set'i boşaltıldı.", trimmed);
        return Task.FromResult(trimmed > 0);
    }
    private Task<bool> TryCleanDiskCriticalAsync()
    {
        try
        {
            // TEMP temizliği (24 saatten eski dosyalar)
            var temp = Path.GetTempPath();
            int deleted = 0;
            if (Directory.Exists(temp))
            {
                foreach (var fi in new DirectoryInfo(temp).EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (DateTime.UtcNow - fi.LastWriteTimeUtc > TimeSpan.FromHours(24))
                        {
                            fi.Delete(); deleted++;
                        }
                    }
                    catch { /* kilitli atlanır */ }
                }
            }

            // Geri Dönüşüm kutusu boşaltma
            Shell32.EmptyRecycleBin();

            _lastDiskClean = DateTimeOffset.UtcNow;
            _logger.LogInformation("Otomatik kritik disk temizliği: {N} TEMP dosyası + Geri Dönüşüm.", deleted);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kritik disk temizliği başarısız.");
            return Task.FromResult(false);
        }
    }

    /// <summary>Defender imza güncellemesi (imza > 7 gün).</summary>
    private async Task<bool> TryUpdateSignatureAsync()
    {
        int code = await _runner.RunAsync("powershell.exe",
            "-NoProfile -Command Update-MpSignature", null);
        _lastSigUpdate = DateTimeOffset.UtcNow;
        return code == 0;
    }
}
