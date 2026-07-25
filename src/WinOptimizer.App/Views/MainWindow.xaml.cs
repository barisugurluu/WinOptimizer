using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace WinOptimizer.App.Views;

/// <summary>
/// Ana pencere — Fluent Dark, Mika arka plan, sol NavigationView.
/// (Master plan Bölüm 12.2 yerleşimi — modül bazlı sol navigasyon.)
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // NavigationView'a DI sayfa çözümleyiciyi bağla (WPF-UI IPageService).
        // Böylece TargetPageType ile DI constructor'lı sayfalar güvenle yaratılır.
        if (App.Services.GetService(typeof(Wpf.Ui.IPageService)) is Wpf.Ui.IPageService pageService)
        {
            RootNavigation.SetPageService(pageService);
        }
        // İlk açılışta Panosu (Dashboard) seçili olsun.
        RootNavigation.Navigate(typeof(DashboardPage));

        // Yüksek Kontrast (HC) teması desteği — master plan Bölüm 21.2 (WCAG 2.1 AA).
        // HC açıkken Mica backdrop düzgün görünmediğinden kapatılır; sistem HC renkleri kullanılır.
        ApplyHighContrastSafeBackdrop();
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    /// <summary>
    /// Sistem ayarı değişince (ör. Yüksek Kontrast aç/kapat) pencere kromunu günceller.
    /// </summary>
    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
        {
            Dispatcher.BeginInvoke(ApplyHighContrastSafeBackdrop);
        }
    }

    /// <summary>
    /// Yüksek Kontrast modunda Mica backdrop'u kapatır (HC'de görünmez/düzgün boyanmaz);
    /// normal temada Fluent Mica uygular. HC değişiminde otomatik tetiklenir.
    /// </summary>
    private void ApplyHighContrastSafeBackdrop()
    {
        WindowBackdropType = SystemParameters.HighContrast
            ? WindowBackdropType.None
            : WindowBackdropType.Mica;
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        base.OnClosed(e);
    }

    /// <summary>
    /// Bir NavigationViewItem seçilince, Tag'inden modül kimliğini alır ve
    /// <see cref="ModuleNavigator"/> üzerinden ModulePage'e aktarır.
    /// </summary>
    private void OnNavigationSelectionChanged(Wpf.Ui.Controls.NavigationView sender, RoutedEventArgs args)
    {
        if (sender.SelectedItem is Wpf.Ui.Controls.NavigationViewItem item && item.Tag is string moduleId)
        {
            ModuleNavigator.CurrentModuleId = moduleId;
        }
    }
}

