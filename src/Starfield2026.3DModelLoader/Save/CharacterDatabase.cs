#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Starfield2026.ModelLoader.Save;

public record CharacterRecord(int Id, string Name, string Category, string ManifestPath);

public class CharacterDatabase : IDisposable
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
            CREATE TABLE IF NOT EXISTS characters (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                category TEXT NOT NULL DEFAULT 'Default',
                manifest_path TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();
    }

    public int GetCharacterCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM characters;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void RebuildCharacters(List<(string name, string category, string manifestPath)> entries)
    {
        using var tx = _connection.BeginTransaction();

        using var del = _connection.CreateCommand();
        del.CommandText = "DELETE FROM characters;";
        del.ExecuteNonQuery();

        foreach (var (name, category, manifestPath) in entries)
        {
            using var ins = _connection.CreateCommand();
            ins.CommandText = "INSERT INTO characters (name, category, manifest_path) VALUES (@name, @cat, @path);";
            ins.Parameters.AddWithValue("@name", name);
            ins.Parameters.AddWithValue("@cat", category);
            ins.Parameters.AddWithValue("@path", manifestPath);
            ins.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public List<CharacterRecord> GetAllCharacters()
    {
        var results = new List<CharacterRecord>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, category, manifest_path FROM characters ORDER BY category, name;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new CharacterRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }
        return results;
    }

    public CharacterRecord? GetCharacter(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, name, category, manifest_path FROM characters WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new CharacterRecord(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    public string? GetSetting(string key)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @key;";
        cmd.Parameters.AddWithValue("@key", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES (@key, @value);";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
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
