using System.Diagnostics;

namespace WinOptimizer.Updater;

/// <summary>
/// İndirilen Inno Setup kurulumunu (<c>*-setup.exe</c>) çalıştırır (master plan Bölüm 20.6).
/// Kurulum önceki sürümü kendisi kaldırır. İşlemi başlatır ve hemen döner; çağıran
/// uygulamayı kapatmalıdır (aksi halde kurulum kilitli dosyalarla karşılaşır — kurulum
/// bunu <c>AppMutex</c> ile fark edip kapatmayı teklif eder).
/// Geri alma: kurulumdan önce Windows Sistem Geri Yükleme noktası alınmalıdır (çağıranın
/// sorumluluğu; bkz. <c>WinOptimizer.Safety.RestorePointService</c>).
/// </summary>
public sealed class UpdateInstaller
{
    /// <summary>
    /// Kurulumu başlatır. <paramref name="quiet"/> true ise hiç arayüz göstermez
    /// (<c>/VERYSILENT</c>), değilse yalnızca ilerleme penceresi (<c>/SILENT</c>).
    /// Yeniden başlatma istenmez (<c>/NORESTART</c>).
    /// </summary>
    /// <returns>Başlatılan kurulum işlemi.</returns>
    /// <exception cref="FileNotFoundException">Kurulum paketi diskte yok.</exception>
    public Process Install(string setupPath, bool quiet = true)
    {
        if (!File.Exists(setupPath))
            throw new FileNotFoundException("Kurulum paketi bulunamadı.", setupPath);

        // Inno Setup anahtarları (msiexec DEĞİL — MSI hattı kaldırıldı).
        // ArgumentList: yol string birleştirmeyle komuta gömülmez (§17.5).
        var psi = new ProcessStartInfo(setupPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(quiet ? "/VERYSILENT" : "/SILENT");
        psi.ArgumentList.Add("/SUPPRESSMSGBOXES");
        psi.ArgumentList.Add("/NORESTART");

        return Process.Start(psi) ?? throw new InvalidOperationException("Kurulum başlatılamadı.");
    }
}
