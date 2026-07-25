using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Modules.BenchmarkEngine;
using WinOptimizer.Orchestration;
using WinOptimizer.Updater;
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

    /// <summary>--json çıktısı için paylaşılan ayarlar (her çağrıda yeniden kurmak pahalıdır).</summary>
    private static readonly JsonSerializerOptions JsonOutputOptions = new() { WriteIndented = true };

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
                "update" => await RunUpdate(opts),
                "benchmark" => await RunBenchmark(engine, opts),
                "rollback" => await RunRollback(sp.GetRequiredService<RollbackService>(), opts),
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
            Console.WriteLine(JsonSerializer.Serialize(
                new { items, totalBytes = total, modules = analyses }, JsonOutputOptions));
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

    /// <summary>
    /// update komutu — yeni sürüm denetler; <c>--yes</c> ile indir + doğrula + kur.
    /// Geri alma: kurulum öncesi Sistem Geri Yükleme noktası (master plan Bölüm 20.6).
    /// </summary>
    private static async Task<int> RunUpdate(CliOptions opts)
    {
        var checkerOpts = new UpdateOptions(
            Channel: opts.Channel ?? UpdateChannel.Stable,
            CurrentVersion: typeof(Program).Assembly.GetName().Version);
        var result = await new UpdateChecker(checkerOpts).CheckAsync();
        var m = result.Latest;

        if (!result.IsUpdateAvailable || m is null)
        {
            if (opts.Json)
                Console.WriteLine(JsonSerializer.Serialize(new { upToDate = true, current = result.CurrentVersion.ToString() }));
            else
                Console.WriteLine($"Güncel: v{result.CurrentVersion}");
            return ExitSuccess;
        }

        if (opts.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            { upToDate = false, latest = m.Version.ToString(), url = m.DownloadUrl, prerelease = m.IsPrerelease }));
            return ExitSuccess;
        }

        Console.WriteLine($"Yeni sürüm: {m.ToSummary()}");
        Console.WriteLine($"İndirme: {m.DownloadUrl}");
        if (!string.IsNullOrWhiteSpace(m.ReleaseNotes))
            Console.WriteLine($"\n{m.ReleaseNotes}\n");

        if (!opts.Yes)
        {
            Console.Error.WriteLine("Kurulum için --yes gerekir (önce indir + doğrula + kur).");
            return ExitError;
        }

        string tmp = Path.Combine(Path.GetTempPath(), $"WinOptimizer-{m.Version}.msi");
        Console.Write("İndiriliyor... ");
        await new UpdateDownloader().DownloadAsync(m.DownloadUrl, tmp);
        Console.WriteLine("tamam.");

        var vr = new UpdateVerifier().Verify(tmp, m.Sha256);
        if (!vr.IsInstallable)
        {
            Console.Error.WriteLine($"Doğrulama başarısız (hash={vr.HashOk}, imza={vr.SignatureOk}). Kurulum iptal.");
            return ExitError;
        }
        Console.WriteLine("Doğrulama tamam. Kurulum başlatılıyor (uygulama kapanacak)...");
        new UpdateInstaller().Install(tmp, quiet: true);
        return ExitSuccess;
    }

    /// <summary>
    /// benchmark komutu — önce/sonra performans ölçümü (master plan Bölüm 13).
    /// <c>--yes</c> ile önce temizlik (Clean + Memory) uygulanır, sonra kazanç ölçülür.
    /// </summary>
    private static async Task<int> RunBenchmark(JobOrchestrationEngine engine, CliOptions opts)
    {
        var measurer = new BenchmarkMeasurer();
        var before = measurer.Measure();
        if (!opts.Json)
            Console.WriteLine($"ÖLÇÜM (önce): {before.ToSummary()}");

        if (opts.Yes)
        {
            await engine.PrepareSafetyAsync("benchmark öncesi");
            var progress = new Progress<ProgressInfo>(p =>
            {
                if (!opts.Json) Console.WriteLine($"  [{p.Percent,3}%] {p.Message}");
            });
            var cleanModules = opts.Modules ?? new[] { "CleanEngine", "MemoryEngine" };
            await engine.ExecuteAsync(cleanModules, progress);
            await Task.Delay(500); // metriklerin oturması için kısa bekle
        }

        var after = measurer.Measure();
        var delta = BenchmarkMeasurer.Diff(before, after);
        string report = delta.ToReport(before, after);

        if (opts.Json)
            Console.WriteLine(JsonSerializer.Serialize(
                new { before = before.ToSummary(), after = after.ToSummary(), report }, JsonOutputOptions));
        else
            Console.WriteLine(report);
        return ExitSuccess;
    }

    /// <summary>
    /// rollback komutu — change journal kayıtlarını listeler veya geri alır (master plan Bölüm 15.1).
    /// <c>--list</c> listele · <c>--id &lt;kayıt&gt;</c> tek kayıt · <c>--last [N]</c> son N değişiklik.
    /// Geri alma sistemi değiştirdiğinden <c>--yes</c> ister.
    /// </summary>
    private static async Task<int> RunRollback(RollbackService rollback, CliOptions opts)
    {
        // Varsayılan davranış listelemektir: yanlışlıkla geri alma yapılmasın.
        if (opts.List || (opts.ChangeId is null && opts.Last is null))
        {
            var records = await rollback.ReadRecentAsync();
            if (opts.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(records, JsonOutputOptions));
            }
            else if (records.Count == 0)
            {
                Console.WriteLine("Son 7 günde geri alınabilir değişiklik yok.");
            }
            else
            {
                Console.WriteLine($"Son 7 gün — {records.Count} değişiklik:\n");
                foreach (var r in records)
                {
                    Console.WriteLine($"  {r.Id}  {r.Timestamp.LocalDateTime:dd.MM HH:mm}  " +
                                      $"{r.Module,-18} {r.Operation,-18} {r.Target}");
                }
                Console.WriteLine("\nGeri almak için: rollback --id <kayıt> --yes   veya   rollback --last [N] --yes");
            }
            return ExitSuccess;
        }

        if (!opts.Yes)
        {
            Console.Error.WriteLine("Geri alma sistemi değiştirir. Onaylamak için --yes ekleyin.");
            return ExitError;
        }

        var outcomes = opts.ChangeId is not null
            ? new[] { await rollback.RollbackByIdAsync(opts.ChangeId) }
            : (await rollback.RollbackLastAsync(opts.Last ?? 1)).ToArray();

        if (opts.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(outcomes, JsonOutputOptions));
        }
        else if (outcomes.Length == 0)
        {
            Console.WriteLine("Geri alınacak değişiklik bulunamadı.");
        }
        else
        {
            foreach (var o in outcomes)
            {
                Console.WriteLine($"  [{(o.IsSuccess ? "OK " : "HATA")}] {o.ModuleId} {o.ChangeId}: {o.Message}");
            }
        }

        return outcomes.Any(o => !o.IsSuccess) ? ExitPartial : ExitSuccess;
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
        Console.WriteLine("  status    Kayıtlı modülleri listele");
        Console.WriteLine("  update    Yeni sürümü denetle/indir/kur (--yes ile kur, --channel beta)");
        Console.WriteLine("  benchmark Önce/sonra performans ölçümü (--yes ile optimize et)");
        Console.WriteLine("  rollback  Değişiklikleri listele/geri al (--list, --id <kayıt>, --last [N])\n");
        Console.WriteLine("SEÇENEKLER:");
        Console.WriteLine("  --json          Yapılandırılmış JSON çıktı (CI/CD)");
        Console.WriteLine("  --yes           Otomatik onay (düşük/orta riskli)");
        Console.WriteLine("  --module <id>   Belirli modül(ler) (virgülle)");
        Console.WriteLine("  --profile <id>  Profil uygula (balanced/gaming/work/battery)");
        Console.WriteLine("  --id <kayıt>    rollback: geri alınacak değişiklik kimliği");
        Console.WriteLine("  --last [N]      rollback: son N değişikliği geri al (varsayılan 1)");
        Console.WriteLine("  --list          rollback: yalnızca listele (varsayılan)");
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
                case "--channel" when i + 1 < args.Length:
                    o.Channel = Enum.TryParse<UpdateChannel>(args[++i], ignoreCase: true, out var ch) ? ch : UpdateChannel.Stable;
                    break;
                case "--id" when i + 1 < args.Length: o.ChangeId = args[++i]; break;
                case "--list": o.List = true; break;
                case "--last":
                    // --last tek başına 1 anlamına gelir; sayı verilirse o kadar kayıt.
                    o.Last = i + 1 < args.Length && int.TryParse(args[i + 1], out int n) ? (++i, n).Item2 : 1;
                    break;
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
        public UpdateChannel? Channel { get; set; }

        /// <summary>rollback: geri alınacak tek kaydın kimliği (--id).</summary>
        public string? ChangeId { get; set; }

        /// <summary>rollback: en son N değişikliği geri al (--last, varsayılan 1).</summary>
        public int? Last { get; set; }

        /// <summary>rollback: yalnızca kayıtları listele (--list).</summary>
        public bool List { get; set; }
    }
}
