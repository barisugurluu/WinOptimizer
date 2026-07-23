using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace WinOptimizer.Safety;

/// <summary>
/// Kritik sistem servislerinin beyaz listesi — bu servislere ASLA dokunulmaz
/// (master plan Bölüm 3.5). Bir işlem hedefi beyaz listedeyse SafetyGuard engeller.
/// </summary>
public sealed class SafetyGuard
{
    /// <summary>
    /// Asla durdurulmayacak/devre dışı bırakılmayacak kritik servisler.
    /// Dokunmak sistemi kullanılamaz hale getirebilir.
    /// </summary>
    public static readonly IReadOnlySet<string> CriticalServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "EventLog", "PlugPlay", "WinDefend", "SecurityHealthService", "wuauserv",
        "Schedule", "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "Winmgmt",
        "gpsvc", "TrkWks", "ProfSvc", "MpSSvc", "Schedule", "Spooler",
        "SystemMetrics", "TextInputManagementService", "CoreMessagingRegistrar",
        "BrokerInfrastructure", "DCOMLauncher", "Power", "UserManager"
    };

    /// <summary>Silinemeyecek / dokunulamayacak kritik dizinler.</summary>
    public static readonly IReadOnlySet<string> ProtectedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        @"C:\Windows\System32",
        @"C:\Windows\SysWOW64",
        @"C:\Windows\System",
        @"C:\Windows\Boot",
        @"C:\Windows\WinSxS",
        @"C:\Windows\Fonts",
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
    };

    private readonly ILogger<SafetyGuard> _logger;

    public SafetyGuard(ILogger<SafetyGuard> logger) => _logger = logger;

    /// <summary>Bir servis adının kritik (korunmalı) olup olmadığını döndürür.</summary>
    public bool IsCriticalService(string serviceName) =>
        CriticalServices.Contains(serviceName);

    /// <summary>Bir yolun korunan bir dizinin altında olup olmadığını kontrol eder.</summary>
    public bool IsProtectedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var full = Path.GetFullPath(path.TrimEnd('\\', '/')).TrimEnd('\\');
        foreach (var protectedFolder in ProtectedFolders)
        {
            if (full.StartsWith(protectedFolder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Bir işleme izin verilip verilmediğini onaylar; izin yoksa günlükler.</summary>
    public bool IsAllowed(string target, out string reason)
    {
        if (!string.IsNullOrEmpty(target))
        {
            // Servis adı olarak kontrol
            if (CriticalServices.Contains(target))
            {
                reason = $"'{target}' kritik bir sistem servisidir — dokunulamaz.";
                _logger.LogWarning("SafetyGuard engelledi: {Reason}", reason);
                return false;
            }

            // Yol olarak kontrol
            if (IsProtectedPath(target))
            {
                reason = $"'{target}' korunan bir sistem dizinidir.";
                _logger.LogWarning("SafetyGuard engelledi: {Reason}", reason);
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }
}
