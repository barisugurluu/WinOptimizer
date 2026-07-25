using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Controls;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Yönetim sekmesini temsil eden UI modeli. <see cref="View"/>, DI ile çözümlenmiş
/// hazır bir UserControl'dür; ContentControl ona bağlanır (DataTemplate sihrine gerek yok).
/// </summary>
public sealed class ManagementTabItem
{
    public required string Title { get; init; }
    public required SymbolRegular Icon { get; init; }
    /// <summary>Sekme içeriği olarak gösterilecek hazır görünüm (DI çözümlü).</summary>
    public required FrameworkElement View { get; init; }
}

/// <summary>
/// Yönetim merkezi görünüm modeli — sekmeli "Control Center" (master plan Bölüm 12 genişletmesi).
/// Sekme listesini ve seçili sekmeyi tutar. Sekme görünümleri ManagementPage tarafından
/// DI'dan çözümlenip <see cref="Tabs"/> koleksiyonuna eklenir.
/// </summary>
public partial class ManagementViewModel : ObservableObject
{
    [ObservableProperty] private ManagementTabItem? _selectedTab;

    public ObservableCollection<ManagementTabItem> Tabs { get; } = new();
}
