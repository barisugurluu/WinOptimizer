using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.App.Resources;
using WinOptimizer.Orchestration;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Guard sekmesi görünüm modeli — RealtimeGuard hizmetini kur/başlat/durdur/kaldır/onar.
/// </summary>
/// <remarks>
/// Bu ekran olmadan kullanıcının servisi kurmak/onarmak için hiçbir yolu yoktu: kurulum
/// sihirbazındaki (artık varsayılan kapalı) kutuyu kaçırdıysa, tek sinyal Genel Bakış
/// sekmesindeki "servis çalışmıyor" satırıydı ve yapabileceği bir şey yoktu.
/// </remarks>
public partial class GuardViewModel : ObservableObject
{
    private readonly GuardServiceController _controller;
    private readonly SettingsService _settings;

    [ObservableProperty] private bool _guardEnabled;
    [ObservableProperty] private bool _autoRemediate;
    [ObservableProperty] private bool _autoTrimRam;
    [ObservableProperty] private bool _autoCleanDiskCritical;
    [ObservableProperty] private bool _autoUpdateDefenderSignatures;

    [ObservableProperty] private GuardServiceState _state = GuardServiceState.Unknown;
    [ObservableProperty] private string _stateText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _serviceExePath = string.Empty;
    [ObservableProperty] private bool _isServiceExeMissing;

    public GuardViewModel(GuardServiceController controller, SettingsService settings)
    {
        _controller = controller;
        _settings = settings;

        var guard = settings.Current.RealtimeGuard;
        GuardEnabled = guard.Enabled;
        AutoRemediate = guard.AutoRemediate;
        AutoTrimRam = guard.AutoTrimRam;
        AutoCleanDiskCritical = guard.AutoCleanDiskCritical;
        AutoUpdateDefenderSignatures = guard.AutoUpdateDefenderSignatures;

        string? exe = GuardServiceController.ResolveServiceExePath();
        // ResolveServiceExePath dosya yoksa null döner; "en iyi tahmin" yol döndürüp sonra
        // başarı bildirmek yasak — kullanıcıya eksik dosyanın yolu gösterilip düğmeler kapatılır.
        IsServiceExeMissing = exe is null;
        ServiceExePath = exe ?? Path.Combine(AppContext.BaseDirectory, "WinOptimizer.Service.exe");
        Refresh();
    }

    /// <summary>Servis kurulu mu (başlat/durdur/kaldır için ön koşul).</summary>
    public bool IsInstalled => State is not (GuardServiceState.NotInstalled or GuardServiceState.Unknown);

    /// <summary>Kur düğmesi etkin mi.</summary>
    public bool CanInstall => !IsBusy && !IsServiceExeMissing && State == GuardServiceState.NotInstalled;

    /// <summary>Başlat düğmesi etkin mi.</summary>
    public bool CanStart => !IsBusy && State == GuardServiceState.Stopped;

    /// <summary>Durdur düğmesi etkin mi.</summary>
    public bool CanStop => !IsBusy && State == GuardServiceState.Running;

    /// <summary>Kaldır/Onar düğmeleri etkin mi.</summary>
    public bool CanModify => !IsBusy && !IsServiceExeMissing && IsInstalled;

    /// <summary>Durumu yeniden okur ve düğme etkinliklerini bildirir.</summary>
    public void Refresh()
    {
        State = _controller.GetState();
        StateText = Describe(State);
        NotifyButtonStates();
    }

    [RelayCommand]
    private void RefreshState() => Refresh();

    /// <summary>
    /// Guard ayarlarını kaydeder. Servis dosyayı 5 sn içinde okuyup uygular —
    /// yeniden başlatma gerekmez.
    /// </summary>
    [RelayCommand]
    private void SaveSettings()
    {
        var guard = _settings.Current.RealtimeGuard;
        guard.Enabled = GuardEnabled;
        guard.AutoRemediate = AutoRemediate;
        guard.AutoTrimRam = AutoTrimRam;
        guard.AutoCleanDiskCritical = AutoCleanDiskCritical;
        guard.AutoUpdateDefenderSignatures = AutoUpdateDefenderSignatures;

        StatusMessage = _settings.Save() ? Strings.GuardSettingsSaved : Strings.SettingsSaveFailed;
        HasStatus = true;
    }

    [RelayCommand]
    private Task InstallAsync() => RunAsync(ct => _controller.InstallAsync(ct));

    [RelayCommand]
    private Task StartAsync() => RunAsync(ct => _controller.StartAsync(ct));

    [RelayCommand]
    private Task StopAsync() => RunAsync(ct => _controller.StopAsync(ct));

    [RelayCommand]
    private Task UninstallAsync() => RunAsync(ct => _controller.UninstallAsync(ct));

    [RelayCommand]
    private Task RepairAsync() => RunAsync(ct => _controller.RepairAsync(ct));

    private async Task RunAsync(Func<CancellationToken, Task<bool>> operation)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        NotifyButtonStates();
        try
        {
            bool ok = await operation(CancellationToken.None);
            StatusMessage = ok ? Strings.GuardOpSucceeded : Strings.GuardOpFailed;
            HasStatus = true;
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    private void NotifyButtonStates()
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanModify));
        InstallCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
        RepairCommand.NotifyCanExecuteChanged();
    }

    private static string Describe(GuardServiceState state) => state switch
    {
        GuardServiceState.NotInstalled => Strings.GuardStateNotInstalled,
        GuardServiceState.Stopped => Strings.GuardStateStopped,
        GuardServiceState.StartPending => Strings.GuardStateStartPending,
        GuardServiceState.StopPending => Strings.GuardStateStopPending,
        GuardServiceState.Running => Strings.GuardStateRunning,
        _ => Strings.GuardStateUnknown,
    };
}
