namespace WinOptimizer.DevConsole.Services;

/// <summary>
/// Cozum ve yol tespiti — DevConsole'un calistigi yerden yukari dogru .sln
/// arar, dotnet exe'yi (PATH ya da ~/.dotnet) bulur. Statik (gelistirme araci).
/// </summary>
public static class DevPaths
{
    private static readonly Lazy<string> _solutionRoot = new(FindSolutionRoot);
    private static readonly Lazy<string> _dotnet = new(FindDotnet);

    /// <summary>Cozum koku (WinOptimizer.sln bulundugu dizin).</summary>
    public static string SolutionRoot => _solutionRoot.Value;

    /// <summary>dotnet exe tam yolu (yoksa "dotnet" doner — PATH'e birakir).</summary>
    public static string Dotnet => _dotnet.Value;

    /// <summary>.NET SDK surumu (bilgi cubugu icin).</summary>
    public static string DotnetVersion { get; private set; } = "?";

    private static string FindSolutionRoot()
    {
        // AppContext.BaseDirectory'den (bin/.../Debug/net8.0-windows) yukari cik.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WinOptimizer.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        // Bulunamazsa calisma dizinine dus.
        return Environment.CurrentDirectory;
    }

    private static string FindDotnet()
    {
        // 1) ~/.dotnet/dotnet.exe (bu makinede yuklu yer).
        string userDotnet = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet.exe");
        if (File.Exists(userDotnet))
        {
            DotnetVersion = ResolveVersion(userDotnet);
            return userDotnet;
        }

        // 2) PATH'teki dotnet.
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar is not null)
        {
            foreach (string entry in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = Path.Combine(entry.Trim(), "dotnet.exe");
                if (File.Exists(candidate))
                {
                    DotnetVersion = ResolveVersion(candidate);
                    return candidate;
                }
            }
        }

        return "dotnet"; // son care: PATH'e birak
    }

    private static string ResolveVersion(string dotnetExe)
    {
        try
        {
            using var p = new System.Diagnostics.Process();
            p.StartInfo = new(dotnetExe, "--version")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            p.Start();
            string ver = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(3000);
            return ver;
        }
        catch
        {
            return "?";
        }
    }
}