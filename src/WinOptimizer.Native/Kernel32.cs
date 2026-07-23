using System.Runtime.InteropServices;

namespace WinOptimizer.Native;

/// <summary>
/// kernel32 P/Invoke bildirimleri (EmptyWorkingSet, GetForegroundWindow vb.).
/// Bkz. master plan Bölüm 11.1 — RAM optimizasyonu için süreç working set boşaltma.
/// </summary>
public static class Kernel32
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FreeLibrary(IntPtr hModule);

    /// <summary>Ön plan (aktif) pencere tutamacını döndürür.</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EmptyWorkingSet(IntPtr hProcess);
}

/// <summary>
/// psapi süreç listeleme P/Invoke'ları. RAM optimizasyonunda tüm süreçler taranır.
/// </summary>
public static class PsapiNative
{
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumProcesses([Out] uint[] lpidProcess, int cb, out int lpcbNeeded);
}
