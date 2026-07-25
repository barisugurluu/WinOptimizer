using System.Windows;
using System.Windows.Controls;
using WinOptimizer.App.ViewModels;

namespace WinOptimizer.App.Views.Management;

/// <summary>
/// Genel Bakış sekmesi — canlı metrikleri ve etkinlik özetini barındırır.
/// Loaded/Unloaded'da yoklamayı başlatır/durdurur.
/// </summary>
public partial class OverviewTab : UserControl
{
    private readonly OverviewViewModel _viewModel;

    public OverviewTab(OverviewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        _viewModel.StartPolling(App.PollingIntervalSeconds);

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        _viewModel.StopPolling();
}
