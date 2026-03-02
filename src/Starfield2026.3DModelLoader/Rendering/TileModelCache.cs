#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Maps;
using Starfield2026.ModelLoader.Maps.TileMappers;

namespace Starfield2026.ModelLoader.Rendering;

public sealed class TileModelCache : IDisposable
{
    private readonly ConcurrentDictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _textureFileByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _textureQueue = new();
    private readonly HashSet<string> _queuedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _queueLock = new();

    private readonly ConcurrentDictionary<string, StaticModel> _models = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _modelFileByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<(string modelId, string filePath)> _modelQueue = new();
    private readonly HashSet<string> _queuedModelKeys = new(StringComparer.OrdinalIgnoreCase);

    private int _pendingCount;
    private int _indexedModelCount;
    private int _indexedTextureCount;

    public int IndexedModelCount => _indexedModelCount;
    public int PendingLoadCount => Volatile.Read(ref _pendingCount);
    public int ModelCount => Math.Max(_indexedModelCount + _indexedTextureCount, _textures.Count);

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
            texture.Dispose();
        _textures.Clear();
        _textureFileByKey.Clear();

        foreach (var model in _models.Values)
            model.Dispose();
        _models.Clear();
        _modelFileByKey.Clear();

        lock (_queueLock)
        {
            _queuedKeys.Clear();
            _queuedModelKeys.Clear();
        }

        while (_textureQueue.TryDequeue(out _)) { }
        while (_modelQueue.TryDequeue(out _)) { }

