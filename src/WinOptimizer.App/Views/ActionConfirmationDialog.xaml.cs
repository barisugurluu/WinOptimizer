using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using WinOptimizer.Core;
using WinOptimizer.Orchestration.Confirmation;

namespace WinOptimizer.App.Views;

/// <summary>Onay listesindeki tek satır.</summary>
public sealed class ConfirmableAction
{
    public required PreviewAction Action { get; init; }
    public required string Description { get; init; }
    public required string Badge { get; init; }
    public bool IsApproved { get; set; }
}

/// <summary>
/// Uygulanacak eylemleri tek tek onaylatan pencere.
/// </summary>
public partial class ActionConfirmationDialog : Window
{
    private readonly ObservableCollection<ConfirmableAction> _items = [];

    public ActionConfirmationDialog(ConfirmationRequest request)
    {
        InitializeComponent();

        HeadlineText.Text = $"{request.ModuleDisplayName} — uygulanacak eylemler";

        long totalBytes = request.Actions.Sum(a => a.Bytes);
        int needsOptIn = request.Actions.Count(ConfirmationGate.NeedsExplicitOptIn);
        SummaryText.Text = string.Format(
            CultureInfo.InvariantCulture,
            "{0} eylem · {1} · modül riski: {2}{3}",
            request.Actions.Count,
            FileSizeFormatter.Format(totalBytes),
            request.ModuleRisk,
            needsOptIn > 0
                ? $"{Environment.NewLine}{needsOptIn} eylem ek onay istiyor ve işaretsiz geldi."
                : string.Empty);

        foreach (var action in request.Actions)
        {
            bool optIn = ConfirmationGate.NeedsExplicitOptIn(action);
            _items.Add(new ConfirmableAction
            {
                Action = action,
                Description = action.Description,
                Badge = string.Format(
                    CultureInfo.InvariantCulture,
                    "risk: {0}{1}{2}",
                    action.Risk,
                    action.Bytes > 0 ? " · " + FileSizeFormatter.Format(action.Bytes) : string.Empty,
                    optIn ? " · EK ONAY GEREKİR" : string.Empty),
                // Ek onay isteyenler işaretsiz; diğerleri hazır işaretli.
                IsApproved = !optIn,
            });
        }

        ActionsList.ItemsSource = _items;
    }

    /// <summary>Kullanıcının onayladığı eylemler (İptal edildiyse boş).</summary>
    public IReadOnlyList<PreviewAction> ApprovedActions { get; private set; } = [];

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items)
        {
            item.IsApproved = true;
        }

        // ItemsControl basit bağlama kullandığı için listeyi yeniden bağlamak en ucuz yenileme.
        ActionsList.ItemsSource = null;
        ActionsList.ItemsSource = _items;
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        ApprovedActions = _items.Where(i => i.IsApproved).Select(i => i.Action).ToList();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        ApprovedActions = [];
        DialogResult = false;
        Close();
    }
}
