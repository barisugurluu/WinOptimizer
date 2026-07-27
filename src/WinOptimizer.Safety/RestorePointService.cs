using System.Diagnostics;
using System.Management;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Safety;

/// <summary>
/// Sistem Geri Yükleme noktası oluşturma (WMI SystemRestore).
/// Tüm riskli işlemlerden ÖNCE çağrılır (master plan Bölüm 11.2, Faz 0).
/// Dayanıklılık: WMI çağrısı geçici hatalara karşı <see cref="Resilience"/> boru
/// hattı içinde yeniden denenir (§19 — RPC sunucusu meşgul vb.).
/// </summary>
public sealed class RestorePointService
{
    private readonly ILogger<RestorePointService> _logger;

    public RestorePointService(ILogger<RestorePointService> logger) => _logger = logger;

    /// <summary>
    /// Yeni bir sistem geri yükleme noktası oluşturur.
    /// </summary>
    /// <param name="description">Geri yükleme noktası açıklaması (ör. "WinOptimizer öncesi").</param>
    /// <returns>Başarılıysa true; sistem koruması kapalıysa veya hata oluşursa false.</returns>
    public bool Create(string description)
    {
        try
        {
            Resilience.ExecuteAsync(_ =>
            {
                InvokeCreateRestorePoint(description);
                return Task.FromResult(true);
            }, CancellationToken.None).GetAwaiter().GetResult();

            _logger.LogInformation("Sistem geri yükleme noktası oluşturuldu: {Desc}", description);
            return true;
        }
        catch (ManagementException ex)
        {
            // Genellikle sistem koruması kapalıdır veya 24 saat içinde sınır aşımı.
            _logger.LogWarning(ex, "Geri yükleme noktası oluşturulamadı (koruma kapalı olabilir): {Desc}", description);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geri yükleme noktası beklenmeyen hata: {Desc}", description);
            return false;
        }
    }

    private void InvokeCreateRestorePoint(string description)
    {
        using var mc = new ManagementClass(@"\\.\root\default", "SystemRestore", null);
        var inParams = mc.GetMethodParameters("CreateRestorePoint");
        inParams["Description"] = description;
        inParams["RestorePointType"] = 12;   // MODIFY_SETTINGS
        inParams["EventType"] = 100;         // BEGIN_SYSTEM_CHANGE
        mc.InvokeMethod("CreateRestorePoint", inParams, null);
    }

    /// <summary>Sistem korumasının etkin olup olmadığını kontrol eder.</summary>
    public bool IsEnabled()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\default", "SELECT * FROM SystemRestore");
            return searcher.Get().Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Sistem geri yükleme koruması sorgulanamadı.");
            return false;
        }
    }
}
