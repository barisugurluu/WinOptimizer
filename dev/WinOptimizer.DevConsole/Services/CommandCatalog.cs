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
            Title = "Build Release",
            Category = "Derle",
            File = dotnet,
            Args = new[] { "build", "WinOptimizer.sln", "-c", "Release", "--verbosity", "minimal" },
            Description = "Tum cozumu Release yapilandirmasinda derler."
        });
        list.Add(new DevCommand
        {
            Title = "Build Debug",
            Category = "Derle",
            File = dotnet,
            Args = new[] { "build", "WinOptimizer.sln", "-c", "Debug", "--verbosity", "minimal" }
        });
        list.Add(new DevCommand
        {
            Title = "Restore",
            Category = "Derle",
            File = dotnet,
            Args = new[] { "restore", "WinOptimizer.sln" }
        });

        // --- Test ---
        list.Add(new DevCommand
        {
            Title = "Test",
            Category = "Test",
            File = dotnet,
            Args = new[] { "test", "WinOptimizer.sln", "-c", "Release", "--no-build", "--verbosity", "minimal" },
            Description = "Tum birim/E2E testleri calistirir (yaklasik 178)."
        });
        // Ozel: kapsam goruntuleme (surec degil, diyalog).
        list.Add(new DevCommand
        {
            Title = "Kapsam Goster",
            Category = "Test",
            File = "__coverage__",
            Description = "Cobertura kapsam raporlarini proje bazinda goster (18.3 esikleri)."
        });
        list.Add(new DevCommand
        {
            Title = "Test + Kapsam",
            Category = "Test",
            File = dotnet,
            Args = new[] { "test", "WinOptimizer.sln", "-c", "Release", "--no-build", "--collect:", "XPlat Code Coverage", "--verbosity", "minimal" },
            Description = "Testleri calistirir + cobertura kapsam raporu uretir."
        });

        // --- Format ---
        list.Add(new DevCommand
        {
            Title = "Format Kontrol",
            Category = "Format",
            File = dotnet,
            Args = new[] { "format", "WinOptimizer.sln", "--verify-no-changes", "--verbosity", "minimal" },
            Description = "Kod stilini kontrol eder (CI kapisi). Temizse 0 doner."
        });
        list.Add(new DevCommand
        {
            Title = "Format Uygula",
            Category = "Format",
            File = dotnet,
            Args = new[] { "format", "WinOptimizer.sln", "--verbosity", "minimal" },
            Description = "CRLF/bicim sorunlarini duzeltir."
        });

        // --- Paketle ---
        // TEK dagitim hatti: build-installer.ps1 -> installer\build\*-setup.exe (self-contained).
        // MSI/WiX hatti ve self-signed test sertifikasi kaldirildi (bkz. docs\KURULUM.md).
        list.Add(new DevCommand
        {
            Title = "Kurulum Uret (setup.exe)",
            Category = "Paketle",
            File = "powershell.exe",
            Args = new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "build\\build-installer.ps1" },
            Description = "Tam hat: derle + test + self-contained publish + setup.exe + SHA256 (~48 MB). Inno Setup gerekli."
        });
        list.Add(new DevCommand
        {
            Title = "Kurulum Uret (testsiz)",
            Category = "Paketle",
            File = "powershell.exe",
            Args = new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "build\\build-installer.ps1", "-SkipTests" },
            Description = "Ayni hat, dotnet test adimi atlanir (hizli yineleme)."
        });
        list.Add(new DevCommand
        {
            Title = "Ikonu Yeniden Uret",
            Category = "Paketle",
            File = "powershell.exe",
            Args = new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "build\\generate-icon.ps1" },
            Description = "src\\WinOptimizer.App\\Resources\\WinOptimizer.ico dosyasini yeniden uretir."
        });

        list.Add(new DevCommand
        {
            Title = "Kurulum klasorunu ac",
            Category = "Paketle",
            File = "explorer.exe",
            Args = new[] { "installer\\build" },
            IsFolder = true
        });

        // --- Calistir ---
        list.Add(new DevCommand
        {
            Title = "WPF Uygulamayi Ac",
            Category = "Calistir",
            File = dotnet,
            Args = new[] { "run", "--project", "src\\WinOptimizer.App", "-c", "Debug", "--no-build" },
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


        // --- Servis (RealtimeGuard Windows servisi kontrolu) ---
        // sc.exe yonetici gerektirir; DevConsole normal kullanicida calisirsa hata konsola duser.
        list.Add(new DevCommand { Title = "Servis Baslat", Category = "Servis", File = "sc.exe", Args = new[] { "start", "WinOptimizerGuard" }, Description = "RealtimeGuard servisini baslatir (yonetici gerekir)." });
        list.Add(new DevCommand { Title = "Servis Durdur", Category = "Servis", File = "sc.exe", Args = new[] { "stop", "WinOptimizerGuard" }, Description = "RealtimeGuard servisini durdurur (yonetici gerekir)." });
        list.Add(new DevCommand { Title = "Servis Durum", Category = "Servis", File = "sc.exe", Args = new[] { "query", "WinOptimizerGuard" }, Description = "RealtimeGuard servis durumunu gosterir." });
        list.Add(new DevCommand { Title = "Servis Listesi", Category = "Servis", File = "sc.exe", Args = new[] { "query", "type=", "service", "state=", "all" }, Description = "Tum Windows servislerini listeler." });

        // --- Bakim (gunluk/yedek/publish klasorleri) ---
        list.Add(new DevCommand { Title = "Publish Klasoru", Category = "Bakim", File = "explorer.exe", Args = new[] { "src\\WinOptimizer.App\\bin\\Release\\net8.0-windows\\publish" }, IsFolder = true });
        list.Add(new DevCommand { Title = "Backup Klasoru", Category = "Bakim", File = "explorer.exe", Args = new[] { Path.Combine(programData, "backups") }, IsFolder = true });
        list.Add(new DevCommand
        {
            Title = "Gunlukleri Temizle",
            Category = "Bakim",
            File = "powershell.exe",
            Args = new[] { "-NoProfile", "-Command",
                "Get-ChildItem -Path (Join-Path $env:ProgramData 'WinOptimizer\\logs') -Recurse -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue; Write-Output 'Gunlukler temizlendi.'" },
            Description = "C:\\ProgramData\\WinOptimizer\\logs altindaki tum gunluk dosyalarini siler (klasorleri korur)."
        });


        return list;
    }
}
