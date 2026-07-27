using Microsoft.Extensions.Logging;
using WinOptimizer.Core;

namespace WinOptimizer.Modules.DeepCleanEngine;

/// <summary>
/// Disk tarama yardımcıları — klasör boyutu/dosya sayısı, büyük/yinelenen dosya tespiti.
/// Erişilemez/kilitli öğeler sessizce atlanır; her atlama Debug düzeyinde günlüklenir.
/// </summary>
internal sealed class DiskScanner
{
    private readonly ILogger<DiskScanner>? _logger;

    public DiskScanner(ILogger<DiskScanner>? logger = null) => _logger = logger;

    public (int Count, long Bytes) ScanFolder(string folder)
    {
        if (!Directory.Exists(folder)) return (0, 0);
        int count = 0; long bytes = 0;
        try
        {
            foreach (var fi in new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { count++; bytes += fi.Length; }
                catch (UnauthorizedAccessException ex) { _logger?.LogDebug(ex, "Erişim engelli dosya atlandı: {File}", fi.FullName); }
                catch (IOException ex) { _logger?.LogDebug(ex, "Kilitli/okunamayan dosya atlandı: {File}", fi.FullName); }
            }
        }
        catch (UnauthorizedAccessException ex) { _logger?.LogDebug(ex, "Erişim engelli kök dizin atlandı: {Folder}", folder); }
        return (count, bytes);
    }

    public long GetFolderSize(string folder) => ScanFolder(folder).Bytes;

    public IReadOnlyList<FileInfo> FindLargeFiles(string root, long thresholdBytes, CancellationToken ct = default)
    {
        var results = new List<FileInfo>();
        if (!Directory.Exists(root)) return results;
        try
        {
            foreach (var fi in new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try { if (fi.Length >= thresholdBytes) results.Add(fi); }
                catch (UnauthorizedAccessException ex) { _logger?.LogDebug(ex, "Büyük dosya taramasında erişim engelli atlandı: {File}", fi.FullName); }
            }
        }
        catch (UnauthorizedAccessException ex) { _logger?.LogDebug(ex, "Erişim engelli kök dizin atlandı: {Folder}", root); }
        return results;
    }

    public IReadOnlyList<IReadOnlyList<FileInfo>> FindDuplicateFiles(
        string root, long minSizeBytes = 1024, CancellationToken ct = default)
    {
        var duplicates = new List<IReadOnlyList<FileInfo>>();
        if (!Directory.Exists(root)) return duplicates;

        var bySize = new Dictionary<long, List<FileInfo>>();
        try
        {
            foreach (var fi in new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (fi.Length < minSizeBytes) continue;
                    if (!bySize.TryGetValue(fi.Length, out var list)) { list = new(); bySize[fi.Length] = list; }
                    list.Add(fi);
                }
                catch (UnauthorizedAccessException ex) { _logger?.LogDebug(ex, "Yinelenen taramasında erişim engelli atlandı: {File}", fi.FullName); }
            }
        }
        catch (UnauthorizedAccessException ex) { _logger?.LogDebug(ex, "Erişim engelli kök dizin atlandı: {Folder}", root); }

        foreach (var group in bySize.Values.Where(g => g.Count > 1))
        {
            ct.ThrowIfCancellationRequested();
            var byHash = new Dictionary<string, List<FileInfo>>(StringComparer.Ordinal);
            foreach (var fi in group)
            {
                try
                {
                    string hash = ComputeHash(fi);
                    if (!byHash.TryGetValue(hash, out var list)) { list = new(); byHash[hash] = list; }
                    list.Add(fi);
                }
                catch (IOException ex) { _logger?.LogDebug(ex, "Hash hesaplanamadı (kilitli): {File}", fi.FullName); }
                catch (UnauthorizedAccessException ex) { _logger?.LogDebug(ex, "Hash hesaplanamadı (erişim engelli): {File}", fi.FullName); }
            }
            foreach (var dupGroup in byHash.Values.Where(g => g.Count > 1))
                duplicates.Add(dupGroup);
        }
        return duplicates;
    }

    private static string ComputeHash(FileInfo fi)
    {
        using var stream = fi.OpenRead();
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    public static string FormatBytes(long bytes) => FileSizeFormatter.Format(bytes);
}
