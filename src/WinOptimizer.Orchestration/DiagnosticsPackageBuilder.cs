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
    private readonly Func<string>? _requirementsReportProvider;

    /// <summary>Teşhis paketi oluşturucuyu kurar.</summary>
    /// <param name="baseDir">Veri dizini (<c>%ProgramData%\WinOptimizer</c>).</param>
    /// <param name="logger">Günlükleyici.</param>
    /// <param name="requirementsReportProvider">
    /// Gereksinim raporunu düz metin üreten geri çağırım (isteğe bağlı). Delege olarak
    /// alınır ki bu tür <c>SystemRequirementsChecker</c>'a bağımlı olmasın; hata penceresi
    /// paketi ölü bir DI kapsayıcısıyla, sağlayıcı olmadan da üretebilsin.
    /// </param>
    public DiagnosticsPackageBuilder(
        string baseDir,
        ILogger<DiagnosticsPackageBuilder> logger,
        Func<string>? requirementsReportProvider = null)
    {
        _baseDir = baseDir;
        _logger = logger;
        _requirementsReportProvider = requirementsReportProvider;
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
            // logs\*.log artık app-*, service-* ve cli-* dosyalarını birlikte yakalar
            // (LoggingBootstrap üç sürecin tamamını aynı klasöre yazar).
            included.AddRange(await AddDirectoryAsync(zip, "logs", "logs", "*.log", ct));
            included.AddRange(await AddDirectoryAsync(zip, "journal", "journal", "*.jsonl", ct));

            // Çökme dökümleri: CrashDumper zaten yazıyordu ama pakete hiç girmiyordu.
            included.AddRange(await AddDirectoryAsync(zip, "dumps", "dumps", "*.txt", ct));

            string settings = Path.Combine(_baseDir, "settings.json");
            if (File.Exists(settings) && await TryAddFileAsync(zip, settings, "settings.json", ct))
            {
                included.Add("settings.json (tercihler ve eşikler — parola/anahtar içermez)");
            }

            await WriteEntryAsync(zip, "sistem-bilgisi.txt", BuildSystemInfo(), ct);
            included.Add("sistem-bilgisi.txt");

            // Servis durumu: "servis çalışmıyor" şikâyetlerinin ilk bakılacak yeri.
            await WriteEntryAsync(zip, "servis-durumu.txt", BuildServiceInfo(), ct);
            // NOT: açıklama metinlerinde '/' KULLANMA — IncludedItems'ta eğik çizgi içeren
            // her öğe arşiv yolu sayılır (DiagnosticsPackageBuilderTests bunu doğrular).
            included.Add("servis-durumu.txt (RealtimeGuard hizmetinin durumu)");

            // Windows Olay Görüntüleyici kayıtları: dosya sink'i eklenmeden önce üretilmiş
            // (yalnız EventLog'a yazan) sürümlerden gelen bilgiyi de kurtarır.
            await WriteEntryAsync(zip, "windows-olay-gunlugu.txt", BuildEventLogInfo(), ct);
            included.Add("windows-olay-gunlugu.txt (Uygulama olay günlüğündeki WinOptimizer kayıtları)");

            if (_requirementsReportProvider is not null)
            {
                await WriteEntryAsync(zip, "gereksinimler.txt", _requirementsReportProvider(), ct);
                included.Add("gereksinimler.txt (sistem gereksinim kontrolü sonucu)");
            }

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

    /// <summary>
    /// RealtimeGuard hizmetinin kayıt ve durum bilgisi. Servis LocalSystem olarak çalıştığı
    /// ve teşhis paketini oluşturan sürecin dışında olduğu için bu bilgi ayrıca toplanır.
    /// </summary>
    private static string BuildServiceInfo()
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("WinOptimizer — RealtimeGuard Hizmet Durumu");
        sb.AppendLine("==========================================");

        try
        {
            using var controller = System.ServiceProcess.ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName.Equals(
                    GuardServiceController.ServiceName, StringComparison.OrdinalIgnoreCase));

            if (controller is null)
            {
                sb.AppendLine(c, $"Hizmet '{GuardServiceController.ServiceName}' KAYITLI DEĞİL.");
                sb.AppendLine("Kurulum sihirbazındaki hizmet kutusu işaretlenmemiş olabilir;");
                sb.AppendLine("uygulama içindeki Guard sekmesinden kurulabilir.");
            }
            else
            {
                sb.AppendLine(c, $"Hizmet adı  : {controller.ServiceName}");
                sb.AppendLine(c, $"Görünen ad  : {controller.DisplayName}");
                sb.AppendLine(c, $"Durum       : {controller.Status}");
                sb.AppendLine(c, $"Durdurulabilir: {controller.CanStop}");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                     or System.ComponentModel.Win32Exception)
        {
            sb.AppendLine(c, $"Hizmet durumu okunamadı: {ex.Message}");
        }

        sb.AppendLine();
        sb.AppendLine("Hizmet günlüğü: logs/service-*.log (bu pakete dahildir)");
        sb.AppendLine("Kurulum günlüğü: logs/service-install.log");
        return sb.ToString();
    }

    /// <summary>
    /// Windows Uygulama olay günlüğündeki son WinOptimizer kayıtları (en fazla 50).
    /// </summary>
    private static string BuildEventLogInfo()
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("WinOptimizer — Windows Olay Günlüğü (Uygulama)");
        sb.AppendLine("=============================================");

        if (!OperatingSystem.IsWindows())
        {
            sb.AppendLine("(Windows dışı platform)");
            return sb.ToString();
        }

        try
        {
            using var log = new System.Diagnostics.EventLog("Application");
            var entries = log.Entries.Cast<System.Diagnostics.EventLogEntry>()
                .Where(e => e.Source.StartsWith("WinOptimizer", StringComparison.OrdinalIgnoreCase))
                .TakeLast(50)
                .ToList();

            if (entries.Count == 0)
            {
                sb.AppendLine("WinOptimizer kaynaklı kayıt bulunamadı.");
            }

            foreach (var entry in entries)
            {
                sb.AppendLine(c, $"[{entry.TimeGenerated:yyyy-MM-dd HH:mm:ss}] {entry.EntryType} " +
                                 $"{entry.Source}: {entry.Message}");
            }
        }
        catch (Exception ex)
        {
            // Olay günlüğü okuma yetki/politika nedeniyle engellenebilir; paket üretimi
            // bu yüzden başarısız olmamalı.
            sb.AppendLine(c, $"Olay günlüğü okunamadı: {ex.GetType().Name}: {ex.Message}");
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
