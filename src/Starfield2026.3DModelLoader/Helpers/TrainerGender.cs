#nullable enable
using System;
using System.IO;

namespace Starfield2026.ModelLoader.Helpers;

public static class TrainerGender
{
    public enum BodyType { Boy, Girl, Man, Woman, Unknown }

    /// <summary>
    /// Classify a trainer's body type by checking if its folder path contains
    /// a body-type parent directory (boy/, girl/, man/, woman/).
    /// </summary>
    public static BodyType Classify(string characterFolderPath)
    {
        string normalized = characterFolderPath.Replace('\\', '/').TrimEnd('/');

        // Walk parent directories looking for a body type folder name
        string? dir = Path.GetDirectoryName(normalized.Replace('/', Path.DirectorySeparatorChar));
        while (dir != null)
        {
            string folderName = Path.GetFileName(dir);
            if (TryParseBodyType(folderName, out var bt))
                return bt;
            string? parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }

        return BodyType.Unknown;
    }

    public static bool IsTrainerFolder(string characterFolderPath)
    {
        string folderName = Path.GetFileName(characterFolderPath.TrimEnd('/', '\\'));
        return folderName.Length >= 6 && folderName.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
            && char.IsDigit(folderName[2]);
    }

    public static bool IsFeminine(string characterFolderPath)
    {
        var bt = Classify(characterFolderPath);
        return bt == BodyType.Girl || bt == BodyType.Woman;
    }

    public static string GetSharedFolderName(BodyType bodyType) => bodyType switch
    {
        BodyType.Boy   => "boy",
        BodyType.Girl  => "girl",
        BodyType.Man   => "man",
        BodyType.Woman => "woman",
        _              => "man",
    };

    private static bool TryParseBodyType(string folderName, out BodyType bodyType)
    {
        if (folderName.Equals("boy", StringComparison.OrdinalIgnoreCase))
        { bodyType = BodyType.Boy; return true; }
        if (folderName.Equals("girl", StringComparison.OrdinalIgnoreCase))
        { bodyType = BodyType.Girl; return true; }
        if (folderName.Equals("man", StringComparison.OrdinalIgnoreCase))
        { bodyType = BodyType.Man; return true; }
        if (folderName.Equals("woman", StringComparison.OrdinalIgnoreCase))
        { bodyType = BodyType.Woman; return true; }

        bodyType = BodyType.Unknown;
        return false;
    }
}
