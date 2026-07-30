namespace WinOptimizer.DevConsole.Models;

/// <summary>Tek bir kaynak projesinin kod kapsamı ozeti.</summary>
/// <param name="Project">Kaynak proje adi (WinOptimizer.Core vb.).</param>
/// <param name="LinePercent">Satir kapsamı yuzdesi (0-100).</param>
/// <param name="BranchPercent">Dal kapsamı yuzdesi (0-100); bilinmiyorsa null.</param>
/// <param name="SourceFile">Okunan cobertura XML yolu (izlenebilirlik icin).</param>
public sealed record CoverageEntry(string Project, double LinePercent, double? BranchPercent, string SourceFile)
{
    /// <summary>Master plan 18.3 esigine gore durum (Core/Safety %85, moduller %70).</summary>
    public bool MeetsThreshold => Project.Contains("Modules")
        ? LinePercent >= 70
        : LinePercent >= 85;
}
