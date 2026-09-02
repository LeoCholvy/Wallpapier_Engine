using Wallpapier.WinClient.Models;
using Wallpapier.WinClient.Services;

namespace Wallpapier.WinClient.Controllers;

public class WallpaperManager
{
    private readonly DatabaseService _db;
    private readonly Random _random = new();

    public LocalPhoto? CurrentPhoto { get; private set; }
    public event Action<LocalPhoto>? OnWallpaperChanged;

    public WallpaperManager(DatabaseService db)
    {
        _db = db;
    }

    public bool ApplyNextWallpaper()
    {
        var candidate = SelectNextPhoto();
        if (candidate == null)
        {
            return false;
        }

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

    private LocalPhoto? SelectNextPhoto()
    {
        // Règle 1 : Photo normale la plus récente non encore affichée
        var unshownNormal = _db.GetNextUnshownNormalPhoto();
        if (unshownNormal != null)
        {
            return unshownNormal;
        }

        // Règle 2 : Tirage pondéré aléatoire basé sur le ratio
        var ratioStr = _db.GetSetting("FavRatio") ?? "20";
        if (!int.TryParse(ratioStr, out var favRatioPercent))
        {
            favRatioPercent = 20;
        }

        var roll = _random.Next(0, 100);
        if (roll < favRatioPercent)
        {
            return _db.GetRandomFavoritePhoto() ?? _db.GetRandomShownNormalPhoto();
        }

        return _db.GetRandomShownNormalPhoto() ?? _db.GetRandomFavoritePhoto();
    }
}