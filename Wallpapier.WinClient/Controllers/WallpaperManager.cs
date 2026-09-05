using Wallpapier.WinClient.Models;
using Wallpapier.WinClient.Services;

namespace Wallpapier.WinClient.Controllers;

public class WallpaperManager
{
    private readonly DatabaseService _db;
    private readonly Random _random = new();

    public LocalPhoto? CurrentPhoto { get; private set; }
    public bool IsCurrentPhotoFresh { get; private set; }
    
    public event Action<LocalPhoto>? OnWallpaperChanged;

    public WallpaperManager(DatabaseService db)
    {
        _db = db;
    }

    public bool ApplyNextWallpaper()
    {
        var candidate = SelectNextPhoto(CurrentPhoto?.Id);
        if (candidate == null) return false;

        // Si l'image n'existe plus sur le disque
        if (!File.Exists(candidate.Filepath))
        {
            _db.DeletePhoto(candidate.Id);
            return ApplyNextWallpaper();
        }

        var success = SystemIntegration.SetWallpaper(candidate.Filepath);
        if (success)
        {
            _db.MarkAsShown(candidate.Id);
            candidate.HasBeenShown = true;
            CurrentPhoto = candidate;
            OnWallpaperChanged?.Invoke(candidate);
            return true;
        }

        return false;
    }

    private LocalPhoto? SelectNextPhoto(string? currentIdToExclude)
    {
        var unshownNormal = _db.GetNextUnshownNormalPhoto(currentIdToExclude);
        if (unshownNormal != null)
        {
            IsCurrentPhotoFresh = true;
            return unshownNormal;
        }

        IsCurrentPhotoFresh = false;

        var ratioStr = _db.GetSetting("FavRatio") ?? "20";
        if (!int.TryParse(ratioStr, out var favRatioPercent)) favRatioPercent = 20;

        var roll = _random.Next(0, 100);
        LocalPhoto? candidate = null;

        // Tentative selon les probabilités
        if (roll < favRatioPercent)
        {
            candidate = _db.GetRandomFavoritePhoto(currentIdToExclude) ?? _db.GetRandomShownNormalPhoto(currentIdToExclude);
        }
        else
        {
            candidate = _db.GetRandomShownNormalPhoto(currentIdToExclude) ?? _db.GetRandomFavoritePhoto(currentIdToExclude);
        }

        // Ultime secours : s'il n'y a VRAIMENT aucune autre photo, on lève l'exclusion pour au moins afficher quelque chose
        return candidate 
            ?? _db.GetRandomShownNormalPhoto(null) 
            ?? _db.GetRandomFavoritePhoto(null) 
            ?? _db.GetNextUnshownNormalPhoto(null);
    }
}