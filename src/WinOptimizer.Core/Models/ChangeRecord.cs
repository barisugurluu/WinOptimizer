using System.Text.Json.Serialization;

namespace WinOptimizer.Core;

/// <summary>
/// Change journal'a (journal/YYYY-MM-DD.jsonl) yazılan tek bir değişiklik kaydı.
/// Her yazma/silme/tweak geri alınabilir olmak ZORUNDA (master plan Bölüm 1.2 & 16.3).
/// </summary>
public sealed class ChangeRecord
{
    /// <summary>Benzersiz kayıt kimliği (kısa GUID).</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N").Substring(0, 8);

    /// <summary>UTC zaman damgası (ISO 8601).</summary>
    [JsonPropertyName("ts")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Değişikliği yapan modül (ör. "SystemTweaker").</summary>
    [JsonPropertyName("module")]
    public string Module { get; init; } = string.Empty;

    /// <summary>İşlem türü.</summary>
    [JsonPropertyName("op")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChangeOperationType Operation { get; init; }

    /// <summary>Etkilenen hedef (registry yolu, dosya yolu, servis adı...).</summary>
    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    /// <summary>Önceki değer (geri alma için).</summary>
    [JsonPropertyName("prev")]
    public string? PreviousValue { get; init; }

    /// <summary>Yeni değer.</summary>
    [JsonPropertyName("next")]
    public string? NewValue { get; init; }

    /// <summary>Alınan yedek dosyasının göreceli yolu (ör. registry .reg export'u).</summary>
    [JsonPropertyName("backup")]
    public string? Backup { get; init; }

    /// <summary>Ek açıklama (yerelleştirilmiş olabilir).</summary>
    [JsonPropertyName("note")]
    public string? Note { get; init; }
}
