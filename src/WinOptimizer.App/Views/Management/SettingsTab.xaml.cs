using System.Windows.Controls;
using WinOptimizer.App.ViewModels;

namespace WinOptimizer.App.Views.Management;

/// <summary>Ayarlar sekmesi — dil, tema, SafetyNet ve RealtimeGuard eşik ayarları.</summary>
public partial class SettingsTab : UserControl
{
    public SettingsTab(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
