#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.Controllers;
using Starfield2026.ModelLoader.Input;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Helpers;

public sealed class OverworldCharacter : IDisposable
{
    private ClipPlayer? _player;
    private AnimationSet? _animSet;
    private SkinnedModel? _model;
    private BasicEffect? _effect;
    private string? _activeTag;
    private float _fitScale = 1f;

    private enum AnimState { Normal, ThrowAnim, Deployed, RecallAnim }
    private AnimState _animState = AnimState.Normal;

    private PokeballController? _pokeballCtrl;

    // Pokemon party
    private PokemonParty? _party;
    private Vector3 _deployPosition;
    private bool _pendingRedeploy;

    private const float ThrowAnimSpeed = 1.125f;
    private const float RecallAnimSpeed = 0.9375f;
    private const float BeamThickness = 0.015f;

    private static readonly Color BeamColor = new Color(255, 50, 50) * 0.4f;
    private VertexPositionColor[]? _beamVerts;
    private static readonly short[] BeamIndices = { 0,1,2, 0,2,3, 4,5,6, 4,6,7 };

    private const float TargetHumanHeight = 2.0f;
    private const float SunMoonRefHeight = 170f;
    private const float ScarletRefHeight = 1.2f;
    private const float GroupThreshold = 10f;

    public bool IsLoaded { get; private set; }
    public AnimationSet? AnimationSet => _animSet;
    public PokemonParty? Party => _party;
    public string PartyStatusText => _party != null
        ? $"[{_party.ActiveIndex + 1}/{_party.SlotCount}: {_party.ActiveDisplayName}]"
        : "";

    public void Load(GraphicsDevice device, AnimationSet animSet)
    {
        Dispose();

        _animSet = animSet;
        _player = new ClipPlayer(animSet.Skeleton);
        _model = new SkinnedModel();
        _model.Load(device, animSet.ModelPath, animSet.Skeleton);

        _effect = new BasicEffect(device)
        {
            LightingEnabled = false,
            VertexColorEnabled = false,
        };

        // Setup pokeball controller
        _pokeballCtrl = new PokeballController();
        _pokeballCtrl.DetectHandBone(animSet.Skeleton);

        Play("Idle");
        _player.Update(0f);
        _model.UpdatePose(device, _player.SkinPose);
        _model.ComputeSkinnedBounds(_player.SkinPose);

        float modelHeight = _model.BoundsMax.Y - _model.BoundsMin.Y;
        if (modelHeight > 0.001f)
        {
            float refHeight = modelHeight > GroupThreshold ? SunMoonRefHeight : ScarletRefHeight;
            _fitScale = TargetHumanHeight / refHeight;
        }

        var bodyType = TrainerGender.Classify(animSet.ModelPath);
        _pokeballCtrl.Configure(_fitScale, bodyType, animSet.ModelPath);

        IsLoaded = true;
    }

    public void LoadPokeball(GraphicsDevice device, string pokeballDaePath)
    {
        _pokeballCtrl?.Load(device, pokeballDaePath, _fitScale, _animSet?.ModelPath ?? "");
    }

    public void LoadParty(GraphicsDevice device, string[] pokemonFolderPaths)
    {
        _party?.Dispose();
        _party = new PokemonParty();
        _party.LoadAll(device, pokemonFolderPaths);
        ModelLoaderLog.Info($"[Party] Loaded {_party.SlotCount} Pokemon for trainer");
    }

    public bool Play(string tag, bool loop = true, bool resetTime = true)
    {
        if (_player is null || _animSet is null) return false;
        if (_activeTag == tag && !resetTime) return true;

        _activeTag = tag;
        var clip = _animSet.GetByTag(tag);
        if (clip is null) return false;

        _player.Play(clip, loop, resetTime);
        return true;
    }

    public bool HasClip(string tag) => _animSet?.HasTag(tag) ?? false;

