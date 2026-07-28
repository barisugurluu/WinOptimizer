namespace WinOptimizer.DevConsole.Models;

/// <summary>
/// Tek bir calistirilabilir komut tanimi. Buton katalogundan
/// <see cref="Services.CommandRunner"/> tarafindan calistirilir.
/// </summary>
public sealed class DevCommand
{
    /// <summary>Gosterilen ad (buton metni).</summary>
    public required string Title { get; init; }

    /// <summary>Kategori (grup basligi).</summary>
    public required string Category { get; init; }

    /// <summary>Calistirilacak dosya (dotnet/powershell/exe) veya "folder" (klasor ac).</summary>
    public required string File { get; init; }

    /// <summary>Argumanlar (komut enjeksiyonu onlenmis: ArgumentList ile verilir).</summary>
    public IReadOnlyList<string> Args { get; init; } = Array.Empty<string>();

    /// <summary>Bu bir klasor-acma komutu mu (süreç baslatma degil).</summary>
    public bool IsFolder { get; init; }

    /// <summary>Calisma dizini (cozum koku icin goreli). Null = cozum koku.</summary>
    public string? WorkingDir { get; init; }

    /// <summary>Acilama (tooltip).</summary>
    public string? Description { get; init; }
}