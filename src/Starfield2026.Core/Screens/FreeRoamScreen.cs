#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Controllers;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;
using Starfield2026.ModelLoader.Skeletal;

namespace Starfield2026.Core.Screens;

/// <summary>
/// Free Roam screen — on-foot exploration with skeletal character, own camera,
/// and quadrant-colored grid. Completely independent of OverworldScreen and OrbitCamera.
/// </summary>
public class FreeRoamScreen : IGameScreen
{
    private GraphicsDevice _device = null!;
    private QuadrantGridRenderer _grid = null!;
    private CubeRenderer _cubeRenderer = null!;
    private OverworldCharacter? _character;
    private PlayerController _player = new();

    // ─── Own camera state ───────────────────────────────────────────
    private float _camYaw;
    private float _camPitch = -0.15f;
    private float _camDist = 12f;
    private float _camYawOffset;
    private Vector3 _camTarget;
    private float _camSmoothedYaw;
    private bool _camInitialized;

    private const float CamFollowSpeed = 3f;
    private const float CamYawSpeed = 2f;
    private const float CamPitchSpeed = 1f;
    private const float CamZoomSpeed = 10f;
    private const float CamMinDist = 3f;
    private const float CamMaxDist = 40f;
    private const float CamMinPitch = -1.4f;
    private const float CamMaxPitch = -0.1f;
    private const float Fov = MathHelper.PiOver4;
    private const float NearPlane = 0.1f;
    private const float FarPlane = 500f;

    private Matrix _view;
    private Matrix _projection;
    private Vector3 _camPosition;

    // ─── Public accessors ────────────────────────────────────────────
    public Vector3 Position => _player.Position;
    public float Yaw => _player.Yaw;

    // ─── Status text ────────────────────────────────────────────────
    public string StatusText { get; private set; } = "No model loaded";

    public void Initialize(GraphicsDevice device)
    {
        _device = device;

        _grid = new QuadrantGridRenderer
        {
            Spacing = 2f,
            GridHalfSize = 250,
            PlaneOffset = 0f,
        };
        _grid.Initialize(device);

        _cubeRenderer = new CubeRenderer();
        _cubeRenderer.Initialize(device);

        _player.Initialize(new Vector3(0, 0.825f, 0));
        _player.WorldHalfSize = 500f;
        _camTarget = _player.Position;
    }

    public void SetPosition(Vector3 position)
    {
        _player.SetPosition(position, _player.Yaw);
        _camTarget = position;
        _camInitialized = false;
    }

    public void LoadCharacter(string folderPath)
    {
        // Clear stale log
        string logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "character_load.log");
        try { System.IO.File.Delete(logPath); } catch { }

        try
        {
            _character?.Dispose();
            _character = new OverworldCharacter();
            _character.Load(_device, folderPath);
            StatusText = $"Loaded: {System.IO.Path.GetFileName(folderPath)}";

            // Diagnostic: log mesh stats
            var (positions, normals, texCoords, indices, skinWeightIndices) =
                ColladaSkeletalLoader.LoadGeometry(
                    System.IO.Path.Combine(folderPath, "model.dae"));
            var (weights, jointNames, _) =
                ColladaSkeletalLoader.LoadSkinWeights(
                    System.IO.Path.Combine(folderPath, "model.dae"));
            System.IO.File.WriteAllText(logPath,
                $"Folder: {folderPath}\n" +
                $"Deduped verts: {positions.Length}\n" +
                $"Indices: {indices.Length} ({indices.Length / 3} tris)\n" +
                $"Skin weights: {weights.Length}\n" +
                $"Joint names: {jointNames.Length}\n" +
                $"SkinWeightIndices: {skinWeightIndices.Length}\n" +
                $"SkinWeightIdx range: {(skinWeightIndices.Length > 0 ? $"{skinWeightIndices.Min()}..{skinWeightIndices.Max()}" : "empty")}\n");
        }
        catch (Exception ex)
        {
            _character?.Dispose();
            _character = null;
            StatusText = $"Load failed: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[FreeRoam] Character load failed: {ex}");
            System.IO.File.WriteAllText(logPath, $"Folder: {folderPath}\n\n{ex}");
        }
    }

    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // ── Camera rotation/zoom from input ──
        if (input.CameraYaw != 0)
            _camYawOffset += input.CameraYaw * CamYawSpeed * dt;
        if (input.CameraPitch != 0)
            _camPitch = MathHelper.Clamp(_camPitch + input.CameraPitch * CamPitchSpeed * dt, CamMinPitch, CamMaxPitch);
        if (input.CameraZoom != 0)
            _camDist = MathHelper.Clamp(_camDist + input.CameraZoom * CamZoomSpeed * dt, CamMinDist, CamMaxDist);

        // ── Player movement (walk/run/jump, no hover) ──
        _player.Update(dt, input);

        // ── Update character animation ──
        _character?.Update(dt, _player.IsMoving, _player.IsRunning, _player.IsGrounded);

        // ── Update camera ──
        UpdateCamera(dt);
    }

    private void UpdateCamera(float dt)
    {
        if (!_camInitialized)
        {
            _camTarget = _player.Position;
            _camSmoothedYaw = _player.Yaw + MathHelper.Pi + _camYawOffset;
            _camInitialized = true;
        }

        // Only follow position and yaw when the player is actually moving (has speed)
        // Turning in place (head swivel) should NOT drag the camera
        bool playerMoving = _player.Speed > 0.5f;

        float t = 1f - (float)Math.Exp(-CamFollowSpeed * dt);
        _camTarget = Vector3.Lerp(_camTarget, _player.Position, t);

        if (playerMoving)
        {
            float targetYaw = _player.Yaw + MathHelper.Pi + _camYawOffset;
            float yawDiff = targetYaw - _camSmoothedYaw;
            while (yawDiff > MathHelper.Pi) yawDiff -= MathHelper.TwoPi;
            while (yawDiff < -MathHelper.Pi) yawDiff += MathHelper.TwoPi;
            _camSmoothedYaw += yawDiff * CamFollowSpeed * dt;
        }

        _camYaw = _camSmoothedYaw;

        var lookAt = _camTarget + Vector3.Up * 1.5f;

        var offset = new Vector3(
            (float)(_camDist * Math.Cos(_camPitch) * Math.Sin(_camSmoothedYaw)),
            (float)(_camDist * -Math.Sin(_camPitch)),
            (float)(_camDist * Math.Cos(_camPitch) * Math.Cos(_camSmoothedYaw)));

        _camPosition = lookAt + offset;

        float aspect = _device.Viewport.Width / (float)_device.Viewport.Height;
        _view = Matrix.CreateLookAt(_camPosition, lookAt, Vector3.Up);
        _projection = Matrix.CreatePerspectiveFieldOfView(Fov, aspect, NearPlane, FarPlane);
    }

    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(20, 25, 50));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.BlendState = BlendState.AlphaBlend;

        _grid.Draw(device, _view, _projection);

        var pos = _player.Position;
        float yaw = _player.Yaw;

        // Shadow
        _cubeRenderer.Draw(device, _view, _projection,
            new Vector3(pos.X, 0.05f, pos.Z),
            yaw, new Vector3(1.5f, 0.05f, 1.5f), Color.Black * 0.4f);

        // Character or fallback cube
        var drawPos = pos;

        if (_character is { IsLoaded: true })
        {
            _character.Draw(device, _view, _projection, drawPos, yaw);
        }
        else
        {
            _cubeRenderer.Draw(device, _view, _projection, drawPos, yaw, 1.5f, new Color(0, 220, 255));
        }
    }

    public void OnEnter() { }
    public void OnExit() { }
}
