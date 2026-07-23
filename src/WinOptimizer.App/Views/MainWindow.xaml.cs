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
        // İlk açılışta Panosu (Dashboard) seçili olsun.
        RootNavigation.Navigate(typeof(DashboardPage));
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

