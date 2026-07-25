using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Orchestration;

/// <summary>Oluşturulan teşhis paketinin sonucu.</summary>
/// <param name="FilePath">Üretilen .zip dosyasının tam yolu.</param>
/// <param name="SizeBytes">Paket boyutu (bayt).</param>
/// <param name="IncludedItems">Pakete giren öğelerin insan-okur listesi.</param>
public sealed record DiagnosticsPackageResult(string FilePath, long SizeBytes, IReadOnlyList<string> IncludedItems);

/// <summary>
/// DiagnosticsPackageBuilder — kullanıcının gönüllü olarak dışa aktardığı teşhis paketi
/// (master plan Bölüm 19.5).
///
/// <para><b>Gizlilik:</b> WinOptimizer telemetri toplamaz (hedef G6). Bu paket yalnızca kullanıcı
/// açıkça istediğinde üretilir, hiçbir yere gönderilmez ve diske yazılır — ne gönderileceğine
/// kullanıcı karar verir. Paketin içine, içeriğini açıklayan bir <c>OKUBENI.txt</c> konur ki
/// kullanıcı paylaşmadan önce ne gönderdiğini görebilsin.</para>
///
/// <para>Journal ve günlük dosyaları dosya/kayıt defteri yollarını içerir; bu yollarda kullanıcı
/// adı geçebilir. Bu bilinçli bir ödünleşimdir (destek için gerekli) ve OKUBENI.txt'de belirtilir.
/// Kullanıcı adı, makine adı gibi alanlar sistem bilgisine ayrıca eklenmez.</para>
/// </summary>
public sealed class DiagnosticsPackageBuilder
{
    private readonly string _baseDir;
    private readonly ILogger<DiagnosticsPackageBuilder> _logger;

    public DiagnosticsPackageBuilder(string baseDir, ILogger<DiagnosticsPackageBuilder> logger)
    {
        _baseDir = baseDir;
        _logger = logger;
    }

    /// <summary>Teşhis paketlerinin yazıldığı varsayılan dizin.</summary>
    public static string DefaultOutputDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinOptimizer", "diagnostics");

    /// <summary>
    /// Teşhis paketini oluşturur.
    /// </summary>
    /// <param name="targetPath">Hedef .zip yolu; verilmezse varsayılan dizine zaman damgalı ad.</param>
    public async Task<DiagnosticsPackageResult> CreateAsync(
        string? targetPath = null, CancellationToken ct = default)
    {
        string path = targetPath ?? Path.Combine(
            DefaultOutputDirectory,
            $"winoptimizer-teshis-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var included = new List<string>();

        // Dosya kilitliyken de okuyabilmek için kopyalayarak değil, akışla ekliyoruz.
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            included.AddRange(await AddDirectoryAsync(zip, "logs", "logs", "*.log", ct));
            included.AddRange(await AddDirectoryAsync(zip, "journal", "journal", "*.jsonl", ct));

            string settings = Path.Combine(_baseDir, "settings.json");
            if (File.Exists(settings) && await TryAddFileAsync(zip, settings, "settings.json", ct))
            {
                included.Add("settings.json (tercihler ve eşikler — parola/anahtar içermez)");
            }

            await WriteEntryAsync(zip, "sistem-bilgisi.txt", BuildSystemInfo(), ct);
            included.Add("sistem-bilgisi.txt");

            await WriteEntryAsync(zip, "OKUBENI.txt", BuildReadme(included), ct);
        }

        long size = new FileInfo(path).Length;
        _logger.LogInformation("Teşhis paketi oluşturuldu: {Path} ({Size} bayt, {Count} öğe)",
            path, size, included.Count);

        return new DiagnosticsPackageResult(path, size, included);
    }

    /// <summary>Bir alt dizini pakete ekler; erişilemeyen dosyalar atlanır.</summary>
    private async Task<List<string>> AddDirectoryAsync(
        ZipArchive zip, string sourceFolder, string entryFolder, string pattern, CancellationToken ct)
    {
        var added = new List<string>();
        string dir = Path.Combine(_baseDir, sourceFolder);
        if (!Directory.Exists(dir))
        {
            return added;
        }

        foreach (var file in Directory.EnumerateFiles(dir, pattern))
        {
            ct.ThrowIfCancellationRequested();
            string entryName = $"{entryFolder}/{Path.GetFileName(file)}";
            if (await TryAddFileAsync(zip, file, entryName, ct))
            {
                added.Add(entryName);
            }
        }
        return added;
    }

