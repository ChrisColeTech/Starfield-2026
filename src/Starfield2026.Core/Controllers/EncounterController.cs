using System;
using Microsoft.Xna.Framework;
using Starfield2026.Core.Encounters;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Controllers;

public class EncounterController
{
    public event Action? OnEncounter;
    
    private float _checkInterval = 0.5f;
    private float _baseChance = 0.08f;
    private float _timer;
    private readonly Random _random = new();
    
    public EncounterController(float checkInterval = 0.5f, float baseChance = 0.08f)
    {
        _checkInterval = checkInterval;
        _baseChance = baseChance;
    }
    
    public void Update(float dt, bool isMoving, MapDefinition? map, Vector3 playerPosition)
    {
        if (!isMoving)
        {
            _timer = 0;
            return;
        }
        
        _timer += dt;
        
        if (_timer < _checkInterval) return;
        _timer = 0;
        
        if (map != null)
        {
            float scale = 2f;
            int tx = (int)(playerPosition.X / scale + map.Width / 2f);
            int tz = (int)(playerPosition.Z / scale + map.Height / 2f);
            
            if (tx >= 0 && tx < map.Width && tz >= 0 && tz < map.Height)
            {
                int tileId = map.GetBaseTile(tx, tz);
                var tileDef = TileRegistry.GetTile(tileId);
                
                if (tileDef?.OverlayBehavior != null)
                {
                    var result = EncounterRegistry.TryEncounter(tileDef.OverlayBehavior);
                    if (result != null)
                    {
                        OnEncounter?.Invoke();
                        return;
                    }
                }
            }
        }
        
        if (_random.NextDouble() < _baseChance)
            OnEncounter?.Invoke();
    }
}
