using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.Core;
using WinOptimizer.Orchestration;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Modül bazlı sayfa için ViewModel (master plan Bölüm 12.3 Akış B — ileri kullanıcı).
/// Belirli bir modülü çalıştırır: Analiz et → Önizle → Uygula.
/// </summary>
public partial class ModulePageViewModel : ObservableObject
{
    private readonly JobOrchestrationEngine _engine;
    private readonly ModuleRegistry _registry;
    private IOptimizationModule? _module;
    private AnalysisResult? _analysis;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _pageTitle = string.Empty;
    [ObservableProperty] private string _analysisText = "Henüz analiz edilmedi.";
    [ObservableProperty] private string _resultText = string.Empty;
    [ObservableProperty] private string _riskBadge = "Low";
    [ObservableProperty] private int _progress;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private bool _hasActions;

    public ObservableCollection<string> Actions { get; } = new();

    public ModulePageViewModel(JobOrchestrationEngine engine, ModuleRegistry registry)
    {
        _engine = engine;
        _registry = registry;
    }

    /// <summary>Bu ViewModel'i belirli bir modüle bağlar.</summary>
    public void Bind(string moduleId)
    {
        _module = _registry.Find(moduleId);
        if (_module is null)
        {
            PageTitle = "Bilinmeyen modül";
            return;
        }
        PageTitle = _module.DisplayName;
        RiskBadge = _module.Risk.ToString();
        AnalysisText = "Henüz analiz edilmedi. \"Analiz Et\" ile başlayın.";
        HasActions = false;
        Actions.Clear();
    }

    /// <summary>Modülü analiz eder.</summary>
    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (_module is null || IsBusy) return;
        IsBusy = true; Progress = 0; HasResult = false;
        try
        {
            _cts = new CancellationTokenSource();
            _analysis = await _module.AnalyzeAsync(_cts.Token);
            AnalysisText = _analysis.Summary;
            var preview = await _module.PreviewAsync(_analysis, _cts.Token);
            Actions.Clear();
            foreach (var a in preview.Actions)
                Actions.Add($"{a.Description}  [{a.Risk}{(a.RequiresExtraConfirmation ? ", onaylı" : "")}]");
            HasActions = Actions.Count > 0;
            if (!HasActions) AnalysisText += " (uygulanacak eylem yok)";
        }
        catch (OperationCanceledException) { AnalysisText = "İptal edildi."; }
        catch (Exception ex) { AnalysisText = "Hata: " + ex.Message; }
        finally { IsBusy = false; Progress = 100; }
    }

    /// <summary>Modülü uygular (SafetyNet ile).</summary>
    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (_module is null || _analysis is null || IsBusy) return;
        IsBusy = true; Progress = 0; HasResult = false;
        try
        {
            _cts = new CancellationTokenSource();
            await _engine.PrepareSafetyAsync($"WinOptimizer — {_module.DisplayName}");
            var progress = new Progress<ProgressInfo>(p =>
            {
                Progress = p.Percent;
                AnalysisText = $"{p.Message} ({p.Percent}%)";
            });
            var preview = await _module.PreviewAsync(_analysis, _cts.Token);
            var result = await _module.ExecuteAsync(preview, progress, _cts.Token);
            ResultText = $"Tamamlandı: {result.Succeeded} başarılı, {result.Skipped} atlanan, " +
                         $"{result.Failed} başarısız" +
                         (result.GainBytes > 0 ? $", +{FormatBytes(result.GainBytes)} kazanç." : ".");
            HasResult = true;
        }
        catch (OperationCanceledException) { ResultText = "İptal edildi."; HasResult = true; }
        catch (Exception ex) { ResultText = "Hata: " + ex.Message; HasResult = true; }
        finally { IsBusy = false; Progress = 100; }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F0} KB",
        _ => $"{bytes} B"
    };
}
