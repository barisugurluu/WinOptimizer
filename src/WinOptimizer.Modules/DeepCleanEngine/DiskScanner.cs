using Microsoft.Extensions.Logging;

namespace WinOptimizer.Modules.DeepCleanEngine;

/// <summary>
/// Disk tarama yardımcıları — klasör boyutu/dosya sayısı, büyük/yinelenen dosya tespiti.
/// </summary>
internal sealed class DiskScanner
{
    /// <summary>Bir klasörün toplam boyutunu ve dosya sayısını döndürür.</summary>
    public (int Count, long Bytes) ScanFolder(string folder)
    {
        if (!Directory.Exists(folder)) return (0, 0);
        int count = 0; long bytes = 0;
        try
        {
            foreach (var fi in new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { count++; bytes += fi.Length; }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
        catch (UnauthorizedAccessException) { }
        return (count, bytes);
    }

    public long GetFolderSize(string folder) => ScanFolder(folder).Bytes;

    /// <summary>Bir kök dizinde büyük dosyaları listeler (>eşik). Silmez.</summary>
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
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (UnauthorizedAccessException) { }
        return results;
    }

    /// <summary>
    /// Yinelenen dosyaları bulur (hash-tabanlı — Bölüm 3.2).
    /// Önce boyuta göre gruplar, sonra aynı boyuttakileri içerik hash'i ile karşılaştırır.
    /// Silmez — yalnızca raporlar. Döndürür: her grup bir yinelenen kümesi (≥2 dosya).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<FileInfo>> FindDuplicateFiles(
        string root, long minSizeBytes = 1024, CancellationToken ct = default)
    {
        var duplicates = new List<IReadOnlyList<FileInfo>>();
        if (!Directory.Exists(root)) return duplicates;

        // 1) Boyuta göre grupla
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
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (UnauthorizedAccessException) { }

        // 2) Aynı boyuttakileri hash'le, hash'e göre grupla
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
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
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

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F0} KB",
        _ => $"{bytes} B"
    };
}
