using System.Runtime.InteropServices;

namespace WinOptimizer.Native;

/// <summary>
/// wintrust P/Invoke bildirimleri — Authenticode imza doğrulama (WinVerifyTrust).
/// Bkz. master plan Bölüm 17.2 — servis/kurulum, ikilinin imzasını başlatılmadan önce doğrular.
/// </summary>
public static class WinTrust
{
    // WINTRUST_ACTION_GENERIC_VERIFY_V2 — Authenticode (dijital imza) doğrulaması.
    private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 =
        new(0x00AAC56B, 0xCD44, 0x11D0, 0x8C, 0xC2, 0x00, 0xC0, 0x4F, 0xC2, 0x95, 0xEE);

    // UIChoice: hiç UI gösterme (sunucu/servis ortamı).
    private const int WTD_UI_NONE = 2;
    // RevocationChecks: iptal listesi denetimi yapma (çevrimdışı senaryolarda güvenilir).
    private const int WTD_REVOKE_NONE = 0;
    // UnionChoice: dosya tabanlı doğrulama.
    private const int WTD_CHOICE_FILE = 1;
    // StateAction: state kaydetme (tek seferlik doğrulama).
    private const int WTD_STATEACTION_IGNORE = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public int cbStruct;
        public IntPtr pcwszFilePath;     // LPCWSTR — doğrulanacak dosya yolu
        public IntPtr hFile;             // opsiyonel; nullptr = yol ile aç
        public IntPtr pgKnownSubject;    // opsiyonel; nullptr
    }

    // WINTRUST_DATA — sequential layout; union üyesini tek IntPtr (pFile) olarak temsil ederuz.
    // C++ union'ı tek pointer boyutu (8 byte x64 / 4 byte x86) kaplar; pFile da aynı boyutta.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;             // WINTRUST_FILE_INFO* (union'un ilk üyesi)
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public string? pwszURLReference;
        public IntPtr psProvInfoContext; // Dul (legacy) — sıralama için korunur
        public uint dwUIContext;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(
        IntPtr hwnd,
        [In] ref Guid pgActionID,
        [In] ref WINTRUST_DATA pWVTData);

    /// <summary>
    /// Dosyanın gömülü Authenticode imzasını doğrular (master plan Bölüm 17.2).
    /// Servis, başlatılmadan önce kendi ikilisini / güncelleme paketini doğrulamak için kullanır.
    /// </summary>
    /// <param name="filePath">Doğrulanacak exe/dll/msi dosyasının tam yolu.</param>
    /// <returns>İmza geçerliyse <c>true</c>; imza yoksa, geçersizse veya dosya bulunamazsa <c>false</c>.</returns>
    public static bool VerifyEmbeddedSignature(string filePath)
    {
        if (!OperatingSystem.IsWindows())
            return false;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        // WINTRUST_FILE_INFO başlat — yolu yönetilmeyen belleğe kopyala.
        var fileInfo = new WINTRUST_FILE_INFO
        {
            cbStruct = Marshal.SizeOf<WINTRUST_FILE_INFO>(),
            pcwszFilePath = Marshal.StringToHGlobalUni(filePath),
            hFile = IntPtr.Zero,
            pgKnownSubject = IntPtr.Zero
        };

        var data = new WINTRUST_DATA
        {
            cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
            dwUIChoice = WTD_UI_NONE,
            fdwRevocationChecks = WTD_REVOKE_NONE,
            dwUnionChoice = WTD_CHOICE_FILE,
            pFile = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>()),
            dwStateAction = WTD_STATEACTION_IGNORE,
            pwszURLReference = null,
            psProvInfoContext = IntPtr.Zero
        };

        // FileInfo struct'ını yönetilmeyen belleğe kopyala (pFile -> WINTRUST_FILE_INFO*).
        Marshal.StructureToPtr(fileInfo, data.pFile, fDeleteOld: false);

        try
        {
            var actionId = WINTRUST_ACTION_GENERIC_VERIFY_V2;
            // 0 = ERROR_SUCCESS; imza geçersizse negatif olmayan hata kodu döner.
            return WinVerifyTrust(IntPtr.Zero, ref actionId, ref data) == 0;
        }
        finally
        {
            Marshal.DestroyStructure<WINTRUST_FILE_INFO>(data.pFile);
            Marshal.FreeHGlobal(data.pFile);
            if (fileInfo.pcwszFilePath != IntPtr.Zero)
                Marshal.FreeHGlobal(fileInfo.pcwszFilePath);
        }
    }
}
