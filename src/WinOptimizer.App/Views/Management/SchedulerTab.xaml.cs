using System.Windows.Controls;
using WinOptimizer.App.ViewModels;

namespace WinOptimizer.App.Views.Management;

/// <summary>Zamanlayıcı sekmesi — haftalık otomatik bakım görevi yönetimi.</summary>
public partial class SchedulerTab : UserControl
{
    public SchedulerTab(SchedulerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}