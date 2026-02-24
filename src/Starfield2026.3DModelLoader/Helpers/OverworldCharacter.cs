#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Starfield2026.ModelLoader.Animations;
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

    private StaticModel? _pokeball;
    private int _handBoneIndex = -1;
    private float _pokeballScale;

    // Ball flight runs independently of character animation
    private enum FlightPhase { None, InHand, FlyingOut, FlyingBack }
    private FlightPhase _flight = FlightPhase.None;
    private Vector3 _ballStartPos;
    private Vector3 _ballLandPos;
    private float _ballFlightElapsed;

    private const float ThrowReleasePointSunMoon = 0.75f;
    private const float ThrowReleasePointDefault = 0.47f;
    private const float ThrowDistanceBase = 4f;
    private const float ThrowDistanceMan = 5.5f;
    private const float ThrowArcHeight = 1.5f;
    private const float OutFlightDuration = 0.35f;
    private const float ReturnFlightDuration = 0.3f;
    private const float ThrowAnimSpeed = 1.125f;   // 10% slower than 1.25
    private const float RecallAnimSpeed = 0.9375f;  // 25% slower than 1.25

    private float _throwDistance = ThrowDistanceBase;
    private float _throwReleasePoint = ThrowReleasePointDefault;
    private bool _isSunMoon;

    private const float TargetHumanHeight = 2.0f;
    private const float SunMoonRefHeight = 170f;
    private const float ScarletRefHeight = 1.2f;
    private const float GroupThreshold = 10f;
    private const float PokeballDiameterSunMoon = 0.15f;
    private const float PokeballDiameterPLZA = 0.17f;
    private const float PokeballDiameterDefault = 0.15f; // Scarlet
    private static readonly string[] HandBoneNames =
    {
        "EffBall",           // Sun-Moon — dedicated ball attachment bone
        "right_attach_on",   // Scarlet — right hand attachment point
        "right_attach",      // PZLA — right hand attachment point
        "RHand",             // Sun-Moon — right hand
        "right_hand",        // Scarlet / PZLA — right hand fallback
    };

    public bool IsLoaded { get; private set; }
    public AnimationSet? AnimationSet => _animSet;

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

        _handBoneIndex = -1;
        foreach (var name in HandBoneNames)
        {
            if (animSet.Skeleton.TryGetBoneIndex(name, out int idx))
            {
                _handBoneIndex = idx;
                break;
            }
        }

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

        // Gen-specific tuning
        _isSunMoon = _fitScale < 0.1f;
        _throwReleasePoint = _isSunMoon ? ThrowReleasePointSunMoon : ThrowReleasePointDefault;

        var bodyType = TrainerGender.Classify(animSet.ModelPath);
        _throwDistance = (bodyType == TrainerGender.BodyType.Man)
            ? ThrowDistanceMan
            : ThrowDistanceBase;

        IsLoaded = true;
    }

    public void LoadPokeball(GraphicsDevice device, string pokeballDaePath)
    {
        _pokeball?.Dispose();
        _pokeball = new StaticModel();
        _pokeball.Load(device, pokeballDaePath);

        float modelDiameter = Math.Max(
            _pokeball.BoundsMax.X - _pokeball.BoundsMin.X,
            Math.Max(_pokeball.BoundsMax.Y - _pokeball.BoundsMin.Y,
                     _pokeball.BoundsMax.Z - _pokeball.BoundsMin.Z));

        // Size pokeball per generation
        string modelPath = _animSet?.ModelPath ?? "";
        bool isPLZA = modelPath.Contains("PZLA", StringComparison.OrdinalIgnoreCase);
        float targetDiameter = _isSunMoon ? PokeballDiameterSunMoon
            : isPLZA ? PokeballDiameterPLZA
            : PokeballDiameterDefault;

        if (modelDiameter > 0.001f && _fitScale > 0.0001f)
            _pokeballScale = targetDiameter / (modelDiameter * _fitScale);
        else
            _pokeballScale = 1f;

        ModelLoaderLog.Info($"[Pokeball] Loaded: diameter={modelDiameter:F3}, fitScale={_fitScale:F6}, pokeballScale={_pokeballScale:F6}, handBone={_handBoneIndex}");
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

        // Update ball flight independently
        UpdateBallFlight(dt, position, rotationY);

        // Alt key triggers
        if (input != null && input.IsKeyJustPressed(Keys.LeftAlt))
        {
            if (_animState == AnimState.Normal && HasClip("BallThrow"))
            {
                _animState = AnimState.ThrowAnim;
                _flight = FlightPhase.InHand;
                Play("BallThrow", loop: false);
                _player.Speed = ThrowAnimSpeed;
                _player.Update(dt);
                return;
            }
            else if (_animState == AnimState.Deployed && HasClip("BallRecall"))
            {
                _animState = AnimState.RecallAnim;
                _flight = FlightPhase.InHand;
                Play("BallRecall", loop: false);
                _player.Speed = RecallAnimSpeed;
                ModelLoaderLog.Info($"[Ball] Recall started — flight={_flight}, handBone={_handBoneIndex}, pokeball={_pokeball != null}");
                _player.Update(dt);
                return;
            }
        }

        if (_animState == AnimState.ThrowAnim)
        {
            // Check for release point
            if (_flight == FlightPhase.InHand && _player.ActiveClip != null)
            {
                float progress = _player.CurrentTime / _player.ActiveClip.Duration;
                if (progress >= _throwReleasePoint)
                {
                    _flight = FlightPhase.FlyingOut;
                    _ballFlightElapsed = 0f;
                    _ballStartPos = GetBallHandPosition(position, rotationY);
                    float fx = (float)Math.Sin(rotationY);
                    float fz = (float)Math.Cos(rotationY);
                    _ballLandPos = position + new Vector3(fx * _throwDistance, 0f, fz * _throwDistance);
                }
            }

            if (_player.IsFinished)
            {
                // Throw anim done → go to Deployed, resume normal locomotion
                _animState = AnimState.Deployed;
                Play("Idle");
            }

            _player.Update(dt);
            return;
        }

        if (_animState == AnimState.RecallAnim)
        {
            _player.Update(dt);
            if (_player.IsFinished)
            {
                _animState = AnimState.Normal;
                _flight = FlightPhase.None;
                Play("Idle");
            }
            return;
        }

        // Normal locomotion (Normal or Deployed — same movement, just tracks whether pokemon is out)
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

    private void UpdateBallFlight(float dt, Vector3 position, float rotationY)
    {
        if (_flight == FlightPhase.FlyingOut)
        {
            _ballFlightElapsed += dt;
            if (_ballFlightElapsed >= OutFlightDuration)
            {
                // Ball landed → fly back to hand
                _flight = FlightPhase.FlyingBack;
                _ballFlightElapsed = 0f;
                _ballStartPos = _ballLandPos;
            }
        }
        else if (_flight == FlightPhase.FlyingBack)
        {
            _ballFlightElapsed += dt;
            if (_ballFlightElapsed >= ReturnFlightDuration)
                _flight = FlightPhase.None;
        }
    }

    private Vector3 GetBallHandPosition(Vector3 characterPos, float rotationY)
    {
        if (_player is null || _model is null || _handBoneIndex < 0)
            return characterPos + Vector3.Up * 1.5f;

        float baseY = _model.BoundsMin.Y * _fitScale;
        var characterWorld = Matrix.CreateScale(_fitScale)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(characterPos.X, characterPos.Y - baseY, characterPos.Z);

        var ballWorld = Matrix.CreateScale(_pokeballScale)
            * _player.WorldPose[_handBoneIndex]
            * characterWorld;
        return ballWorld.Translation;
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float rotationY)
    {
        if (_model is null || _effect is null || _player is null) return;

        _model.UpdatePose(device, _player.SkinPose);

        float baseY = _model.BoundsMin.Y * _fitScale;

        var characterWorld = Matrix.CreateScale(_fitScale)
            * Matrix.CreateRotationY(rotationY)
            * Matrix.CreateTranslation(position.X, position.Y - baseY, position.Z);

        _effect.World = characterWorld;
        _effect.View = view;
        _effect.Projection = projection;

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = SamplerState.LinearWrap;

        _model.Draw(device, _effect);

        if (_pokeball != null && _handBoneIndex >= 0)
            DrawPokeball(device, characterWorld, position, rotationY);
    }

    private void DrawPokeball(GraphicsDevice device, Matrix characterWorld,
        Vector3 position, float rotationY)
    {
        if (_pokeball is null || _effect is null || _player is null) return;

        Matrix ballWorld;
        float worldScale = _pokeballScale * _fitScale;

        switch (_flight)
        {
            case FlightPhase.InHand:
                // Ball attached to hand bone
                ballWorld = Matrix.CreateScale(_pokeballScale)
                    * _player.WorldPose[_handBoneIndex]
                    * characterWorld;
                break;

            case FlightPhase.FlyingOut:
            {
                float t = MathHelper.Clamp(_ballFlightElapsed / OutFlightDuration, 0f, 1f);
                var ballPos = Vector3.Lerp(_ballStartPos, _ballLandPos, t);
                // Parabolic arc: rises then falls
                ballPos.Y += ThrowArcHeight * 4f * t * (1f - t);
                ballWorld = Matrix.CreateScale(worldScale) * Matrix.CreateTranslation(ballPos);
                break;
            }

            case FlightPhase.FlyingBack:
            {
                float t = MathHelper.Clamp(_ballFlightElapsed / ReturnFlightDuration, 0f, 1f);
                var handPos = GetBallHandPosition(position, rotationY);
                var ballPos = Vector3.Lerp(_ballStartPos, handPos, t);
                // Lower arc on return
                ballPos.Y += ThrowArcHeight * 0.3f * 4f * t * (1f - t);
                ballWorld = Matrix.CreateScale(worldScale) * Matrix.CreateTranslation(ballPos);
                break;
            }

            default:
                return;
        }

        _effect.World = ballWorld;
        _pokeball.Draw(device, _effect);
    }

    public void Dispose()
    {
        _model?.Dispose();
        _effect?.Dispose();
        _pokeball?.Dispose();
        _model = null;
        _effect = null;
        _pokeball = null;
        _player = null;
        _animSet = null;
        _activeTag = null;
        _handBoneIndex = -1;
        _animState = AnimState.Normal;
        _flight = FlightPhase.None;
        IsLoaded = false;
    }
}
