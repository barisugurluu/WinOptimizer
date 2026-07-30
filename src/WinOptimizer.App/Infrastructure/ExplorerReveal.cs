using System.Diagnostics;

namespace WinOptimizer.App.Infrastructure;

/// <summary>
/// Dosya Gezgini'nde bir dosyayı/klasörü açar. Ayarlar sekmesi (teşhis paketi) ve
/// hata penceresi (günlük klasörü) aynı davranışı kullanır.
/// </summary>
public static class ExplorerReveal
{
    /// <summary>Dosyayı Gezgin'de <b>seçili</b> olarak gösterir.</summary>
    public static void SelectFile(string filePath) =>
        Launch("/select,\"" + filePath + "\"");

    /// <summary>Klasörü Gezgin'de açar.</summary>
    public static void OpenFolder(string folderPath) =>
        Launch("\"" + folderPath + "\"");

    private static void Launch(string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = arguments,
                UseShellExecute = true,
            })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                     or InvalidOperationException)
        {
            // Gezgin açılamadıysa sorun değil — yol her zaman metin olarak da gösterilir,
            // kullanıcı kopyalayıp elle açabilir.
        }
    }
}
