using System;

namespace MiniToolbox.Spica.Formats.CtrGfx.Light
{
    [Flags]
    public enum GfxVertexLightFlags : uint
    {
        IsInheritingDirectionRotation = 1 << 1
    }
}
