using System.Text;
using MiniToolbox.Gdb1.Models;

namespace MiniToolbox.Gdb1.Extraction;

/// <summary>
/// Extracts color animations from .constcoloranimgdb files.
/// </summary>
public class AnimationExtractor
{
    private const float FrameRate = 30.0f;
    private const int MaxKeyframesPerTrack = 50;

    private readonly string _gdbPath;
    private readonly string _metaPath;
    private readonly byte[] _gdbData;
    private readonly Gdb1Parser _parser;

    public AnimationExtractor(string gdbPath)
    {
        _gdbPath = gdbPath;
        _metaPath = Path.ChangeExtension(gdbPath, ".resourcemetadata");

        _gdbData = File.ReadAllBytes(_gdbPath);
        _parser = new Gdb1Parser(_gdbData);
    }

    /// <summary>
    /// Gets the animation name from metadata or filename.
    /// </summary>
    public string GetAnimationName()
    {
        if (File.Exists(_metaPath))
        {
            var metaData = File.ReadAllBytes(_metaPath);
            var metaParser = new Gdb1Parser(metaData);
            var strings = metaParser.ExtractStrings();

            foreach (var s in strings)
            {
                if (s.Contains("anim", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("color", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFileNameWithoutExtension(s);
            }
        }

        return Path.GetFileNameWithoutExtension(_gdbPath);
    }

    /// <summary>
    /// Finds table names in the animation data.
    /// </summary>
    private List<string> FindTables()
    {
        var strings = _parser.ExtractStrings();
        var tables = new HashSet<string>();

        foreach (var s in strings)
        {
            if (s.StartsWith("Table") && s.Length > 5)
            {
                if (int.TryParse(s.Substring(5), out _))
                    tables.Add(s);
            }
        }

        return tables.OrderBy(t => t).ToList();
    }

    /// <summary>
    /// Extracts the color animation.
    /// </summary>
    public ConstColorAnimation Extract()
    {
        string name = GetAnimationName();
        var tables = FindTables();

        var animation = new ConstColorAnimation
        {
            Name = name,
            SourceFile = Path.GetFileName(_gdbPath)
        };

        foreach (var tableName in tables)
        {
            var track = new ColorTrack { Name = tableName };

            // Find the position of this table in the data
            byte[] tableBytes = Encoding.UTF8.GetBytes(tableName);
            int pos = FindBytes(_gdbData, tableBytes);

            if (pos != -1)
            {
                // Read color data after the table name
                int searchStart = pos + tableBytes.Length + 10;
                int searchEnd = Math.Min(searchStart + 200, _gdbData.Length);

                float time = 0.0f;
                for (int i = searchStart; i < searchEnd - 3; i += 4)
                {
                    byte r = _gdbData[i];
                    byte g = _gdbData[i + 1];
                    byte b = _gdbData[i + 2];
                    byte a = (i + 3 < _gdbData.Length) ? _gdbData[i + 3] : (byte)255;

                    // Heuristic: valid color data if at least one channel is not at extremes
                    if ((r > 0 && r < 255) || (g > 0 && g < 255) || (b > 0 && b < 255))
                    {
                        track.Keyframes.Add(new ColorKeyframe(time, r, g, b, a));
                        time += 1.0f / FrameRate;
                    }

                    if (track.Keyframes.Count >= MaxKeyframesPerTrack)
                        break;
                }
            }

            if (track.Keyframes.Count > 0)
            {
                animation.Tracks.Add(track);
                float trackDuration = track.Keyframes.Count > 0
                    ? track.Keyframes[^1].Time
                    : 0;
                animation.Duration = Math.Max(animation.Duration, trackDuration);
            }
        }

        return animation;
    }

    private static int FindBytes(byte[] source, byte[] pattern)
    {
        for (int i = 0; i <= source.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }
        return -1;
    }

    public string GdbPath => _gdbPath;
}
