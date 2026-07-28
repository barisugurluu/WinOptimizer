using WinOptimizer.DevConsole.Models;

namespace WinOptimizer.DevConsole.Services;

/// <summary>
/// Tum gelistirici komutlarinin katalogu. Butonlar bu listeyi kullanarak uretilir.
/// Komutlar kategoriye gore gruplanir; her biri DevCommand olarak tanimlanir.
/// </summary>
public static class CommandCatalog
{
    /// <summary>Tum komutlar kategori sirasinda.</summary>
    public static IReadOnlyList<DevCommand> All => Build();

    /// <summary>Belirli bir komutu ada gore bulur (CLI secici icin).</summary>
    public static DevCommand? Find(string title) =>
        All.FirstOrDefault(c => string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase));

    private static List<DevCommand> Build()
    {
        string dotnet = DevPaths.Dotnet;
        var list = new List<DevCommand>();

        // --- Derle ---
        list.Add(new DevCommand
        {
            Title = "Build Release", Category = "Derle",
            File = dotnet, Args = new[] { "build", "WinOptimizer.sln", "-c", "Release", "--verbosity", "minimal" },
            Description = "Tum cozumu Release yapilandirmasinda derler."
        });
        list.Add(new DevCommand
        {
            Title = "Build Debug", Category = "Derle",
            File = dotnet, Args = new[] { "build", "WinOptimizer.sln", "-c", "Debug", "--verbosity", "minimal" }
        });
        list.Add(new DevCommand
        {
            Title = "Restore", Category = "Derle",
            File = dotnet, Args = new[] { "restore", "WinOptimizer.sln" }
        });

        // --- Test ---
        list.Add(new DevCommand
        {
            Title = "Test", Category = "Test",
            File = dotnet, Args = new[] { "test", "WinOptimizer.sln", "-c", "Release", "--no-build", "--verbosity", "minimal" },
            Description = "Tum birim/E2E testleri calistirir (yaklasik 178)."
        });
        list.Add(new DevCommand
        {
            Title = "Test + Kapsam", Category = "Test",
            File = dotnet, Args = new[] { "test", "WinOptimizer.sln", "-c", "Release", "--no-build", "--collect:", "XPlat Code Coverage", "--verbosity", "minimal" },
            Description = "Testleri calistirir + cobertura kapsam raporu uretir."
        });

        // --- Format ---
        list.Add(new DevCommand
        {
            Title = "Format Kontrol", Category = "Format",
            File = dotnet, Args = new[] { "format", "WinOptimizer.sln", "--verify-no-changes", "--verbosity", "minimal" },
            Description = "Kod stilini kontrol eder (CI kapisi). Temizse 0 doner."
        });
        list.Add(new DevCommand
        {
            Title = "Format Uygula", Category = "Format",
            File = dotnet, Args = new[] { "format", "WinOptimizer.sln", "--verbosity", "minimal" },
            Description = "CRLF/bicim sorunlarini duzeltir."
        });

        // --- Paketle ---
        list.Add(new DevCommand
        {
            Title = "MSI Uret", Category = "Paketle",
            File = "powershell.exe", Args = new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "build\\build-installer.ps1", "-SkipSign" },
            Description = "Imzasiz gelistirme MSI uretir (~3 MB). WiX gerekli."
        });
        list.Add(new DevCommand
        {
            Title = "MSI klasorunu ac", Category = "Paketle",
            File = "explorer.exe", Args = new[] { "installer\\wix\\bin" }, IsFolder = true
        });

        // --- Calistir ---
        list.Add(new DevCommand
        {
            Title = "WPF Uygulamayi Ac", Category = "Calistir",
            File = dotnet, Args = new[] { "run", "--project", "src\\WinOptimizer.App", "-c", "Debug", "--no-build" },
            Description = "Fluent Dark dashboard'u acar. Yonetici (UAC) onayi ister."
        });

        // --- Klasorler ---
        string programData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinOptimizer");
        list.Add(new DevCommand { Title = "Cozum koku", Category = "Klasorler", File = "explorer.exe", Args = new[] { "." }, IsFolder = true });
        list.Add(new DevCommand { Title = "Günlükler", Category = "Klasorler", File = "explorer.exe", Args = new[] { Path.Combine(programData, "logs") }, IsFolder = true });
        list.Add(new DevCommand { Title = "Journal", Category = "Klasorler", File = "explorer.exe", Args = new[] { Path.Combine(programData, "journal") }, IsFolder = true });
        list.Add(new DevCommand { Title = "Dumps", Category = "Klasorler", File = "explorer.exe", Args = new[] { Path.Combine(programData, "dumps") }, IsFolder = true });

        // --- CLI (canli cikti) ---
        // Komutlar MainForm'taki CLI secicide sevilir; ek argumanlar orada eklenir.
        string cliProj = "src\\WinOptimizer.Cli\\WinOptimizer.Cli.csproj";
        list.Add(new DevCommand { Title = "status", Category = "CLI", File = dotnet, Args = new[] { "run", "--project", cliProj, "-c", "Release", "--no-build", "--", "status" }, Description = "Kayitli modulleri listeler (guvenli)." });
        list.Add(new DevCommand { Title = "analyze", Category = "CLI", File = dotnet, Args = new[] { "run", "--project", cliProj, "-c", "Release", "--no-build", "--", "analyze" }, Description = "Sistemi tarar; degisiklik yapmaz." });
        list.Add(new DevCommand { Title = "optimize --yes", Category = "CLI", File = dotnet, Args = new[] { "run", "--project", cliProj, "-c", "Release", "--no-build", "--", "optimize", "--yes" }, Description = "Tum modulleri uygular (yonetici gerekir)." });
        list.Add(new DevCommand { Title = "clean --yes", Category = "CLI", File = dotnet, Args = new[] { "run", "--project", cliProj, "-c", "Release", "--no-build", "--", "clean", "--yes" } });
        list.Add(new DevCommand { Title = "benchmark", Category = "CLI", File = dotnet, Args = new[] { "run", "--project", cliProj, "-c", "Release", "--no-build", "--", "benchmark" } });
        list.Add(new DevCommand { Title = "rollback --list", Category = "CLI", File = dotnet, Args = new[] { "run", "--project", cliProj, "-c", "Release", "--no-build", "--", "rollback", "--list" } });
        list.Add(new DevCommand { Title = "update --check", Category = "CLI", File = dotnet, Args = new[] { "run", "--project", cliProj, "-c", "Release", "--no-build", "--", "update", "--check" } });

        return list;
    }
}