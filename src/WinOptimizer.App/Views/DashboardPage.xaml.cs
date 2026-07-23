using System.Windows.Controls;
using WinOptimizer.App.ViewModels;

namespace WinOptimizer.App.Views;

/// <summary>
/// Panosu sayfası — "Önizle → Onayla → Uygula" akışını barındırır.
/// DataContext, DI ile sağlanan DashboardViewModel'e bağlanır.
/// </summary>
public partial class DashboardPage : Page
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
