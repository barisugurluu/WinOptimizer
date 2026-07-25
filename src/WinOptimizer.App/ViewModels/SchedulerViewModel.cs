using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.App.Resources;
using WinOptimizer.Orchestration;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Zamanlayıcı sekmesi görünüm modeli — haftalık otomatik bakım görevini (Görev Zamanlayıcı)
/// oluşturur/siler/denetler. CLI exe yolunu uygulama dizininden çözümler.
/// (Master plan Bölüm 8 zamanlama — SchedulerService sarmalayıcısı.)
/// </summary>
public partial class SchedulerViewModel : ObservableObject
{
    private readonly SchedulerService _scheduler;

    /// <summary>Seçilebilir günler (İngilizce ad — schtasks /d parametresi).</summary>
    public ObservableCollection<string> Days { get; } = new()
    {
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
    };

    [ObservableProperty] private bool _weeklyEnabled = true;
    [ObservableProperty] private string _selectedDay = "Sunday";
    [ObservableProperty] private string _selectedTime = "03:00";
    [ObservableProperty] private string _cliPath = string.Empty;
    [ObservableProperty] private bool _taskExists;
    [ObservableProperty] private string _taskStatusText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _isBusy;

    public SchedulerViewModel(SchedulerService scheduler)
    {
        _scheduler = scheduler;
        _cliPath = ResolveCliPath();
        _ = RefreshAsync();
    }

    /// <summary>WinOptimizer.Cli.exe yolunu uygulama dizininde arar (kurulum yanında).</summary>
    private static string ResolveCliPath()
    {
        string dir = AppContext.BaseDirectory;
        string exe = Path.Combine(dir, "WinOptimizer.Cli.exe");
        if (File.Exists(exe)) return exe;
        // Geliştirme/derleme senaryosu: net8.0-windows yanında olmayabilir; en iyi tahmin.
        return exe;
    }

    /// <summary>Zamanlanmış görevin varlığını denetler.</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        TaskExists = await Task.Run(() => _scheduler.IsTaskExists());
        TaskStatusText = TaskExists ? Strings.SchedulerTaskExists : Strings.SchedulerTaskMissing;
    }

    /// <summary>Haftalık görevi oluşturur (veya günceller).</summary>
    [RelayCommand]
    private async Task CreateTaskAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            bool ok = await Task.Run(() => _scheduler.CreateWeeklyTask(SelectedDay, SelectedTime, CliPath));
            ShowStatus(ok ? Strings.TaskCreated : Strings.TaskCreateFailed);
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    /// <summary>Zamanlanmış görevi siler.</summary>
    [RelayCommand]
    private async Task DeleteTaskAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            bool ok = await Task.Run(() => _scheduler.DeleteWeeklyTask());
            ShowStatus(ok ? Strings.TaskDeleted : Strings.TaskDeleteFailed);
            await RefreshAsync();
        }
        finally { IsBusy = false; }
    }

    private void ShowStatus(string msg)
    {
        StatusMessage = msg;
        HasStatus = true;
    }
}
