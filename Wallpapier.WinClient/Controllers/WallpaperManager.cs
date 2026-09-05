using Wallpapier.WinClient.Models;
using Wallpapier.WinClient.Services;

namespace Wallpapier.WinClient.Controllers;

public class WallpaperManager
{
    private readonly DatabaseService _db;
    private readonly Random _random = new();

    public LocalPhoto? CurrentPhoto { get; private set; }
    public bool IsCurrentPhotoFresh { get; private set; }
    
    public event Action<LocalPhoto, bool>? OnWallpaperChanged;

    public WallpaperManager(DatabaseService db)
    {
        _db = db;
    }

    public bool ApplyNextWallpaper()
    {
        var candidate = SelectNextPhoto(CurrentPhoto?.Id);
        if (candidate == null) return false;

        if (!File.Exists(candidate.Filepath))
        {
            _db.DeletePhoto(candidate.Id);
            return ApplyNextWallpaper();
        }

        var success = SystemIntegration.SetWallpaper(candidate.Filepath);
        if (success)
        {
            // On vérifie si c'est la toute première fois
            bool isFirstShow = !candidate.HasBeenShown;
        
            if (isFirstShow) 
            {
                _db.MarkAsShown(candidate.Id);
                candidate.HasBeenShown = true;
            }

            CurrentPhoto = candidate;
            // On déclenche l'événement avec le booléen
            OnWallpaperChanged?.Invoke(candidate, isFirstShow); 
            return true;
        }

        return false;
    }

    private LocalPhoto? SelectNextPhoto(string? currentIdToExclude)
    {
        // On priorise TOUTE nouvelle photo non vue, favorite ou non
        var unshown = _db.GetNextUnshownPhoto(currentIdToExclude);
        if (unshown != null)
        {
            IsCurrentPhotoFresh = true;
            return unshown;
        }

        IsCurrentPhotoFresh = false;

        var ratioStr = _db.GetSetting("FavRatio") ?? "20";
        if (!int.TryParse(ratioStr, out var favRatioPercent)) favRatioPercent = 20;

        var roll = _random.Next(0, 100);
        LocalPhoto? candidate = null;

        if (roll < favRatioPercent)
        {
            candidate = _db.GetRandomFavoritePhoto(currentIdToExclude) ?? _db.GetRandomShownNormalPhoto(currentIdToExclude);
        }
        else
        {
            candidate = _db.GetRandomShownNormalPhoto(currentIdToExclude) ?? _db.GetRandomFavoritePhoto(currentIdToExclude);
        }

        return candidate 
               ?? _db.GetRandomShownNormalPhoto(null) 
               ?? _db.GetRandomFavoritePhoto(null) 
               ?? _db.GetNextUnshownPhoto(null); // Mis à jour ici aussi
    }
}