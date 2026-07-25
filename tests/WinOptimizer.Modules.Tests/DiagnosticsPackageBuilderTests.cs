using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Orchestration;
using Xunit;

namespace WinOptimizer.Modules.Tests;

/// <summary>
/// Teşhis paketi (master plan Bölüm 19.5) — kullanıcının gönüllü olarak dışa aktardığı destek zip'i.
/// Ürün sıfır-telemetri olduğundan bu paketin içeriği ve sınırları sözleşmenin parçasıdır:
/// ne fazlasını toplamalı, ne de kilitli bir dosya yüzünden üretilememelidir.
/// </summary>
public class DiagnosticsPackageBuilderTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _outDir;
    private readonly DiagnosticsPackageBuilder _builder;

    public DiagnosticsPackageBuilderTests()
    {
        string root = Path.Combine(Path.GetTempPath(), "WinOptimizerDiagTests", Guid.NewGuid().ToString("N"));
        _baseDir = Path.Combine(root, "data");
        _outDir = Path.Combine(root, "out");
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(_outDir);
        _builder = new DiagnosticsPackageBuilder(_baseDir, NullLogger<DiagnosticsPackageBuilder>.Instance);
    }

    [Fact]
    public async Task Package_collects_logs_journal_and_settings()
    {
        WriteFile("logs/app-20260726.log", "gunluk satiri");
        WriteFile("journal/journal-2026-07-26.jsonl", "{\"id\":\"abc\"}");
        WriteFile("settings.json", "{\"Theme\":\"dark\"}");

        var result = await _builder.CreateAsync(TargetPath());

        var entries = ReadEntryNames(result.FilePath);
        entries.Should().Contain("logs/app-20260726.log");
        entries.Should().Contain("journal/journal-2026-07-26.jsonl");
        entries.Should().Contain("settings.json");
    }

    [Fact]
    public async Task Package_always_explains_its_own_contents()
    {
        WriteFile("logs/app.log", "x");

        var result = await _builder.CreateAsync(TargetPath());

        var readme = ReadEntryText(result.FilePath, "OKUBENI.txt");
        readme.Should().Contain("telemetri", "kullanıcı paketin gönderilmediğini görebilmeli");
        readme.Should().Contain("logs/app.log", "içindekiler listelenmeli");
    }

    [Fact]
    public async Task System_info_omits_user_and_machine_identity()
    {
        var result = await _builder.CreateAsync(TargetPath());

        var info = ReadEntryText(result.FilePath, "sistem-bilgisi.txt");
        info.Should().Contain("İşletim sistemi");
        info.Should().NotContain(Environment.UserName, "kullanıcı adı teşhis paketine konmaz");
        info.Should().NotContain(Environment.MachineName, "makine adı teşhis paketine konmaz");
    }

    [Fact]
    public async Task Package_is_created_even_when_there_is_nothing_to_collect()
    {
        var result = await _builder.CreateAsync(TargetPath());

        File.Exists(result.FilePath).Should().BeTrue();
        result.SizeBytes.Should().BeGreaterThan(0);
        ReadEntryNames(result.FilePath).Should().Contain("OKUBENI.txt");
    }

    [Fact]
    public async Task A_log_file_held_open_by_the_logger_is_still_collected()
    {
        // Serilog etkin günlük dosyasını açık tutar; paket yine de o dosyayı içermeli.
        WriteFile("logs/acik.log", "canli gunluk");
        string open = Path.Combine(_baseDir, "logs", "acik.log");
        using var _ = new FileStream(open, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

        var result = await _builder.CreateAsync(TargetPath());

        ReadEntryNames(result.FilePath).Should().Contain("logs/acik.log");
    }

    [Fact]
    public async Task Only_known_file_types_are_collected()
    {
        WriteFile("logs/app.log", "dahil");
        WriteFile("logs/gecici.tmp", "haric");
        WriteFile("backups/gizli.reg", "haric");

        var result = await _builder.CreateAsync(TargetPath());

        var entries = ReadEntryNames(result.FilePath);
        entries.Should().Contain("logs/app.log");
        entries.Should().NotContain(e => e.EndsWith(".tmp", StringComparison.Ordinal));
        entries.Should().NotContain(e => e.Contains("backups", StringComparison.Ordinal),
            "registry yedekleri destek paketine kendiliğinden konmaz");
    }

    [Fact]
    public async Task Reported_items_match_what_is_actually_in_the_archive()
    {
        WriteFile("logs/a.log", "x");
        WriteFile("journal/j.jsonl", "{}");

        var result = await _builder.CreateAsync(TargetPath());

        var entries = ReadEntryNames(result.FilePath);
        result.IncludedItems.Should().Contain("logs/a.log");
        result.IncludedItems.Should().Contain("journal/j.jsonl");
        entries.Should().Contain(result.IncludedItems.Where(i => i.Contains('/', StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Default_output_path_is_under_local_app_data()
    {
        // Varsayılan yol kullanıcı profilinde olmalı: yönetici hakkı gerektirmemeli.
        DiagnosticsPackageBuilder.DefaultOutputDirectory.Should().StartWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        await Task.CompletedTask;
    }

    private string TargetPath() => Path.Combine(_outDir, $"paket-{Guid.NewGuid():N}.zip");

    private void WriteFile(string relativePath, string content)
    {
        string path = Path.Combine(_baseDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    private static List<string> ReadEntryNames(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return zip.Entries.Select(e => e.FullName).ToList();
    }

    private static string ReadEntryText(string zipPath, string entryName)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(entryName);
        entry.Should().NotBeNull($"'{entryName}' pakette bulunmalı");
        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_baseDir)!, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
