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
    private System.Threading.Timer? _tickTimer;
    private readonly object _syncLock = new();
    
    private DateTime _lastFullSyncDate = DateTime.MinValue;
    
    private int _visibleSecondsElapsed = 0;
    private bool _syncDoneForThisCycle = false;

    public ConnectionStatus CurrentStatus { get; private set; } = ConnectionStatus.Disconnected;
    
    public bool IsPaused { get; private set; } = false;
    public int RemainingSeconds { get; private set; } = 0;
    
    public event Action<ConnectionStatus>? OnStatusChanged;
    public event Action? OnTickUpdate;

    public SyncManager(DatabaseService db, ApiClient api, WallpaperManager wallpaperManager)
    {
        _db = db;
        _api = api;
        _wallpaperManager = wallpaperManager;
    }

    public void Start()
    {
        _tickTimer = new System.Threading.Timer(OnTick, null, 0, 1000);
    }

    public void ResetCycle()
    {
        _visibleSecondsElapsed = 0;
        _syncDoneForThisCycle = false;
    }

    private void OnTick(object? state)
    {
        IsPaused = SystemIntegration.IsDesktopObscured();

        // Remplacement des int.TryParse par double.TryParse
        var timeStr = _db.GetSetting("TimePerPhoto")?.Replace(",", ".");
        var timePerPhotoMin = double.TryParse(timeStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 60.0;

        var syncStr = _db.GetSetting("SyncAnticipationTime")?.Replace(",", ".");
        var syncAnticipationMin = double.TryParse(syncStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 5.0;
    
        // On convertit les minutes (double) en secondes (int)
        var totalCycleSec = (int)(timePerPhotoMin * 60);
        var anticipationSec = (int)(syncAnticipationMin * 60);
        var syncThresholdSec = Math.Max(1, totalCycleSec - anticipationSec);

        if (!IsPaused)
        {
            _visibleSecondsElapsed++;
        }

        RemainingSeconds = Math.Max(0, totalCycleSec - _visibleSecondsElapsed);

        if (_visibleSecondsElapsed >= syncThresholdSec && !_syncDoneForThisCycle)
        {
            _syncDoneForThisCycle = true;
            _ = ExecuteSyncProcessAsync(forceFull: false);
        }

        if (_visibleSecondsElapsed >= totalCycleSec)
        {
            _wallpaperManager.ApplyNextWallpaper();
            ResetCycle();
        }
        
        OnTickUpdate?.Invoke();
    }

    public async Task ExecuteSyncProcessAsync(bool forceFull = false, bool applyNewPhoto = false)
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
                    if (!serverIds.Contains(localId)) _db.DeletePhoto(localId);
                }
                _lastFullSyncDate = DateTime.Now;
                
                if (_wallpaperManager.CurrentPhoto != null && !serverIds.Contains(_wallpaperManager.CurrentPhoto.Id))
                {
                    _wallpaperManager.ApplyNextWallpaper();
                    ResetCycle();
                }
            }

            foreach (var fav in manifest.FavoritesChanged)
            {
                _db.UpdateFavorite(fav.Id, fav.IsFavorite);
            }

            var pendingPhotos = manifest.Created
                .Where(p => !_db.PhotoExists(p.Id))
                .OrderByDescending(p => p.Id)
                .ToList();

            var downloadSuccess = true;
            var hasDownloadedNewPhotos = false;

            if (pendingPhotos.Count > 0)
            {
                // Boucle sur TOUTES les photos manquantes au lieu de .First()
                foreach (var candidateToDownload in pendingPhotos)
                {
                    var destination = Path.Combine(_db.StorageDirectory, $"{candidateToDownload.Id}.jpg");
                    var success = await _api.DownloadPhotoAsync(candidateToDownload.Id, destination);

                    if (success)
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
                        hasDownloadedNewPhotos = true;
                    }
                    else
                    {
                        // En cas d'erreur de réseau, on arrête pour ne pas valider le manifeste à tort
                        downloadSuccess = false;
                        break; 
                    }
                }

                if (hasDownloadedNewPhotos)
                {
                    // On garantit à une nouvelle photo de faire son cycle complet.
                    // On force le passage à la nouvelle image UNIQUEMENT SI :
                    // 1. L'écran est vide (CurrentPhoto == null)
                    // 2. L'utilisateur a cliqué sur "Reconnexion / Synchro" (applyNewPhoto)
                    // 3. La photo actuelle est une "vieille" photo (IsCurrentPhotoFresh == false)
                    if (_wallpaperManager.CurrentPhoto == null || applyNewPhoto || !_wallpaperManager.IsCurrentPhotoFresh)
                    {
                        _wallpaperManager.ApplyNextWallpaper();
                        ResetCycle();
                    }
                }
            } // Fin du if(pendingPhotos.Count > 0)
            
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