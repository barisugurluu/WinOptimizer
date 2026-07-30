namespace WinOptimizer.DevConsole.Services;

/// <summary>
/// Cozum ve yol tespiti — DevConsole calistigi yerden yukari .sln arar,
/// dotnet exeyi bulur (kullanici ayari > ~/.dotnet > PATH). Statik.
/// </summary>
public static class DevPaths
{
    private static readonly Lazy<string> _solutionRoot = new(FindSolutionRoot);
    private static readonly Lazy<string> _dotnet = new(FindDotnet);

    /// <summary>Cozum koku (WinOptimizer.sln bulundugu dizin).</summary>
    public static string SolutionRoot => _solutionRoot.Value;

    /// <summary>dotnet exe tam yolu.</summary>
    public static string Dotnet => _dotnet.Value;

    /// <summary>.NET SDK surumu (bilgi cubugu icin).</summary>
    public static string DotnetVersion { get; private set; } = "?";

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WinOptimizer.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Environment.CurrentDirectory;
    }

    private static string FindDotnet()
    {
        // 1) Kullanici ayari (elle girilmis dotnet yolu).
        var settings = UserSettings.Load();
        if (!string.IsNullOrWhiteSpace(settings.DotnetOverride) &&
            File.Exists(settings.DotnetOverride))
        {
            DotnetVersion = ResolveVersion(settings.DotnetOverride);
            return settings.DotnetOverride;
        }

        // 2) ~/.dotnet/dotnet.exe.
        string userDotnet = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet.exe");
        if (File.Exists(userDotnet))
        {
            DotnetVersion = ResolveVersion(userDotnet);
            return userDotnet;
        }

        // 3) PATH.
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

        return "dotnet";
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
