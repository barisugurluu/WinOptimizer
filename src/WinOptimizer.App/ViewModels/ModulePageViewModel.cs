using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.Core;
using WinOptimizer.Orchestration;
using WinOptimizer.Orchestration.Confirmation;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Modül bazlı sayfa için ViewModel (master plan Bölüm 12.3 Akış B — ileri kullanıcı).
/// Belirli bir modülü çalıştırır: Analiz et → Önizle → Uygula.
/// </summary>
public partial class ModulePageViewModel : ObservableObject, IDisposable
{
    private readonly JobOrchestrationEngine _engine;
    private readonly ModuleRegistry _registry;
    private readonly SettingsService _settings;
    private readonly IActionConfirmation _confirmation;
    private IOptimizationModule? _module;
    private AnalysisResult? _analysis;
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

    [ObservableProperty] private string _pageTitle = string.Empty;
    [ObservableProperty] private string _analysisText = "Henüz analiz edilmedi.";
    [ObservableProperty] private string _resultText = string.Empty;
    [ObservableProperty] private string _riskBadge = "Low";
    [ObservableProperty] private int _progress;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private bool _hasActions;

    public ObservableCollection<string> Actions { get; } = new();

    public ModulePageViewModel(
        JobOrchestrationEngine engine,
        ModuleRegistry registry,
        SettingsService settings,
        IActionConfirmation confirmation)
    {
        _engine = engine;
        _registry = registry;
        _settings = settings;
        _confirmation = confirmation;
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
        PageTitle = Resources.ModuleDisplayNameResolver.Resolve(_module);
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
            var ct = StartNewOperation().Token;
            _analysis = await _module.AnalyzeAsync(ct);
            AnalysisText = _analysis.Summary;
            var preview = await _module.PreviewAsync(_analysis, ct);
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
            var ct = StartNewOperation().Token;
            await _engine.PrepareSafetyAsync($"WinOptimizer — {_module.DisplayName}");
            var progress = new Progress<ProgressInfo>(p =>
            {
                Progress = p.Percent;
                AnalysisText = $"{p.Message} ({p.Percent}%)";
            });
            var preview = await _module.PreviewAsync(_analysis, ct);

            // ONAY KAPISI — bu sayfa motoru bilinçli olarak atlıyor (_engine.ExecuteAsync
            // Analyze+Preview'i yeniden çalıştırır ve kullanıcının az önce incelediği
            // önizlemeyi çöpe atardı). Bu yüzden AYNI politika burada da uygulanır:
            // tek predicate (ConfirmationGate), tek arayüz (IActionConfirmation), iki çağıran.
            // Eskiden bu yolda hiçbir onay yoktu: "Uygula" düğmesi Hyper-V'yi doğrudan açardı.
            if (ConfirmationGate.RequiresConfirmation(_module, preview, _settings.Current))
            {
                var approved = await _confirmation.ConfirmAsync(
                    new ConfirmationRequest(_module.Id, _module.DisplayName, _module.Risk, preview.Actions),
                    ct);

                if (approved.Count == 0)
                {
                    ResultText = "İşlem onaylanmadı; hiçbir değişiklik yapılmadı.";
                    HasResult = true;
                    return;
                }

                preview = ConfirmationGate.WithActions(preview, approved);
            }

            var result = await _module.ExecuteAsync(preview, progress, ct);
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

    private static string FormatBytes(long bytes) => FileSizeFormatter.Format(bytes);

    public void Dispose()
    {
        _cts?.Dispose();
        _cts = null;
        GC.SuppressFinalize(this);
    }
}
