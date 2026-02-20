using System.Buffers.Binary;
using System.Text;

namespace MiniToolbox.Gdb1;

/// <summary>
/// Parser for GDB1 (Game Database) serialized format used by Star Fox Zero/Guard.
/// </summary>
public class Gdb1Parser
{
    public const int HeaderSize = 20;
    public const string Magic = "GDB1";

    private readonly byte[] _data;

    public Gdb1Parser(byte[] data)
    {
        _data = data;
    }

    public static bool IsGdb1File(byte[] data)
    {
        if (data.Length < 4)
            return false;
        return data[0] == 'G' && data[1] == 'D' && data[2] == 'B' && data[3] == '1';
    }

    public static bool IsGdb1File(string path)
    {
        using var fs = File.OpenRead(path);
        var magic = new byte[4];
        if (fs.Read(magic, 0, 4) < 4)
            return false;
        return IsGdb1File(magic);
    }

    /// <summary>
    /// Extracts all readable ASCII strings from the data.
    /// </summary>
    public List<string> ExtractStrings()
    {
        var strings = new List<string>();
        var current = new StringBuilder();

        foreach (byte b in _data)
        {
            if (b >= 32 && b < 127)
            {
                current.Append((char)b);
            }
            else if (b == 0 && current.Length > 0)
            {
                if (current.Length >= 2)
                    strings.Add(current.ToString());
                current.Clear();
            }
            else
            {
                if (current.Length >= 3)
                    strings.Add(current.ToString());
                current.Clear();
            }
        }

        return strings;
    }

    /// <summary>
    /// Finds ResourceID references (32-bit values matching known IDs).
    /// </summary>
    public List<string> FindResourceIds(HashSet<string> validIds)
    {
        var found = new HashSet<string>();

        for (int i = HeaderSize; i < _data.Length - 4; i += 4)
        {
            uint val = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(i, 4));
            string hexId = val.ToString("x8");
            if (validIds.Contains(hexId))
                found.Add(hexId);
        }

        return found.ToList();
    }

    /// <summary>
    /// Reads a uint32 at the specified offset.
    /// </summary>
    public uint ReadUInt32(int offset)
    {
        if (offset + 4 > _data.Length)
            return 0;
        return BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset, 4));
    }

    /// <summary>
    /// Reads a uint16 at the specified offset.
    /// </summary>
    public ushort ReadUInt16(int offset)
    {
        if (offset + 2 > _data.Length)
            return 0;
        return BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(offset, 2));
    }

    /// <summary>
    /// Reads a int16 at the specified offset.
    /// </summary>
    public short ReadInt16(int offset)
    {
        if (offset + 2 > _data.Length)
            return 0;
        return BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(offset, 2));
    }

    public byte[] Data => _data;
    public int Length => _data.Length;
}
