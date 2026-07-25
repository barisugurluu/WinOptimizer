using FluentAssertions;
using WinOptimizer.Modules.DeepCleanEngine;
using Xunit;

namespace WinOptimizer.Modules.Tests;

/// <summary>
/// DeepCleanEngine tarama mantığı (master plan Bölüm 3.2) — geçici bir klasör ağacı üzerinde,
/// gerçek kullanıcı verisine dokunmadan çalışır. Tarama SİLMEZ, yalnızca raporlar; bu testler
/// o ayrımı da korur.
/// </summary>
public class DiskScannerTests : IDisposable
{
    private readonly string _root;
    private readonly DiskScanner _scanner = new();

    public DiskScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "WinOptimizerScanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ScanFolder_counts_files_recursively_with_total_size()
    {
        WriteFile("a.bin", 100);
        WriteFile("alt/b.bin", 250);
        WriteFile("alt/derin/c.bin", 150);

        var (count, bytes) = _scanner.ScanFolder(_root);

        count.Should().Be(3);
        bytes.Should().Be(500);
    }

    [Fact]
    public void ScanFolder_returns_zero_for_missing_folder()
    {
        var (count, bytes) = _scanner.ScanFolder(Path.Combine(_root, "yok"));

        count.Should().Be(0);
        bytes.Should().Be(0);
    }

    [Fact]
    public void FindLargeFiles_returns_only_files_at_or_above_the_threshold()
    {
        WriteFile("kucuk.bin", 10);
        WriteFile("esik.bin", 100);
        WriteFile("buyuk.bin", 500);

        var large = _scanner.FindLargeFiles(_root, thresholdBytes: 100);

        large.Select(f => f.Name).Should().BeEquivalentTo("esik.bin", "buyuk.bin");
    }

    [Fact]
    public void FindLargeFiles_does_not_delete_anything()
    {
        WriteFile("buyuk.bin", 500);

        _scanner.FindLargeFiles(_root, thresholdBytes: 1);

        File.Exists(Path.Combine(_root, "buyuk.bin")).Should().BeTrue("tarama yalnızca raporlar");
    }

    [Fact]
    public void FindDuplicateFiles_groups_files_with_identical_content()
    {
        WriteContent("bir.txt", "aynı içerik");
        WriteContent("alt/iki.txt", "aynı içerik");
        WriteContent("farkli.txt", "başka içerik");

        var groups = _scanner.FindDuplicateFiles(_root, minSizeBytes: 1);

        groups.Should().HaveCount(1);
        groups[0].Select(f => f.Name).Should().BeEquivalentTo("bir.txt", "iki.txt");
    }

    [Fact]
    public void FindDuplicateFiles_ignores_same_size_files_with_different_content()
    {
        // Aynı boyut, farklı içerik — yalnızca boyuta bakan bir uygulama bunları yanlışlıkla eşler.
        WriteContent("x.txt", "AAAA");
        WriteContent("y.txt", "BBBB");

        var groups = _scanner.FindDuplicateFiles(_root, minSizeBytes: 1);

        groups.Should().BeEmpty("içerik hash'i farklı olduğu için yinelenen sayılmamalı");
    }

    [Fact]
    public void FindDuplicateFiles_skips_files_below_the_minimum_size()
    {
        WriteContent("kucuk1.txt", "ab");
        WriteContent("kucuk2.txt", "ab");

        var groups = _scanner.FindDuplicateFiles(_root, minSizeBytes: 1024);

        groups.Should().BeEmpty();
    }

    [Fact]
    public void FindDuplicateFiles_can_be_cancelled()
    {
        WriteContent("bir.txt", "aynı");
        WriteContent("iki.txt", "aynı");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _scanner.FindDuplicateFiles(_root, minSizeBytes: 1, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    [InlineData(5 * 1024 * 1024, "5,0 MB")]
    public void FormatBytes_uses_the_largest_fitting_unit(long bytes, string expected)
    {
        // Kültüre bağlı ondalık ayırıcıyı testin kendisinde normalize et.
        var actual = DiskScanner.FormatBytes(bytes).Replace('.', ',');

        actual.Should().Be(expected);
    }

    private void WriteFile(string relativePath, int sizeBytes)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[sizeBytes]);
    }

    private void WriteContent(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
