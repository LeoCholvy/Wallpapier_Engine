using System.Runtime.InteropServices;

namespace Wallpapier.WinClient.Services;

public static partial class SystemIntegration
{
    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    public static bool SetWallpaper(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return false;
        }

        return SystemParametersInfo(
            SPI_SETDESKWALLPAPER,
            0,
            fullPath,
            SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
        );
    }
}