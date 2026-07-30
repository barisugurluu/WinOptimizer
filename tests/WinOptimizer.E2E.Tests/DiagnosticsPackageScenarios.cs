using System.IO.Compression;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Orchestration;
using Xunit;

namespace WinOptimizer.E2E.Tests;

/// <summary>
/// Teşhis paketinin gerçekten destek için yeterli içerikle üretildiğini doğrular.
/// Bu paket "uygulama açılmıyor" / "servis çalışmıyor" şikâyetlerinde ilk istenen şeydir;
/// eskiden yalnızca app günlüğü + journal içeriyordu, yani servis sorunlarında boş çıkıyordu.
/// </summary>
public class DiagnosticsPackageScenarios : IDisposable
{
    private readonly string _baseDir = Path.Combine(
        Path.GetTempPath(), "winopt-diag-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _outputPath;

    public DiagnosticsPackageScenarios()
    {
        Directory.CreateDirectory(Path.Combine(_baseDir, "logs"));
        Directory.CreateDirectory(Path.Combine(_baseDir, "journal"));
        Directory.CreateDirectory(Path.Combine(_baseDir, "dumps"));
        _outputPath = Path.Combine(_baseDir, "paket.zip");
    }

    [Fact]
    public async Task Diagnostics_package_includes_service_and_requirements_reports()
    {
        // Üç sürecin günlüğü aynı klasöre yazar (LoggingBootstrap önekleri).
        await File.WriteAllTextAsync(Path.Combine(_baseDir, "logs", "app-20260730.log"), "app satiri");
        await File.WriteAllTextAsync(Path.Combine(_baseDir, "logs", "service-20260730.log"), "servis satiri");
        await File.WriteAllTextAsync(Path.Combine(_baseDir, "logs", "cli-20260730.log"), "cli satiri");
        await File.WriteAllTextAsync(Path.Combine(_baseDir, "dumps", "crash-1.txt"), "dokum");
        await File.WriteAllTextAsync(Path.Combine(_baseDir, "journal", "journal-2026-07-30.jsonl"), "{}");
        await File.WriteAllTextAsync(Path.Combine(_baseDir, "settings.json"), "{}");

        var builder = new DiagnosticsPackageBuilder(
            _baseDir,
            NullLogger<DiagnosticsPackageBuilder>.Instance,
            requirementsReportProvider: () => "GEREKSINIM RAPORU");

        var result = await builder.CreateAsync(_outputPath);

        result.FilePath.Should().Be(_outputPath);
        File.Exists(_outputPath).Should().BeTrue();

        using var zip = ZipFile.OpenRead(_outputPath);
        var entries = zip.Entries.Select(e => e.FullName).ToList();

        entries.Should().Contain("logs/app-20260730.log");
        entries.Should().Contain("logs/service-20260730.log", "servis sorunları için ŞART");
        entries.Should().Contain("logs/cli-20260730.log", "zamanlanmış çalışmaların izi");
        entries.Should().Contain("dumps/crash-1.txt", "CrashDumper çıktısı eskiden hiç toplanmıyordu");
        entries.Should().Contain("journal/journal-2026-07-30.jsonl");
        entries.Should().Contain("settings.json");
        entries.Should().Contain("sistem-bilgisi.txt");
        entries.Should().Contain("servis-durumu.txt");
        entries.Should().Contain("windows-olay-gunlugu.txt");
        entries.Should().Contain("gereksinimler.txt");
        entries.Should().Contain("OKUBENI.txt", "kullanıcı paylaşmadan önce ne gönderdiğini görmeli");
    }

    [Fact]
    public async Task Diagnostics_package_works_without_requirements_provider()
    {
        // Hata penceresi paketi ölü bir DI kapsayıcısıyla, sağlayıcı vermeden üretir.
        var builder = new DiagnosticsPackageBuilder(
            _baseDir, NullLogger<DiagnosticsPackageBuilder>.Instance);

        var result = await builder.CreateAsync(_outputPath);

        using var zip = ZipFile.OpenRead(result.FilePath);
        var entries = zip.Entries.Select(e => e.FullName).ToList();
        entries.Should().Contain("servis-durumu.txt");
        entries.Should().NotContain("gereksinimler.txt");
    }

    [Fact]
    public async Task Service_status_report_names_the_guard_service()
    {
        var builder = new DiagnosticsPackageBuilder(
            _baseDir, NullLogger<DiagnosticsPackageBuilder>.Instance);
        await builder.CreateAsync(_outputPath);

        using var zip = ZipFile.OpenRead(_outputPath);
        var entry = zip.GetEntry("servis-durumu.txt");
        entry.Should().NotBeNull();

        using var reader = new StreamReader(entry!.Open());
        string content = await reader.ReadToEndAsync();

        // Servis kurulu olsun ya da olmasın rapor okunabilir bir cevap vermeli.
        content.Should().Contain(GuardServiceController.ServiceName);
        content.Should().Contain("service-install.log");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir))
            {
                Directory.Delete(_baseDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Geçici klasör silinemezse test sonucu etkilenmez.
        }
        GC.SuppressFinalize(this);
    }
}
