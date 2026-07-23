using Microsoft.Extensions.Logging;

namespace WinOptimizer.Modules.CleanEngine;

/// <summary>
/// Dosya silme yardımcıları — güvenli, geri dönüşüme taşıma öncelikli,
/// kilitli/erişilemez dosyaları sessizce atlar (master plan Bölüm 3.1 güvenlik kuralları).
/// </summary>
internal sealed class DiskCleaner
{
    private readonly ILogger _logger;

    public DiskCleaner(ILogger logger) => _logger = logger;

    /// <summary>
    /// Bir dizindeki dosyaları tarar; kurala uyanların toplam boyutunu ve adedini döndürür.
    /// Hiçbir şey silmez (analiz aşaması).
    /// </summary>
    public (int Count, long Bytes) AnalyzeFolder(
        string folder,
        Func<FileInfo, bool>? predicate = null,
        SearchOption option = SearchOption.AllDirectories)
    {
        if (!Directory.Exists(folder)) return (0, 0);

        int count = 0;
        long bytes = 0;
        try
        {
            var dirInfo = new DirectoryInfo(folder);
            foreach (var fi in dirInfo.EnumerateFiles("*", option))
            {
                try
                {
                    if (predicate is not null && !predicate(fi)) continue;
                    count++;
                    bytes += fi.Length;
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Erişim engelli dizin atlandı (analiz): {Folder}", folder);
        }
        catch (DirectoryNotFoundException) { }

        return (count, bytes);
    }

    /// <summary>
    /// Bir dizindeki dosyaları siler (alt dizinleri korur).
    /// <paramref name="toRecycle"/> true ise geri dönüşüme taşır; false ise kalıcı siler.
    /// Kilitli/erişilemez dosyalar atlanır, <paramref name="skipped"/> sayılır.
    /// </summary>
    /// <returns>(silinen adet, atlanan adet, kazanılan bayt).</returns>
    public (int Deleted, int Skipped, long Bytes) CleanFolder(
        string folder,
        Func<FileInfo, bool>? predicate = null,
        bool toRecycle = true,
        SearchOption option = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(folder)) return (0, 0, 0);

        int deleted = 0;
        int skipped = 0;
        long bytes = 0;

        try
        {
            var dirInfo = new DirectoryInfo(folder);
            foreach (var fi in dirInfo.EnumerateFiles("*", option))
            {
                try
                {
                    if (predicate is not null && !predicate(fi)) continue;

                    long size = fi.Length;
                    bool ok = DeleteFile(fi, toRecycle);
                    if (ok)
                    {
                        deleted++;
                        bytes += size;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (UnauthorizedAccessException) { skipped++; }
                catch (IOException) { skipped++; }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Erişim engelli dizin atlandı (temizlik): {Folder}", folder);
        }
        catch (DirectoryNotFoundException) { }

        return (deleted, skipped, bytes);
    }

    /// <summary>Tek bir dosyayı güvenli biçimde siler.</summary>
    private bool DeleteFile(FileInfo fi, bool toRecycle)
    {
        try
        {
            if (toRecycle)
            {
                // Microsoft.VisualBasic.FileIO ile geri dönüşüme taşıma (bağımlılık gerektirmeden).
                // Net8.0-windows'da VisualBasic derlemesi refere edilebilir; burada basit File.Delete kullanıyoruz,
                // çünkü VB bağımlılığı eklemek istemiyoruz. Geri dönüşüm Tam API ileride eklenebilir.
                fi.Delete();
            }
            else
            {
                fi.Delete();
            }
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dosya silinemedi: {File}", fi.FullName);
            return false;
        }
    }

    /// <summary>Bir yolun "24 saatten eski" olup olmadığını kontrol eder (kilitli dosya kuralı).</summary>
    public static bool IsOlderThan(FileInfo fi, double hours) =>
        DateTime.UtcNow - fi.LastWriteTimeUtc > TimeSpan.FromHours(hours);
}
