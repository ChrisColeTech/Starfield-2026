using System;

namespace Starfield2026.Core.Pokemon;

/// <summary>
/// Calculates Pokemon stats using the Gen III-V formula.
/// </summary>
public static class StatCalculator
{
    /// <summary>
    /// Calculate HP stat.
    /// HP = floor((2*Base + IV + floor(EV/4)) * Level / 100) + Level + 10
    /// </summary>
    public static int CalculateHP(int baseHP, int iv, int ev, int level)
    {
        return (2 * baseHP + iv + ev / 4) * level / 100 + level + 10;
    }

    /// <summary>
    /// Calculate a non-HP stat (Atk, Def, SpA, SpD, Spe).
    /// Stat = floor((floor((2*Base + IV + floor(EV/4)) * Level / 100) + 5) * NatureMod)
    /// </summary>
    public static int CalculateStat(int baseStat, int iv, int ev, int level, float natureMod = 1.0f)
    {
        int raw = (2 * baseStat + iv + ev / 4) * level / 100 + 5;
        return (int)(raw * natureMod);
    }

    /// <summary>
    /// Calculate all stats for a Pokemon from its species data and individual values.
    /// Returns (hp, atk, def, spAtk, spDef, speed).
    /// </summary>
    public static (int hp, int atk, int def, int spAtk, int spDef, int speed)
        CalculateAll(SpeciesData species, int level, int[] ivs, int[] evs)
    {
        // IVs/EVs: [HP, Atk, Def, SpA, SpD, Spe]
        return (
            CalculateHP(species.BaseHP, ivs[0], evs[0], level),
            CalculateStat(species.BaseAttack, ivs[1], evs[1], level),
            CalculateStat(species.BaseDefense, ivs[2], evs[2], level),
            CalculateStat(species.BaseSpAttack, ivs[3], evs[3], level),
            CalculateStat(species.BaseSpDefense, ivs[4], evs[4], level),
            CalculateStat(species.BaseSpeed, ivs[5], evs[5], level)
        );
    }

    /// <summary>
    /// Generate random IVs (0-31 each) for a new Pokemon.
    /// </summary>
    public static int[] RandomIVs(Random rng)
    {
        return new[]
        {
            rng.Next(32), rng.Next(32), rng.Next(32),
            rng.Next(32), rng.Next(32), rng.Next(32)
        };
    }

    public static int[] ZeroEVs() => new int[6];
}
