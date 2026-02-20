using System;
using System.Collections.Generic;
using System.Linq;
using Starfield2026.Core.Battle;
using Starfield2026.Core.Data;

namespace Starfield2026.Core.Pokemon;

public enum Gender : byte { Male, Female, Unknown }

/// <summary>
/// Result of adding EXP — captures levels gained, new moves learned, and pending evolution.
/// </summary>
public record LevelUpResult(
    int LevelsGained,
    List<int> NewMoveIds,
    List<int> ReplacedMoveIds,
    EvolutionData? PendingEvolution
)
{
    public static readonly LevelUpResult None = new(0, [], [], null);
}

/// <summary>
/// A Pokemon in the player's party with full stats, EXP tracking, and level-up support.
/// </summary>
public class PartyPokemon
{
    public string Nickname { get; set; } = "MissingNo";
    public int SpeciesId { get; set; }
    public int Level { get; set; } = 1;
    public int CurrentHP { get; set; } = 10;
    public int MaxHP { get; set; } = 10;
    public Gender Gender { get; set; } = Gender.Unknown;
    public StatusCondition StatusCondition { get; set; } = StatusCondition.None;

    public string? StatusAbbreviation => StatusCondition switch
    {
        StatusCondition.None => null,
        StatusCondition.Poison => "PSN",
        StatusCondition.Burn => "BRN",
        StatusCondition.Freeze => "FRZ",
        StatusCondition.Sleep => "SLP",
        StatusCondition.Paralysis => "PAR",
        StatusCondition.Confusion => "CNF",
        _ => null
    };
    public int? HeldItemId { get; set; }

    // Stats
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SpAttack { get; set; }
    public int SpDefense { get; set; }
    public int Speed { get; set; }

    // EXP
    public uint ExperiencePoints { get; set; }
    public GrowthRate GrowthRate { get; set; } = GrowthRate.MediumFast;

    // IVs and EVs: [HP, Atk, Def, SpA, SpD, Spe]
    public int[] IVs { get; set; } = new int[6];
    public int[] EVs { get; set; } = new int[6];

    // Moves (up to 4)
    public int[] MoveIds { get; set; } = Array.Empty<int>();
    public int[] MovePPs { get; set; } = Array.Empty<int>();

    public float HPPercent => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
    public bool IsFainted => CurrentHP <= 0;

    /// <summary>
    /// Species name from registry, or nickname.
    /// </summary>
    public string SpeciesName => SpeciesRegistry.GetSpecies(SpeciesId)?.Name ?? Nickname;

    /// <summary>
    /// EXP bar fill percentage (0..1) within the current level.
    /// </summary>
    public float EXPPercent => GrowthRateHelper.GetEXPPercent(ExperiencePoints, Level, GrowthRate);

    /// <summary>
    /// Add EXP and handle level-ups, move learning, and evolution checks.
    /// </summary>
    public LevelUpResult AddEXP(uint amount)
    {
        if (Level >= GrowthRateHelper.MaxLevel) return LevelUpResult.None;

        ExperiencePoints += amount;
        int levelsGained = 0;
        var newMoves = new List<int>();
        var replacedMoves = new List<int>();

        while (Level < GrowthRateHelper.MaxLevel)
        {
            uint expNeeded = GrowthRateHelper.GetEXPForLevel(GrowthRate, Level + 1);
            if (ExperiencePoints < expNeeded) break;

            Level++;
            levelsGained++;
            RecalculateStats();
            LearnMovesForLevel(Level, newMoves, replacedMoves);
        }

        // Cap EXP at max level threshold
        if (Level >= GrowthRateHelper.MaxLevel)
        {
            ExperiencePoints = GrowthRateHelper.GetEXPForLevel(GrowthRate, GrowthRateHelper.MaxLevel);
        }

        // Check for evolution after all level-ups
        EvolutionData? pendingEvo = levelsGained > 0 ? CheckEvolution() : null;

        return new LevelUpResult(levelsGained, newMoves, replacedMoves, pendingEvo);
    }

    /// <summary>
    /// Learn any moves this species gets at the given level.
    /// Auto-replaces the oldest move if already at 4 moves.
    /// </summary>
    private void LearnMovesForLevel(int level, List<int> newMoves, List<int> replacedMoves)
    {
        var movesToLearn = GameDataDb.GetMovesLearnedAtLevel(SpeciesId, level);

        foreach (int moveId in movesToLearn)
        {
            // Skip if already known
            if (MoveIds.Contains(moveId)) continue;

            var moveData = MoveRegistry.GetMove(moveId);
            int pp = moveData?.MaxPP ?? 5;

            if (MoveIds.Length < 4)
            {
                // Append
                MoveIds = MoveIds.Append(moveId).ToArray();
                MovePPs = MovePPs.Append(pp).ToArray();
            }
            else
            {
                // Replace oldest (index 0), shift others down
                replacedMoves.Add(MoveIds[0]);
                for (int i = 0; i < 3; i++)
                {
                    MoveIds[i] = MoveIds[i + 1];
                    MovePPs[i] = MovePPs[i + 1];
                }
                MoveIds[3] = moveId;
                MovePPs[3] = pp;
            }

            newMoves.Add(moveId);
        }
    }

