namespace Starfield2026.Core.Data;

/// <summary>
/// A single evolution path from one species to another.
/// </summary>
public record EvolutionData(
    int FromSpeciesId,
    int ToSpeciesId,
    string Trigger,
    int? MinLevel = null,
    string? Item = null,
    string? HeldItem = null,
    string? KnownMove = null,
    string? KnownMoveType = null,
    int? MinHappiness = null,
    string? TimeOfDay = null,
    int? Gender = null
);
