using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.App.Infrastructure;
using WinOptimizer.App.Resources;
using WinOptimizer.Orchestration;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Ayarlar sekmesi görünüm modeli — dil, tema, SafetyNet ve RealtimeGuard eşik ayarları.
/// <see cref="SettingsService.Current"/> üzerinde çalışan kopya düzenler; "Kaydet" diske yazar.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly DiagnosticsPackageBuilder _diagnostics;

    /// <summary>Seçilebilir diller (değer → etiket).</summary>
    public ObservableCollection<KeyValuePair<string, string>> Languages { get; } = new()
    {
        new("tr-TR", "Türkçe"),
        new("en-US", "English"),
    };

    /// <summary>Seçilebilir temalar.</summary>
    public ObservableCollection<KeyValuePair<string, string>> Themes { get; } = new()
    {
        new("dark", "Koyu (Dark)"),
        new("light", "Açık (Light)"),
    };

    [ObservableProperty] private KeyValuePair<string, string> _selectedLanguage;
    [ObservableProperty] private KeyValuePair<string, string> _selectedTheme;
    [ObservableProperty] private bool _autoRestorePoint;
    [ObservableProperty] private bool _autoRegistryBackup;
    [ObservableProperty] private bool _requireConfirmationForHighRisk;
    [ObservableProperty] private bool _guardEnabled;
    [ObservableProperty] private int _ramUsageThreshold;
    [ObservableProperty] private int _diskFreeThreshold;
    [ObservableProperty] private int _diskFreeCriticalThreshold;
    [ObservableProperty] private int _cpuPerProcessThreshold;
    [ObservableProperty] private int _tempThreshold;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasStatus;
    /// <summary>Dışa aktarma sürerken butonu devre dışı bırakır (komut CanExecute'una bağlı).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportDiagnosticsCommand))]
    private bool _isExportingDiagnostics;

    public SettingsViewModel(SettingsService settings, DiagnosticsPackageBuilder diagnostics)
    {
        _settings = settings;
        _diagnostics = diagnostics;
        LoadFromCurrent();
    }

    /// <summary>Ayarları görünüm alanına yükler (yeniden yükleme/düzeltme için).</summary>
    private void LoadFromCurrent()
    {
        var c = _settings.Current;
        SelectedLanguage = Languages.FirstOrDefault(kv => kv.Key == c.Language, Languages[0]);
        SelectedTheme = Themes.FirstOrDefault(kv => kv.Key == c.Theme, Themes[0]);
        AutoRestorePoint = c.SafetyNet.AutoRestorePoint;
        AutoRegistryBackup = c.SafetyNet.AutoRegistryBackup;
        RequireConfirmationForHighRisk = c.SafetyNet.RequireConfirmationForHighRisk;
        GuardEnabled = c.RealtimeGuard.Enabled;
        RamUsageThreshold = c.RealtimeGuard.Thresholds.RamUsagePercent;
        DiskFreeThreshold = c.RealtimeGuard.Thresholds.DiskFreePercent;
        DiskFreeCriticalThreshold = c.RealtimeGuard.Thresholds.DiskFreeCriticalPercent;
        CpuPerProcessThreshold = c.RealtimeGuard.Thresholds.CpuPerProcessPercent;
        TempThreshold = c.RealtimeGuard.Thresholds.TempCelsius;
    }

    /// <summary>Düzenlenen değerleri ayar modeline yazar ve diske kaydeder.</summary>
    [RelayCommand]
    private void Save()
    {
        var c = _settings.Current;
        bool languageChanged = !string.Equals(c.Language, SelectedLanguage.Key, StringComparison.Ordinal);
        c.Language = SelectedLanguage.Key;
        c.Theme = SelectedTheme.Key;
        c.SafetyNet.AutoRestorePoint = AutoRestorePoint;
        c.SafetyNet.AutoRegistryBackup = AutoRegistryBackup;
        c.SafetyNet.RequireConfirmationForHighRisk = RequireConfirmationForHighRisk;
        c.RealtimeGuard.Enabled = GuardEnabled;
        c.RealtimeGuard.Thresholds.RamUsagePercent = RamUsageThreshold;
        c.RealtimeGuard.Thresholds.DiskFreePercent = DiskFreeThreshold;
        c.RealtimeGuard.Thresholds.DiskFreeCriticalPercent = DiskFreeCriticalThreshold;
        c.RealtimeGuard.Thresholds.CpuPerProcessPercent = CpuPerProcessThreshold;
        c.RealtimeGuard.Thresholds.TempCelsius = TempThreshold;

        if (!_settings.Save())
        {
            ShowStatus(Strings.SettingsSaveFailed);
            return;
        }

        // Tema anında uygulanır; dil ise XAML x:Static bağları sayfa kurulumunda bir kez
        // çözüldüğü için yeniden başlatma ister. Canlı geçişi TAKLİT ETME.
        ThemeBootstrap.Apply(c.Theme);

        ShowStatus(languageChanged ? Strings.SettingsSavedRestartRequired : Strings.SettingsSaved);
    }

    /// <summary>Düzenlemeleri atar, kayıtlı değerlere döner.</summary>
    [RelayCommand]
    private void Reset()
    {
        _settings.Load();
        LoadFromCurrent();
        ShowStatus(Strings.SettingsReverted);
    }

    /// <summary>
    /// Teşhis paketini oluşturur ve klasörünü açar (master plan Bölüm 19.5).
    /// Gönüllüdür: paket yalnızca diske yazılır, hiçbir yere gönderilmez — telemetri yoktur.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportDiagnostics))]
    private async Task ExportDiagnosticsAsync()
    {
        IsExportingDiagnostics = true;
        try
        {
            var result = await _diagnostics.CreateAsync();
            ShowStatus(Strings.DiagnosticsExported(result.FilePath));
            RevealInExplorer(result.FilePath);
        }
        catch (Exception ex)
        {
            ShowStatus(Strings.Error(ex.Message));
        }
        finally
        {
            IsExportingDiagnostics = false;
        }
    }

    private bool CanExportDiagnostics() => !IsExportingDiagnostics;

    /// <summary>Üretilen paketi Dosya Gezgini'nde seçili olarak gösterir.</summary>
    private static void RevealInExplorer(string filePath) =>
        Infrastructure.ExplorerReveal.SelectFile(filePath);

    private void ShowStatus(string msg)
    {
        StatusMessage = msg;
        HasStatus = true;
    }
}
