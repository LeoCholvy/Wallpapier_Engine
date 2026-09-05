using Wallpapier.WinClient.Controllers;
using Wallpapier.WinClient.Models;
using Wallpapier.WinClient.Services;

namespace Wallpapier.WinClient.Views;

public partial class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly DatabaseService _db;
    private readonly ApiClient _api;
    private readonly WallpaperManager _wallpaperManager;
    private readonly SyncManager _syncManager;

    private readonly ToolStripMenuItem _menuNext;
    private readonly ToolStripMenuItem _menuFavorite;
    private readonly ToolStripMenuItem _menuReconnect;
    private readonly ToolStripMenuItem _menuSettings;
    private readonly ToolStripMenuItem _menuExit;

    // Contexte UI pour mettre à jour le Tooltip proprement depuis le thread d'arrière-plan
    private readonly SynchronizationContext? _uiContext;

    public TrayApplicationContext(
        DatabaseService db,
        ApiClient api,
        WallpaperManager wallpaperManager,
        SyncManager syncManager)
    {
        _db = db;
        _api = api;
        _wallpaperManager = wallpaperManager;
        _syncManager = syncManager;
        _uiContext = SynchronizationContext.Current;

        var contextMenu = new ContextMenuStrip();
        
        _menuNext = new ToolStripMenuItem("Passer à la suivante", null, (s, e) => {
            _wallpaperManager.ApplyNextWallpaper();
            _syncManager.ResetCycle(); 
        });
        
        _menuFavorite = new ToolStripMenuItem("Ajouter aux favoris", null, async (s, e) => await ToggleFavoriteAsync());
        
        _menuReconnect = new ToolStripMenuItem("Reconnexion / Synchro", null, (s, e) => {
            _ = _syncManager.ExecuteSyncProcessAsync(forceFull: true, applyNewPhoto: true);
        });
        
        _menuSettings = new ToolStripMenuItem("Paramètres...", null, (s, e) => OpenSettings());
        _menuExit = new ToolStripMenuItem("Quitter", null, (s, e) => Exit());

        contextMenu.Items.AddRange([
            _menuNext, 
            _menuFavorite, 
            new ToolStripSeparator(), 
            _menuReconnect, 
            _menuSettings, 
            _menuExit
        ]);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Visible = true,
            Text = "Wallpapier - Démarrage..."
        };

        // Abonnements
        _syncManager.OnStatusChanged += UpdateStatusIcon;
        _syncManager.OnTickUpdate += () => {
            if (_uiContext != null) _uiContext.Post(_ => RefreshTooltip(), null);
            else RefreshTooltip();
        };
        
        RefreshMenuState();
        UpdateStatusIcon(_syncManager.CurrentStatus);
    }

    private void UpdateStatusIcon(ConnectionStatus status)
    {
        var color = status switch
        {
            ConnectionStatus.Connected => Color.LimeGreen,
            ConnectionStatus.Disconnected => Color.Crimson,
            ConnectionStatus.Syncing => Color.DodgerBlue,
            _ => Color.Gray
        };

        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 13, 13);
            using var pen = new Pen(Color.FromArgb(80, 0, 0, 0), 1);
            g.DrawEllipse(pen, 1, 1, 13, 13);
        }

        var iconHandle = bitmap.GetHicon();
        var newIcon = Icon.FromHandle(iconHandle);
        _notifyIcon.Icon = (Icon)newIcon.Clone();
        DestroyIcon(iconHandle);
    }

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr hIcon);

    private void RefreshMenuState()
    {
        var hasPhoto = _wallpaperManager.CurrentPhoto != null;
        _menuNext.Enabled = hasPhoto;
        _menuFavorite.Enabled = hasPhoto;
    }

    private void RefreshTooltip()
    {
        RefreshMenuState();
        var photo = _wallpaperManager.CurrentPhoto;

        // 1. Ligne 1 : Statut et Temps
        string connStatus = _syncManager.CurrentStatus switch
        {
            ConnectionStatus.Connected => "OK",
            ConnectionStatus.Disconnected => "Hors ligne",
            ConnectionStatus.Syncing => "Synchro...",
            _ => "?"
        };

        string pauseState = _syncManager.IsPaused ? " [En pause]" : "";
        
        TimeSpan t = TimeSpan.FromSeconds(_syncManager.RemainingSeconds);
        string timeStr = t.TotalHours >= 1 
            ? $"{(int)t.TotalHours:D2}h{t.Minutes:D2}m" 
            : $"{t.Minutes:D2}m{t.Seconds:D2}s";

        string tooltip = $"{connStatus}{pauseState} | -{timeStr}";

        // 2. Lignes 2 & 3 : Photo infos
        if (photo != null)
        {
            var dateStr = photo.CaptureDate.HasValue ? photo.CaptureDate.Value.ToString("dd/MM/yy HH:mm") : "Date Inconnue";
            var locStr = !string.IsNullOrWhiteSpace(photo.Location) ? photo.Location : "Lieu inconnu";
            var favStr = photo.IsFavorite ? "★" : "☆";

            tooltip += $"\n{locStr}\n{dateStr} {favStr}";
            _menuFavorite.Text = photo.IsFavorite ? "★ Retirer des favoris" : "☆ Ajouter aux favoris";
        }
        else
        {
            tooltip += "\n\nAucune photo disponible.";
            _menuFavorite.Text = "☆ Ajouter aux favoris";
        }

        // Sécurité système : limite stricte Win32 NotifyIcon (127 caractères)
        if (tooltip.Length > 127) 
        {
            tooltip = tooltip[..124] + "...";
        }

        // Assigne uniquement si le texte a changé (réduit les scintillements UI)
        if (_notifyIcon.Text != tooltip)
        {
            _notifyIcon.Text = tooltip;
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (_wallpaperManager.CurrentPhoto == null) return;

        var photo = _wallpaperManager.CurrentPhoto;
        var newStatus = !photo.IsFavorite;

        photo.IsFavorite = newStatus;
        _db.UpdateFavorite(photo.Id, newStatus);
        
        // Rafraîchit l'UI immédiatement après le clic
        RefreshTooltip();

        try
        {
            await _api.UpdatePhotoFavoriteAsync(photo.Id, newStatus);
        }
        catch { }
    }

    private void OpenSettings()
    {
        using var settingsForm = new SettingsForm(_db, _api);
        settingsForm.ShowDialog();
        
        _ = _syncManager.ExecuteSyncProcessAsync(forceFull: true);
        _syncManager.ResetCycle();
    }

    private void Exit()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }
}