        _pendingCount = 0;
        _indexedModelCount = 0;
        _indexedTextureCount = 0;
    }

    public void LoadFromRegistry(string mapsFolder)
    {
        // Legacy API retained for compatibility; map-scoped loading is done via BuildForMap.
        _textureFileByKey.Clear();
        _indexedModelCount = 0;
        _indexedTextureCount = 0;
        ClearLoadedAssets();
    }

    public void BuildForMap(MapDefinition map, string mapsFolder)
    {
        _textureFileByKey.Clear();
        _modelFileByKey.Clear();
        _indexedModelCount = 0;
        _indexedTextureCount = 0;
        ClearLoadedAssets();

        if (Directory.Exists(mapsFolder))
        {
            foreach (var file in Directory.EnumerateFiles(mapsFolder, "*.png", SearchOption.AllDirectories))
            {
                string keyFile = Path.GetFileName(file);
                string keyRel = Path.GetRelativePath(mapsFolder, file).Replace('\\', '/');
                _textureFileByKey.TryAdd(keyFile, file);
                _textureFileByKey.TryAdd(keyRel, file);
            }

            foreach (var ext in new[] { "*.dae", "*.fbx" })
            {
                foreach (var file in Directory.EnumerateFiles(mapsFolder, ext, SearchOption.AllDirectories))
                {
                    string keyFile = Path.GetFileName(file);
                    string keyRel = Path.GetRelativePath(mapsFolder, file).Replace('\\', '/');
                    _modelFileByKey.TryAdd(keyFile, file);
                    _modelFileByKey.TryAdd(keyRel, file);
                }
            }
        }

        var usedTileIds = new HashSet<int>();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                usedTileIds.Add(map.GetBaseTile(x, y));
                int? overlay = map.GetOverlayTile(x, y);
                if (overlay.HasValue)
                    usedTileIds.Add(overlay.Value);
            }
        }

        foreach (int tileId in usedTileIds)
        {
            var tile = TileRegistry.GetTile(tileId);
            if (tile == null)
                continue;

            if (!string.IsNullOrWhiteSpace(tile.ModelId))
                _indexedModelCount++;

            if (!string.IsNullOrWhiteSpace(tile.TexturePath))
            {
                _indexedTextureCount++;
                QueueTexturePath(tile.TexturePath);
            }

            if (!string.IsNullOrWhiteSpace(tile.ModelId) &&
                AnimeForestTileMapper.TryGetAsset(tileId, out var asset))
            {
                if (!string.IsNullOrWhiteSpace(asset.TexturePath))
                    QueueTexturePath(asset.TexturePath);

                if (!string.IsNullOrWhiteSpace(asset.ModelPath))
                {
                    string? resolvedPath = ResolveModelFilePath(asset.ModelPath);
                    if (resolvedPath != null)
                        QueueModelPath(tile.ModelId, resolvedPath);
                }
            }
        }
    }

    public void BuildForMaps(IEnumerable<MapDefinition> maps, string mapsFolder)
    {
        _textureFileByKey.Clear();
        _modelFileByKey.Clear();
        _indexedModelCount = 0;
        _indexedTextureCount = 0;
        ClearLoadedAssets();

        if (Directory.Exists(mapsFolder))
        {
            foreach (var file in Directory.EnumerateFiles(mapsFolder, "*.png", SearchOption.AllDirectories))
            {
                string keyFile = Path.GetFileName(file);
                string keyRel = Path.GetRelativePath(mapsFolder, file).Replace('\\', '/');
                _textureFileByKey.TryAdd(keyFile, file);
                _textureFileByKey.TryAdd(keyRel, file);
            }

            foreach (var ext in new[] { "*.dae", "*.fbx" })
            {
                foreach (var file in Directory.EnumerateFiles(mapsFolder, ext, SearchOption.AllDirectories))
                {
                    string keyFile = Path.GetFileName(file);
                    string keyRel = Path.GetRelativePath(mapsFolder, file).Replace('\\', '/');
                    _modelFileByKey.TryAdd(keyFile, file);
                    _modelFileByKey.TryAdd(keyRel, file);
                }
            }
        }

        var usedTileIds = new HashSet<int>();
        foreach (var map in maps)
        {
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    usedTileIds.Add(map.GetBaseTile(x, y));
                    int? overlay = map.GetOverlayTile(x, y);
                    if (overlay.HasValue)
                        usedTileIds.Add(overlay.Value);
                }
        }

        foreach (int tileId in usedTileIds)
        {
            var tile = TileRegistry.GetTile(tileId);
            if (tile == null) continue;

            if (!string.IsNullOrWhiteSpace(tile.ModelId))
                _indexedModelCount++;

            if (!string.IsNullOrWhiteSpace(tile.TexturePath))
            {
                _indexedTextureCount++;
                QueueTexturePath(tile.TexturePath);
            }

            if (!string.IsNullOrWhiteSpace(tile.ModelId) &&
                AnimeForestTileMapper.TryGetAsset(tileId, out var asset))
            {
                if (!string.IsNullOrWhiteSpace(asset.TexturePath))
                    QueueTexturePath(asset.TexturePath);

                if (!string.IsNullOrWhiteSpace(asset.ModelPath))
                {
                    string? resolvedPath = ResolveModelFilePath(asset.ModelPath);
                    if (resolvedPath != null)
                        QueueModelPath(tile.ModelId, resolvedPath);
                }
            }
        }
    }

    public void QueueLoadForModelIds(HashSet<string> modelIds)
    {
        // Models are disabled in the flat renderer path.
    }

    public int LoadQueuedBlocking(GraphicsDevice device)
    {
        int loaded = 0;
        while (TryLoadOneTexture(device) || TryLoadOneModel(device))
            loaded++;
        return loaded;
    }

    public void PumpQueuedLoads(GraphicsDevice device, int maxModelsPerUpdate, double maxMilliseconds)
    {
        long start = Stopwatch.GetTimestamp();
        int loaded = 0;

        while (loaded < maxModelsPerUpdate)
        {
            double elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            if (elapsedMs >= maxMilliseconds)
                break;

            if (!TryLoadOneTexture(device) && !TryLoadOneModel(device))
                break;

            loaded++;
        }
    }

    public bool TryGetTexture(string path, out Texture2D texture)
    {
        if (_textures.TryGetValue(path, out var exact) && exact != null)
        {
            texture = exact;
            return true;
        }

        string normalized = NormalizeKey(path);
        if (_textures.TryGetValue(normalized, out var normalizedTexture) && normalizedTexture != null)
        {
            texture = normalizedTexture;
            return true;
        }

        texture = null!;
        return false;
    }

    public bool TryGetModel(string modelId, out StaticModel model)
    {
        if (_models.TryGetValue(modelId, out var m) && m != null && m.IsLoaded)
        {
            model = m;
            return true;
        }

        model = null!;
        return false;
    }

    private void QueueTexturePath(string texturePath)
    {
        string key = NormalizeKey(texturePath);
        if (string.IsNullOrWhiteSpace(key))
            return;

        lock (_queueLock)
        {
            if (_textures.ContainsKey(key) || !_queuedKeys.Add(key))
                return;

            _textureQueue.Enqueue(key);
            Interlocked.Increment(ref _pendingCount);
        }
    }

    private void QueueModelPath(string modelId, string filePath)
    {
        lock (_queueLock)
        {
            if (_models.ContainsKey(modelId) || !_queuedModelKeys.Add(modelId))
                return;

            _modelQueue.Enqueue((modelId, filePath));
            Interlocked.Increment(ref _pendingCount);
        }
    }

    private bool TryLoadOneTexture(GraphicsDevice device)
    {
        if (!_textureQueue.TryDequeue(out var key))
            return false;

        lock (_queueLock)
            _queuedKeys.Remove(key);

        try
        {
            if (_textures.ContainsKey(key))
                return true;

            string? filePath = ResolveTexturePath(key);
            if (filePath == null || !File.Exists(filePath))
                return true;

            using var stream = File.OpenRead(filePath);
            Texture2D texture = Texture2D.FromStream(device, stream);

            _textures[key] = texture;

            string fileName = Path.GetFileName(filePath);
            _textures[fileName] = texture;

            return true;
        }
        catch
        {
            return true;
        }
        finally
        {
            Interlocked.Decrement(ref _pendingCount);
        }
    }

    private bool TryLoadOneModel(GraphicsDevice device)
    {
        if (!_modelQueue.TryDequeue(out var entry))
            return false;

        lock (_queueLock)
            _queuedModelKeys.Remove(entry.modelId);

        try
        {
            if (_models.ContainsKey(entry.modelId))
                return true;

            if (!File.Exists(entry.filePath))
                return true;

            var model = new StaticModel();
            if (entry.filePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                model.LoadFbx(device, entry.filePath);
            else
                model.Load(device, entry.filePath);

            if (model.IsLoaded)
                _models[entry.modelId] = model;
            else
                model.Dispose();

            return true;
        }
        catch
        {
            return true;
        }
        finally
        {
            Interlocked.Decrement(ref _pendingCount);
        }
    }

    private string? ResolveTexturePath(string key)
    {
        if (_textureFileByKey.TryGetValue(key, out var path))
            return path;

        string fileName = Path.GetFileName(key);
        if (_textureFileByKey.TryGetValue(fileName, out path))
            return path;

        if (File.Exists(key))
            return key;

        return null;
    }

    private string? ResolveModelFilePath(string relativePath)
    {
        if (_modelFileByKey.TryGetValue(relativePath, out var exact))
            return exact;

        string fileName = Path.GetFileName(relativePath);
        if (_modelFileByKey.TryGetValue(fileName, out var byName))
            return byName;

        string daePath = Path.ChangeExtension(relativePath, ".dae");
        if (_modelFileByKey.TryGetValue(daePath, out var dae))
            return dae;

        string daeFileName = Path.ChangeExtension(fileName, ".dae");
        if (_modelFileByKey.TryGetValue(daeFileName, out var daeByName))
            return daeByName;

        return null;
    }

    private static string NormalizeKey(string key) => key.Replace('\\', '/').Trim();

    private void ClearLoadedAssets()
    {
        foreach (var texture in _textures.Values)
            texture.Dispose();
        _textures.Clear();

        foreach (var model in _models.Values)
            model.Dispose();
        _models.Clear();

        lock (_queueLock)
        {
            _queuedKeys.Clear();
            _queuedModelKeys.Clear();
        }

        while (_textureQueue.TryDequeue(out _)) { }
        while (_modelQueue.TryDequeue(out _)) { }
        _pendingCount = 0;
    }
}
