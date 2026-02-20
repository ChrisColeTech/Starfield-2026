namespace Starfield2026.Core.Pokemon;

public class SpeciesData
{
    public int SpeciesId { get; set; }
    public string Name { get; set; } = "";

    // Base stats
    public int BaseHP { get; set; }
    public int BaseAttack { get; set; }
    public int BaseDefense { get; set; }
    public int BaseSpAttack { get; set; }
    public int BaseSpDefense { get; set; }
    public int BaseSpeed { get; set; }

    // Typing
    public Battle.MoveType Type1 { get; set; }
    public Battle.MoveType Type2 { get; set; } = Battle.MoveType.Normal; // same as Type1 if mono-type

    // EXP system
    public int BaseEXPYield { get; set; }
    public GrowthRate GrowthRate { get; set; }

    // Catch rate
    public int CatchRate { get; set; } = 45;

    // 3D model folder name (e.g. "pm0001_00"), relative to Pokemon3D/
    public string? ModelFolder { get; set; }
}
