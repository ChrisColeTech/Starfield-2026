using System.Text.Json.Serialization;

namespace Starfield2026.Core.Encounters;

/// <summary>
/// A group of encounter entries for a specific encounter type on a map.
/// </summary>
public class EncounterTable
{
    [JsonPropertyName("encounterType")]
    public string EncounterType { get; set; } = "";

    [JsonPropertyName("baseEncounterRate")]
    public int BaseEncounterRate { get; set; } = 26;

    [JsonPropertyName("entries")]
    public EncounterEntry[] Entries { get; set; } = [];
}
