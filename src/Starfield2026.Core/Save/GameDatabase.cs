using System;
using Microsoft.Data.Sqlite;
using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Save;

public class GameDatabase : IDisposable
{
    private SqliteConnection _connection = null!;
    private bool _disposed;

    public void Initialize(string dbPath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS player_profile (
                id INTEGER PRIMARY KEY DEFAULT 1,
                coin_count INTEGER NOT NULL DEFAULT 0,
                position_x REAL NOT NULL DEFAULT 0,
                position_y REAL NOT NULL DEFAULT 0,
                position_z REAL NOT NULL DEFAULT 0,
                current_screen TEXT NOT NULL DEFAULT 'space',
                current_map_id TEXT NOT NULL DEFAULT 'home_base_center',
                last_saved TEXT NOT NULL DEFAULT '',
                gold_ammo INTEGER NOT NULL DEFAULT 0,
                red_ammo INTEGER NOT NULL DEFAULT 0,
                boost_count INTEGER NOT NULL DEFAULT 0
            );
        ";
        cmd.ExecuteNonQuery();

        // Migration: Add ammo columns if they don't exist
        using var migrateCmd = _connection.CreateCommand();
        migrateCmd.CommandText = @"
            PRAGMA table_info(player_profile);
        ";
        using var reader = migrateCmd.ExecuteReader();
        bool hasGoldAmmo = false;
        bool hasRedAmmo = false;
        bool hasCurrentMapId = false;
        bool hasBoostCount = false;
        while (reader.Read())
        {
            string colName = reader.GetString(1);
            if (colName == "gold_ammo") hasGoldAmmo = true;
            if (colName == "red_ammo") hasRedAmmo = true;
            if (colName == "current_map_id") hasCurrentMapId = true;
            if (colName == "boost_count") hasBoostCount = true;
        }

        if (!hasGoldAmmo)
        {
            using var addCmd = _connection.CreateCommand();
            addCmd.CommandText = "ALTER TABLE player_profile ADD COLUMN gold_ammo INTEGER NOT NULL DEFAULT 100;";
            addCmd.ExecuteNonQuery();
        }
        if (!hasRedAmmo)
        {
            using var addCmd = _connection.CreateCommand();
            addCmd.CommandText = "ALTER TABLE player_profile ADD COLUMN red_ammo INTEGER NOT NULL DEFAULT 50;";
            addCmd.ExecuteNonQuery();
        }
        if (!hasCurrentMapId)
        {
            using var addCmd = _connection.CreateCommand();
            addCmd.CommandText = "ALTER TABLE player_profile ADD COLUMN current_map_id TEXT NOT NULL DEFAULT 'home_base_center';";
            addCmd.ExecuteNonQuery();
        }
        if (!hasBoostCount)
        {
            using var addCmd = _connection.CreateCommand();
            addCmd.CommandText = "ALTER TABLE player_profile ADD COLUMN boost_count INTEGER NOT NULL DEFAULT 0;";
            addCmd.ExecuteNonQuery();
        }
    }

    public void SaveProfile(PlayerProfile profile)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO player_profile (id, coin_count, position_x, position_y, position_z, current_screen, current_map_id, last_saved, gold_ammo, red_ammo, boost_count)
            VALUES (1, @coins, @px, @py, @pz, @screen, @mapId, @saved, @gold, @red, @boosts)
            ON CONFLICT(id) DO UPDATE SET
                coin_count = @coins,
                position_x = @px,
                position_y = @py,
                position_z = @pz,
                current_screen = @screen,
                current_map_id = @mapId,
                last_saved = @saved,
                gold_ammo = @gold,
                red_ammo = @red,
                boost_count = @boosts;
        ";
        cmd.Parameters.AddWithValue("@coins", profile.CoinCount);
        cmd.Parameters.AddWithValue("@px", profile.Position.X);
        cmd.Parameters.AddWithValue("@py", profile.Position.Y);
        cmd.Parameters.AddWithValue("@pz", profile.Position.Z);
        cmd.Parameters.AddWithValue("@screen", profile.CurrentScreen);
        cmd.Parameters.AddWithValue("@mapId", profile.CurrentMapId);
        cmd.Parameters.AddWithValue("@saved", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@gold", profile.GoldAmmo);
        cmd.Parameters.AddWithValue("@red", profile.RedAmmo);
        cmd.Parameters.AddWithValue("@boosts", profile.BoostCount);
        cmd.ExecuteNonQuery();
    }

    public PlayerProfile? LoadProfile()
    {
        if (_connection == null) return null;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT coin_count, position_x, position_y, position_z, current_screen, current_map_id, last_saved, gold_ammo, red_ammo, boost_count FROM player_profile WHERE id = 1;";

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new PlayerProfile
        {
            CoinCount = reader.GetInt32(0),
            Position = new Vector3(reader.GetFloat(1), reader.GetFloat(2), reader.GetFloat(3)),
            CurrentScreen = reader.GetString(4),
            CurrentMapId = reader.GetString(5),
            LastSaved = DateTime.TryParse(reader.GetString(6), out var dt) ? dt : DateTime.UtcNow,
            GoldAmmo = reader.GetInt32(7),
            RedAmmo = reader.GetInt32(8),
            BoostCount = reader.GetInt32(9),
        };
    }

    public void SaveCoinCount(int coinCount)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO player_profile (id, coin_count, last_saved)
            VALUES (1, @coins, @saved)
            ON CONFLICT(id) DO UPDATE SET
                coin_count = @coins,
                last_saved = @saved;
        ";
        cmd.Parameters.AddWithValue("@coins", coinCount);
        cmd.Parameters.AddWithValue("@saved", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void SaveAmmo(int goldAmmo, int redAmmo)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO player_profile (id, gold_ammo, red_ammo, last_saved)
            VALUES (1, @gold, @red, @saved)
            ON CONFLICT(id) DO UPDATE SET
                gold_ammo = @gold,
                red_ammo = @red,
                last_saved = @saved;
        ";
        cmd.Parameters.AddWithValue("@gold", goldAmmo);
        cmd.Parameters.AddWithValue("@red", redAmmo);
        cmd.Parameters.AddWithValue("@saved", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Close();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
