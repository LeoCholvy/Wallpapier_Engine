using Wallpapier.WinClient.Services;

namespace Wallpapier.WinClient.Views;

public class SettingsForm : Form
{
    private readonly DatabaseService _db;
    private readonly ApiClient _api;

    private readonly TextBox _txtServerIp = new();
    private readonly TextBox _txtPin = new();
    private readonly NumericUpDown _numRatio = new();
    private readonly NumericUpDown _numTimePerPhoto = new();
    private readonly NumericUpDown _numSyncAnticipation = new();
    private readonly TextBox _txtResetTime = new();
    private readonly Button _btnSave = new();

    public SettingsForm(DatabaseService db, ApiClient api)
    {
        _db = db;
        _api = api;

        InitializeComponents();
        LoadSettings();
    }

    private void InitializeComponents()
    {
        Text = "Wallpapier - Paramètres";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 310);

        var lblIp = new Label { Text = "IP Serveur (Tailscale) :", Location = new Point(20, 20), AutoSize = true };
        _txtServerIp.Location = new Point(200, 18);
        _txtServerIp.Size = new Size(150, 23);

        var lblPin = new Label { Text = "Code PIN de sécurité :", Location = new Point(20, 55), AutoSize = true };
        _txtPin.Location = new Point(200, 53);
        _txtPin.Size = new Size(150, 23);
        _txtPin.UseSystemPasswordChar = true;

        var lblRatio = new Label { Text = "Ratio Favoris (%) :", Location = new Point(20, 90), AutoSize = true };
        _numRatio.Location = new Point(200, 88);
        _numRatio.Size = new Size(150, 23);
        _numRatio.Minimum = 0;
        _numRatio.Maximum = 100;

        var lblTime = new Label { Text = "Temps d'affichage (min) :", Location = new Point(20, 125), AutoSize = true };
        _numTimePerPhoto.Location = new Point(200, 123);
        _numTimePerPhoto.Size = new Size(150, 23);
        _numTimePerPhoto.Minimum = 1;
        _numTimePerPhoto.Maximum = 1440;

        var lblSync = new Label { Text = "Anticipation synchro (min) :", Location = new Point(20, 160), AutoSize = true };
        _numSyncAnticipation.Location = new Point(200, 158);
        _numSyncAnticipation.Size = new Size(150, 23);
        _numSyncAnticipation.Minimum = 1;
        _numSyncAnticipation.Maximum = 60;

        var lblReset = new Label { Text = "Heure Reset Serveur (HH:mm) :", Location = new Point(20, 195), AutoSize = true };
        _txtResetTime.Location = new Point(200, 193);
        _txtResetTime.Size = new Size(150, 23);

        _btnSave.Text = "Enregistrer";
        _btnSave.Location = new Point(240, 250);
        _btnSave.Size = new Size(110, 32);
        _btnSave.Click += async (s, e) => await SaveSettingsAsync();

        Controls.AddRange([
            lblIp, _txtServerIp,
            lblPin, _txtPin,
            lblRatio, _numRatio,
            lblTime, _numTimePerPhoto,
            lblSync, _numSyncAnticipation,
            lblReset, _txtResetTime,
            _btnSave
        ]);
    }

    private void LoadSettings()
    {
        _txtServerIp.Text = _db.GetSetting("ServerIP") ?? "";
        _txtPin.Text = _db.GetSetting("Pin") ?? "";
        _numRatio.Value = decimal.TryParse(_db.GetSetting("FavRatio"), out var r) ? r : 20;
        _numTimePerPhoto.Value = decimal.TryParse(_db.GetSetting("TimePerPhoto"), out var t) ? t : 60;
        _numSyncAnticipation.Value = decimal.TryParse(_db.GetSetting("SyncAnticipationTime"), out var sa) ? sa : 5;
        _txtResetTime.Text = _db.GetSetting("ServerResetTime") ?? "04:00";
    }

    private async Task SaveSettingsAsync()
    {
        var currentIp = _db.GetSetting("ServerIP") ?? "";
        var newIp = _txtServerIp.Text.Trim();

        if (currentIp != newIp)
        {
            // Réinitialisation forcée si l'IP change
            _db.SetSetting("LastSyncDate", "");
        }

        _db.SetSetting("ServerIP", newIp);
        _db.SetSetting("Pin", _txtPin.Text.Trim());
        _db.SetSetting("FavRatio", _numRatio.Value.ToString());
        _db.SetSetting("TimePerPhoto", _numTimePerPhoto.Value.ToString());
        _db.SetSetting("SyncAnticipationTime", _numSyncAnticipation.Value.ToString());

        var currentResetTime = _db.GetSetting("ServerResetTime");
        var newResetTime = _txtResetTime.Text.Trim();

        if (currentResetTime != newResetTime)
        {
            _db.SetSetting("ServerResetTime", newResetTime);
            try
            {
                await _api.UpdateServerResetTimeAsync(newResetTime);
            }
            catch
            {
                MessageBox.Show("Configuration enregistrée, mais impossible de joindre le serveur pour mettre à jour l'heure de reset.", "Avertissement", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
        }

        MessageBox.Show("Paramètres sauvegardés.", "Wallpapier", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}