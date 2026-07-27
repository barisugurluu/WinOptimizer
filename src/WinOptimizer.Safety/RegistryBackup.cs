using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Safety;

/// <summary>
/// Registry anahtarını <c>reg export</c> ile .reg dosyasına yedekler.
/// Her registry yazma işleminden ÖNCE çağrılır — geri almanın ön koşulu
/// (master plan Bölüm 3.9 SystemTweaker güvenlik kuralları).
/// Bütünlük: her .reg yedeği HMAC yan dosyasıyla imzalanır (§17.4).
/// Güvenlik: argümanlar <see cref="ProcessStartInfo.ArgumentList"/> ile dizi olarak
/// verilir — string birleştirme/komut enjeksiyonu yok (§17.5).
/// </summary>
public sealed class RegistryBackup
{
    private readonly string _backupDir;
    private readonly ILogger<RegistryBackup> _logger;
    private readonly IntegrityGuard? _integrity;

    public RegistryBackup(string baseDir, ILogger<RegistryBackup> logger, IntegrityGuard? integrity = null)
    {
        _backupDir = Path.Combine(baseDir, "backups", "registry");
        Directory.CreateDirectory(_backupDir);
        _logger = logger;
        _integrity = integrity;
    }

    /// <summary>
    /// Bir registry anahtarını .reg dosyasına yedekler.
    /// </summary>
    /// <param name="hive">"HKLM", "HKCU", "HKCR", "HKU", "HKCC" biçiminde kök.</param>
    /// <param name="subKey">Anahtar yolu (ör. "SOFTWARE\Microsoft\...").</param>
    /// <returns>Oluşturulan yedek dosyasının tam yolu; başarısızsa null.</returns>
    public async Task<string?> ExportAsync(string hive, string subKey)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var safeKey = string.Concat(subKey.Split(Path.GetInvalidFileNameChars())).TrimStart('\\');
        var fileName = $"{hive}_{safeKey}_{stamp}.reg";
        var filePath = Path.Combine(_backupDir, fileName);

        // ArgumentList: argümanlar ayrı öğeler olarak verilir, enjeksiyona kapalı (§17.5).
        var psi = new ProcessStartInfo("reg.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        psi.ArgumentList.Add("export");
        psi.ArgumentList.Add($"{hive}\\{subKey}");
        psi.ArgumentList.Add(filePath);
        psi.ArgumentList.Add("/y");

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
                if (_integrity is not null)
                {
                    await _integrity.SignFileAsync(filePath);
                }

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
