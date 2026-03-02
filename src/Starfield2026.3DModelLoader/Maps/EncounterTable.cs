#nullable enable
namespace Starfield2026.ModelLoader.Maps;

public class EncounterEntry
{
    public int SpeciesId { get; set; }
    public int MinLevel { get; set; }
    public int MaxLevel { get; set; }
    public int Weight { get; set; } = 10;
    public int RequiredBadges { get; set; }
    public string[]? RequiredFlags { get; set; }
}

public class EncounterTable
{
    public string EncounterType { get; set; } = "";
    public int BaseEncounterRate { get; set; } = 26;
    public EncounterEntry[] Entries { get; set; } = [];
}