    /// <summary>
    /// Check if this Pokemon should evolve at its current level.
    /// Only checks simple level-up triggers (no items, happiness, time, etc.).
    /// </summary>
    public EvolutionData? CheckEvolution()
    {
        var evolutions = GameDataDb.GetEvolutions(SpeciesId);

        foreach (var evo in evolutions)
        {
            if (evo.Trigger != "level-up") continue;
            if (evo.MinLevel == null) continue; // needs level threshold
            if (Level < evo.MinLevel) continue;

            // Skip evolutions that require additional conditions
            if (evo.MinHappiness != null) continue;
            if (evo.TimeOfDay != null) continue;
            if (evo.KnownMove != null) continue;
            if (evo.KnownMoveType != null) continue;
            if (evo.HeldItem != null) continue;
            if (evo.Item != null) continue;
            if (evo.Gender != null) continue;

            return evo;
        }

        return null;
    }

    /// <summary>
    /// Evolve this Pokemon into a new species.
    /// Updates species ID, nickname (if not custom), growth rate, and stats.
    /// </summary>
    public void Evolve(int newSpeciesId)
    {
        var oldSpecies = SpeciesRegistry.GetSpecies(SpeciesId);
        var newSpecies = SpeciesRegistry.GetSpecies(newSpeciesId);
        if (newSpecies == null) return;

        // Only update nickname if it matches the old species name
        if (oldSpecies != null && Nickname == oldSpecies.Name)
            Nickname = newSpecies.Name;

        SpeciesId = newSpeciesId;
        GrowthRate = newSpecies.GrowthRate;
        RecalculateStats();
    }

    /// <summary>
    /// Recalculate all stats from base stats, IVs, EVs, and level.
    /// Adjusts current HP proportionally (adds the MaxHP difference).
    /// </summary>
    public void RecalculateStats()
    {
        var species = SpeciesRegistry.GetSpecies(SpeciesId);
        if (species == null) return;

        int oldMaxHP = MaxHP;
        var stats = StatCalculator.CalculateAll(species, Level, IVs, EVs);
        MaxHP = stats.hp;
        Attack = stats.atk;
        Defense = stats.def;
        SpAttack = stats.spAtk;
        SpDefense = stats.spDef;
        Speed = stats.speed;

        // On level-up, add HP difference so damage taken stays the same
        if (CurrentHP > 0 && MaxHP != oldMaxHP)
        {
            CurrentHP = Math.Max(1, CurrentHP + (MaxHP - oldMaxHP));
        }
    }

    /// <summary>
    /// Create a PartyPokemon from species data with random IVs and calculated stats.
    /// </summary>
    public static PartyPokemon Create(int speciesId, int level, Gender gender, Random? rng = null)
    {
        var species = SpeciesRegistry.GetSpecies(speciesId);
        if (species == null)
            throw new ArgumentException($"Unknown species ID: {speciesId}");

        rng ??= Random.Shared;
        var ivs = StatCalculator.RandomIVs(rng);
        var evs = StatCalculator.ZeroEVs();
        var stats = StatCalculator.CalculateAll(species, level, ivs, evs);

        // Generate moveset: last 4 level-up moves the species learns at or below this level
        var (moveIds, movePPs) = GenerateMoveset(speciesId, level);

        return new PartyPokemon
        {
            Nickname = species.Name,
            SpeciesId = speciesId,
            Level = level,
            CurrentHP = stats.hp,
            MaxHP = stats.hp,
            Gender = gender,
            GrowthRate = species.GrowthRate,
            ExperiencePoints = GrowthRateHelper.GetEXPForLevel(species.GrowthRate, level),
            IVs = ivs,
            EVs = evs,
            Attack = stats.atk,
            Defense = stats.def,
            SpAttack = stats.spAtk,
            SpDefense = stats.spDef,
            Speed = stats.speed,
            MoveIds = moveIds,
            MovePPs = movePPs,
        };
    }

    /// <summary>
    /// Pick the last 4 level-up moves a species would know at the given level.
    /// Falls back to Tackle if no learnset data exists.
    /// </summary>
    private static (int[] moveIds, int[] movePPs) GenerateMoveset(int speciesId, int level)
    {
        var learned = GameDataDb.GetLevelUpMoves(speciesId, level);

        if (learned.Count == 0)
            return (new[] { 33 }, new[] { 35 }); // Tackle (ID 33) fallback

        // Take up to 4 (already ordered highest-level first)
        int count = Math.Min(learned.Count, 4);
        var ids = new int[count];
        var pps = new int[count];

        for (int i = 0; i < count; i++)
        {
            ids[i] = learned[i].moveId;
            var moveData = MoveRegistry.GetMove(ids[i]);
            pps[i] = moveData?.MaxPP ?? 5;
        }

        return (ids, pps);
    }
}
