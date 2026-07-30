using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Orchestration;

/// <summary>
/// Bir geri alma denemesinin sonucu — hangi kayıt, hangi modül, başarılı mı.
/// </summary>
/// <param name="ChangeId">Geri alınmaya çalışılan değişiklik kaydının kimliği.</param>
/// <param name="ModuleId">Kaydı üreten modül.</param>
/// <param name="IsSuccess">Geri alma başarılı oldu mu.</param>
/// <param name="Message">Kullanıcıya gösterilecek açıklama (başarısızlık nedeni dahil).</param>
public sealed record RollbackOutcome(string ChangeId, string ModuleId, bool IsSuccess, string Message);

/// <summary>
/// RollbackService — change journal kayıtlarını, kaydı üreten modülün
/// <see cref="IOptimizationModule.RollbackAsync"/> uygulamasına yönlendirir.
///
/// <para>Bu katman olmadan geri alma erişilemezdi: modüller geri almayı uyguluyor ve journal
/// değişiklikleri kaydediyordu, ancak ikisini birbirine bağlayan bir yol yoktu
/// (master plan hedef G3 — "tüm tweak'ler tek tıkla geri alınabilir").</para>
///
/// <para>Her geri alma denemesi, denetim izi için <c>Information</c> düzeyinde yapılandırılmış
/// olay olarak günlüklenir; başarılı geri almalar ayrıca journal'a
/// <see cref="ChangeOperationType.Other"/> kaydı olarak yazılır — böylece "neyin geri alındığı"
/// da geçmişte görünür.</para>
/// </summary>
public sealed class RollbackService
{
    private readonly ModuleRegistry _registry;
    private readonly ChangeJournal _journal;
    private readonly IntegrityGuard _integrity;
    private readonly ILogger<RollbackService> _logger;

    public RollbackService(
        ModuleRegistry registry,
        ChangeJournal journal,
        IntegrityGuard integrity,
        ILogger<RollbackService> logger)
    {
        _registry = registry;
        _journal = journal;
        _integrity = integrity;
        _logger = logger;
    }

    /// <summary>
    /// Bir günün journal dosyasının HMAC imzası geçerli mi?
    /// </summary>
    /// <remarks>
    /// CLAUDE.md §3.2 ve README "kurcalama geri almayı durdurur" diyordu ama
    /// <c>IntegrityGuard.VerifyFileAsync</c> yalnızca testlerden çağrılıyordu — yani
    /// bu güvence gerçekte yoktu. Artık geri alma öncesi doğrulanır.
    /// </remarks>
    public async Task<bool> IsDayVerifiedAsync(DateTime day, CancellationToken ct = default)
    {
        string path = _journal.GetFilePath(day);
        if (!File.Exists(path))
        {
            return true;   // dosya yoksa doğrulanacak bir şey de yok
        }

        bool ok = await _integrity.VerifyFileAsync(path, ct).ConfigureAwait(false);
        if (!ok)
        {
            _logger.LogError(
                "Değişiklik günlüğü bütünlük doğrulaması BAŞARISIZ: {Path}. " +
                "Bu güne ait kayıtlar geri alınmayacak.", path);
        }

        return ok;
    }

    /// <summary>Tek bir değişiklik kaydını geri alır.</summary>
    public async Task<RollbackOutcome> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        // BÜTÜNLÜK KAPISI: kaydın geldiği gün dosyası kurcalanmışsa geri alma yapılmaz.
        // Kurcalanmış bir journal, "geri alma" adı altında saldırganın seçtiği işlemleri
        // yönetici yetkisiyle çalıştırmak için kullanılabilirdi.
        if (!await IsDayVerifiedAsync(change.Timestamp.UtcDateTime, ct).ConfigureAwait(false))
        {
            return new RollbackOutcome(change.Id, change.Module, false,
                "Değişiklik günlüğü doğrulanamadı (dosya değiştirilmiş olabilir); geri alma reddedildi.");
        }

