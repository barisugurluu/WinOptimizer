using System.Windows.Controls;
using WinOptimizer.App.ViewModels;

namespace WinOptimizer.App.Views.Management;

/// <summary>Modüller sekmesi — tek tıkla optimizasyonun kapsamını belirler.</summary>
public partial class ModulesTab : UserControl
{
    public ModulesTab(ModulesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
