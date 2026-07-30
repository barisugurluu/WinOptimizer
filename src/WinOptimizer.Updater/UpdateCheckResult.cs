namespace WinOptimizer.Updater;

/// <summary>Güncelleme kontrolünün sonucu (master plan Bölüm 20.6).</summary>
/// <remarks>
/// <b>"Güncelleme yok" ile "denetleyemedim" ayrı durumlardır.</b> Eskiden ağ hatası,
/// 404 ve gerçekten güncel olma durumu aynı sonuca (<c>IsUpdateAvailable=false</c>)
/// düşüyordu; CLI de bunu "Güncel: v0.1.0" olarak yazdırıyordu. Kullanıcı, güncelleme
/// denetimi tamamen bozukken kendisini güncel sanıyordu. <see cref="CheckFailed"/>
/// bu iki durumu ayırır; çağıran <see cref="FailureReason"/>'ı göstermek zorundadır.
/// </remarks>
/// <param name="IsUpdateAvailable">Yeni sürüm mevcut mu?</param>
/// <param name="Latest">Bulunan en son sürüm manifesti (yoksa null).</param>
/// <param name="CurrentVersion">Şu anki yüklü sürüm.</param>
/// <param name="CheckFailed">Denetim yapılamadı (ağ/API/asset hatası) — güncel olduğu anlamına GELMEZ.</param>
/// <param name="FailureReason">Denetim neden yapılamadı (kullanıcıya gösterilecek kısa metin).</param>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    UpdateManifest? Latest,
    Version CurrentVersion,
    bool CheckFailed = false,
    string? FailureReason = null);
