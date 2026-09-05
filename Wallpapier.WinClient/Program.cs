using Microsoft.Win32;
using Wallpapier.WinClient.Controllers;
using Wallpapier.WinClient.Services;
using Wallpapier.WinClient.Views;

namespace Wallpapier.WinClient;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // 1. Inscription au démarrage de Windows (Registre)
        var appName = "Wallpapier";
        var exePath = Application.ExecutablePath;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)!)
        {
            if (key.GetValue(appName) == null)
            {
                key.SetValue(appName, $"\"{exePath}\"");
            }
        }

        // 2. Initialisation des services
        var db = new DatabaseService();
        var api = new ApiClient(db);
        var wallpaperManager = new WallpaperManager(db);
        var syncManager = new SyncManager(db, api, wallpaperManager);

        // 3. Premier changement au démarrage si des photos existent
        wallpaperManager.ApplyNextWallpaper();

        // 4. Lancement immédiat de la première vérification de synchronisation puis planification
        _ = syncManager.ExecuteSyncProcessAsync(forceFull: false);
        syncManager.Start();

        // 5. Lancement de l'application en arrière-plan (System Tray)
        Application.Run(new TrayApplicationContext(db, api, wallpaperManager, syncManager));
    }
}