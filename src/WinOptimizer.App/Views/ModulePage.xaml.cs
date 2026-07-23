using System.Windows.Controls;
using WinOptimizer.App.ViewModels;

namespace WinOptimizer.App.Views;

/// <summary>
/// Modül bazlı sayfa (master plan Bölüm 12.3 Akış B).
/// <see cref="ModuleNavigator.CurrentModuleId"/> üzerinden hangi modülü
/// göstereceğini belirler. NavigationView navigasyon ögesi tıklanınca
/// bu statik özellik ayarlanır, sayfa açılınca bağlanır.
/// </summary>
public partial class ModulePage : Page
{
    private readonly ModulePageViewModel _viewModel;

    public ModulePage(ModulePageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        // Navigasyon ögesi tarafından ayarlanan modül kimliğine bağlan.
        if (!string.IsNullOrEmpty(ModuleNavigator.CurrentModuleId))
        {
            _viewModel.Bind(ModuleNavigator.CurrentModuleId);
        }
    }
}

/// <summary>
/// NavigationView öğesi ile sayfa arasında modül kimliğini taşır.
/// MainWindow, bir NavigationViewItem tıklanınca burayı ayarlar.
/// </summary>
public static class ModuleNavigator
{
    public static string? CurrentModuleId { get; set; }
}
