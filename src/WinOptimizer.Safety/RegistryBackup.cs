using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Safety;

/// <summary>
/// Registry anahtarını <c>reg export</c> ile .reg dosyasına yedekler.
/// Her registry yazma işleminden ÖNCE çağrılır — geri almanın ön koşulu
/// (master plan Bölüm 3.9 SystemTweaker güvenlik kuralları).
/// </summary>
public sealed class RegistryBackup
{
    private readonly string _backupDir;
    private readonly ILogger<RegistryBackup> _logger;

    public RegistryBackup(string baseDir, ILogger<RegistryBackup> logger)
    {
        _backupDir = Path.Combine(baseDir, "backups", "registry");
        Directory.CreateDirectory(_backupDir);
        _logger = logger;
    }

    /// <summary>
    /// Bir registry anahtarını .reg dosyasına yedekler.
    /// </summary>
    /// <param name="hive">"HKLM", "HKCU", "HKCR", "HKU", "HKCC" biçiminde kök.</param>
    /// <param name="subKey">Anahtar yolu (ör. "SOFTWARE\Microsoft\...").</param>
    /// <returns>Oluşturulan yedek dosyasının tam yolu; başarısızsa null.</returns>
    public async Task<string?> ExportAsync(string hive, string subKey)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var safeKey = string.Concat(subKey.Split(Path.GetInvalidFileNameChars())).TrimStart('\\');
        var fileName = $"{hive}_{safeKey}_{stamp}.reg";
        var filePath = Path.Combine(_backupDir, fileName);

        var psi = new ProcessStartInfo("reg.exe", $"export {hive}\\{subKey} \"{filePath}\" /y")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null)
            {
                return null;
            }

            await p.WaitForExitAsync();
            if (p.ExitCode == 0 && File.Exists(filePath))
            {
                _logger.LogDebug("Registry yedeklendi: {Path}", filePath);
                return filePath;
            }

            _logger.LogWarning("Registry export başarısız (exit {Code}): {Hive}\\{Key}",
                p.ExitCode, hive, subKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registry export hatası: {Hive}\\{Key}", hive, subKey);
            return null;
        }
    }
}
