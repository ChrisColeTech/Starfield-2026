using Microsoft.Xna.Framework;

namespace Starfield2026.Core.UI.Fonts;

/// <summary>
/// Pre-defined color palettes matching the old engine's FontColors.cs.
///
/// Each palette is a Color array where:
///   [0] = transparent (background)
///   [1] = main text color
///   [2] = shadow/outline color
///   [3] = (optional, for fonts with bpp allowing 4+ indices)
///
/// The old engine used Vector4 arrays with the same semantics, passed as shader
/// uniforms. Here they are baked into the atlas texture directly.
///
/// Suffix conventions from the original:
///   _I = "inner" style (light text, darker shadow)
///   _O = "outer" style (brighter text, darker outline)
/// </summary>
public static class KermFontPalettes
{
    // DefaultDisabled
    public static Color[] Disabled { get; } = {
        Color.Transparent,
        new Color(133, 133, 141),
        new Color(58, 50, 50),
    };

    // DefaultBlack_I
    public static Color[] BlackInner { get; } = {
        Color.Transparent,
        new Color(15, 25, 30),
        new Color(170, 185, 185),
    };

    // DefaultBlue_I
    public static Color[] BlueInner { get; } = {
        Color.Transparent,
        new Color(0, 110, 250),
        new Color(120, 185, 230),
    };

    // DefaultBlue_O
    public static Color[] BlueOuter { get; } = {
        Color.Transparent,
        new Color(115, 148, 255),
        new Color(0, 0, 214),
    };

    // DefaultCyan_O
    public static Color[] CyanOuter { get; } = {
        Color.Transparent,
        new Color(50, 255, 255),
        new Color(0, 90, 140),
    };

    // DefaultDarkGray_I
    public static Color[] DarkGrayInner { get; } = {
        Color.Transparent,
        new Color(90, 82, 82),
        new Color(165, 165, 173),
    };

    // DefaultRed_I
    public static Color[] RedInner { get; } = {
        Color.Transparent,
        new Color(230, 30, 15),
        new Color(250, 170, 185),
    };

    // DefaultRed_O
    public static Color[] RedOuter { get; } = {
        Color.Transparent,
        new Color(255, 50, 50),
        new Color(110, 0, 0),
    };

    // DefaultRed_Lighter_O
    public static Color[] RedLighterOuter { get; } = {
        Color.Transparent,
        new Color(255, 115, 115),
        new Color(198, 0, 0),
    };

    // DefaultYellow_O
    public static Color[] YellowOuter { get; } = {
        Color.Transparent,
        new Color(255, 224, 22),
        new Color(188, 165, 16),
    };

    // DefaultWhite_I (used for standard message text in the old engine)
    public static Color[] WhiteInner { get; } = {
        Color.Transparent,
        new Color(239, 239, 239),
        new Color(132, 132, 132),
    };

    // DefaultWhite_DarkerOutline_I
    public static Color[] WhiteDarkerOutlineInner { get; } = {
        Color.Transparent,
        new Color(250, 250, 250),
        new Color(80, 80, 80),
    };
}
