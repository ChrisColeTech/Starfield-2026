#nullable enable
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.Rendering;

/// <summary>
/// Shared GPU render states for reuse across screens.
/// </summary>
public static class RenderStates
{
    /// <summary>
    /// No backface culling — renders both sides of every triangle.
    /// Use for foliage, trees, mountains, and any mesh with single-sided faces.
    /// </summary>
    public static readonly RasterizerState CullNone = new()
    {
        CullMode = CullMode.None,
        FillMode = FillMode.Solid,
        MultiSampleAntiAlias = true,
    };
}
