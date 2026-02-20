using System.Text.Json.Serialization;

namespace Starfield2026.Core.Encounters;

/// <summary>
/// A single enemy type that can appear in an encounter table.
/// </summary>
public class EncounterEntry
{
    [JsonPropertyName("speciesId")]
    public int SpeciesId { get; set; }

    [JsonPropertyName("minLevel")]
    public int MinLevel { get; set; }

    [JsonPropertyName("maxLevel")]
    public int MaxLevel { get; set; }

    [JsonPropertyName("weight")]
    public int Weight { get; set; } = 10;

    [JsonPropertyName("requiredBadges")]
    public int RequiredBadges { get; set; }

    [JsonPropertyName("requiredFlags")]
    public string[]? RequiredFlags { get; set; }
}
