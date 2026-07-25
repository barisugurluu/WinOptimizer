using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Updater;

/// <summary>
/// GitHub Releases API üzerinden güncelleme bildirimi çeker (master plan Bölüm 20.6).
/// Stable: <c>/releases/latest</c>; Beta: <c>/releases</c> listesinden ilk ön sürüm.
/// </summary>
public sealed class UpdateChecker
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
    });

    private readonly UpdateOptions _options;
    private readonly ILogger<UpdateChecker>? _logger;

    static UpdateChecker()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("WinOptimizer-Updater", "1.0"));
        HttpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public UpdateChecker(UpdateOptions options, ILogger<UpdateChecker>? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>Yeni sürüm var mı denetler. Ağ hatasında <c>IsUpdateAvailable=false</c>.</summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        UpdateManifest? latest;
        try
        {
            latest = await FetchLatestAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger?.LogWarning(ex, "Güncelleme kontrolünde ağ hatası.");
            return new UpdateCheckResult(IsUpdateAvailable: false, Latest: null, _options.CurrentVersionValue);
        }

        bool available = latest is not null && latest.Version > _options.CurrentVersionValue;
        _logger?.LogInformation("Güncelleme kontrolü: {Latest} — mevcut {Current} → güncelleme={Avail}",
            latest?.ToSummary() ?? "yok", _options.CurrentVersionValue, available);
        return new UpdateCheckResult(available, latest, _options.CurrentVersionValue);
    }

    private async Task<UpdateManifest?> FetchLatestAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_options.TimeoutValue);

        string url = _options.Channel == UpdateChannel.Beta
            ? $"https://api.github.com/repos/{_options.Owner}/{_options.Repo}/releases"
            : $"https://api.github.com/repos/{_options.Owner}/{_options.Repo}/releases/latest";

        using var resp = await HttpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
        _logger?.LogDebug("GitHub API {Url} → {Status}", url, resp.StatusCode);
        if (!resp.IsSuccessStatusCode)
        {
            _logger?.LogWarning("GitHub API başarısız: {Status}", resp.StatusCode);
            return null;
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        if (_options.Channel == UpdateChannel.Beta)
        {
            var releases = await JsonSerializer.DeserializeAsync<List<JsonElement>>(stream, cancellationToken: cts.Token)
                .ConfigureAwait(false);
            JsonElement? beta = releases?.FirstOrDefault(r =>
                r.TryGetProperty("prerelease", out var p) && p.GetBoolean());
            return beta.HasValue ? ParseRelease(beta.Value) : null;
        }

        var latest = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: cts.Token)
            .ConfigureAwait(false);
        return ParseRelease(latest);
    }

    private static UpdateManifest ParseRelease(JsonElement release)
    {
        string tag = release.TryGetProperty("tag_name", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        Version version = ParseVersion(tag);
        bool prerelease = release.TryGetProperty("prerelease", out var p) && p.GetBoolean();
        string notes = release.TryGetProperty("body", out var b) ? b.GetString() ?? string.Empty : string.Empty;
        DateTimeOffset published = release.TryGetProperty("published_at", out var d) && d.TryGetDateTimeOffset(out var dto)
            ? dto : DateTimeOffset.UtcNow;

        // x64 MSI asset'ini bul (arm64 olanı atla).
        string downloadUrl = string.Empty;
        if (release.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var u)
                        ? u.GetString() ?? string.Empty : string.Empty;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
            throw new InvalidOperationException("Release'de x64 MSI asset bulunamadı.");

        // SHA-256 release içinde ayrıca yayımlanmadıkça bilinmez → boş (Verify best-effort atlar).
        return new UpdateManifest(version, downloadUrl, Sha256: string.Empty, notes, published, prerelease);
    }

    /// <summary>Sürüm etiketini <see cref="Version"/>a ayrıştırır: baştaki 'v' ve ön sürüm ekini atlar.</summary>
    public static Version ParseVersion(string tag)
    {
        string s = tag.TrimStart('v', 'V');
        int dash = s.IndexOf('-');
        if (dash >= 0) s = s[..dash];
        return Version.TryParse(s, out var v) ? v : new Version(0, 0, 0, 0);
    }
}