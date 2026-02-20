using System;
using Microsoft.Xna.Framework;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Controllers;

public class WorldController
{
    public MapDefinition? CurrentMap => _currentMap;
    public string? CurrentMapId => _currentMap?.Id;
    
    private MapDefinition? _currentMap;
    private float _warpCooldown;
    
    public event Action<WarpConnection>? OnMapTransition;
    
    public void LoadMap(MapDefinition map)
    {
        _currentMap = map;
    }
    
    public void LoadMap(string mapId, int spawnX, int spawnY)
    {
        if (MapCatalog.TryGetMap(mapId, out var map) && map != null)
        {
            _currentMap = map;
        }
    }
    
    public void SetMapBounds(PlayerController player)
    {
        if (_currentMap is Maps.Generated.OverworldGrid)
            player.WorldHalfSize = 500f;
        else if (_currentMap != null)
            player.WorldHalfSize = _currentMap.Width * 2f / 2f;
    }
    
    public void Update(float dt, Vector3 playerPosition)
    {
        if (_warpCooldown > 0)
        {
            _warpCooldown -= dt;
            return;
        }
        
        if (_currentMap == null) return;
        
        float scale = 2f;
        int px = (int)Math.Floor(playerPosition.X / scale + _currentMap.Width / 2f);
        int pz = (int)Math.Floor(playerPosition.Z / scale + _currentMap.Height / 2f);
        
        px = Math.Clamp(px, 0, _currentMap.Width - 1);
        pz = Math.Clamp(pz, 0, _currentMap.Height - 1);
        
        foreach (var warp in _currentMap.Warps)
        {
            if (warp.X == px && warp.Y == pz && warp.Trigger == WarpTrigger.Step)
            {
                _warpCooldown = 0.5f;
                OnMapTransition?.Invoke(warp);
                return;
            }
        }
    }
    
    public WarpConnection? GetInteractableWarp(Vector3 playerPosition)
    {
        if (_currentMap == null) return null;
        
        float scale = 2f;
        int px = (int)Math.Floor(playerPosition.X / scale + _currentMap.Width / 2f);
        int pz = (int)Math.Floor(playerPosition.Z / scale + _currentMap.Height / 2f);
        
        px = Math.Clamp(px, 0, _currentMap.Width - 1);
        pz = Math.Clamp(pz, 0, _currentMap.Height - 1);
        
        foreach (var warp in _currentMap.Warps)
        {
            if (warp.X == px && warp.Y == pz && warp.Trigger == WarpTrigger.Interact)
                return warp;
        }
        
        return null;
    }
}
