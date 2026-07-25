using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.App.Infrastructure;

/// <summary>
/// GuardPipeClient — RealtimeGuard Windows servisine Named Pipe istemcisi.
/// Pipe: \\.\pipe\WinOptimizerGuard (servis tarafı <see cref="WinOptimizer.Service.GuardIpcServer"/>).
/// Komutlar: "status" / "metrics" / "alerts". Servis çalışmıyorsa zarif şekilde null döner.
/// (Master plan Bölüm 11.6 & 3.17 — servis↔UI IPC.)
/// </summary>
public sealed class GuardPipeClient
{
    private const string PipeName = "WinOptimizerGuard";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ILogger<GuardPipeClient> _logger;

    public GuardPipeClient(ILogger<GuardPipeClient> logger) => _logger = logger;

    /// <summary>Servise bir istek gönderir; yanıtı ham metin olarak döndürür. Bağlantı yoksa null.</summary>
    public async Task<string?> SendAsync(string request, CancellationToken ct = default)
    {
        try
        {
            await using var client = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(1500, ct);

            using var sr = new StreamReader(client);
            await using var sw = new StreamWriter(client) { AutoFlush = true };
            await sw.WriteLineAsync(request.AsMemory(), ct);
            return await sr.ReadLineAsync(ct);
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Guard pipe bağlantısı kurulamadı (servis çalışmıyor olabilir).");
            return null;
        }
    }

    /// <summary>Servis çalışıyor mu?</summary>
    public async Task<bool> IsServiceRunningAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync("status", ct);
        return !string.IsNullOrEmpty(resp) && resp.Contains("running", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Servisten son metrik örneğini çeker; yoksa null.</summary>
    public async Task<LiveMetric?> GetMetricsAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync("metrics", ct);
        if (string.IsNullOrWhiteSpace(resp) || resp.Trim() == "null") return null;
        try
        {
            return JsonSerializer.Deserialize<LiveMetric>(resp, JsonOpts);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Guard metrik JSON ayrıştırılamadı.");
            return null;
        }
    }
}