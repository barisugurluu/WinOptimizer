using System.Security.Principal;

namespace WinOptimizer.Orchestration.Preflight;

/// <summary>
/// Yönetici ayrıcalığı sorgusu. App, CLI ve gereksinim kontrolü aynı yanıtı kullanır.
/// </summary>
public static class Elevation
{
    /// <summary>Süreç yükseltilmiş (Administrators rolünde) mi?</summary>
    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

/// <summary>
/// Bir ön koşul sağlanmadığı için işlem başlatılamadığında atılır. Mesajı doğrudan
/// kullanıcıya gösterilebilecek şekilde <b>eyleme dönüştürülebilir</b> olmalıdır
/// ("şunu yap" bilgisi içermeli), çünkü CLI bunu tek satır olarak yazdırır.
/// </summary>
public sealed class PreflightException : Exception
{
    public PreflightException(string message) : base(message) { }

    public PreflightException(string message, Exception innerException)
        : base(message, innerException) { }

    public PreflightException() { }
}
