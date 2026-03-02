#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Runtime;

public sealed class WorldRenderRuntime
{
    private readonly QuadrantGridRenderer _grid = new();
    private readonly CubeRenderer _cube = new();
    private readonly StarfieldRenderer _stars = new();

    public void Initialize(GraphicsDevice device)
    {
        _grid.Spacing = 2f;
        _grid.GridHalfSize = 250;
        _grid.PlaneOffset = 0f;
        _grid.Initialize(device);
        _cube.Initialize(device);
        _stars.Initialize(device);
    }

    public void BeginFrame(GraphicsDevice device)
    {
        device.Clear(new Color(20, 25, 50));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;
        device.SamplerStates[0] = SamplerState.AnisotropicClamp;
    }

    public void DrawWorld(GraphicsDevice device, Matrix view, Matrix projection, Vector3 cameraPos)
    {
        _stars.Draw(device, view, projection, cameraPos);
        _grid.Draw(device, view, projection);
    }

    public void DrawActorOrFallback(
        GraphicsDevice device,
        Matrix view,
        Matrix projection,
        Vector3 pos,
        float yaw,
        OverworldActorRuntime actor,
        Color fallback)
    {
        _cube.Draw(device, view, projection, new Vector3(pos.X, 0.05f, pos.Z), yaw,
            new Vector3(1.5f, 0.05f, 1.5f), Color.Black * 0.4f);

        if (actor.IsLoaded) actor.Draw(device, view, projection, pos, yaw);
        else _cube.Draw(device, view, projection, new Vector3(pos.X, pos.Y + 0.75f, pos.Z), yaw, 1.5f, fallback);
    }
}
