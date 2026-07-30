using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.App.Infrastructure;
using WinOptimizer.Orchestration;
using WinOptimizer.Orchestration.Preflight;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// "Sistem &amp; Veri" sekmesi — gereksinim kontrolünü istendiği zaman yeniden çalıştırır ve
/// teşhis paketini dışa aktarır.
/// </summary>
/// <remarks>
/// İlk açılış sihirbazı yalnızca bir kez görünür; destek konuşmaları ise genellikle
/// "gereksinim kontrolünü çalıştırıp çıktıyı gönderin" ile başlar. Bu sekme o girişi
/// kalıcı hale getirir (eskiden burada "yakında" yer tutucusu vardı).
/// </remarks>
public partial class SystemDataViewModel : ObservableObject
{
    private readonly SystemRequirementsChecker _checker;
    private readonly DiagnosticsPackageBuilder _diagnostics;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasBlocking;
    [ObservableProperty] private bool _hasWarnings;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasStatus;

    public ObservableCollection<RequirementRow> Requirements { get; } = new();

    public SystemDataViewModel(SystemRequirementsChecker checker, DiagnosticsPackageBuilder diagnostics)
    {
        _checker = checker;
        _diagnostics = diagnostics;
        _ = RunCheckAsync();
    }

    /// <summary>Gereksinim kontrolünü çalıştırır.</summary>
    [RelayCommand]
    public async Task RunCheckAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var report = await _checker.RunAsync();
            Requirements.Clear();
            foreach (var check in report.Checks)
            {
                Requirements.Add(new RequirementRow
                {
                    Glyph = check.Severity switch
                    {
                        RequirementSeverity.Ok => "✓",
                        RequirementSeverity.Warning => "⚠",
                        _ => "✕",
                    },
                    Title = check.Title,
                    Detail = check.Detail,
                    RemedyHint = check.RemedyHint,
                });
            }

            HasBlocking = report.HasBlocking;
            HasWarnings = report.HasWarnings;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Teşhis paketini üretir ve Gezgin'de gösterir.</summary>
    [RelayCommand]
    private async Task ExportDiagnosticsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _diagnostics.CreateAsync();
            ExplorerReveal.SelectFile(result.FilePath);
            StatusMessage = $"{result.FilePath} ({result.SizeBytes / 1024} KB, {result.IncludedItems.Count} öğe)";
            HasStatus = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Teşhis paketi oluşturulamadı: {ex.Message}";
            HasStatus = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
