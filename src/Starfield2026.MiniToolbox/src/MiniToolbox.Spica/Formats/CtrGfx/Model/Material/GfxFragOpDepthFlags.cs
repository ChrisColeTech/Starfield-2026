using System;

namespace MiniToolbox.Spica.Formats.CtrGfx.Model.Material
{
    [Flags]
    public enum GfxFragOpDepthFlags : uint
    {
        IsTestEnabled = 1 << 0,
        IsMaskEnabled = 1 << 1
    }
}
