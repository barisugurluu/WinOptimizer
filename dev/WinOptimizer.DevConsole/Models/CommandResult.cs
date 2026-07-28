namespace WinOptimizer.DevConsole.Models;

/// <summary>Bir komut calistirma sonucu.</summary>
/// <param name="Title">Komut basligi.</param>
/// <param name="ExitCode">0 basari; 0> farkli basarisizlik; null henuz bitmedi/iptal.</param>
/// <param name="Duration">Toplam sure.</param>
public sealed record CommandResult(string Title, int? ExitCode, TimeSpan Duration)
{
    public bool IsSuccess => ExitCode == 0;
    public string StatusIcon => ExitCode switch
    {
        0 => "OK",
        null => "...",
        _ => "FAIL"
    };
}

/// <summary>Tek bir cikti satiri (seviye + metin). Renklendirme icin kullanilir.</summary>
public sealed record OutputLine(string Text, OutputLevel Level)
{
    public DateTime Timestamp { get; } = DateTime.Now;
}

/// <summary>Cikti onem seviyesi — renk eslemesi icin.</summary>
public enum OutputLevel
{
    Info,
    Warning,
    Error,
    Success,
    Command
}