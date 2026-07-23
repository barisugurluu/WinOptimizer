using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Service;

/// <summary>
/// RealtimeGuardWorker — arka plan döngüsü. Her poll aralığında metrik toplar,
/// eşikleri değerlendirir ve <see cref="GuardState"/> üzerinden uyarıları saklar.
/// Kaynak hedefi: &lt;30 MB RAM, &lt;%1 CPU (master plan Bölüm 3.17).
/// </summary>
public sealed class RealtimeGuardWorker : BackgroundService
{
    private readonly MetricsCollector _collector;
    private readonly ThresholdEngine _engine;
    private readonly GuardIpcServer _ipc;
    private readonly GuardState _state;
    private readonly RemediationEngine _remediation;
    private readonly ILogger<RealtimeGuardWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public RealtimeGuardWorker(
        MetricsCollector collector,
        ThresholdEngine engine,
        GuardIpcServer ipc,
        GuardState state,
        RemediationEngine remediation,
        ILogger<RealtimeGuardWorker> logger)
    {
        _collector = collector;
        _engine = engine;
        _ipc = ipc;
        _state = state;
        _remediation = remediation;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RealtimeGuard başlatıldı. Poll aralığı: {Sec} sn.", _pollInterval.TotalSeconds);

        var ipcTask = Task.Run(() => _ipc.RunAsync(stoppingToken), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var metric = _collector.Collect();
                var alerts = _engine.Evaluate(metric);
                _state.Update(metric, alerts);

                foreach (var alert in alerts.Where(a => a.Severity == AlertSeverity.Critical))
                    _logger.LogCritical("[{Metric}] {Message} — {Action}",
                        alert.Metric, alert.Message, alert.RecommendedAction);
                foreach (var alert in alerts.Where(a => a.Severity == AlertSeverity.Warning))
                    _logger.LogWarning("[{Metric}] {Message}", alert.Metric, alert.Message);

                // Otomatik müdahale: güvenli eylemleri uygula (Bölüm 3.17)
                int remediated = await _remediation.ApplyAsync(alerts);
                if (remediated > 0)
                    _logger.LogInformation("{N} otomatik müdahale uygulandı.", remediated);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Metrik toplama hatası."); }

            try { await Task.Delay(_pollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        await ipcTask;
    }
}
