namespace WinOptimizer.Updater;

/// <summary>Güncelleme kontrolünün sonucu (master plan Bölüm 20.6).</summary>
/// <param name="IsUpdateAvailable">Yeni sürüm mevcut mu?</param>
/// <param name="Latest">Bulunan en son sürüm manifesti (yoksa null).</param>
/// <param name="CurrentVersion">Şu anki yüklü sürüm.</param>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    UpdateManifest? Latest,
    Version CurrentVersion);
