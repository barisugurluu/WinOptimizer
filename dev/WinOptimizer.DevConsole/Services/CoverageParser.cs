using System.Xml.Linq;
using WinOptimizer.DevConsole.Models;

namespace WinOptimizer.DevConsole.Services;

/// <summary>
/// Cobertura kapsam raporlarini (coverage.cobertura.xml) okuyup proje bazinda
/// yuzdeye cevirir. Birden cok test projesi ciktisini toplar; ayni proje icin
/// en yuksek kapsami tutar (en genis ispat).
/// </summary>
public static class CoverageParser
{
    /// <summary>
    /// Cozum koku altindaki tum cobertura raporlarini toplar.
    /// Arama yerleri: tests/*/TestResults/**, dev/cov-tmp/**.
    /// </summary>
    public static IReadOnlyList<CoverageEntry> Collect()
    {
        string root = DevPaths.SolutionRoot;
        var files = new List<string>();
        files.AddRange(SafeEnumerate(Path.Combine(root, "tests"), "coverage.cobertura.xml"));
        files.AddRange(SafeEnumerate(Path.Combine(root, "dev"), "coverage.cobertura.xml"));

        // Proje adi -> en iyi kapsam. Cakismalarda en yuksek kazanir.
        var byProject = new Dictionary<string, CoverageEntry>(StringComparer.Ordinal);
        foreach (string file in files)
        {
            foreach (var entry in ParseFile(file))
            {
                if (!byProject.TryGetValue(entry.Project, out var existing) ||
                    entry.LinePercent > existing.LinePercent)
                {
                    byProject[entry.Project] = entry;
                }
            }
        }

        return byProject.Values
            .OrderByDescending(e => e.LinePercent)
            .ToList();
    }

    /// <summary>Tek bir cobertura XML'ini ayristirir.</summary>
    private static IEnumerable<CoverageEntry> ParseFile(string path)
    {
        XDocument doc;
        try { doc = XDocument.Load(path); }
        catch { yield break; }

        XElement? coverage = doc.Element("coverage");
        if (coverage is null) yield break;

        foreach (XElement pkg in coverage.Descendants("package"))
        {
            string? name = (string?)pkg.Attribute("name");
            string? lineRate = (string?)pkg.Attribute("line-rate");
            string? branchRate = (string?)pkg.Attribute("branch-rate");
            if (name is null || lineRate is null) continue;

            if (double.TryParse(lineRate, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double lr))
            {
                double? br = null;
                if (branchRate is not null &&
                    double.TryParse(branchRate, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double b))
                {
                    br = b * 100;
                }

                yield return new CoverageEntry(name, lr * 100, br, path);
            }
        }
    }

    private static IEnumerable<string> SafeEnumerate(string root, string pattern)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories);
        }
        catch { return Array.Empty<string>(); }
    }
}