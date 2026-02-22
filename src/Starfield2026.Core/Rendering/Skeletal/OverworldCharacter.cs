#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering.Skeletal;

public sealed class OverworldCharacter : IDisposable
{
    public bool IsLoaded { get; private set; }

    private AnimationController? _controller;
    private SkinnedDaeModel? _model;
    private BasicEffect? _effect;
    private float _fitScale = 1f;
    private const float TargetHeight = 2.0f;

    public void Load(GraphicsDevice device, string characterFolderPath)
    {
        Dispose();

        var animSet = SplitModelAnimationSetLoader.Load(characterFolderPath);

        _controller = new AnimationController(animSet);
        _model = new SkinnedDaeModel();

        // Find albedo texture
        string? texturePath = FindAlbedoTexture(characterFolderPath);
        _model.Load(device, animSet.ModelPath, animSet.Skeleton, texturePath);

        _effect = new BasicEffect(device)
        {
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            VertexColorEnabled = false,
            TextureEnabled = _model.Texture != null,
            Texture = _model.Texture,
        };
        _effect.EnableDefaultLighting();

        // Start with idle animation to compute bounds
        _controller.Play("Idle");
        _controller.Update(0f);
        _model.UpdatePose(device, _controller.SkinPose);
        _model.ComputeSkinnedBounds(_controller.SkinPose);

        // Compute fit scale
        float modelHeight = _model.BoundsMax.Y - _model.BoundsMin.Y;
        if (modelHeight > 0.001f)
            _fitScale = TargetHeight / modelHeight;

        IsLoaded = true;
    }

    public void Update(float dt, bool isMoving, bool isRunning, bool isGrounded)
    {
        if (_controller == null) return;

        string desiredTag;
        if (isRunning && _controller.HasClip("Run"))
            desiredTag = "Run";
        else if (isMoving && _controller.HasClip("Walk"))
            desiredTag = "Walk";
        else
            desiredTag = "Idle";

        if (_controller.ActiveTag != desiredTag)
            _controller.Play(desiredTag);

        _controller.Update(dt);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float rotationY)
    {
        if (_model == null || _effect == null || _controller == null)
            return;

        _model.UpdatePose(device, _controller.SkinPose);

        // Center the model at its feet
        float baseY = _model.BoundsMin.Y * _fitScale;

        Matrix world = Matrix.CreateScale(_fitScale)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(position.X, position.Y - baseY, position.Z);

        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;

        _model.Draw(device, _effect);
    }

    public void Dispose()
    {
        _model?.Dispose();
        _effect?.Dispose();
        _model = null;
        _effect = null;
        _controller = null;
        IsLoaded = false;
    }

    private static string? FindAlbedoTexture(string folderPath)
    {
        string manifestPath = Path.Combine(folderPath, "manifest.json");
        if (!File.Exists(manifestPath)) return null;

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("textures", out var texArray) ||
                doc.RootElement.TryGetProperty("Textures", out texArray))
            {
                foreach (var tex in texArray.EnumerateArray())
                {
                    string? name = tex.GetString();
                    if (name != null && name.Contains("alb", StringComparison.OrdinalIgnoreCase))
                    {
                        string fullPath = Path.Combine(folderPath, name);
                        if (File.Exists(fullPath))
                            return fullPath;
                    }
                }
                // Fallback: first texture
                foreach (var tex in texArray.EnumerateArray())
                {
                    string? name = tex.GetString();
                    if (name != null)
                    {
                        string fullPath = Path.Combine(folderPath, name);
                        if (File.Exists(fullPath))
                            return fullPath;
                    }
                }
            }
        }
        catch { }

        return null;
    }
}
