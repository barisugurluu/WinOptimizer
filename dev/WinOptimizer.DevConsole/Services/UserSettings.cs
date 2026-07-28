using System.Text.Json;

namespace WinOptimizer.DevConsole.Services;

/// <summary>
/// DevConsole kullanici ayarlari — dotnet yolu gecersiz kilmayi saglar.
/// %AppData%\WinOptimizer.DevConsole\settings.json olarak kalici saklanir.
/// </summary>
public sealed class UserSettings
{
    /// <summary>Dotnet exe yolunu elle gecersiz kilmak (bos = otomatik tespit).</summary>
    public string? DotnetOverride { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>Ayarlar dosyasinin tam yolu.</summary>
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinOptimizer.DevConsole", "settings.json");

    /// <summary>Kaynaktan yukler; dosya yoksa bos ayar doner.</summary>
    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<UserSettings>(json, JsonOpts) ?? new UserSettings();
            }
        }
        catch { /* bozuk ayar -> varsayilan */ }

        return new UserSettings();
    }

    /// <summary>Ayarlari diske kaydeder.</summary>
    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(FilePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* yazilamiyorsa sessiz gec */ }
    }
}