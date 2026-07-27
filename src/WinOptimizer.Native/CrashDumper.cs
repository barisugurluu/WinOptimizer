using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace WinOptimizer.Native;

/// <summary>
/// Çökme anında minidump üretir (master plan §19 — çökme yakalama / minidump).
/// <see cref="AppDomain.UnhandledException"/> işleyicisinde çağrılır; üretilen
/// dump dosyası (<c>crash-&lt;zaman&gt;.dmp</c>) teşhis paketi için saklanır.
/// Asla ek istisna fırlatmaz — çökme işleyicisinde güvenli olması gerekir.
/// </summary>
public static class CrashDumper
{
    [Flags]
    private enum MiniDumpType : uint
    {
        MiniDumpNormal = 0x00000000,
        MiniDumpWithThreadInfo = 0x00001000
    }

    [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MiniDumpWriteDump(
        IntPtr hProcess, uint processId, IntPtr hFile,
        MiniDumpType dumpType, IntPtr exceptionParam, IntPtr userStreamParam, IntPtr callbackParam);

    /// <summary>
    /// Mevcut süreç için bir minidump yazar. Başarısız olursa en azından exception
    /// metnini bir <c>.txt</c> yan dosyasına bırakır. Hata durumunda sessiz kalır.
    /// </summary>
    /// <param name="dumpDir">Dump dosyalarının yazılacağı dizin.</param>
    /// <param name="exception">Çökme istisnası (metin yedeği için); null olabilir.</param>
    public static void Write(string dumpDir, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(dumpDir);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(dumpDir, "crash-" + stamp + ".dmp");

            using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var proc = Process.GetCurrentProcess();
            bool ok = MiniDumpWriteDump(proc.Handle, (uint)proc.Id, fs.SafeFileHandle.DangerousGetHandle(),
                MiniDumpType.MiniDumpNormal | MiniDumpType.MiniDumpWithThreadInfo,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (!ok && exception is not null)
            {
                File.WriteAllText(path + ".txt", exception.ToString());
            }
        }
        catch
        {
            // Çökme işleyicisinde asla ek istisna fırlatma.
        }
    }
}
