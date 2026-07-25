using System.Threading;

namespace WinOptimizer.Service;

/// <summary>
/// Worker ve IPC sunucusu arasında paylaşılan durum (en son metrik + uyarılar).
/// Thread-safe erişim için lock kullanır.
/// </summary>
public sealed class GuardState
{
    private GuardMetric? _metric;
    private IReadOnlyList<GuardAlert> _alerts = Array.Empty<GuardAlert>();
    private readonly object _lock = new();

    public void Update(GuardMetric? metric, IReadOnlyList<GuardAlert> alerts)
    {
        lock (_lock)
        {
            _metric = metric;
            _alerts = alerts;
        }
    }

    public GuardMetric? GetMetric()
    {
        lock (_lock) return _metric;
    }

    public IReadOnlyList<GuardAlert> GetAlerts()
    {
        lock (_lock) return _alerts;
    }
}
