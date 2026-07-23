using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Service;

/// <summary>
/// GuardIpcServer — Named Pipe IPC sunucusu (servis ↔ UI).
/// Pipe adı: \\.\pipe\WinOptimizerGuard (master plan Bölüm 11.6 & 3.17).
/// UI'dan gelen JSON sorguları yanıtlar: "status", "metrics", "alerts".
/// </summary>
public sealed class GuardIpcServer
{
    private readonly string _pipeName = "WinOptimizerGuard";
    private readonly Func<GuardMetric?> _latestMetricProvider;
    private readonly Func<IReadOnlyList<GuardAlert>> _alertsProvider;
    private readonly ILogger<GuardIpcServer> _logger;
    private CancellationTokenSource? _cts;

    public GuardIpcServer(
        Func<GuardMetric?> latestMetricProvider,
        Func<IReadOnlyList<GuardAlert>> alertsProvider,
        ILogger<GuardIpcServer> logger)
    {
        _latestMetricProvider = latestMetricProvider;
        _alertsProvider = alertsProvider;
        _logger = logger;
    }

    /// <summary>Pipe sunucusunu başlatır; bağlantıları kabul eder.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _logger.LogInformation("GuardIpcServer başlatıldı: {Pipe}", _pipeName);

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(_pipeName,
                    PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cts.Token);

                using var sr = new StreamReader(server);
                await using var sw = new StreamWriter(server) { AutoFlush = true };
                var request = await sr.ReadLineAsync(_cts.Token);
                var response = HandleRequest(request ?? string.Empty);
                await sw.WriteLineAsync(response.AsMemory(), _cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "IPC bağlantı hatası (istemci bağlantısı kesilmiş olabilir).");
            }
        }
    }

    private string HandleRequest(string request)
    {
        var opts = new JsonSerializerOptions { WriteIndented = false };
        return request.ToLowerInvariant() switch
        {
            "status" => JsonSerializer.Serialize(new
            {
                status = "running",
                ts = DateTimeOffset.UtcNow
            }, opts),
            "metrics" => JsonSerializer.Serialize(_latestMetricProvider(), opts),
            "alerts" => JsonSerializer.Serialize(_alertsProvider(), opts),
            _ => JsonSerializer.Serialize(new { error = "Bilinmeyen sorgu: " + request }, opts)
        };
    }
}