        var module = _registry.Find(change.Module);
        if (module is null)
        {
            _logger.LogWarning("Geri alma atlandı — modül kayıtlı değil: {Module} (kayıt {ChangeId})",
                change.Module, change.Id);
            return new RollbackOutcome(change.Id, change.Module, false,
                $"'{change.Module}' modülü kayıtlı değil; geri alınamadı.");
        }

        _logger.LogInformation("Geri alma başlıyor: {Module}/{Operation} → {Target} (kayıt {ChangeId})",
            change.Module, change.Operation, change.Target, change.Id);

        try
        {
            var result = await module.RollbackAsync(change, ct);
            string message = result.Error ?? (result.IsSuccess ? "Geri alındı." : "Geri alınamadı.");

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Geri alma başarılı: {Module}/{Operation} → {Target} ({Detail}) (kayıt {ChangeId})",
                    change.Module, change.Operation, change.Target, message, change.Id);
                await RecordRollbackAsync(change, message, ct);
            }
            else
            {
                _logger.LogWarning("Geri alma başarısız: {Module} → {Target} — {Detail} (kayıt {ChangeId})",
                    change.Module, change.Target, message, change.Id);
            }

            return new RollbackOutcome(change.Id, change.Module, result.IsSuccess, message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geri alma hata verdi: {Module} → {Target} (kayıt {ChangeId})",
                change.Module, change.Target, change.Id);
            return new RollbackOutcome(change.Id, change.Module, false, ex.Message);
        }
    }

    /// <summary>Kimliğiyle bir kaydı bulup geri alır.</summary>
    public async Task<RollbackOutcome> RollbackByIdAsync(string changeId, CancellationToken ct = default)
    {
        var change = await _journal.FindAsync(changeId, ct);
        if (change is null)
        {
            _logger.LogWarning("Geri alınacak kayıt bulunamadı: {ChangeId}", changeId);
            return new RollbackOutcome(changeId, string.Empty, false, "Kayıt bulunamadı.");
        }
        return await RollbackAsync(change, ct);
    }

    /// <summary>
    /// Son <paramref name="count"/> değişikliği (en yeniden eskiye) geri alır.
    /// Geri alma sırası kasıtlı olarak terstir: sonradan yapılan değişiklik önce geri alınır.
    /// </summary>
    public async Task<IReadOnlyList<RollbackOutcome>> RollbackLastAsync(
        int count = 1, int searchDays = 7, CancellationToken ct = default)
    {
        var recent = await ReadRecentAsync(searchDays, ct);
        var targets = recent.Take(count).ToList();

        if (targets.Count == 0)
        {
            _logger.LogInformation("Geri alınacak değişiklik yok (son {Days} gün).", searchDays);
            return Array.Empty<RollbackOutcome>();
        }

        var outcomes = new List<RollbackOutcome>(targets.Count);
        foreach (var change in targets)
        {
            ct.ThrowIfCancellationRequested();
            outcomes.Add(await RollbackAsync(change, ct));
        }
        return outcomes;
    }

    /// <summary>
    /// Son <paramref name="days"/> günün değişiklik kayıtlarını en yeniden eskiye döndürür.
    /// Geri alma ekranı da bu sıralamayı kullanır.
    /// </summary>
    public async Task<IReadOnlyList<ChangeRecord>> ReadRecentAsync(int days = 7, CancellationToken ct = default)
    {
        var all = new List<ChangeRecord>();
        for (int i = 0; i < days; i++)
        {
            ct.ThrowIfCancellationRequested();
            all.AddRange(await _journal.ReadDayAsync(DateTime.UtcNow.AddDays(-i), ct));
        }
        return all.OrderByDescending(r => r.Timestamp).ToList();
    }

    /// <summary>Geri almanın kendisini de journal'a işler (geçmişte görünür olsun diye).</summary>
    private Task RecordRollbackAsync(ChangeRecord original, string message, CancellationToken ct) =>
        _journal.WriteAsync(new ChangeRecord
        {
            Module = original.Module,
            Operation = ChangeOperationType.Other,
            Target = original.Target,
            PreviousValue = original.NewValue,
            NewValue = original.PreviousValue,
            Note = $"Geri alındı (kayıt {original.Id}): {message}"
        }, ct);
}
