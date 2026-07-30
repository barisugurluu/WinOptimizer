using System.Windows;
using WinOptimizer.App.Views;
using WinOptimizer.Core;
using WinOptimizer.Orchestration.Confirmation;

namespace WinOptimizer.App.Infrastructure;

/// <summary>
/// Arayüz onay mercii — <see cref="ActionConfirmationDialog"/> gösterir.
/// </summary>
/// <remarks>
/// Onay isteği arka plan iş parçacığından gelebileceği için diyalog Dispatcher üzerinden
/// açılır (motor <c>ExecuteAsync</c> içinde <c>Task.Run</c> altında çalışıyor olabilir).
/// </remarks>
public sealed class DialogActionConfirmation : IActionConfirmation
{
    public Task<IReadOnlyList<PreviewAction>> ConfirmAsync(
        ConfirmationRequest request, CancellationToken ct = default)
    {
        if (request.Actions.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<PreviewAction>>([]);
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            // Arayüz yoksa (ör. kapanış sırasında) hiçbir şey onaylanmaz — sessizce
            // uygulamaktansa atlamak güvenli taraftır.
            return Task.FromResult<IReadOnlyList<PreviewAction>>([]);
        }

        return dispatcher.InvokeAsync(() =>
        {
            var dialog = new ActionConfirmationDialog(request);
            // Ana pencere henüz yoksa (ilk açılış akışı) Owner atanmaz; diyalog yine açılır.
            if (Application.Current?.MainWindow is { IsLoaded: true } owner)
            {
                dialog.Owner = owner;
            }

            dialog.ShowDialog();
            return dialog.ApprovedActions;
        }).Task;
    }
}
