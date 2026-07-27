using Microsoft.Extensions.Logging;

namespace WinOptimizer.Modules.CleanEngine;

/// <summary>
/// Dosya silme yardımcıları — güvenli, geri dönüşüme taşıma öncelikli,
/// kilitli/erişilemez dosyaları sessizce atlar (master plan Bölüm 3.1 güvenlik kuralları).
/// Her atlama Debug düzeyinde günlüklenir (ayıklanabilirlik).
/// </summary>
internal sealed class DiskCleaner
{
    private readonly ILogger _logger;

    public DiskCleaner(ILogger logger) => _logger = logger;

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
                catch (UnauthorizedAccessException ex) { _logger.LogDebug(ex, "Erişim engelli dosya atlandı (analiz): {File}", fi.FullName); }
                catch (IOException ex) { _logger.LogDebug(ex, "Kilitli/okunamayan dosya atlandı (analiz): {File}", fi.FullName); }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Erişim engelli dizin atlandı (analiz): {Folder}", folder);
        }
        catch (DirectoryNotFoundException ex) { _logger.LogDebug(ex, "Dizin bulunamadı (analiz): {Folder}", folder); }

        return (count, bytes);
    }

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
                catch (UnauthorizedAccessException ex) { skipped++; _logger.LogDebug(ex, "Silinemedi (erişim engelli): {File}", fi.FullName); }
                catch (IOException ex) { skipped++; _logger.LogDebug(ex, "Silinemedi (kilitli): {File}", fi.FullName); }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Erişim engelli dizin atlandı (temizlik): {Folder}", folder);
        }
        catch (DirectoryNotFoundException ex) { _logger.LogDebug(ex, "Dizin bulunamadı (temizlik): {Folder}", folder); }

        return (deleted, skipped, bytes);
    }

    private bool DeleteFile(FileInfo fi, bool toRecycle)
    {
        try
        {
            if (toRecycle)
            {
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

    public static bool IsOlderThan(FileInfo fi, double hours) =>
        DateTime.UtcNow - fi.LastWriteTimeUtc > TimeSpan.FromHours(hours);
}
