using System.Globalization;

namespace WinOptimizer.Updater;

/// <summary>
/// Bir GitHub Release'den çıkarılan güncelleme bildirimi (master plan Bölüm 20.6).
/// SHA-256 ve indirme URL'si paketin bütünlüğünü garanti eder; ön sürüm bayrağı kanal süzgecinde kullanılır.
/// </summary>
/// <param name="Version">Yayın sürümü (tag'den, baştaki 'v' ve ön sürüm eki sıyrılır).</param>
/// <param name="DownloadUrl">x64 MSI paketinin indirme URL'si.</param>
/// <param name="Sha256">Beklenen SHA-256 (64 hex); bilinmiyorsa boş (best-effort).</param>
/// <param name="ReleaseNotes">Sürüm notları (Markdown).</param>
/// <param name="PublishedAt">Yayın tarihi (UTC).</param>
/// <param name="IsPrerelease">Ön sürüm mü?</param>
public sealed record UpdateManifest(
    Version Version,
    string DownloadUrl,
    string Sha256,
    string ReleaseNotes,
    DateTimeOffset PublishedAt,
    bool IsPrerelease)
{
    /// <summary>Güncelleme için insan-okur özet.</summary>
    public string ToSummary() =>
        string.Format(CultureInfo.InvariantCulture,
            "v{0} ({1:yyyy-MM-dd}){2}", Version, PublishedAt, IsPrerelease ? " [ön sürüm]" : string.Empty);
}
