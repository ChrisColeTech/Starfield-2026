using Microsoft.Xna.Framework;
using Starfield2026.Core.Maps;

namespace Starfield2026.Core.Systems.Coins;

public interface ICoinSpawner
{
    void Initialize(CoinCollectibleSystem system);
    void Update(float dt, Vector3 playerPos, float playerSpeed, CoinCollectibleSystem system, MapDefinition? map = null);
    void OnMapLoaded(MapDefinition map, CoinCollectibleSystem system);
}
