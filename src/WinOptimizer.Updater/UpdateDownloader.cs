using System.IO;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Updater;

/// <summary>
/// Güncelleme paketini akışlı (streaming) olarak indirir; ilerlemeyi raporlar.
/// Büyük MSI dosyalarını belleğe yüklemeden diske yazar.
/// </summary>
public sealed class UpdateDownloader
{
    private static readonly HttpClient HttpClient = new();
    private readonly ILogger<UpdateDownloader>? _logger;

    public UpdateDownloader(ILogger<UpdateDownloader>? logger = null) => _logger = logger;

    /// <summary>Paketi <paramref name="destinationPath"/>e indirir; <paramref name="progress"/> toplam bayt sayısını raporlar.</summary>
    public async Task DownloadAsync(string downloadUrl, string destinationPath,
        IProgress<long>? progress = null, CancellationToken ct = default)
    {
        using var resp = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        long total = resp.Content.Headers.ContentLength ?? -1;
        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var content = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            read += n;
            progress?.Report(read);
            _logger?.LogDebug("İndirme: {Read} bayt / {Total}", read, total > 0 ? total.ToString() : "?");
        }

        _logger?.LogInformation("İndirme tamam: {Path} ({Bytes} bayt)", destinationPath, read);
    }
}
