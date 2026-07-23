using System.Globalization;
using System.Management;

namespace WinOptimizer.Modules.BenchmarkEngine;

/// <summary>
/// Tek bir benchmark ölçüm anlık görüntüsü (master plan Bölüm 13.1 & 16.4).
/// </summary>
public sealed class BenchmarkSnapshot
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public double? BootSec { get; init; }
    public long? FreeRamMb { get; init; }
    public double? DiskFreeGb { get; init; }
    public int? CpuLoadPct { get; init; }
    public bool? RealTimeProtection { get; init; }
    public int? SecurityScore { get; init; }

    public string ToSummary() =>
        $"Boot: {(BootSec is null ? "?" : BootSec.Value.ToString("F1", CultureInfo.InvariantCulture))} sn • " +
        $"Boş RAM: {(FreeRamMb is null ? "?" : FreeRamMb.Value.ToString("N0"))} MB • " +
        $"C: boş: {(DiskFreeGb is null ? "?" : DiskFreeGb.Value.ToString("F1", CultureInfo.InvariantCulture))} GB • " +
        $"CPU: {(CpuLoadPct is null ? "?" : CpuLoadPct.Value.ToString())}% • " +
        $"Güvenlik: {(SecurityScore is null ? "?" : SecurityScore.Value.ToString())}/100";
}

/// <summary>İki snapshot arasındaki fark — "önce/sonra" kazanç raporu (Bölüm 13.3).</summary>
public sealed record BenchmarkDelta(double? BootSec, long? FreeRamMb, double? DiskFreeGb, int? SecurityScore)
{
    /// <summary>Karşılaştırma raporunu metin olarak üretir.</summary>
    public string ToReport(BenchmarkSnapshot before, BenchmarkSnapshot after)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"═════════ Optimizasyon Raporu — {after.Timestamp.LocalDateTime:dd.MM.yyyy HH:mm} ═════════");
        AddBootLine(sb, before, after);
        AddRamLine(sb, before, after);
        AddDiskLine(sb, before, after);
        AddScoreLine(sb, before, after);
        sb.Append(new string('═', 70));
        return sb.ToString();
    }

    private void AddBootLine(System.Text.StringBuilder sb, BenchmarkSnapshot b, BenchmarkSnapshot a)
    {
        if (BootSec is not double d || b.BootSec is not double bv || a.BootSec is not double av) return;
        bool improved = d < 0; // boot azaldı = iyileşme
        string arrow = d == 0 ? "—" : (improved ? "▼" : "▲");
        string deltaStr = d == 0 ? "değişmedi" : $"{arrow} {Math.Abs(d):F1} sn";
        sb.AppendLine($"  Boot süresi:    {bv:F1} sn → {av:F1} sn   {deltaStr}");
    }

    private void AddRamLine(System.Text.StringBuilder sb, BenchmarkSnapshot b, BenchmarkSnapshot a)
    {
        if (FreeRamMb is not long d || b.FreeRamMb is not long bv || a.FreeRamMb is not long av) return;
        bool improved = d > 0; // boş RAM arttı = iyileşme
        string arrow = d == 0 ? "—" : (improved ? "▲" : "▼");
        string deltaStr = d == 0 ? "değişmedi" : $"{arrow} {Math.Abs(d):N0} MB";
        sb.AppendLine($"  Boş RAM:        {bv:N0} MB → {av:N0} MB   {deltaStr}");
    }

    private void AddDiskLine(System.Text.StringBuilder sb, BenchmarkSnapshot b, BenchmarkSnapshot a)
    {
        if (DiskFreeGb is not double d || b.DiskFreeGb is not double bv || a.DiskFreeGb is not double av) return;
        bool improved = d > 0;
        string arrow = d == 0 ? "—" : (improved ? "▲" : "▼");
        string deltaStr = d == 0 ? "değişmedi" : $"{arrow} {Math.Abs(d):F1} GB";
        sb.AppendLine($"  Boş disk (C:):  {bv:F1} GB → {av:F1} GB   {deltaStr}");
    }

    private void AddScoreLine(System.Text.StringBuilder sb, BenchmarkSnapshot b, BenchmarkSnapshot a)
    {
        if (SecurityScore is not int d || b.SecurityScore is not int bv || a.SecurityScore is not int av) return;
        bool improved = d > 0;
        string arrow = d == 0 ? "—" : (improved ? "▲" : "▼");
        string deltaStr = d == 0 ? "değişmedi" : $"{arrow} {Math.Abs(d)}";
        sb.AppendLine($"  Güvenlik skoru: {bv}/100 → {av}/100   {deltaStr}");
    }
}
