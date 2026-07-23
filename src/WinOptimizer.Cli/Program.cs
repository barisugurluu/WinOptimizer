using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Orchestration;
using WinOptimizer.Safety;

namespace WinOptimizer.Cli;

/// <summary>
/// WinOptimizer CLI — komut satırı arayüzü (master plan Bölüm 15).
/// Komutlar: analyze, optimize, clean, status. Exit: 0 başarı, 1 kısmi, 2 hata.
/// </summary>
public static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitPartial = 1;
    private const int ExitError = 2;

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return ExitSuccess;
        }

        string command = args[0].ToLowerInvariant();
        var opts = ParseOptions(args[1..]);

        var sp = ServiceFactory.Build();
        var registry = sp.GetRequiredService<ModuleRegistry>();
        var engine = sp.GetRequiredService<JobOrchestrationEngine>();

        try
        {
            return command switch
            {
                "analyze" => await RunAnalyze(engine, opts),
                "optimize" => await RunOptimize(engine, opts),
                "clean" => await RunClean(engine, opts),
                "status" => RunStatus(registry),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Hata: {ex.Message}");
            return ExitError;
        }
    }

    private static async Task<int> RunAnalyze(JobOrchestrationEngine engine, CliOptions opts)
    {
        var analyses = await engine.AnalyzeAllAsync();
        long total = analyses.Values.Sum(a => a.TotalBytes);
        int items = analyses.Values.Sum(a => a.ItemCount);

        if (opts.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { items, totalBytes = total, modules = analyses },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine($"Toplam: {items} öğe, {total / 1e9:F2} GB temizlenebilir.\n");
            foreach (var (id, a) in analyses)
                Console.WriteLine($"  {id,-22} {a.ItemCount,6} öğe  {a.Summary}");
        }
        return ExitSuccess;
    }

    private static async Task<int> RunOptimize(JobOrchestrationEngine engine, CliOptions opts)
    {
        if (!opts.Yes)
        {
            Console.Error.WriteLine("Onay gerekli. Otomatik onay için --yes kullanın.");
            return ExitError;
        }
        await engine.PrepareSafetyAsync("WinOptimizer CLI öncesi");
        var progress = new Progress<ProgressInfo>(p =>
        {
            if (!opts.Json) Console.WriteLine($"  [{p.Percent,3}%] {p.ModuleId}: {p.Message}");
        });
        var results = await engine.ExecuteAsync(opts.Modules, progress);
        int totalSucceeded = results.Sum(r => r.Succeeded);
        int totalFailed = results.Sum(r => r.Failed);

        if (opts.Json)
            Console.WriteLine(JsonSerializer.Serialize(new { succeeded = totalSucceeded, failed = totalFailed }));
        else
            Console.WriteLine($"\nTamamlandı: {totalSucceeded} başarılı, {totalFailed} başarısız.");
        return totalFailed > 0 ? ExitPartial : ExitSuccess;
    }

    private static async Task<int> RunClean(JobOrchestrationEngine engine, CliOptions opts)
    {
        var cleanModules = opts.Modules ?? new[] { "CleanEngine", "MemoryEngine" };
        await engine.PrepareSafetyAsync("WinOptimizer CLI temizlik öncesi");
        var progress = new Progress<ProgressInfo>(p =>
        {
            if (!opts.Json) Console.WriteLine($"  [{p.Percent,3}%] {p.Message}");
        });
        var results = await engine.ExecuteAsync(cleanModules, progress);
        long gained = results.Sum(r => r.GainBytes);
        if (opts.Json)
            Console.WriteLine(JsonSerializer.Serialize(new { gainedBytes = gained }));
        else
            Console.WriteLine($"\nTemizlendi: {gained / 1e9:F2} GB kazanıldı.");
        return ExitSuccess;
    }

    private static int RunStatus(ModuleRegistry registry)
    {
        Console.WriteLine("Kayıtlı modüller:");
        foreach (var m in registry.Modules)
            Console.WriteLine($"  {m.Id,-22} [{m.Risk}] {m.DisplayName}");
        Console.WriteLine($"\nToplam: {registry.Modules.Count} modül.");
        return ExitSuccess;
    }

    private static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Bilinmeyen komut: {cmd}");
        PrintHelp();
        return ExitError;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("WinOptimizer CLI — Windows Sistem Bakım & Optimizasyon\n");
        Console.WriteLine("KULLANIM: WinOptimizer.Cli <komut> [seçenekler]\n");
        Console.WriteLine("KOMUTLAR:");
        Console.WriteLine("  analyze   Sistemi tara; temizlenebilir alanı raporla");
        Console.WriteLine("  optimize  Tüm modülleri uygula (--yes gerekir)");
        Console.WriteLine("  clean     Yalnızca temizlik (Clean + Memory)");
        Console.WriteLine("  status    Kayıtlı modülleri listele\n");
        Console.WriteLine("SEÇENEKLER:");
        Console.WriteLine("  --json          Yapılandırılmış JSON çıktı (CI/CD)");
        Console.WriteLine("  --yes           Otomatik onay (düşük/orta riskli)");
        Console.WriteLine("  --module <id>   Belirli modül(ler) (virgülle)");
        Console.WriteLine("  --profile <id>  Profil uygula (balanced/gaming/work/battery)");
    }

    private static CliOptions ParseOptions(string[] args)
    {
        var o = new CliOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--json": o.Json = true; break;
                case "--yes": o.Yes = true; break;
                case "--module" when i + 1 < args.Length:
                    o.Modules = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                case "--profile" when i + 1 < args.Length: o.Profile = args[++i]; break;
            }
        }
        return o;
    }

    private sealed class CliOptions
    {
        public bool Json { get; set; }
        public bool Yes { get; set; }
        public string[]? Modules { get; set; }
        public string? Profile { get; set; }
    }
}
