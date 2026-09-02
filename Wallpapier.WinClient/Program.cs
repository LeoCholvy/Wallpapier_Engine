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

        var db = new DatabaseService();
        var api = new ApiClient(db);
        var wallpaperManager = new WallpaperManager(db);
        var syncManager = new SyncManager(db, api, wallpaperManager);

        // Premier changement au démarrage si des photos existent
        wallpaperManager.ApplyNextWallpaper();

        // Lancement immédiat de la première vérification de synchronisation puis planification
        _ = syncManager.ExecuteSyncProcessAsync();
        syncManager.Start();

        Application.Run(new TrayApplicationContext(db, api, wallpaperManager, syncManager));
    }
}