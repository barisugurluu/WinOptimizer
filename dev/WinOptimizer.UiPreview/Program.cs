using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.App.ViewModels;
using WinOptimizer.App.Views;
using WinOptimizer.Core;
using WinOptimizer.Orchestration;
using WinOptimizer.Orchestration.Confirmation;
using WinOptimizer.Orchestration.Preflight;
using WinOptimizer.Safety;

namespace WinOptimizer.UiPreview;

/// <summary>
/// GELİŞTİRME ARACI — pencereleri PNG'ye render eder, dağıtılmaz.
/// </summary>
/// <remarks>
/// <para><b>Neden var?</b> WinOptimizer.App <c>requireAdministrator</c> ile çalışır; her
/// açılışta UAC ister ve arayüzü görmek için kurulum ya da Windows Sandbox turu gerekir.
/// Bu araç olmadan bir XAML değişikliğinin doğru olup olmadığı ancak tam kurulum
/// döngüsünden sonra anlaşılıyordu — beyaz-zeminde-beyaz-yazı hatası tam bu yüzden
/// üst üste iki kez yanlış teşhis edildi.</para>
/// <para><b>Kaynak sürüklenmesi:</b> <c>AppResources.xaml</c> uygulamanın kendi kullandığı
/// dosyadır; burada kopyalanmaz, merge edilir. Böylece önizleme gerçekten uygulamadaki
/// görüntüdür.</para>
/// <para>Kullanım: <c>dotnet run --project dev\WinOptimizer.UiPreview [çıktı-dizini]</c></para>
/// </remarks>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        string outputDir = args.Length > 0
            ? args[0]
            : Path.Combine(Path.GetTempPath(), "winoptimizer-ui-preview");
        Directory.CreateDirectory(outputDir);

        // Uygulamanın kendi kaynakları — kopya değil, aynı dosya.
        // ShutdownMode: varsayılan OnLastWindowClose, ilk pencereyi kapattığımızda
        // Application'ı da kapatır ve sonraki pencereler "Application kapatılıyor"
        // hatasıyla yüklenemez.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/WinOptimizer.App;component/Resources/AppResources.xaml",
                UriKind.Absolute),
        });

        // Geçici veri dizini: gerçek %ProgramData%\WinOptimizer'a dokunulmaz.
        string dataDir = Path.Combine(
            Path.GetTempPath(), "winopt-uipreview-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dataDir);

        int exitCode = 0;
        try
        {
            // XAML AYRIŞTIRMA DOĞRULAMASI — render'dan önce.
            // MainWindow'u yalnızca OLUŞTURUR (göstermez): InitializeComponent XAML'i
            // ayrıştırır ve SymbolRegular gibi ENUM değerlerini çözer. Geçersiz bir ikon
            // adı DERLEYİCİ TARAFINDAN YAKALANMAZ, yalnız çalışma zamanında patlar ve
            // uygulama hiç açılmaz. Gerçekten böyle oldu: `Brain24` ve `Developer24`
            // SymbolRegular'da yoktu, MainWindow yüklenemiyordu.
            // Gösterilmez çünkü OnSourceInitialized DI kapsayıcısını (App.Services) ister.
            VerifyXamlParses("MainWindow", () => new WinOptimizer.App.Views.MainWindow());

            Capture(outputDir, "01-FirstRunWindow.png", CreateFirstRunWindow(dataDir));

            // Aynı pencere sona kaydırılmış: onay kutusunun ETİKETİ ve ilke listesi
            // kaydırma alanının altında kalıyor; hatanın ilk göründüğü yer orasıydı.
            Capture(outputDir, "01b-FirstRunWindow-scrolled.png", CreateFirstRunWindow(dataDir),
                ScrollToEnd);

            Capture(outputDir, "02-ErrorDialog.png", CreateErrorDialog(dataDir));

            // Expander açık: başlığı ve içindeki yığın izi TextBox'ı da doğrulanmalı.
            Capture(outputDir, "02b-ErrorDialog-expanded.png", CreateErrorDialog(dataDir), window =>
            {
                if (FindDescendant<System.Windows.Controls.Expander>(window) is { } expander)
                {
                    expander.IsExpanded = true;
                }
            });

            Capture(outputDir, "03-ActionConfirmationDialog.png", CreateConfirmationDialog());

            Console.WriteLine();
            Console.WriteLine("==> Önizlemeler hazır: " + outputDir);
        }
        catch (Exception ex)
        {
            // İÇ İSTİSNA ZİNCİRİ ŞART: XamlParseException'ın kendi mesajı ("değer sağlama
            // işlemi özel durum döndürdü") hiçbir şey söylemez; asıl sebep — geçersiz enum
            // adı, bulunamayan pack:// kaynağı — en içteki istisnadadır.
            Console.Error.WriteLine("Önizleme başarısız:");
            for (Exception? current = ex; current is not null; current = current.InnerException)
            {
                Console.Error.WriteLine($"  {current.GetType().Name}: {current.Message}");
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(ex.ToString());
            exitCode = 1;
        }
        finally
        {
            TryDelete(dataDir);
            app.Shutdown();
        }

        return exitCode;
    }

    /// <summary>
    /// Pencereyi ekran dışında gösterir, yerleşim ve async yüklemenin oturmasını bekler,
    /// PNG'ye render eder ve kapatır.
    /// </summary>
    /// <remarks>
    /// Pencere <b>gösterilmek zorunda</b>: gösterilmemiş bir <see cref="Window"/>'un görsel
    /// ağacı oluşmaz ve <see cref="RenderTargetBitmap"/> boş çıkar. Ekran dışına konumlandırma
    /// (-10000) ekranda titreme olmasını önler.
    /// </remarks>
    private static void Capture(
        string outputDir, string fileName, Window window, Action<Window>? beforeRender = null)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -10000;
        window.Top = -10000;
        window.ShowInTaskbar = false;
        window.Show();

        // FirstRunWindow.OnLoadedAsync gereksinim kontrolünü ASYNC çalıştırır (WMI sorguları
        // dahil); render'dan önce tamamlanması gerekir, yoksa liste boş yakalanır.
        PumpDispatcher(TimeSpan.FromSeconds(3));

        if (beforeRender is not null)
        {
            beforeRender(window);
            PumpDispatcher(TimeSpan.FromMilliseconds(400));
        }

        window.UpdateLayout();

        var source = PresentationSource.FromVisual(window);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        int pixelWidth = (int)Math.Ceiling(window.ActualWidth * dpiX);
        int pixelHeight = (int)Math.Ceiling(window.ActualHeight * dpiY);

        var bitmap = new RenderTargetBitmap(
            pixelWidth, pixelHeight, 96 * dpiX, 96 * dpiY, PixelFormats.Pbgra32);
        bitmap.Render(window);

        string path = Path.Combine(outputDir, fileName);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path))
        {
            encoder.Save(stream);
        }

        window.Close();
        PumpDispatcher(TimeSpan.FromMilliseconds(200));

        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "  {0,-34} {1}x{2}px", fileName, pixelWidth, pixelHeight));
    }

    /// <summary>
    /// Bir pencerenin XAML'inin ayrıştırılabildiğini doğrular. Başarısızlıkta istisnayı
    /// yeniden fırlatır — araç sıfırdan farklı çıkış koduyla biter, CI/geliştirici görür.
    /// </summary>
    private static void VerifyXamlParses(string name, Func<Window> factory)
    {
        Window window = factory();
        window.Close();
        Console.WriteLine(string.Format(
            CultureInfo.InvariantCulture, "  {0,-34} XAML ayrıştırma OK", name));
    }

    /// <summary>Penceredeki ilk <see cref="System.Windows.Controls.ScrollViewer"/>'ı sona kaydırır.</summary>
    private static void ScrollToEnd(Window window) =>
        FindDescendant<System.Windows.Controls.ScrollViewer>(window)?.ScrollToEnd();

    /// <summary>Görsel ağaçta ilk <typeparamref name="T"/> torununu bulur.</summary>
    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// Dispatcher kuyruğunu belirtilen süre boyunca işler — <c>Show()</c> sonrası yerleşim,
    /// bağlama ve async yüklemelerin tamamlanması için.
    /// </summary>
    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(
            duration, DispatcherPriority.Background,
            (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    private static FirstRunWindow CreateFirstRunWindow(string dataDir)
    {
        // Bağımlılık zinciri E2E testlerindeki desenle aynı kurulur
        // (tests/WinOptimizer.E2E.Tests/SystemRequirementsScenarios.cs).
        var runner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        var restorePoint = new RestorePointService(NullLogger<RestorePointService>.Instance);
        var integrity = new IntegrityGuard(
            IntegrityKeyStore.LoadOrCreate(dataDir), NullLogger<IntegrityGuard>.Instance);
        var journal = new ChangeJournal(dataDir, NullLogger<ChangeJournal>.Instance, integrity);
        var registryBackup = new RegistryBackup(dataDir, NullLogger<RegistryBackup>.Instance, integrity);
        var safety = new SafetyNet(restorePoint, journal, registryBackup,
            new SafetyGuard(NullLogger<SafetyGuard>.Instance), NullLogger<SafetyNet>.Instance);
        var guardService = new GuardServiceController(
            runner, safety, NullLogger<GuardServiceController>.Instance);
        var checker = new SystemRequirementsChecker(
            dataDir, guardService, restorePoint, NullLogger<SystemRequirementsChecker>.Instance);
        var settings = new SettingsService(dataDir, NullLogger<SettingsService>.Instance);

        var viewModel = new FirstRunViewModel(checker, guardService, settings);
        return new FirstRunWindow(viewModel, dataDir);
    }

    private static ErrorDialog CreateErrorDialog(string dataDir)
    {
        // Gerçekçi bir istisna: yığın izi dolu olsun ki Expander/TextBox okunabilirliği
        // gerçek koşulda görülsün.
        Exception exception;
        try
        {
            throw new InvalidOperationException(
                "Önizleme için üretilmiş örnek hata: modül kaydı çözümlenemedi.");
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        return new ErrorDialog(
            exception,
            dataDir,
            Path.Combine(dataDir, "startup-error.txt"),
            isFatal: true);
    }

    private static ActionConfirmationDialog CreateConfirmationDialog()
    {
        var actions = new List<PreviewAction>
        {
            new()
            {
                Description = "Geçici dosyaları sil (C:\\Windows\\Temp)",
                Risk = RiskLevel.Low,
                Target = "TempFiles",
                Bytes = 1_234_567_890,
            },
            new()
            {
                Description = "Geri Dönüşüm kutusunu boşalt",
                Risk = RiskLevel.Low,
                Target = "RecycleBin",
                Bytes = 987_654_321,
                RequiresExtraConfirmation = true,
            },
            new()
            {
                Description = "Hyper-V özelliğini etkinleştir (yeniden başlatma gerekir)",
                Risk = RiskLevel.High,
                Target = "HyperV",
            },
        };

        return new ActionConfirmationDialog(
            new ConfirmationRequest("CleanEngine", "Disk & Önbellek Temizliği", RiskLevel.Low, actions));
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Geçici klasör silinemezse önizleme sonucu etkilenmez.
        }
        catch (UnauthorizedAccessException)
        {
            // Aynı gerekçe.
        }
    }
}
