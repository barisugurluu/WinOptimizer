using System.Text.Json;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;

namespace WinOptimizer.Safety;

/// <summary>
/// Change Journal — yapılan her değişikliği günlük JSONL dosyasına yazar
/// ve geri almada okur (master plan Bölüm 2.1 SafetyNet, 16.3 şema).
/// Dosya adı: journal/YYYY-MM-DD.jsonl (her gün ayrı dosya).
/// Bütünlük: her yazım sonrası <see cref="IntegrityGuard"/> ile HMAC imzalanır
/// (master plan §17.4) — kurcalanma geri almada tespit edilir.
/// </summary>
public sealed class ChangeJournal
{
    private readonly string _journalDir;
    private readonly ILogger<ChangeJournal> _logger;
    private readonly IntegrityGuard? _integrity;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ChangeJournal(string baseDir, ILogger<ChangeJournal> logger, IntegrityGuard? integrity = null)
    {
        _journalDir = Path.Combine(baseDir, "journal");
        Directory.CreateDirectory(_journalDir);
        _logger = logger;
        _integrity = integrity;
    }

    /// <summary>Bugünün journal dosyasının tam yolu.</summary>
    public string GetTodayFilePath() => GetFilePath(DateTime.UtcNow);

    /// <summary>Belirli bir güne ait journal dosyasının yolu (bütünlük doğrulaması için).</summary>
    public string GetFilePath(DateTime day) =>
        Path.Combine(_journalDir, $"journal-{day:yyyy-MM-dd}.jsonl");

    /// <summary>Bir değişikliği günlüğe yazar (ekleme modu, bir satır = bir JSON kayıt).</summary>
    public async Task WriteAsync(ChangeRecord record, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(record, JsonOpts);
        var path = GetTodayFilePath();
        await using (var writer = new StreamWriter(path, append: true))
        {
            await writer.WriteLineAsync(line.AsMemory(), ct);
        }

        if (_integrity is not null)
        {
            await _integrity.SignFileAsync(path, ct);
        }

        _logger.LogDebug("Journal yazıldı: {Module}/{Op} -> {Target}", record.Module, record.Operation, record.Target);
    }

    /// <summary>Birden çok kaydı tek seferde yazar.</summary>
    public async Task WriteRangeAsync(IEnumerable<ChangeRecord> records, CancellationToken ct = default)
    {
        var path = GetTodayFilePath();
        await using (var writer = new StreamWriter(path, append: true))
        {
            foreach (var record in records)
            {
                var line = JsonSerializer.Serialize(record, JsonOpts);
                await writer.WriteLineAsync(line.AsMemory(), ct);
            }
        }

        if (_integrity is not null)
        {
            await _integrity.SignFileAsync(path, ct);
        }
    }

    /// <summary>Belirli bir güne ait tüm kayıtları okur.</summary>
    public async Task<IReadOnlyList<ChangeRecord>> ReadDayAsync(DateTime day, CancellationToken ct = default)
    {
        var path = Path.Combine(_journalDir, $"journal-{day:yyyy-MM-dd}.jsonl");
        if (!File.Exists(path))
        {
            return Array.Empty<ChangeRecord>();
        }

        var results = new List<ChangeRecord>();
        foreach (var line in await File.ReadAllLinesAsync(path, ct))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                results.Add(JsonSerializer.Deserialize<ChangeRecord>(line, JsonOpts)!);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Bozuk journal satırı atlandı: {Path}", path);
            }
        }
        return results;
    }

    /// <summary>Belirli bir kayıt kimliğine göre geri alıncak kaydı bulur.</summary>
    public async Task<ChangeRecord?> FindAsync(string changeId, CancellationToken ct = default)
    {
        foreach (var file in Directory.EnumerateFiles(_journalDir, "journal-*.jsonl")
                                   .OrderByDescending(f => f))
        {
            foreach (var line in await File.ReadAllLinesAsync(file, ct))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var rec = JsonSerializer.Deserialize<ChangeRecord>(line, JsonOpts);
                    if (rec is not null && rec.Id == changeId)
                    {
                        return rec;
                    }
                }
                catch (JsonException)
                {
                    _logger.LogDebug("Bozuk satır atlandı (arama): {Path}", file);
                }
            }
        }
        return null;
    }
}
