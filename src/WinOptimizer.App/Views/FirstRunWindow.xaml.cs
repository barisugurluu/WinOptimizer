using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.App.Infrastructure;
using WinOptimizer.App.ViewModels;
using WinOptimizer.Orchestration;

namespace WinOptimizer.App.Views;

/// <summary>
/// İlk açılış penceresi — gereksinim raporu, davranış özeti ve isteğe bağlı hizmet kurulumu.
/// Ana pencere açılmadan ÖNCE gösterilir.
/// </summary>
public partial class FirstRunWindow : Window
{
    private readonly FirstRunViewModel _viewModel;
    private readonly string _dataDir;

    public FirstRunWindow(FirstRunViewModel viewModel, string dataDir)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _dataDir = dataDir;
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
        Loaded += OnLoadedAsync;
    }

    /// <summary>Kullanıcı devam etmeyi seçti mi (engelleyen madde varsa false).</summary>
    public bool Continued => _viewModel.Continued;

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();

        // Engelleyen bir madde varsa devam ettirmenin anlamı yok: uygulama zaten
        // modüllerin derinlerinde hata verecekti. "Başla" gizlenir, teşhis yolu kalır.
        if (_viewModel.HasBlocking)
        {
            ContinueButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnCreateDiagnostics(object sender, RoutedEventArgs e)
    {
        DiagnosticsButton.IsEnabled = false;
        try
        {
            string reportText = await _viewModel.BuildReportTextAsync();
            var builder = new DiagnosticsPackageBuilder(
                _dataDir,
                NullLogger<DiagnosticsPackageBuilder>.Instance,
                requirementsReportProvider: () => reportText);

            var result = await builder.CreateAsync();
            ExplorerReveal.SelectFile(result.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"Teşhis paketi oluşturulamadı:{Environment.NewLine}{ex.Message}",
                "WinOptimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            DiagnosticsButton.IsEnabled = true;
        }
    }
}
