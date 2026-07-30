using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.App.Infrastructure;
using WinOptimizer.Orchestration;

namespace WinOptimizer.App.Views;

/// <summary>
/// Hata penceresi — başlangıçta veya çalışma sırasında yakalanan istisnayı kullanıcıya
/// <b>görünür</b> biçimde bildirir ve teşhis için somut eylemler sunar.
/// </summary>
/// <remarks>
/// <para>Bilinçli olarak DI kullanmaz ve <c>App.Services</c>'e dokunmaz: başlangıç hatasında
/// kapsayıcı hiç kurulmamış ya da yarı kurulmuş olabilir. Teşhis paketi oluşturucu doğrudan
/// (<c>NullLogger</c> ile) örneklenir.</para>
/// <para>Bu pencere olmadan başlangıç hatası kullanıcıya hiçbir şey göstermiyordu; süreç
/// arayüzsüz olarak ayakta kalıyordu.</para>
/// </remarks>
public partial class ErrorDialog : Window
{
    private readonly Exception _exception;
    private readonly string? _dataDir;

    public ErrorDialog(Exception exception, string? dataDir, string? fallbackReportPath, bool isFatal)
    {
        InitializeComponent();
        _exception = exception;
        _dataDir = dataDir;

        HeadlineText.Text = isFatal
            ? "WinOptimizer başlatılamadı"
            : "Beklenmeyen bir hata oluştu";

        SummaryText.Text = isFatal
            ? "Uygulama açılırken bir hata oluştu ve kapatılacak. Aşağıdaki bilgiyi destek " +
              "için saklayabilirsiniz."
            : "İşlem tamamlanamadı, ancak uygulama açık kalıyor. Aynı hata tekrar ederse " +
              "aşağıdaki teşhis paketini oluşturup paylaşın.";

        DetailsText.Text = $"{exception.GetType().FullName}: {exception.Message}" +
                           Environment.NewLine + Environment.NewLine + exception;

        PathsText.Text = BuildPaths(dataDir, fallbackReportPath);

        // Günlük klasörü yoksa (asıl hata o olabilir) düğmeyi kapatmak, tıklayıp hiçbir şey
        // olmamasından iyidir.
        OpenLogsButton.IsEnabled = dataDir is not null && Directory.Exists(Path.Combine(dataDir, "logs"));
    }

    private static string BuildPaths(string? dataDir, string? fallbackReportPath)
    {
        var sb = new StringBuilder();
        if (dataDir is not null)
        {
            sb.AppendLine(Path.Combine(dataDir, "logs"));
        }
        else
        {
            sb.AppendLine("(Veri dizini oluşturulamadı — günlük yazılamamış olabilir.)");
        }

        if (!string.IsNullOrEmpty(fallbackReportPath))
        {
            sb.AppendLine(fallbackReportPath);
        }

        return sb.ToString().TrimEnd();
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        if (_dataDir is not null)
        {
            ExplorerReveal.OpenFolder(Path.Combine(_dataDir, "logs"));
        }
    }

    private async void OnCreateDiagnostics(object sender, RoutedEventArgs e)
    {
        DiagnosticsButton.IsEnabled = false;
        try
        {
            // Veri dizini bilinmiyorsa varsayılan konumdan devam edilir: paket en azından
            // sistem bilgisi + olay günlüğü + bu istisnayı içerir.
            string baseDir = _dataDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "WinOptimizer");

            var builder = new DiagnosticsPackageBuilder(
                baseDir, NullLogger<DiagnosticsPackageBuilder>.Instance);
            var result = await builder.CreateAsync();

            ExplorerReveal.SelectFile(result.FilePath);
            SummaryText.Text = $"Teşhis paketi oluşturuldu:{Environment.NewLine}{result.FilePath}";
        }
        catch (Exception ex)
        {
            // Bu pencere son savunma hattı: burada da patlarsa kullanıcı yine bir şey görmeli.
            SummaryText.Text = $"Teşhis paketi oluşturulamadı: {ex.GetType().Name}: {ex.Message}";
            DiagnosticsButton.IsEnabled = true;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
