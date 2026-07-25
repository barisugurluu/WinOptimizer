using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.App.Infrastructure;
using WinOptimizer.Modules.CleanEngine;
using WinOptimizer.Safety;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Genel Bakış sekmesi görünüm modeli — canlı CPU/RAM/Disk metrikleri, servis durumu ve
/// son etkinlik özeti. Öncelikle RealtimeGuard servisinden (pipe) alır; servis yoksa
/// WMI fallback. <see cref="StartPolling"/>/c> ile DispatcherTimer başlatılır.
/// </summary>
public partial class OverviewViewModel : ObservableObject
{
    private readonly LiveMetricsProvider _metrics;
    private readonly ChangeJournal _journal;
    private DispatcherTimer? _timer;

    [ObservableProperty] private bool _isServiceRunning;
    [ObservableProperty] private string _serviceStatusText = "Servis durumu kontrol ediliyor…";
    [ObservableProperty] private int _cpuPercent;
    [ObservableProperty] private int _ramPercent;
    [ObservableProperty] private int _diskFreePercent;
    [ObservableProperty] private string _ramFreeText = "—";
    [ObservableProperty] private string _diskFreeText = "—";
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private string _activitySummary = "Etkinlik yükleniyor…";
    [ObservableProperty] private string _metricSourceText = string.Empty;

    public ObservableCollection<string> RecentActivity { get; } = new();

    public OverviewViewModel(LiveMetricsProvider metrics, ChangeJournal journal)
    {
        _metrics = metrics;
        _journal = journal;
    }

    /// <summary>Canlı metrik yoklamasını başlatır (sayfa yüklenince çağrılır).</summary>
    public void StartPolling(int intervalSeconds)
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds))
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        // İlk anlık görüntüyü hemen al.
        _ = RefreshAsync();
        _ = LoadActivityAsync();
    }

    /// <summary>Yoklamayı durdurur (sayfa kapatılınca çağrılır).</summary>
    public void StopPolling()
    {
        _timer?.Stop();
        _timer = null;
    }

    /// <summary>Servis durumu + metrikleri yeniler.</summary>
    public async Task RefreshAsync()
    {
        bool running = await _metrics.IsServiceRunningAsync();
        IsServiceRunning = running;
        ServiceStatusText = running
            ? "RealtimeGuard servisi çalışıyor"
            : "Servis çalışmıyor (yerel WMA ölçüm kullanılıyor)";

        var metric = await _metrics.GetAsync();
        if (metric is null)
        {
            MetricSourceText = "Metrik alınamadı";
            return;
        }

        CpuPercent = Math.Clamp(metric.CpuUsagePercent, 0, 100);
        RamPercent = Math.Clamp(metric.RamUsagePercent, 0, 100);
        DiskFreePercent = Math.Clamp(metric.CDriveFreePercent, 0, 100);

        CpuText = metric.CpuTemperatureC is int t ? $"%{metric.CpuUsagePercent} • {t}°C" : $"%{metric.CpuUsagePercent}";
        RamFreeText = $"{CleanEngineModule.FormatBytes(metric.RamFreeBytes)} boş";
        DiskFreeText = $"{CleanEngineModule.FormatBytes(metric.CDriveFreeBytes)} boş";

        MetricSourceText = running ? "Kaynak: RealtimeGuard servisi" : "Kaynak: yerel WMI";

        if (metric.SmartFailurePredicted)
            ServiceStatusText = "⚠ SMART disk arıza uyarısı!";
        if (metric.BatteryPercent is int batt)
            DiskFreeText += $" • Pil %{batt}";
    }

    /// <summary>Son 3 günün change journal kayıtlarından etkinlik özeti çıkarır.</summary>
    private async Task LoadActivityAsync()
    {
        RecentActivity.Clear();
        int total = 0;
        for (int i = 0; i < 3; i++)
        {
            var records = await _journal.ReadDayAsync(DateTime.UtcNow.AddDays(-i));
            total += records.Count;
            foreach (var r in records)
            {
                RecentActivity.Add($"[{r.Timestamp.LocalDateTime:dd.MM HH:mm}] {r.Module}: {r.Operation} — {Truncate(r.Target, 50)}");
                if (RecentActivity.Count >= 8) break;
            }
            if (RecentActivity.Count >= 8) break;
        }
        ActivitySummary = total > 0
            ? $"Son 3 günde {total} değişiklik kaydedildi."
            : "Son 3 günde değişiklik kaydı yok.";
    }

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
