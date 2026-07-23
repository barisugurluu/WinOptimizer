using System.Windows.Controls;
using WinOptimizer.App.ViewModels;

namespace WinOptimizer.App.Views;

/// <summary>
/// Geri alma zaman çizelgesi sayfası (master plan Bölüm 12.3 Akış C).
/// Change journal kayıtlarını kart listesi olarak gösterir.
/// </summary>
public partial class RollbackPage : Page
{
    private readonly RollbackViewModel _viewModel;

    public RollbackPage(RollbackViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }
}
