using Microsoft.Extensions.Logging;
using WinOptimizer.Core;

namespace WinOptimizer.Safety;

/// <summary>
/// SafetyNet — güvenlik ağı cephesi (Facade). Bir optimizasyon paketini
/// uygulamadan ÖNCE: (1) sistem geri yükleme noktası oluşturur,
/// (2) registry yedekleri için <see cref="RegistryBackup"/>,
/// (3) değişiklikleri <see cref="ChangeJournal"/> ile kaydeder.
/// (Master plan Bölüm 2.1 — SafetyNet: Restore Point + Change Journal.)
/// </summary>
public sealed class SafetyNet
{
    private readonly RestorePointService _restorePoint;
    private readonly ChangeJournal _journal;
    private readonly RegistryBackup _registryBackup;
    private readonly SafetyGuard _guard;
    private readonly ILogger<SafetyNet> _logger;

    public SafetyNet(
        RestorePointService restorePoint,
        ChangeJournal journal,
        RegistryBackup registryBackup,
        SafetyGuard guard,
        ILogger<SafetyNet> logger)
    {
        _restorePoint = restorePoint;
        _journal = journal;
        _registryBackup = registryBackup;
        _guard = guard;
        _logger = logger;
    }

    public ChangeJournal Journal => _journal;
    public RegistryBackup RegistryBackup => _registryBackup;
    public SafetyGuard Guard => _guard;
    public RestorePointService RestorePoint => _restorePoint;

    /// <summary>
    /// Bir bakım/tweak paketi öncesi güvenlik hazırlığı:
    /// sistem geri yükleme noktası oluşturur (mümkünse).
    /// </summary>
    /// <param name="description">Geri yükleme noktası açıklaması.</param>
    /// <param name="createRestorePoint">Otomatik geri yükleme noktası alınsın mı (ayara bağlı).</param>
    public Task PrepareAsync(string description, bool createRestorePoint = true)
    {
        if (createRestorePoint)
        {
            // Arayüzü tıkamaz — başarısız olursa yalnızca uyarı (koruma kapalı olabilir).
            bool ok = _restorePoint.Create(description);
            if (!ok)
            {
                _logger.LogWarning("Geri yükleme noktası alınamadı; işlem yine de journal ile geri alınabilir.");
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Bir değişikliği güvenli biçimde kaydeder (journal).</summary>
    public Task RecordAsync(ChangeRecord record, CancellationToken ct = default) =>
        _journal.WriteAsync(record, ct);

    /// <summary>Bir registry anahtarını yedekler (geri alma için).</summary>
    public Task<string?> BackupRegistryAsync(string hive, string subKey) =>
        _registryBackup.ExportAsync(hive, subKey);
}
