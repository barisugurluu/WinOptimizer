using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.Core;
using WinOptimizer.Modules.CleanEngine;
using WinOptimizer.Orchestration;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Panosu görünüm modeli — "Tek Tıkla En İyi Hale Getir" akışını yönetir
/// (master plan Bölüm 12.3 Akış A: Önizle → Onayla → Uygula).
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly JobOrchestrationEngine _engine;
    private readonly ModuleRegistry _registry;
    private readonly SettingsService _settings;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Yeni bir iptal kaynağı açar; bir öncekini serbest bırakır.
    /// (Doğrudan yeniden atama, her işlemde bir CancellationTokenSource sızdırıyordu.)
    /// </summary>
    private CancellationTokenSource StartNewOperation()
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        return _cts;
    }

    public DashboardViewModel(
        JobOrchestrationEngine engine, ModuleRegistry registry, SettingsService settings)
    {
        _engine = engine;
        _registry = registry;
        _settings = settings;
        UpdateStatus();
    }

    [ObservableProperty] private string _statusSummary = "Hazır.";
    [ObservableProperty] private string _analysisSummary = "Henüz analiz yapılmadı. \"Önizle\" ile başlayın.";
    [ObservableProperty] private string _resultSummary = string.Empty;
    [ObservableProperty] private string _availableModules = string.Empty;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasResult;

    public ObservableCollection<string> Log { get; } = new();

    /// <summary>Modül listesini ve durum metnini yeniler.</summary>
    private void UpdateStatus()
    {
        var mods = _registry.Modules;
        AvailableModules = mods.Count > 0
            ? $"{Resources.Strings.InstalledModules} {string.Join(", ", mods.Select(Resources.ModuleDisplayNameResolver.Resolve))}"
            : Resources.Strings.NoModules;
        StatusSummary = $"{mods.Count} {Resources.Strings.ModulesActive}";
    }

    /// <summary>"Önizle" — tüm modülleri analiz eder, kazanç özetini gösterir.</summary>
    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (IsBusy) return;
        BeginWork(Resources.Strings.Scanning);

        try
        {
            var analyses = await _engine.AnalyzeAllAsync(StartNewOperation().Token);

            long totalBytes = analyses.Values.Sum(a => a.TotalBytes);
            int totalItems = analyses.Values.Sum(a => a.ItemCount);
            AnalysisSummary = $"{totalItems:N0} / {CleanEngineModule.FormatBytes(totalBytes)} {Resources.Strings.AnalysisResult}";
            Log.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {Resources.Strings.Analyze}: {totalItems} öğe, {CleanEngineModule.FormatBytes(totalBytes)}.");
        }
        catch (OperationCanceledException)
        {
            Log.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {Resources.Strings.AnalysisCanceled}");
        }
        catch (Exception ex)
        {
            AnalysisSummary = Resources.Strings.Error(ex.Message);
        }
        finally
        {
            EndWork();
        }
    }

    /// <summary>"Uygula" — SafetyNet (geri yükleme noktası) + modülleri çalıştırır.</summary>
    [RelayCommand]
    private async Task OptimizeAsync()
    {
        if (IsBusy) return;

        // Kapsam artık ayarlardaki etkin modüllerle sınırlı; kullanıcı neyin çalışacağını
        // ÖNCEDEN görür. Riskli eylemler ayrıca eylem düzeyinde onay ister
        // (JobOrchestrationEngine → ConfirmationGate → ActionConfirmationDialog).
        var enabled = _settings.Current.EnabledModules;
        string scope = enabled.Count > 0 ? string.Join(", ", enabled) : "(hiçbiri)";
        var confirm = MessageBox.Show(
            $"{Resources.Strings.ConfirmMessage}{Environment.NewLine}{Environment.NewLine}" +
            $"Çalıştırılacak modüller ({enabled.Count}):{Environment.NewLine}{scope}{Environment.NewLine}{Environment.NewLine}" +
            "Kapsamı Yönetim → Modüller sekmesinden değiştirebilirsiniz.",
            Resources.Strings.ConfirmTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        BeginWork(Resources.Strings.PreparingRestorePoint);

        try
        {
            var ct = StartNewOperation().Token;

            await _engine.PrepareSafetyAsync("WinOptimizer öncesi");
            var progress = new Progress<ProgressInfo>(info =>
            {
                Progress = info.Percent;
                StatusSummary = $"{info.ModuleId}: {info.Message} ({info.Percent}%)";
            });

            var results = await _engine.ExecuteAsync(null, progress, ct);

            long gained = results.Sum(r => r.GainBytes);
            int succeeded = results.Sum(r => r.Succeeded);
            ResultSummary = Resources.Strings.OptimizeComplete(succeeded, CleanEngineModule.FormatBytes(gained));
            HasResult = true;
            Log.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {Resources.Strings.Optimize}: {succeeded} öğe, +{CleanEngineModule.FormatBytes(gained)}.");
        }
        catch (OperationCanceledException)
        {
            ResultSummary = Resources.Strings.OptimizeCanceled;
            HasResult = true;
        }
        catch (Exception ex)
        {
            ResultSummary = Resources.Strings.Error(ex.Message);
            HasResult = true;
        }
        finally
        {
            EndWork();
            UpdateStatus();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        Log.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {Resources.Strings.CancelRequested}");
    }

    private void BeginWork(string message)
    {
        IsBusy = true;
        Progress = 0;
        HasResult = false;
        StatusSummary = message;
    }

    private void EndWork()
    {
        IsBusy = false;
        Progress = 100;
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _cts = null;
        GC.SuppressFinalize(this);
    }
}
