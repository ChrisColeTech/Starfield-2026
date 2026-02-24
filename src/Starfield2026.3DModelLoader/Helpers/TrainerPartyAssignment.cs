#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Starfield2026.ModelLoader.Helpers;

public static class TrainerPartyAssignment
{
    public static Dictionary<string, string?[]> LoadFromJson(string jsonPath)
    {
        var result = new Dictionary<string, string?[]>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(jsonPath)) return result;

        string json = File.ReadAllText(jsonPath);
        var doc = JsonDocument.Parse(json);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var party = new string?[6];
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var elem in prop.Value.EnumerateArray())
                {
                    if (i >= 6) break;
                    party[i] = elem.ValueKind == JsonValueKind.String ? elem.GetString() : null;
                    i++;
                }
            }
            result[prop.Name] = party;
        }

        return result;
    }

    public static string[]? ResolveParty(
        string trainerFolderPath,
        string pokemonRoot,
        Dictionary<string, string?[]> assignments)
    {
        string trainerName = Path.GetFileName(trainerFolderPath.TrimEnd('/', '\\'));
        if (!assignments.TryGetValue(trainerName, out var relativePaths))
            return null;

        var resolved = new string[6];
        for (int i = 0; i < 6; i++)
        {
            if (!string.IsNullOrWhiteSpace(relativePaths[i]))
                resolved[i] = Path.Combine(pokemonRoot, relativePaths[i]!);
            else
                resolved[i] = "";
        }
        return resolved;
    }
}
