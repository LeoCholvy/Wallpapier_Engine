using Wallpapier.WinClient.Models;
using Wallpapier.WinClient.Services;

namespace Wallpapier.WinClient.Controllers;

public enum ConnectionStatus
{
    Connected,
    Disconnected,
    Syncing
}

public class SyncManager
{
    private readonly DatabaseService _db;
    private readonly ApiClient _api;
    private readonly WallpaperManager _wallpaperManager;
    private System.Threading.Timer? _scheduleTimer;
    private readonly object _syncLock = new();
    
    private DateTime _lastFullSyncDate = DateTime.MinValue;

    public ConnectionStatus CurrentStatus { get; private set; } = ConnectionStatus.Disconnected;
    public event Action<ConnectionStatus>? OnStatusChanged;

    public SyncManager(DatabaseService db, ApiClient api, WallpaperManager wallpaperManager)
    {
        _db = db;
        _api = api;
        _wallpaperManager = wallpaperManager;
    }

    public void Start()
    {
        PlanNextCycle();
    }

    public void PlanNextCycle()
    {
        var timePerPhotoMin = int.TryParse(_db.GetSetting("TimePerPhoto"), out var t) ? t : 60;
        var syncAnticipationMin = int.TryParse(_db.GetSetting("SyncAnticipationTime"), out var s) ? s : 5;

        var delayBeforeSyncMinutes = Math.Max(1, timePerPhotoMin - syncAnticipationMin);
        var syncDelay = TimeSpan.FromMinutes(delayBeforeSyncMinutes);
        var changeDelay = TimeSpan.FromMinutes(timePerPhotoMin);

        _scheduleTimer?.Dispose();
        _scheduleTimer = new System.Threading.Timer(async _ =>
        {
            await ExecuteSyncProcessAsync();

            var remainingTime = changeDelay - syncDelay;
            if (remainingTime <= TimeSpan.Zero) remainingTime = TimeSpan.FromSeconds(5);

            _ = Task.Delay(remainingTime).ContinueWith(_ =>
            {
                _wallpaperManager.ApplyNextWallpaper();
                PlanNextCycle();
            });

        }, null, syncDelay, Timeout.InfiniteTimeSpan);
    }

    public async Task ExecuteSyncProcessAsync(bool forceFull = false)
    {
        lock (_syncLock)
        {
            if (CurrentStatus == ConnectionStatus.Syncing) return;
            SetStatus(ConnectionStatus.Syncing);
        }

        try
        {
            var lastSync = _db.GetSetting("LastSyncDate");
            
            bool isFullSync = forceFull || 
                              string.IsNullOrWhiteSpace(lastSync) || 
                              (DateTime.Now - _lastFullSyncDate).TotalHours >= 1;

            var manifest = await _api.GetManifestAsync(isFullSync ? null : lastSync);

            if (manifest == null)
            {
                SetStatus(ConnectionStatus.Disconnected);
                return;
            }

            // 1. Suppressions locales
            foreach (var deletedId in manifest.Deleted)
            {
                _db.DeletePhoto(deletedId);
            }

            if (isFullSync)
            {
                var serverIds = manifest.Created.Select(p => p.Id).ToHashSet();
                var localIds = _db.GetAllPhotoIds();
                foreach (var localId in localIds)
                {
                    if (!serverIds.Contains(localId))
                    {
                        _db.DeletePhoto(localId);
                    }
                }
                _lastFullSyncDate = DateTime.Now;
                
                // Si la photo actuelle vient d'être supprimée (ex: par le nettoyage d'une autre app)
                if (_wallpaperManager.CurrentPhoto != null && !serverIds.Contains(_wallpaperManager.CurrentPhoto.Id))
                {
                    _wallpaperManager.ApplyNextWallpaper();
                }
            }

            // 2. Mises à jour des statuts favoris
            foreach (var fav in manifest.FavoritesChanged)
            {
                _db.UpdateFavorite(fav.Id, fav.IsFavorite);
            }

            // 3. Traitement des ajouts : télécharger uniquement la plus récente non affichée
            var pendingPhotos = manifest.Created
                .Where(p => !_db.PhotoExists(p.Id))
                .OrderByDescending(p => p.Id)
                .ToList();

            var downloadSuccess = true;
            if (pendingPhotos.Count > 0)
            {
                var candidateToDownload = pendingPhotos.First();
                var destination = Path.Combine(_db.StorageDirectory, $"{candidateToDownload.Id}.jpg");

                downloadSuccess = await _api.DownloadPhotoAsync(candidateToDownload.Id, destination);

                if (downloadSuccess)
                {
                    _db.UpsertPhoto(new LocalPhoto
                    {
                        Id = candidateToDownload.Id,
                        Filepath = destination,
                        IsFavorite = candidateToDownload.IsFavorite,
                        HasBeenShown = false,
                        CaptureDate = candidateToDownload.CaptureDate,
                        Location = candidateToDownload.Location
                    });

                    // NOUVEAU COMPORTEMENT : Si l'app était à vide, on applique l'image tout de suite
                    if (_wallpaperManager.CurrentPhoto == null)
                    {
                        _wallpaperManager.ApplyNextWallpaper();
                    }
                }
            }

            // 4. Acquittement vers le serveur
            if (downloadSuccess)
            {
                await _api.SendManifestAckAsync();
                
                var parisZone = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
                var parisTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, parisZone);
                _db.SetSetting("LastSyncDate", parisTime.ToString("o"));
            }

            SetStatus(ConnectionStatus.Connected);
        }
        catch
        {
            SetStatus(ConnectionStatus.Disconnected);
        }
    }

    public void SetStatus(ConnectionStatus status)
    {
        CurrentStatus = status;
        OnStatusChanged?.Invoke(status);
    }
}