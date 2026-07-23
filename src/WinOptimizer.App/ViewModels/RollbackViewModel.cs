using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.App.ViewModels;

/// <summary>
/// Geri alma zaman çizelgesi ViewModel (master plan Bölüm 12.3 Akış C).
/// Change journal'dan değişiklik kayıtlarını okur ve kart olarak gösterir.
/// </summary>
public partial class RollbackViewModel : ObservableObject
{
    private readonly ChangeJournal _journal;

    [ObservableProperty] private string _statusText = "Kayıtlar yükleniyor…";

    public ObservableCollection<ChangeRecordView> Records { get; } = new();

    public RollbackViewModel(ChangeJournal journal) => _journal = journal;

    /// <summary>Son 7 günün kayıtlarını yükler.</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        Records.Clear();
        int total = 0;
        for (int i = 0; i < 7; i++)
        {
            var day = DateTime.UtcNow.AddDays(-i);
            var records = await _journal.ReadDayAsync(day);
            foreach (var r in records)
            {
                Records.Add(new ChangeRecordView
                {
                    Id = r.Id,
                    Timestamp = r.Timestamp.LocalDateTime.ToString("dd.MM.yyyy HH:mm"),
                    Module = r.Module,
                    Operation = r.Operation.ToString(),
                    Target = r.Target,
                    Note = r.Note ?? string.Empty
                });
                total++;
            }
        }
        StatusText = total > 0 ? $"{total} değişiklik kaydı bulundu." : "Kayıt yok.";
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
}
