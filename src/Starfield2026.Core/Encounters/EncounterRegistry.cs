using System;
using System.Linq;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Encounters;

/// <summary>
/// Result of a successful encounter roll.
/// </summary>
public record WildEncounterResult(int EnemyTypeId, int Level, string EncounterType);

/// <summary>
/// Central service for querying encounter data from map definitions.
/// Encounter tables are baked into generated MapDefinition subclasses.
/// </summary>
public static class EncounterRegistry
{
    private static EncounterTable[] _currentEncounterGroups = [];
    private static float _currentProgressMultiplier;
    private static readonly Random _random = new();

    /// <summary>
    /// Load encounter data from a map definition. Called when the map becomes current.
    /// </summary>
    public static void LoadForMap(MapDefinition mapDef)
    {
        _currentEncounterGroups = mapDef.EncounterGroups.ToArray();
        _currentProgressMultiplier = mapDef.ProgressMultiplier;
    }

    /// <summary>
    /// Get the encounter table for the given encounter type on the current map.
    /// </summary>
    public static EncounterTable? GetTable(string encounterType)
    {
        foreach (var group in _currentEncounterGroups)
        {
            if (group.EncounterType == encounterType)
                return group;
        }
        return null;
    }

    /// <summary>
    /// Roll for a wild encounter on the current tile.
    /// Returns null if no encounter should happen.
    /// </summary>
    public static WildEncounterResult? TryEncounter(string encounterType)
    {
        var table = GetTable(encounterType);
        if (table == null)
            return null;

        // 1. Rate check
        float rate = table.BaseEncounterRate / 255f;
        if (_random.NextDouble() >= rate)
            return null;

        // 2. Select from available entries (no progress filtering for now)
        var entries = table.Entries;
        if (entries.Length == 0)
            return null;

        // 3. Weighted random selection
        var entry = WeightedSelect(entries);
        if (entry == null)
            return null;

        // 4. Calculate level
        int level = _random.Next(entry.MinLevel, entry.MaxLevel + 1);

        // 5. Build result
        return new WildEncounterResult(entry.SpeciesId, level, encounterType);
    }

    /// <summary>
    /// Clear loaded encounter data (e.g., when changing maps).
    /// </summary>
    public static void ClearCache()
    {
        _currentEncounterGroups = [];
        _currentProgressMultiplier = 0f;
    }

    private static EncounterEntry? WeightedSelect(EncounterEntry[] entries)
    {
        if (entries.Length == 0) return null;

        int totalWeight = 0;
        foreach (var entry in entries)
            totalWeight += entry.Weight;

        if (totalWeight <= 0) return null;

        int roll = _random.Next(totalWeight);
        int cumulative = 0;
        foreach (var entry in entries)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
                return entry;
        }

        return entries[^1];
    }
}
