using System.Runtime.InteropServices;

namespace WinOptimizer.Native;

/// <summary>
/// shell32 P/Invoke — Geri Dönüşüm kutusu boşaltma (SHEmptyRecycleBin).
/// Bkz. master plan Bölüm 11.5 ve 3.1 (CleanEngine — Geri Dönüşüm alt görevi).
/// </summary>
public static class Shell32
{
    [Flags]
    public enum RecycleFlags : uint
    {
        SHERB_NOCONFIRMATION = 0x00000001,
        SHERB_NOPROGRESSUI = 0x00000002,
        SHERB_NOSOUND = 0x00000004
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, RecycleFlags dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern uint SHQueryRecycleBin(string? pszRootPath, ref SHQueryRBInfo psps);

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct SHQueryRBInfo
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    /// <summary>Tüm sürücülerin geri dönüşüm kutusundaki toplam boyutu döndürür (bayt).</summary>
    public static long GetRecycleBinSize(string? rootPath = null)
    {
        var info = new SHQueryRBInfo { cbSize = Marshal.SizeOf<SHQueryRBInfo>() };
        SHQueryRecycleBin(rootPath, ref info);
        return info.i64Size;
    }

    /// <summary>Geri dönüşüm kutusunu boşaltır (onay diyaloğu olmadan, sessizce).</summary>
    /// <returns>Başarılıysa true (S_OK = 0).</returns>
    public static bool EmptyRecycleBin(string? rootPath = null)
    {
        uint result = SHEmptyRecycleBin(IntPtr.Zero, rootPath,
            RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND);
        return result == 0; // S_OK
    }
}

