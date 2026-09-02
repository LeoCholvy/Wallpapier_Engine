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
    private readonly ToolStripMenuItem _menuSettings;
    private readonly ToolStripMenuItem _menuExit;

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

        var contextMenu = new ContextMenuStrip();
        
        _menuNext = new ToolStripMenuItem("Passer à la suivante", null, (s, e) => {
            // Force le changement puis relance le chronomètre de zéro
            _wallpaperManager.ApplyNextWallpaper();
            _syncManager.PlanNextCycle(); 
        });
        
        _menuFavorite = new ToolStripMenuItem("Ajouter aux favoris", null, async (s, e) => await ToggleFavoriteAsync());
        
        _menuSettings = new ToolStripMenuItem("Paramètres...", null, (s, e) => OpenSettings());
        
        _menuExit = new ToolStripMenuItem("Quitter", null, (s, e) => Exit());

        contextMenu.Items.AddRange([_menuNext, _menuFavorite, new ToolStripSeparator(), _menuSettings, _menuExit]);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Visible = true,
            Text = "Wallpapier"
        };

        _wallpaperManager.OnWallpaperChanged += (photo) => {
            UpdateTooltip(photo);
            // Refait un manifest (silencieux) dès que la photo change
            _ = _syncManager.ExecuteSyncProcessAsync(forceFull: false);
        };
        
        _syncManager.OnStatusChanged += UpdateStatusIcon;
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

    private void UpdateTooltip(LocalPhoto photo)
    {
        var dateStr = photo.CaptureDate.HasValue
            ? photo.CaptureDate.Value.ToString("dd/MM/yyyy HH:mm")
            : "Inconnue";

        var locStr = !string.IsNullOrWhiteSpace(photo.Location)
            ? photo.Location
            : "Non renseigné";

        var tooltip = $"Wallpapier\nDate : {dateStr}\nLieu : {locStr}";
        if (tooltip.Length > 63) tooltip = tooltip[..60] + "...";
        
        _notifyIcon.Text = tooltip;
        _menuFavorite.Text = photo.IsFavorite ? "★ Retirer des favoris" : "☆ Ajouter aux favoris";
    }

    private async Task ToggleFavoriteAsync()
    {
        if (_wallpaperManager.CurrentPhoto == null) return;

        var photo = _wallpaperManager.CurrentPhoto;
        var newStatus = !photo.IsFavorite;

        photo.IsFavorite = newStatus;
        _db.UpdateFavorite(photo.Id, newStatus);
        UpdateTooltip(photo);

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
        
        // Force un manifest complet et relance le cycle à la fermeture des paramètres
        _ = _syncManager.ExecuteSyncProcessAsync(forceFull: true);
        _syncManager.PlanNextCycle();
    }

    private void Exit()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        Application.Exit();
    }
}