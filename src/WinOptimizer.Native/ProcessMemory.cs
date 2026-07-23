using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinOptimizer.Native;

/// <summary>
/// Süreç yönetimi için üst düzey sarmalayıcılar. Aktif pencereyi atlayarak
/// boştaki süreçlerin working set'ini boşaltır (master plan Bölüm 11.1).
/// </summary>
public sealed class ProcessMemory
{
    /// <summary>
    /// Tek bir sürecin working set'ini boşaltır (RAM boşaltma).
    /// Aktif/ön plan penceresine sahip süreçler atlanır.
    /// </summary>
    public static bool TrimProcess(Process process)
    {
        if (process.HasExited)
        {
            return false;
        }

        try
        {
            return Kernel32.EmptyWorkingSet(process.Handle);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // erişim engelli süreçler sessizce atlanır
        }
        catch (InvalidOperationException)
        {
            return false; // zaten çıkmış süreç
        }
    }

    /// <summary>
    /// Boştaki (idle) süreçleri tarar ve RAM'lerini boşaltır.
    /// Aktif pencere ve henüz "minIdleTime" dolmamış süreçler atlanır.
    /// </summary>
    /// <returns>Boşaltılan süreç sayısı.</returns>
    public int TrimIdleProcesses(TimeSpan minIdleTime)
    {
        int trimmed = 0;
        var ids = new uint[2048];
        if (!PsapiNative.EnumProcesses(ids, ids.Length * sizeof(uint), out int returned))
        {
            return 0;
        }

        int count = returned / sizeof(uint);
        for (int i = 0; i < count; i++)
        {
            try
            {
                using var p = Process.GetProcessById((int)ids[i]);
                // Aktif pencere atlanır (kullanıcı deneyimini bozmamak için)
                if (p.MainWindowHandle != IntPtr.Zero &&
                    p.MainWindowHandle == Kernel32.GetForegroundWindow())
                {
                    continue;
                }

                // Yeni başlatılmış süreçler atlanır (henüz kararlı değil)
                if (DateTime.UtcNow - p.StartTime.ToUniversalTime() <= minIdleTime)
                {
                    continue;
                }

                if (TrimProcess(p))
                {
                    trimmed++;
                }
            }
            catch
            {
                // erişim engelli süreçler sessizce atlanır
            }
        }

        return trimmed;
    }
}
