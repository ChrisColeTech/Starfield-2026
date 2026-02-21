using Starfield2026.Core.Moves;
#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Starfield2026.Core.Pokemon;

namespace Starfield2026.Core.Data;

/// <summary>
/// Read-only SQLite accessor for game data (species, moves, items, etc.).
/// Opens Data/gamedata.db once at startup and holds the connection for the app lifetime.
/// </summary>
public static class GameDataDb
{
    private static SqliteConnection? _conn;
    private static bool _initialized;

    /// <summary>
    /// Open the game data database. Call once at startup.
    /// </summary>
    public static void Initialize(string? dataDirectory = null)
    {
        if (_initialized) return;

        string baseDir = dataDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        string dbPath = System.IO.Path.Combine(baseDir, "Data", "gamedata.db");

        _conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        _conn.Open();

        _initialized = true;
    }

    // --- Items ---

    public static ItemData? GetItem(int itemId)
    {
        EnsureInitialized();

        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT id, name, sprite, category, buy_price, sell_price, usable_in_battle, usable_overworld, effect FROM items WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", itemId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadItemRow(reader);
    }

    public static IReadOnlyList<ItemData> GetAllItems()
    {
        EnsureInitialized();

        var list = new List<ItemData>();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT id, name, sprite, category, buy_price, sell_price, usable_in_battle, usable_overworld, effect FROM items ORDER BY id";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadItemRow(reader));
        }
        return list;
    }

    public record ItemData(int Id, string Name, string Sprite, string Category,
        int BuyPrice, int SellPrice, bool UsableInBattle, bool UsableOverworld, string? Effect);

    private static ItemData ReadItemRow(SqliteDataReader reader)
    {
        return new ItemData(
            Id: reader.GetInt32(0),
            Name: reader.GetString(1),
            Sprite: reader.GetString(2),
            Category: reader.GetString(3),
            BuyPrice: reader.GetInt32(4),
            SellPrice: reader.GetInt32(5),
            UsableInBattle: reader.GetInt32(6) != 0,
            UsableOverworld: reader.GetInt32(7) != 0,
            Effect: reader.IsDBNull(8) ? null : reader.GetString(8)
        );
    }


    public static SpeciesData? GetSpecies(int speciesId)
    {
        EnsureInitialized();

        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT * FROM species WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", speciesId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadSpeciesRow(reader);
    }

    public static IReadOnlyList<SpeciesData> GetAllSpecies()
    {
        EnsureInitialized();

        var list = new List<SpeciesData>();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT * FROM species ORDER BY id";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadSpeciesRow(reader));
        }
        return list;
    }

    public static int SpeciesCount
    {
        get
        {
            EnsureInitialized();

            using var cmd = _conn!.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM species";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    // --- Moves ---

    public static MoveData? GetMove(int moveId)
    {
        EnsureInitialized();

        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT id, name, type, category, power, accuracy, pp, priority FROM moves WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", moveId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadMoveRow(reader);
    }

    // --- Learnsets ---

    /// <summary>
    /// Get level-up moves a species would know at or below the given level,
    /// ordered by level descending (newest first).
    /// </summary>
    public static IReadOnlyList<(int moveId, int level)> GetLevelUpMoves(int speciesId, int maxLevel)
    {
        EnsureInitialized();

        var list = new List<(int, int)>();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = @"
            SELECT move_id, level FROM learnsets
            WHERE species_id = @speciesId AND method = 'level-up' AND level <= @maxLevel
            ORDER BY level DESC, move_id DESC";
        cmd.Parameters.AddWithValue("@speciesId", speciesId);
        cmd.Parameters.AddWithValue("@maxLevel", maxLevel);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add((reader.GetInt32(0), reader.GetInt32(1)));
        }
        return list;
    }

    /// <summary>
    /// Get move IDs learned at exactly the given level (for level-up move learning).
    /// </summary>
    public static IReadOnlyList<int> GetMovesLearnedAtLevel(int speciesId, int level)
    {
        EnsureInitialized();

        var list = new List<int>();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = @"
            SELECT move_id FROM learnsets
            WHERE species_id = @speciesId AND method = 'level-up' AND level = @level";
        cmd.Parameters.AddWithValue("@speciesId", speciesId);
        cmd.Parameters.AddWithValue("@level", level);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(reader.GetInt32(0));
        }
        return list;
    }

    // --- Evolutions ---

    /// <summary>
    /// Get all evolution paths from a given species.
    /// </summary>
    public static IReadOnlyList<EvolutionData> GetEvolutions(int fromSpeciesId)
    {
        EnsureInitialized();

        var list = new List<EvolutionData>();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = @"
            SELECT from_species_id, to_species_id, trigger, min_level, item,
                   held_item, known_move, known_move_type, min_happiness, time_of_day, gender
            FROM evolutions WHERE from_species_id = @id";
        cmd.Parameters.AddWithValue("@id", fromSpeciesId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new EvolutionData(
                FromSpeciesId: reader.GetInt32(0),
                ToSpeciesId: reader.GetInt32(1),
                Trigger: reader.GetString(2),
                MinLevel: reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Item: reader.IsDBNull(4) ? null : reader.GetString(4),
                HeldItem: reader.IsDBNull(5) ? null : reader.GetString(5),
                KnownMove: reader.IsDBNull(6) ? null : reader.GetString(6),
                KnownMoveType: reader.IsDBNull(7) ? null : reader.GetString(7),
                MinHappiness: reader.IsDBNull(8) ? null : reader.GetInt32(8),
                TimeOfDay: reader.IsDBNull(9) ? null : reader.GetString(9),
                Gender: reader.IsDBNull(10) ? null : reader.GetInt32(10)
            ));
        }
        return list;
    }

    // --- Helpers ---

    private static void EnsureInitialized()
    {
        if (!_initialized)
            Initialize();
    }

    private static SpeciesData ReadSpeciesRow(SqliteDataReader reader)
    {
        string type1Str = reader.GetString(reader.GetOrdinal("type1"));
        var type2Ordinal = reader.GetOrdinal("type2");
        string? type2Str = reader.IsDBNull(type2Ordinal) ? null : reader.GetString(type2Ordinal);

        var type1 = ParseType(type1Str);
        var type2 = type2Str != null ? ParseType(type2Str) : type1;

        int id = reader.GetInt32(reader.GetOrdinal("id"));

        return new SpeciesData
        {
            SpeciesId = id,
            Name = reader.GetString(reader.GetOrdinal("name")),
            BaseHP = reader.GetInt32(reader.GetOrdinal("hp")),
            BaseAttack = reader.GetInt32(reader.GetOrdinal("attack")),
            BaseDefense = reader.GetInt32(reader.GetOrdinal("defense")),
            BaseSpAttack = reader.GetInt32(reader.GetOrdinal("sp_attack")),
            BaseSpDefense = reader.GetInt32(reader.GetOrdinal("sp_defense")),
            BaseSpeed = reader.GetInt32(reader.GetOrdinal("speed")),
            Type1 = type1,
            Type2 = type2,
            BaseEXPYield = reader.GetInt32(reader.GetOrdinal("base_exp_yield")),
            GrowthRate = ParseGrowthRate(reader.GetString(reader.GetOrdinal("growth_rate"))),
            CatchRate = reader.GetInt32(reader.GetOrdinal("catch_rate")),
            ModelFolder = $"pm{id:D4}_00",
        };
    }

    private static MoveType ParseType(string typeName)
    {
        if (Enum.TryParse<MoveType>(typeName, ignoreCase: true, out var result))
            return result;
        return MoveType.Normal;
    }

    private static GrowthRate ParseGrowthRate(string rateName)
    {
        if (Enum.TryParse<GrowthRate>(rateName, ignoreCase: true, out var result))
            return result;
        return GrowthRate.MediumFast;
    }

    private static MoveData ReadMoveRow(SqliteDataReader reader)
    {
        return new MoveData(
            id: reader.GetInt32(0),
            name: reader.GetString(1),
            type: ParseType(reader.GetString(2)),
            category: ParseCategory(reader.GetString(3)),
            power: reader.GetInt32(4),
            accuracy: reader.GetInt32(5),
            maxPP: reader.GetInt32(6),
            priority: reader.GetInt32(7)
        );
    }

    private static MoveCategory ParseCategory(string categoryName)
    {
        if (Enum.TryParse<MoveCategory>(categoryName, ignoreCase: true, out var result))
            return result;
        return MoveCategory.Physical;
    }
}
