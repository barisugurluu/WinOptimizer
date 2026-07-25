using System.Diagnostics;

namespace WinOptimizer.Updater;

/// <summary>
/// MSI paketini kurar (master plan Bölüm 20.6). <c>msiexec</c> yeni sürümü kurarken
/// eski sürümü kaldırır (MajorUpgrade). Kurulumu başlatır ve hemen döner; uygulama kapanmalıdır.
/// Geri alma: kurulumdan önce Windows Sistem Geri Yükleme noktası alınmalıdır (çağıranın sorumluluğu;
/// bkz. <c>WinOptimizer.Safety.RestorePointService</c>).
/// </summary>
public sealed class UpdateInstaller
{
    /// <summary>
    /// MSI'yı kurar. <paramref name="quiet"/> true ise tamamen sessiz (<c>/qn</c>), değilse temel UI (<c>/qb</c>).
    /// Yeniden başlatma istenmez (<c>/norestart</c>).
    /// </summary>
    /// <returns>Başlatılan msiexec işlemi (bekleyen).</returns>
    /// <exception cref="FileNotFoundException">MSI paketi diskte yok.</exception>
    public Process Install(string msiPath, bool quiet = true)
    {
        if (!File.Exists(msiPath))
            throw new FileNotFoundException("MSI paketi bulunamadı.", msiPath);

        string args = quiet ? $"/i \"{msiPath}\" /qn /norestart" : $"/i \"{msiPath}\" /qb /norestart";
        var psi = new ProcessStartInfo("msiexec.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        return Process.Start(psi) ?? throw new InvalidOperationException("msiexec başlatılamadı.");
    }
}
