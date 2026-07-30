using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Updater;

/// <summary>
/// GitHub Releases API üzerinden güncelleme bildirimi çeker (master plan Bölüm 20.6).
/// Stable: <c>/releases/latest</c>; Beta: <c>/releases</c> listesinden ilk ön sürüm.
/// </summary>
/// <remarks>
/// Dağıtım biçimi Inno Setup <c>setup.exe</c>'dir (MSI hattı kaldırıldı), bu yüzden
/// aranan asset <c>*-setup.exe</c>'dir. Yanındaki <c>*-setup.exe.sha256</c> asset'i
/// de indirilir: imzasız dağıtımda paketin bütünlüğünü doğrulamanın tek yolu bu hash'tir.
/// </remarks>
public sealed class UpdateChecker
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
    });

    /// <summary><c>sha256sum</c> satırında hash ile dosya adını ayıran karakterler (CA1861).</summary>
    private static readonly char[] ShaSeparators = [' ', '\t'];

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

    /// <summary>
    /// Yeni sürüm var mı denetler. Denetim yapılamazsa sonuçta
    /// <see cref="UpdateCheckResult.CheckFailed"/> işaretlenir — "güncel" DEĞİL.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        UpdateManifest? latest;
        string? failure;
        try
        {
            (latest, failure) = await FetchLatestAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger?.LogWarning(ex, "Güncelleme kontrolünde ağ hatası.");
            return new UpdateCheckResult(
                IsUpdateAvailable: false, Latest: null, _options.CurrentVersionValue,
                CheckFailed: true, FailureReason: ex.Message);
        }

        if (failure is not null)
        {
            _logger?.LogWarning("Güncelleme kontrolü yapılamadı: {Reason}", failure);
            return new UpdateCheckResult(
                IsUpdateAvailable: false, Latest: null, _options.CurrentVersionValue,
                CheckFailed: true, FailureReason: failure);
        }

        bool available = latest is not null && latest.Version > _options.CurrentVersionValue;
        _logger?.LogInformation("Güncelleme kontrolü: {Latest} — mevcut {Current} → güncelleme={Avail}",
            latest?.ToSummary() ?? "yok", _options.CurrentVersionValue, available);
        return new UpdateCheckResult(available, latest, _options.CurrentVersionValue);
    }

    /// <summary>
    /// En son yayını çeker. Başarısızlıkta manifest <c>null</c> ve ikinci öğe kullanıcıya
    /// gösterilebilir bir sebep olur (asla sessizce "güncelleme yok"a düşmez).
    /// </summary>
    private async Task<(UpdateManifest? Manifest, string? Failure)> FetchLatestAsync(CancellationToken ct)
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
            return (null, string.Format(
                CultureInfo.InvariantCulture,
                "GitHub API yanıtı: {0} ({1}/{2})",
                (int)resp.StatusCode, _options.Owner, _options.Repo));
        }

        JsonElement release;
        await using (var stream = await resp.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false))
        {
            if (_options.Channel == UpdateChannel.Beta)
            {
                var releases = await JsonSerializer
                    .DeserializeAsync<List<JsonElement>>(stream, cancellationToken: cts.Token)
                    .ConfigureAwait(false);
                JsonElement? beta = releases?.FirstOrDefault(r =>
                    r.TryGetProperty("prerelease", out var p) && p.GetBoolean());
                if (!beta.HasValue)
                {
                    return (null, "Beta kanalında ön sürüm bulunamadı.");
                }

                release = beta.Value;
            }
            else
            {
                release = await JsonSerializer
                    .DeserializeAsync<JsonElement>(stream, cancellationToken: cts.Token)
                    .ConfigureAwait(false);
            }
        }

        var (manifest, failure) = ParseRelease(release);
        if (manifest is null)
        {
            return (null, failure);
        }

        // SHA256 yan dosyasını çek. Bulunamazsa güncelleme yine sunulur ama hash boş kalır
        // ve UpdateVerifier bütünlük kontrolünü atlar — bu durumu görünür şekilde logla.
        string sha = await FetchSidecarShaAsync(release, manifest.DownloadUrl, cts.Token).ConfigureAwait(false);
        if (string.IsNullOrEmpty(sha))
        {
            _logger?.LogWarning(
                "Release'de .sha256 yan dosyası yok — indirilen paketin bütünlüğü doğrulanamayacak.");
        }

        return (manifest with { Sha256 = sha }, null);
    }

    /// <summary>
    /// <c>&lt;setup&gt;.exe.sha256</c> asset'ini indirir ve 64 hex hash'i ayıklar.
    /// Dosya biçimi <c>sha256sum</c> uyumludur: <c>"&lt;hash&gt;  &lt;dosyaadı&gt;"</c>.
    /// </summary>
    private async Task<string> FetchSidecarShaAsync(
        JsonElement release, string setupUrl, CancellationToken ct)
    {
        string setupName = setupUrl[(setupUrl.LastIndexOf('/') + 1)..];
        string sidecarName = setupName + ".sha256";
        string sidecarUrl = string.Empty;

        if (release.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                if (name.Equals(sidecarName, StringComparison.OrdinalIgnoreCase))
                {
                    sidecarUrl = asset.TryGetProperty("browser_download_url", out var u)
                        ? u.GetString() ?? string.Empty : string.Empty;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(sidecarUrl))
        {
            return string.Empty;
        }

        try
        {
            string content = await HttpClient.GetStringAsync(sidecarUrl, ct).ConfigureAwait(false);
            return ExtractSha256(content);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Hash indirilemedi: güncellemeyi tamamen engellemek yerine hash'siz devam edilir,
            // ama sessiz kalınmaz (çağıran kullanıcıya doğrulanamadığını söyleyebilir).
            _logger?.LogWarning(ex, "SHA256 yan dosyası indirilemedi: {Url}", sidecarUrl);
            return string.Empty;
        }
    }

    /// <summary><c>sha256sum</c> biçimli metinden 64 karakterlik hash'i ayıklar.</summary>
    public static string ExtractSha256(string sidecarContent)
    {
        if (string.IsNullOrWhiteSpace(sidecarContent))
        {
            return string.Empty;
        }

        string first = sidecarContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)[0]
            .Trim()
            .Split(ShaSeparators, StringSplitOptions.RemoveEmptyEntries)[0];

        return first.Length == 64 && first.All(Uri.IsHexDigit) ? first : string.Empty;
    }

    /// <summary>
    /// Yayın nesnesini manifeste dönüştürür. x64 <c>setup.exe</c> asset'i yoksa istisna
    /// FIRLATMAZ — çağırana gösterilebilir bir sebep döner (eskiden burada atılan
    /// <c>InvalidOperationException</c> CLI'da çıplak yığın izi olarak yüzeye çıkıyordu).
    /// </summary>
    private static (UpdateManifest? Manifest, string? Failure) ParseRelease(JsonElement release)
    {
        string tag = release.TryGetProperty("tag_name", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        Version version = ParseVersion(tag);
        bool prerelease = release.TryGetProperty("prerelease", out var p) && p.GetBoolean();
        string notes = release.TryGetProperty("body", out var b) ? b.GetString() ?? string.Empty : string.Empty;
        DateTimeOffset published = release.TryGetProperty("published_at", out var d) && d.TryGetDateTimeOffset(out var dto)
            ? dto : DateTimeOffset.UtcNow;

        // x64 setup.exe asset'ini bul (arm64 olanı atla).
        string downloadUrl = string.Empty;
        if (release.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                if (IsSetupAssetName(name))
                {
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var u)
                        ? u.GetString() ?? string.Empty : string.Empty;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            return (null, $"Yayında (v{version}) x64 setup.exe asset'i bulunamadı.");
        }

        // Sha256 yan dosyadan doldurulur (bkz. FetchSidecarShaAsync).
        return (new UpdateManifest(version, downloadUrl, Sha256: string.Empty, notes, published, prerelease), null);
    }

    /// <summary>Asset adı x64 kurulum paketi mi? (<c>*-setup.exe</c>, arm64 hariç.)</summary>
    public static bool IsSetupAssetName(string name) =>
        name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("arm64", StringComparison.OrdinalIgnoreCase);

    /// <summary>Sürüm etiketini <see cref="Version"/>a ayrıştırır: baştaki 'v' ve ön sürüm ekini atlar.</summary>
    public static Version ParseVersion(string tag)
    {
        string s = tag.TrimStart('v', 'V');
        int dash = s.IndexOf('-');
        if (dash >= 0) s = s[..dash];
        return Version.TryParse(s, out var v) ? v : new Version(0, 0, 0, 0);
    }
}