    public void Update(float dt, bool isMoving, bool isRunning, bool isGrounded,
        InputSnapshot? input = null, Vector3 position = default, float rotationY = 0f)
    {
        if (_player is null) return;

        // Update ball flight — deploy Pokemon the frame the ball lands
        bool ballLanded = _pokeballCtrl?.UpdateFlight(dt, position, rotationY) ?? false;
        if (ballLanded && _animState == AnimState.ThrowAnim)
        {
            _deployPosition = _pokeballCtrl?.LandPosition ?? position;
            _party?.Deploy();
        }

        // Update deployed Pokemon animation
        if (_party is { IsDeployed: true })
            _party.Update(dt);

        // Ctrl cycles Pokemon slot
        if (input != null && _party != null && input.IsKeyJustPressed(Keys.LeftControl))
            _party.CycleNext();

        // Alt key triggers
        if (input != null && input.IsKeyJustPressed(Keys.LeftAlt))
        {
            if (_animState == AnimState.Normal && HasClip("BallThrow"))
            {
                _animState = AnimState.ThrowAnim;
                _pokeballCtrl?.StartThrow();
                _pendingRedeploy = false;
                Play("BallThrow", loop: false);
                _player.Speed = ThrowAnimSpeed;
                _player.Update(dt);
                return;
            }
            else if (_animState == AnimState.Deployed && HasClip("BallRecall"))
            {
                _pendingRedeploy = _party != null && _party.NeedsRecallFirst;

                _animState = AnimState.RecallAnim;
                _party?.StartRecall();
                _pokeballCtrl?.StartThrow();
                Play("BallRecall", loop: false);
                _player.Speed = RecallAnimSpeed;
                _player.Update(dt);
                return;
            }
        }

        if (_animState == AnimState.ThrowAnim)
        {
            // Check for release point
            if (_pokeballCtrl != null && _pokeballCtrl.CheckRelease(_player))
            {
                var charWorld = BuildCharacterWorld(position, rotationY);
                _pokeballCtrl.LaunchBall(position, rotationY, _player, charWorld);
            }

            if (_player.IsFinished)
            {
                _animState = AnimState.Deployed;
                Play("Idle");
            }

            _player.Update(dt);
            return;
        }

        if (_animState == AnimState.RecallAnim)
        {
            // Skip recall animation if player moves or jumps — still recall the Pokemon
            if (isMoving || !isGrounded)
            {
                _party?.Recall();
                _pokeballCtrl?.Reset();

                if (_pendingRedeploy && HasClip("BallThrow"))
                {
                    _pendingRedeploy = false;
                    _animState = AnimState.ThrowAnim;
                    _pokeballCtrl?.StartThrow();
                    Play("BallThrow", loop: false);
                    _player.Speed = ThrowAnimSpeed;
                }
                else
                {
                    _pendingRedeploy = false;
                    _animState = AnimState.Normal;
                    Play(isRunning ? "Run" : "Walk");
                }
                _player.Update(dt);
                return;
            }

            _player.Update(dt);
            if (_player.IsFinished)
            {
                _party?.Recall();
                _pokeballCtrl?.Reset();

                if (_pendingRedeploy && HasClip("BallThrow"))
                {
                    _pendingRedeploy = false;
                    _animState = AnimState.ThrowAnim;
                    _pokeballCtrl?.StartThrow();
                    Play("BallThrow", loop: false);
                    _player.Speed = ThrowAnimSpeed;
                }
                else
                {
                    _pendingRedeploy = false;
                    _animState = AnimState.Normal;
                    Play("Idle");
                }
            }
            return;
        }

        // Normal locomotion (Normal or Deployed — same movement)
        string desiredTag;
        if (!isGrounded && HasClip("Jump"))
            desiredTag = "Jump";
        else if (isRunning && HasClip("Run"))
            desiredTag = "Run";
        else if (isMoving && HasClip("Walk"))
            desiredTag = "Walk";
        else
            desiredTag = "Idle";

        if (_activeTag != desiredTag)
            Play(desiredTag);

        _player.Speed = desiredTag == "Jump" ? 0.5f : 1f;
        _player.Update(dt);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float rotationY)
    {
        if (_model is null || _effect is null || _player is null) return;

        _model.UpdatePose(device, _player.SkinPose);

        var characterWorld = BuildCharacterWorld(position, rotationY);

        _effect.World = characterWorld;
        _effect.View = view;
        _effect.Projection = projection;

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = SamplerState.LinearWrap;

        _model.Draw(device, _effect);

        if (_pokeballCtrl is { HasPokeball: true })
            _pokeballCtrl.Draw(device, _effect, characterWorld, _player, position, rotationY, _fitScale);

        // Draw deployed Pokemon at landing position, facing trainer
        if (_party is { IsDeployed: true })
        {
            float dx = position.X - _deployPosition.X;
            float dz = position.Z - _deployPosition.Z;
            float pokemonYaw = (float)Math.Atan2(dx, dz);
            _party.Draw(device, view, projection, _deployPosition, pokemonYaw);

            // Red beam from Pokemon to pokeball while Pokemon is shrinking
            if (_party.IsRecalling && _party.DeployScale > 0f && _pokeballCtrl != null)
            {
                var ballPos = _pokeballCtrl.GetBallWorldPosition(
                    position, rotationY, _player, characterWorld);
                var pokemonCenter = _deployPosition + Vector3.Up * (0.3f * _party.DeployScale);
                DrawBeam(device, view, projection, pokemonCenter, ballPos);
            }
        }
    }

