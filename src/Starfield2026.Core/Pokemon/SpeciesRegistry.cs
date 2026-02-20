#nullable enable

using System.Collections.Generic;
using Starfield2026.Core.Data;

namespace Starfield2026.Core.Pokemon;

/// <summary>
/// Static registry of all Pokemon species data.
/// Delegates to GameDataDb (SQLite) for all lookups.
/// </summary>
public static class SpeciesRegistry
{
    /// <summary>
    /// Initialize the species data source.
    /// Call once at startup (e.g. from Game1.Initialize).
    /// </summary>
    public static void Initialize(string? dataDirectory = null)
    {
        GameDataDb.Initialize(dataDirectory);
    }

    public static SpeciesData? GetSpecies(int speciesId)
    {
        return GameDataDb.GetSpecies(speciesId);
    }

    public static IReadOnlyCollection<SpeciesData> GetAllSpecies()
    {
        return GameDataDb.GetAllSpecies();
    }

    public static int Count => GameDataDb.SpeciesCount;
}
