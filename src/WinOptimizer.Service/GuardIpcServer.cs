using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
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
    private readonly Func<object> _configProvider;
    private readonly ILogger<GuardIpcServer> _logger;
    private CancellationTokenSource? _cts;

    public GuardIpcServer(
        Func<GuardMetric?> latestMetricProvider,
        Func<IReadOnlyList<GuardAlert>> alertsProvider,
        Func<object> configProvider,
        ILogger<GuardIpcServer> logger)
    {
        _latestMetricProvider = latestMetricProvider;
        _alertsProvider = alertsProvider;
        _configProvider = configProvider;
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
                await using var server = CreateSecuredServer();
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

    /// <summary>
    /// Pipe sunucusunu <b>açık DACL ile</b> oluşturur: yalnızca SYSTEM ve Administrators.
    /// </summary>
    /// <remarks>
    /// Varsayılan (DACL'siz) kurulumda pipe'ın erişimi çağıran token'ından miras alınır ve
    /// iki risk doğar: (1) yerel herhangi bir süreç <c>metrics</c>/<c>alerts</c> okuyabilir,
    /// (2) daha önemlisi, servis başlamadan önce yükseltilmemiş bir süreç bu adı kapıp
    /// <b>yükseltilmiş</b> arayüze saldırganın belirlediği JSON'u besleyebilir.
    /// Administrators yeterlidir çünkü uygulama <c>requireAdministrator</c> ile çalışır.
    /// </remarks>
    private NamedPipeServerStream CreateSecuredServer()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            _pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            security);
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
            // Servisin GERÇEKTEN yüklediği yapılandırma. Arayüzün "kaydettiğini sandığı"
            // değil bu okunur — ayar dosyası ile servis davranışı arasındaki farkı
            // görebilmek, bu özelliğin en faydalı hata ayıklama imkânıdır.
            "config" => JsonSerializer.Serialize(_configProvider(), opts),
            _ => JsonSerializer.Serialize(new { error = "Bilinmeyen sorgu: " + request }, opts)
        };
    }
}