    private void DrawBeam(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 from, Vector3 to)
    {
        if (_effect is null) return;

        // Build a camera-facing quad strip between from and to
        var dir = to - from;
        float length = dir.Length();
        if (length < 0.001f) return;

        var forward = dir / length;
        var camRight = Vector3.Cross(forward, Vector3.Up);
        if (camRight.LengthSquared() < 0.001f)
            camRight = Vector3.Right;
        else
            camRight.Normalize();
        var camUp = Vector3.Cross(camRight, forward);
        camUp.Normalize();

        float half = BeamThickness * 0.5f;
        var rOff = camRight * half;
        var uOff = camUp * half;

        _beamVerts ??= new VertexPositionColor[8];
        // Horizontal quad
        _beamVerts[0] = new VertexPositionColor(from - rOff, BeamColor);
        _beamVerts[1] = new VertexPositionColor(from + rOff, BeamColor);
        _beamVerts[2] = new VertexPositionColor(to + rOff, BeamColor);
        _beamVerts[3] = new VertexPositionColor(to - rOff, BeamColor);
        // Vertical quad
        _beamVerts[4] = new VertexPositionColor(from - uOff, BeamColor);
        _beamVerts[5] = new VertexPositionColor(from + uOff, BeamColor);
        _beamVerts[6] = new VertexPositionColor(to + uOff, BeamColor);
        _beamVerts[7] = new VertexPositionColor(to - uOff, BeamColor);

        var prevBlend = device.BlendState;
        var prevDepth = device.DepthStencilState;
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;

        _effect.World = Matrix.Identity;
        _effect.View = view;
        _effect.Projection = projection;
        _effect.TextureEnabled = false;
        _effect.VertexColorEnabled = true;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                _beamVerts, 0, 8,
                BeamIndices, 0, 4);
        }

        // Restore
        _effect.VertexColorEnabled = false;
        device.BlendState = prevBlend;
        device.DepthStencilState = prevDepth;
    }

    private Matrix BuildCharacterWorld(Vector3 position, float rotationY)
    {
        float baseY = (_model?.BoundsMin.Y ?? 0f) * _fitScale;
        return Matrix.CreateScale(_fitScale)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(position.X, position.Y - baseY, position.Z);
    }

    public void Dispose()
    {
        _party?.Dispose();
        _pokeballCtrl?.Dispose();
        _model?.Dispose();
        _effect?.Dispose();
        _party = null;
        _pokeballCtrl = null;
        _model = null;
        _effect = null;
        _player = null;
        _animSet = null;
        _activeTag = null;
        _animState = AnimState.Normal;
        _pendingRedeploy = false;
        IsLoaded = false;
    }
}
