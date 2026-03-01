#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Screens;

/// <summary>
/// Screen for viewing static 3D map models with an orbit camera.
/// </summary>
public class MapViewerScreen
{
    private GraphicsDevice _device = null!;
    private QuadrantGridRenderer _grid = null!;
    private BasicEffect _effect = null!;
    private StaticModel? _model;
    private Vector3 _modelOffset;

    // Orbit camera state
    private float _camYaw;
    private float _camPitch = -0.4f;
    private float _camDist = 15f;
    private Vector3 _camTarget;

    private const float CamYawSpeed = 2.5f;
    private const float CamPitchSpeed = 1.5f;
    private const float CamZoomSpeed = 15f;
    private const float CamPanSpeed = 10f;
    private const float CamMinDist = 1f;
    private const float CamMaxDist = 2000f;
    private const float CamMinPitch = -1.5f;
    private const float CamMaxPitch = -0.05f;
    private const float Fov = MathHelper.PiOver4;
    private const float NearPlane = 0.1f;
    private const float FarPlane = 10000f;

    private Matrix _view;
    private Matrix _projection;

    public string StatusText { get; private set; } = "No map loaded";

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

        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = false,
            TextureEnabled = true,
            LightingEnabled = true,
            PreferPerPixelLighting = true,
        };

        _effect.EnableDefaultLighting();
        _effect.AmbientLightColor = new Vector3(0.3f, 0.3f, 0.35f);
        _effect.DirectionalLight0.DiffuseColor = new Vector3(0.8f, 0.8f, 0.75f);
        _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.5f, -1f, 0.5f));
        _effect.DirectionalLight0.SpecularColor = new Vector3(0.2f, 0.2f, 0.2f);
    }

    public void LoadMap(string folderPath)
    {
        try
        {
            string daePath = Path.Combine(folderPath, "model.dae");
            if (!File.Exists(daePath))
            {
                string[] daeFiles = Directory.GetFiles(folderPath, "*.dae", SearchOption.TopDirectoryOnly);
                daePath = daeFiles.Length > 0 ? daeFiles[0] : "";
            }
            if (!File.Exists(daePath))
            {
                StatusText = $"No .dae file: {Path.GetFileName(folderPath)}";
                return;
            }

            _model?.Dispose();
            _model = new StaticModel();
            _model.Load(_device, daePath);

            // Offset model so its bottom sits at Y=0 (above the grid)
            _modelOffset = new Vector3(0, -_model.BoundsMin.Y, 0);

            // Reset camera to frame the model
            var offsetCenter = _model.Center + _modelOffset;
            _camTarget = offsetCenter;
            _camDist = Math.Max(5f, _model.Radius * 2.5f);
            _camDist = MathHelper.Clamp(_camDist, CamMinDist, CamMaxDist);
            _camYaw = 0f;
            _camPitch = -0.4f;

            StatusText = $"Loaded: {Path.GetFileName(folderPath)}";
            ModelLoaderLog.Info($"[MapViewer] Loaded map: {folderPath} (bounds: {_model.BoundsMin} - {_model.BoundsMax})");
        }
        catch (Exception ex)
        {
            ModelLoaderLog.Info($"[MapViewer] Failed to load map: {ex.Message}");
            _model?.Dispose();
            _model = null;
            StatusText = $"Failed: {Path.GetFileName(folderPath)}";
        }
    }

    public void Update(GameTime gameTime, InputSnapshot input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Orbit rotation
        if (input.CameraYaw != 0)
            _camYaw += input.CameraYaw * CamYawSpeed * dt;
        if (input.CameraPitch != 0)
            _camPitch = MathHelper.Clamp(_camPitch + input.CameraPitch * CamPitchSpeed * dt, CamMinPitch, CamMaxPitch);
        if (input.CameraZoom != 0)
        {
            float zoomScale = Math.Max(1f, _camDist * 0.2f);
            _camDist = MathHelper.Clamp(_camDist + input.CameraZoom * CamZoomSpeed * zoomScale * dt, CamMinDist, CamMaxDist);
        }

        // Pan with WASD/arrows (negate Z so W moves forward from camera's perspective)
        float panX = input.MoveX;
        float panZ = -input.MoveZ;
        if (panX != 0 || panZ != 0)
        {
            float speed = CamPanSpeed * dt * (_camDist * 0.1f);
            float sinYaw = (float)Math.Sin(_camYaw);
            float cosYaw = (float)Math.Cos(_camYaw);

            _camTarget += new Vector3(
                (cosYaw * panX + sinYaw * panZ) * speed,
                0,
                (-sinYaw * panX + cosYaw * panZ) * speed);
        }

        // Vertical pan
        if (input.MoveY != 0)
            _camTarget.Y += input.MoveY * CamPanSpeed * dt * (_camDist * 0.1f);

        UpdateCamera();
    }

    private void UpdateCamera()
    {
        var lookAt = _camTarget;

        var offset = new Vector3(
            (float)(_camDist * Math.Cos(_camPitch) * Math.Sin(_camYaw)),
            (float)(_camDist * -Math.Sin(_camPitch)),
            (float)(_camDist * Math.Cos(_camPitch) * Math.Cos(_camYaw)));

        var camPosition = lookAt + offset;

        float aspect = _device.Viewport.Width / (float)_device.Viewport.Height;
        _view = Matrix.CreateLookAt(camPosition, lookAt, Vector3.Up);
        _projection = Matrix.CreatePerspectiveFieldOfView(Fov, aspect, NearPlane, FarPlane);
    }

    public void Draw(GraphicsDevice device)
    {
        device.Clear(new Color(25, 30, 45));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        device.BlendState = BlendState.AlphaBlend;
        device.SamplerStates[0] = SamplerState.AnisotropicClamp;

        if (_model is { IsLoaded: true })
        {
            _effect.View = _view;
            _effect.Projection = _projection;
            _effect.World = Matrix.CreateTranslation(_modelOffset);
            _model.Draw(device, _effect);
        }

        _grid.Draw(device, _view, _projection);
    }
}
