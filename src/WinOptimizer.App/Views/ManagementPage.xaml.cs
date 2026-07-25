using System.Windows;
using System.Windows.Controls;
using WinOptimizer.App.Resources;
using WinOptimizer.App.ViewModels;
using WinOptimizer.App.Views.Management;
using Wpf.Ui.Controls;

namespace WinOptimizer.App.Views;

/// <summary>
/// Yönetim merkezi sayfası — sekmeli Control Center (master plan Bölüm 12 genişletmesi).
/// Sol ikincil navigasyon (9 sekme) + sağ içerik. Gerçek sekmeler DI'dan çözümlenir;
/// henüz uygulanmayanlar yer tutucu gösterir.
/// </summary>
public partial class ManagementPage : Page
{
    private readonly ManagementViewModel _viewModel;

    public ManagementPage(ManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        BuildTabs();
        _viewModel.SelectedTab = _viewModel.Tabs.Count > 0 ? _viewModel.Tabs[0] : null;
    }

    /// <summary>9 sekmeyi (gerçek + yer tutucu) oluşturup ViewModel'e ekler.</summary>
    private void BuildTabs()
    {
        AddTab(Strings.TabOverview, SymbolRegular.Gauge24, App.GetService<OverviewTab>());
        AddTab(Strings.TabSettings, SymbolRegular.Settings24, App.GetService<SettingsTab>());
        AddTab(Strings.TabScheduler, SymbolRegular.CalendarClock24, App.GetService<SchedulerTab>());
        AddPlaceholder(Strings.TabModules, SymbolRegular.AppFolder24);
        AddPlaceholder(Strings.TabProfiles, SymbolRegular.PersonStar24);
        AddPlaceholder(Strings.TabGuard, SymbolRegular.Shield24);
        AddPlaceholder(Strings.TabReports, SymbolRegular.DocumentBulletList24);
        AddPlaceholder(Strings.TabUpdate, SymbolRegular.ArrowSync24);
        AddPlaceholder(Strings.TabData, SymbolRegular.FolderZip24);
    }

    private void AddTab(string title, SymbolRegular icon, FrameworkElement view) =>
        _viewModel.Tabs.Add(new ManagementTabItem { Title = title, Icon = icon, View = view });

    /// <summary>Uygulanmamış sekme için başlığı DataContext olarak bağlayan yer tutucu.</summary>
    private void AddPlaceholder(string title, SymbolRegular icon)
    {
        var placeholder = new ComingSoonTab { DataContext = title };
        _viewModel.Tabs.Add(new ManagementTabItem { Title = title, Icon = icon, View = placeholder });
    }
}