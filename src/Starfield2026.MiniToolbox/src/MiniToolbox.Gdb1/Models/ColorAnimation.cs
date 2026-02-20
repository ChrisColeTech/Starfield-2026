namespace MiniToolbox.Gdb1.Models;

/// <summary>
/// Represents a single color keyframe.
/// </summary>
public struct ColorKeyframe
{
    public float Time { get; set; }
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; }

    public ColorKeyframe(float time, byte r, byte g, byte b, byte a = 255)
    {
        Time = time;
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public int[] ToArray() => new int[] { R, G, B, A };
}

/// <summary>
/// Represents a color animation track.
/// </summary>
public class ColorTrack
{
    public string Name { get; set; } = string.Empty;
    public List<ColorKeyframe> Keyframes { get; set; } = new();
}

/// <summary>
/// Represents a constant color animation.
/// </summary>
public class ConstColorAnimation
{
    public string Name { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public float Duration { get; set; }
    public List<ColorTrack> Tracks { get; set; } = new();
}
