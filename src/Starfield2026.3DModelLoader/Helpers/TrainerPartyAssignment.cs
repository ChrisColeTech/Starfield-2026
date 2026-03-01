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
        string key = ExtractPartyKey(trainerFolderPath);
        if (string.IsNullOrEmpty(key))
            return null;

        if (!assignments.TryGetValue(key, out var relativePaths))
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

    private static string ExtractPartyKey(string trainerFolderPath)
    {
        string normalized = trainerFolderPath.Replace('\\', '/').TrimEnd('/');
        
        string[] generations = { "PZLA", "scarlet", "sun-moon-v2", "sun-moon" };
        foreach (var gen in generations)
        {
            int idx = normalized.IndexOf($"/{gen}/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                string afterGen = normalized.Substring(idx + gen.Length + 2);
                string[] parts = afterGen.Split('/');
                foreach (var part in parts)
                {
                    if (part.StartsWith("tr", StringComparison.OrdinalIgnoreCase) && part != "trainers")
                    {
                        return $"{gen}/{part}";
                    }
                }
            }
        }
        
        string trainerName = Path.GetFileName(normalized);
        return trainerName.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? trainerName : "";
    }
}