    /// <summary>
    /// Bir dosyayı pakete ekler. Serilog aktif günlük dosyasını açık tuttuğundan paylaşımlı
    /// okuma kullanılır; yine de erişilemezse dosya sessizce atlanır (paket üretimi
    /// tek bir kilitli dosya yüzünden başarısız olmamalı).
    /// </summary>
    private async Task<bool> TryAddFileAsync(ZipArchive zip, string sourcePath, string entryName, CancellationToken ct)
    {
        try
        {
            await using var source = new FileStream(
                sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            await using var target = entry.Open();
            await source.CopyToAsync(target, ct);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Teşhis paketine eklenemedi (atlandı): {File}", sourcePath);
            return false;
        }
    }

    private static async Task WriteEntryAsync(ZipArchive zip, string entryName, string content, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync(content.AsMemory(), ct);
    }

    /// <summary>
    /// Sorun gidermek için gereken sistem bilgisi. Kullanıcı adı / makine adı kasıtlı olarak yok.
    /// </summary>
    private static string BuildSystemInfo()
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("WinOptimizer — Sistem Bilgisi");
        sb.AppendLine("=============================");
        sb.AppendLine(c, $"Oluşturulma (yerel) : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(c, $"İşletim sistemi     : {Environment.OSVersion}");
        sb.AppendLine(c, $"Derleme numarası    : {Environment.OSVersion.Version.Build}");
        sb.AppendLine(c, $"64-bit işletim sis. : {Environment.Is64BitOperatingSystem}");
        sb.AppendLine(c, $"64-bit süreç        : {Environment.Is64BitProcess}");
        sb.AppendLine(c, $"İşlemci çekirdeği   : {Environment.ProcessorCount}");
        sb.AppendLine(c, $".NET sürümü         : {Environment.Version}");
        sb.AppendLine(c, $"Uygulama sürümü     : {typeof(DiagnosticsPackageBuilder).Assembly.GetName().Version}");
        sb.AppendLine(c, $"Kültür              : {CultureInfo.CurrentCulture.Name}");

        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            sb.AppendLine(c, $"Sistem sürücüsü     : {drive.Name} {drive.DriveFormat}, " +
                             $"boş {drive.AvailableFreeSpace / (1024 * 1024 * 1024)} GB / " +
                             $"{drive.TotalSize / (1024 * 1024 * 1024)} GB");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            sb.AppendLine("Sistem sürücüsü     : (okunamadı)");
        }

        return sb.ToString();
    }

    private static string BuildReadme(IReadOnlyList<string> included)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WinOptimizer — Teşhis Paketi");
        sb.AppendLine("============================");
        sb.AppendLine();
        sb.AppendLine("Bu paketi SİZ oluşturdunuz; WinOptimizer hiçbir veriyi kendiliğinden");
        sb.AppendLine("göndermez ve telemetri toplamaz. Paket yalnızca diskinizdedir —");
        sb.AppendLine("kime göndereceğinize siz karar verirsiniz.");
        sb.AppendLine();
        sb.AppendLine("İÇİNDEKİLER");
        sb.AppendLine("-----------");
        foreach (var item in included)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  - {item}");
        }
        sb.AppendLine();
        sb.AppendLine("PAYLAŞMADAN ÖNCE");
        sb.AppendLine("----------------");
        sb.AppendLine("Günlük (logs) ve değişiklik geçmişi (journal) dosyaları, üzerinde");
        sb.AppendLine("çalışılan dosya ve kayıt defteri yollarını içerir. Bu yollarda Windows");
        sb.AppendLine("kullanıcı adınız geçebilir. Paket düz metindir: göndermeden önce");
        sb.AppendLine("açıp inceleyebilir, istemediğiniz dosyaları silebilirsiniz.");
        sb.AppendLine();
        sb.AppendLine("Parola, lisans anahtarı veya kimlik bilgisi bu pakete konmaz.");
        return sb.ToString();
    }
}
