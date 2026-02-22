using MiniToolbox.Core.Utils;
using MiniToolbox.Trpak.Archive;

namespace MiniToolbox.Hashes;

/// <summary>
/// Generates a hash list (FNV hash → file path) for a TRPFD/TRPFS archive.
///
/// Strategy: Template Matching
/// Given an existing hash list (e.g. from Scarlet), compute FNV hashes of all known
/// paths and check which exist in the target archive's file hash set. Since games using
/// the same engine (Trinity) share file path conventions, this resolves the majority
/// of files without needing to reverse-engineer pack structures.
///
/// For files not found via template matching, a brute-force scan identifies TRMDL files
/// by content and uses their internal references to reconstruct paths.
/// </summary>
public class HashListGenerator
{
    private readonly TrpfsLoader _loader;
    private readonly HashSet<ulong> _archiveHashes;

    public event Action<HashListProgress>? OnProgress;

    public HashListGenerator(TrpfsLoader loader)
    {
        _loader = loader;
        _archiveHashes = new HashSet<ulong>(loader.FileHashes);
    }

    /// <summary>
    /// Phase 1: Template matching — check which paths from a known hash list exist in this archive.
    /// This is O(n) where n = number of template paths.
    /// </summary>
    public Dictionary<ulong, string> GenerateFromTemplates(IEnumerable<string> knownPaths)
    {
        var resolved = new Dictionary<ulong, string>();
        int total = 0;
        int matched = 0;

        foreach (string path in knownPaths)
        {
            total++;
            ulong hash = FnvHash.Hash(path);

            if (_archiveHashes.Contains(hash))
            {
                resolved[hash] = path;
                matched++;
            }

            if (total % 50000 == 0)
            {
                OnProgress?.Invoke(new HashListProgress
                {
                    ProcessedPacks = total,
                    TotalPacks = total, // unknown total
                    ResolvedPaths = matched,
                    CurrentPack = $"template matching: {matched}/{total} matched"
                });
            }
        }

        OnProgress?.Invoke(new HashListProgress
        {
            ProcessedPacks = total,
            TotalPacks = total,
            ResolvedPaths = matched,
            CurrentPack = $"template matching complete: {matched}/{total} matched"
        });

        return resolved;
    }

    /// <summary>
    /// Phase 2: Brute-force scan — for remaining unresolved hashes, extract files and
    /// try to identify TRMDL models by content. Use internal references to reconstruct paths.
    /// </summary>
    public Dictionary<ulong, string> GenerateByContentScan(HashSet<ulong>? alreadyResolved = null)
    {
        var resolved = new Dictionary<ulong, string>();
        var fileHashes = _loader.FileHashes;
        int total = fileHashes.Length;
        int scanned = 0;
        int found = 0;

        foreach (var hash in fileHashes)
        {
            scanned++;

            // Skip already resolved
            if (alreadyResolved != null && alreadyResolved.Contains(hash))
                continue;

            try
            {
                var bytes = _loader.ExtractFile(hash);
                if (bytes == null || bytes.Length < 20) continue;

                // Try to parse as TRMDL
                var mdl = FlatBufferConverter.DeserializeFrom<
                    MiniToolbox.Trpak.Flatbuffers.TR.Model.TRMDL>(bytes);
                if (mdl == null) continue;

                int meshCount = mdl.Meshes?.Length ?? 0;
                bool hasSkeleton = mdl.Skeleton != null &&
                    !string.IsNullOrWhiteSpace(mdl.Skeleton.PathName);

                if (meshCount == 0 && !hasSkeleton) continue;

                // Found a model. Use a synthetic path based on hash + mesh name.
                string meshName = mdl.Meshes?[0]?.PathName ?? "unknown";
                string baseName = Path.GetFileNameWithoutExtension(meshName);
                string syntheticPath = $"_unresolved/{baseName}/{baseName}.trmdl";

                resolved[hash] = syntheticPath;
                found++;
            }
            catch { }

            if (scanned % 10000 == 0)
            {
                OnProgress?.Invoke(new HashListProgress
                {
                    ProcessedPacks = scanned,
                    TotalPacks = total,
                    ResolvedPaths = found,
                    CurrentPack = $"content scan: {found} models in {scanned}/{total}"
                });
            }
        }

        return resolved;
    }

    /// <summary>
    /// Full generation: template matching first, then content scan for the remainder.
    /// </summary>
    public Dictionary<ulong, string> Generate(IEnumerable<string>? templatePaths = null)
    {
        var result = new Dictionary<ulong, string>();

        // Phase 1: Template matching (if templates provided)
        if (templatePaths != null)
        {
            var templateResults = GenerateFromTemplates(templatePaths);
            foreach (var (hash, path) in templateResults)
                result[hash] = path;

            OnProgress?.Invoke(new HashListProgress
            {
                ProcessedPacks = 0,
                TotalPacks = 0,
                ResolvedPaths = result.Count,
                CurrentPack = $"Phase 1 complete: {result.Count} paths from templates"
            });
        }

        // Phase 2: Content scan for unresolved hashes
        int unresolvedCount = _archiveHashes.Count - result.Count;
        if (unresolvedCount > 0)
        {
            OnProgress?.Invoke(new HashListProgress
            {
                ProcessedPacks = 0,
                TotalPacks = unresolvedCount,
                ResolvedPaths = result.Count,
                CurrentPack = $"Phase 2: scanning {unresolvedCount} unresolved hashes..."
            });

            var scanResults = GenerateByContentScan(new HashSet<ulong>(result.Keys));
            foreach (var (hash, path) in scanResults)
                result.TryAdd(hash, path);
        }

        return result;
    }

    /// <summary>
    /// Writes the generated hash list to a file.
    /// Format: 0xHASH path/to/file
    /// </summary>
    public static void WriteHashList(string outputPath, Dictionary<ulong, string> hashList)
    {
        using var writer = new StreamWriter(outputPath);
        foreach (var (hash, path) in hashList.OrderBy(kvp => kvp.Value))
        {
            writer.WriteLine($"0x{hash:X16} {path}");
        }
    }
}

public class HashListProgress
{
    public int ProcessedPacks { get; set; }
    public int TotalPacks { get; set; }
    public int ResolvedPaths { get; set; }
    public string CurrentPack { get; set; } = "";
    public int FilesInPack { get; set; }
    public int IdentifiedInPack { get; set; }
    public double PercentComplete => TotalPacks > 0 ? (double)ProcessedPacks / TotalPacks * 100 : 0;
    public string? DiagnosticMessage { get; set; }
}
