using Microsoft.Data.Sqlite;
using Wallpapier.WinClient.Models;

namespace Wallpapier.WinClient.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    public string StorageDirectory { get; }
    
    // Verrou de sécurité multi-thread pour éviter les crash SQLite
    private readonly object _dbLock = new();

    public DatabaseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDir = Path.Combine(appData, "Wallpapier");
        StorageDirectory = Path.Combine(baseDir, "images");

        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(StorageDirectory);

        var dbPath = Path.Combine(baseDir, "database.sqlite");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        }.ToString();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Local_Photos (
                    id TEXT PRIMARY KEY,
                    filepath TEXT NOT NULL,
                    is_favorite INTEGER NOT NULL DEFAULT 0,
                    has_been_shown INTEGER NOT NULL DEFAULT 0,
                    capture_date TEXT,
                    location TEXT
                );

                CREATE TABLE IF NOT EXISTS Local_Settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
        }

        SetDefaultSettingIfNotExists("ServerIP", "100.64.0.1:8000");
        SetDefaultSettingIfNotExists("Pin", "1234");
        SetDefaultSettingIfNotExists("FavRatio", "20");
        SetDefaultSettingIfNotExists("TimePerPhoto", "60");
        SetDefaultSettingIfNotExists("SyncAnticipationTime", "5");
        SetDefaultSettingIfNotExists("ServerResetTime", "04:00");
        SetDefaultSettingIfNotExists("LastSyncDate", "");
    }

    private void SetDefaultSettingIfNotExists(string key, string defaultValue)
    {
        if (GetSetting(key) == null) SetSetting(key, defaultValue);
    }

    public string? GetSetting(string key)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM Local_Settings WHERE key = $key LIMIT 1;";
            cmd.Parameters.AddWithValue("$key", key);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }
    }

    public void SetSetting(string key, string value)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Local_Settings (key, value) VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = $value;
            ";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }
    }

    public void UpsertPhoto(LocalPhoto photo)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Local_Photos (id, filepath, is_favorite, has_been_shown, capture_date, location)
                VALUES ($id, $filepath, $fav, $shown, $cdate, $loc)
                ON CONFLICT(id) DO UPDATE SET
                    filepath = $filepath,
                    is_favorite = $fav,
                    capture_date = $cdate,
                    location = $loc;
            ";
            cmd.Parameters.AddWithValue("$id", photo.Id);
            cmd.Parameters.AddWithValue("$filepath", photo.Filepath);
            cmd.Parameters.AddWithValue("$fav", photo.IsFavorite ? 1 : 0);
            cmd.Parameters.AddWithValue("$shown", photo.HasBeenShown ? 1 : 0);
            cmd.Parameters.AddWithValue("$cdate", (object?)photo.CaptureDate?.ToString("o") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$loc", (object?)photo.Location ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void UpdateFavorite(string id, bool isFavorite)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Local_Photos SET is_favorite = $fav WHERE id = $id;";
            cmd.Parameters.AddWithValue("$fav", isFavorite ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void MarkAsShown(string id)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Local_Photos SET has_been_shown = 1 WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeletePhoto(string id)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var getCmd = connection.CreateCommand();
            getCmd.CommandText = "SELECT filepath FROM Local_Photos WHERE id = $id;";
            getCmd.Parameters.AddWithValue("$id", id);
            var pathObj = getCmd.ExecuteScalar();

            if (pathObj != null && pathObj != DBNull.Value)
            {
                var path = pathObj.ToString();
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
            }

            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Local_Photos WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public List<string> GetAllPhotoIds()
    {
        lock (_dbLock)
        {
            var ids = new List<string>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id FROM Local_Photos;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetString(0));
            }
            return ids;
        }
    }

    public LocalPhoto? GetPhotoById(string id)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, filepath, is_favorite, has_been_shown, capture_date, location FROM Local_Photos WHERE id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read()) return MapReaderToPhoto(reader);
            return null;
        }
    }

    public LocalPhoto? GetNextUnshownNormalPhoto(string? excludeId = null)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, filepath, is_favorite, has_been_shown, capture_date, location FROM Local_Photos WHERE is_favorite = 0 AND has_been_shown = 0 " +
                              (excludeId != null ? "AND id != $exclude " : "") + "ORDER BY id DESC LIMIT 1;";
            if (excludeId != null) cmd.Parameters.AddWithValue("$exclude", excludeId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read()) return MapReaderToPhoto(reader);
            return null; // Plus d'auto-fallback toxique ici
        }
    }

    public LocalPhoto? GetRandomShownNormalPhoto(string? excludeId = null)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, filepath, is_favorite, has_been_shown, capture_date, location FROM Local_Photos WHERE is_favorite = 0 " +
                              (excludeId != null ? "AND id != $exclude " : "") + "ORDER BY RANDOM() LIMIT 1;";
            if (excludeId != null) cmd.Parameters.AddWithValue("$exclude", excludeId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read()) return MapReaderToPhoto(reader);
            return null;
        }
    }

    public LocalPhoto? GetRandomFavoritePhoto(string? excludeId = null)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, filepath, is_favorite, has_been_shown, capture_date, location FROM Local_Photos WHERE is_favorite = 1 " +
                              (excludeId != null ? "AND id != $exclude " : "") + "ORDER BY RANDOM() LIMIT 1;";
            if (excludeId != null) cmd.Parameters.AddWithValue("$exclude", excludeId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read()) return MapReaderToPhoto(reader);
            return null;
        }
    }

    public bool PhotoExists(string id)
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM Local_Photos WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }

    private static LocalPhoto MapReaderToPhoto(SqliteDataReader reader)
    {
        return new LocalPhoto
        {
            Id = reader.GetString(0),
            Filepath = reader.GetString(1),
            IsFavorite = reader.GetInt32(2) == 1,
            HasBeenShown = reader.GetInt32(3) == 1,
            CaptureDate = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
            Location = reader.IsDBNull(5) ? null : reader.GetString(5)
        };
    }
}