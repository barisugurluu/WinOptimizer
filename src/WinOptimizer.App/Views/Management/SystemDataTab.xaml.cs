using System.Windows.Controls;
using WinOptimizer.App.ViewModels;

namespace WinOptimizer.App.Views.Management;

/// <summary>Sistem &amp; Veri sekmesi — gereksinim kontrolü ve teşhis paketi dışa aktarımı.</summary>
public partial class SystemDataTab : UserControl
{
    public SystemDataTab(SystemDataViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
