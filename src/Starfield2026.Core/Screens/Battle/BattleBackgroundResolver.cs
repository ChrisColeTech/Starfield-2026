using Starfield2026.Core.Rendering;
namespace Starfield2026.Core.Screens.Battle;

public static class BattleBackgroundResolver
{
    public static BattleBackground FromOverlayBehavior(string? behavior) => behavior switch
    {
        "wild_encounter" => BattleBackground.TallGrass,
        "rare_encounter" => BattleBackground.TallGrass,
        "double_encounter" => BattleBackground.TallGrass,
        "cave_encounter" => BattleBackground.Cave,
        "water_encounter" => BattleBackground.Dark,
        "surf_encounter" => BattleBackground.Dark,
        "fishing" => BattleBackground.Dark,
        "fire_encounter" => BattleBackground.Dark,
        "headbutt" => BattleBackground.Grass,
        _ => BattleBackground.Grass,
    };
}
