using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.Core;
using WinOptimizer.Orchestration;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Geri alma zaman çizelgesi ViewModel (master plan Bölüm 12.3 Akış C).
/// Change journal kayıtlarını kart olarak listeler ve seçilen kaydı
/// <see cref="RollbackService"/> üzerinden gerçekten geri alır.
/// </summary>
public partial class RollbackViewModel : ObservableObject
{
    private readonly RollbackService _rollback;

    [ObservableProperty] private string _statusText = "Kayıtlar yükleniyor…";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<ChangeRecordView> Records { get; } = new();

    public RollbackViewModel(RollbackService rollback) => _rollback = rollback;

    /// <summary>Son 7 günün kayıtlarını (en yeniden eskiye) yükler.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        Records.Clear();
        var records = await _rollback.ReadRecentAsync();
        foreach (var r in records)
        {
            Records.Add(ChangeRecordView.From(r));
        }
        StatusText = Records.Count > 0
            ? $"{Records.Count} değişiklik kaydı bulundu."
            : "Kayıt yok.";
    }

    /// <summary>Seçilen değişikliği geri alır ve listeyi tazeler.</summary>
    [RelayCommand]
    private async Task RollbackAsync(ChangeRecordView? record)
    {
        if (record is null || IsBusy) return;

        IsBusy = true;
        try
        {
            var outcome = await _rollback.RollbackByIdAsync(record.Id);
            StatusText = outcome.IsSuccess
                ? $"Geri alındı: {record.Target} — {outcome.Message}"
                : $"Geri alınamadı: {record.Target} — {outcome.Message}";

            // Geri almanın kendisi de journal'a yazıldığı için listeyi tazeliyoruz.
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>UI için change journal kaydının görünüm modeli.</summary>
public sealed class ChangeRecordView
{
    public string Id { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public static ChangeRecordView From(ChangeRecord r) => new()
    {
        Id = r.Id,
        Timestamp = r.Timestamp.LocalDateTime.ToString("dd.MM.yyyy HH:mm"),
        Module = r.Module,
        Operation = r.Operation.ToString(),
        Target = r.Target,
        Note = r.Note ?? string.Empty
    };
}
