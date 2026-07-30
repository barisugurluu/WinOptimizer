using System.Windows.Controls;
using WinOptimizer.App.ViewModels;

namespace WinOptimizer.App.Views.Management;

/// <summary>Guard sekmesi — RealtimeGuard hizmetini kur/başlat/durdur/kaldır/onar.</summary>
public partial class GuardTab : UserControl
{
    public GuardTab(GuardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
