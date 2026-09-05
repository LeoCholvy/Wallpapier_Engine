using System.Runtime.InteropServices;

namespace Wallpapier.WinClient.Services;

public static partial class SystemIntegration
{
    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public static bool SetWallpaper(string fullPath)
    {
        if (!File.Exists(fullPath)) return false;

        return SystemParametersInfo(
            SPI_SETDESKWALLPAPER,
            0,
            fullPath,
            SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
        );
    }

    // Détecte si le bureau est caché (Jeu plein écran ou fenêtre maximisée)
    public static bool IsDesktopObscured()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return false;
        
        IntPtr desktop = GetDesktopWindow();
        IntPtr shell = GetShellWindow();
        
        // Si on a cliqué sur le bureau ou la barre des tâches, il n'est pas caché
        if (hWnd == desktop || hWnd == shell) return false;

        GetWindowRect(hWnd, out RECT appBounds);
        IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        
        MONITORINFO mi = new MONITORINFO();
        mi.cbSize = (uint)Marshal.SizeOf(mi);
        
        if (GetMonitorInfo(hMonitor, ref mi))
        {
            // Vérifie si la fenêtre courante recouvre intégralement la zone de travail (rcWork)
            return appBounds.left <= mi.rcWork.left &&
                   appBounds.top <= mi.rcWork.top &&
                   appBounds.right >= mi.rcWork.right &&
                   appBounds.bottom >= mi.rcWork.bottom;
        }
        return false;
    }
}