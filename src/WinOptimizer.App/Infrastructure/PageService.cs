using System.Windows;
using Wpf.Ui;

namespace WinOptimizer.App.Infrastructure;

/// <summary>
/// PageService — WPF-UI <see cref="IPageService"/> uygulaması. NavigationView, TargetPageType
/// ile gezinirken sayfaları DI kapsayıcısından (App.Services) çözmek için bunu kullanır.
/// Böylece DI constructor'lı sayfalar (DashboardPage, ManagementPage vb.) güvenle yaratılır.
/// (WPF-UI belgeli DI navigasyon paterni.)
/// </summary>
public sealed class PageService : IPageService
{
    private readonly IServiceProvider _serviceProvider;

    public PageService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <summary>Generic sayfa çözümleme.</summary>
    public T? GetPage<T>() where T : class =>
        _serviceProvider.GetService(typeof(T)) as T;

    /// <summary>Tip bazlı sayfa çözümleme — NavigationView burayı çağırır.</summary>
    public FrameworkElement? GetPage(Type pageType) =>
        _serviceProvider.GetService(pageType) as FrameworkElement;
}
