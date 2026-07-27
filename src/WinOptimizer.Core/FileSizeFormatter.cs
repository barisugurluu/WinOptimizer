using System.Globalization;

namespace WinOptimizer.Core;

/// <summary>
/// Bayt boyutlarını insan-okur metne dönüştürür (tekil kaynak — DRY).
/// Kültürden bağımsız (InvariantCulture) biçimlendirir: ondalık ayraç her zaman
/// noktadır, böylece TR yerelinde bile çıktı kararlıdır (CA1305 kapatılmış hali).
/// </summary>
public static class FileSizeFormatter
{
    /// <summary>Bayt sayısını KB/MB/GB/TB olarak okunaklı metne çevirir.</summary>
    public static string Format(long bytes)
    {
        const long kb = 1L << 10;
        const long mb = 1L << 20;
        const long gb = 1L << 30;
        const long tb = 1L << 40;

        return bytes switch
        {
            >= tb => FormatValue(bytes, tb, "F2", "TB"),
            >= gb => FormatValue(bytes, gb, "F2", "GB"),
            >= mb => FormatValue(bytes, mb, "F1", "MB"),
            >= kb => FormatValue(bytes, kb, "F0", "KB"),
            _ => bytes.ToString("D", CultureInfo.InvariantCulture) + " B"
        };
    }

    private static string FormatValue(long bytes, long unit, string precision, string suffix)
    {
        double value = bytes / (double)unit;
        return value.ToString(precision, CultureInfo.InvariantCulture) + " " + suffix;
    }
}
