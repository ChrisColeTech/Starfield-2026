#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.ModelLoader.Animations;
using Starfield2026.ModelLoader.DTOs;
using Starfield2026.ModelLoader.Helpers;
using Starfield2026.ModelLoader.Rendering;

namespace Starfield2026.ModelLoader.Controllers;

public sealed class PokeballController : IDisposable
{
    public enum FlightPhase { None, InHand, FlyingOut, FlyingBack }

    private StaticModel? _pokeball;
    private int _handBoneIndex = -1;
    private float _pokeballScale;

    private FlightPhase _flight = FlightPhase.None;
    private Vector3 _ballStartPos;
    private Vector3 _ballLandPos;
    private float _ballFlightElapsed;

    private float _throwDistance = ThrowDistanceBase;
    private float _throwReleasePoint = ThrowReleasePointDefault;
    private bool _isSunMoon;

    private const float ThrowReleasePointSunMoon = 0.75f;
    private const float ThrowReleasePointDefault = 0.47f;
    private const float ThrowDistanceBase = 4f;
    private const float ThrowDistanceMan = 5.5f;
    private const float ThrowArcHeight = 1.5f;
    private const float OutFlightDuration = 0.35f;
    private const float ReturnFlightDuration = 0.3f;

    private const float PokeballDiameterSunMoon = 0.15f;
    private const float PokeballDiameterPLZA = 0.17f;
    private const float PokeballDiameterDefault = 0.15f;

    private static readonly string[] HandBoneNames =
    {
        "EffBall",           // Sun-Moon — dedicated ball attachment bone
        "right_attach_on",   // Scarlet — right hand attachment point
        "right_attach",      // PZLA — right hand attachment point
        "RHand",             // Sun-Moon — right hand
        "right_hand",        // Scarlet / PZLA — right hand fallback
    };

    public bool HasPokeball => _pokeball != null && _handBoneIndex >= 0;
    public FlightPhase CurrentPhase => _flight;
    public Vector3 LandPosition => _ballLandPos;

    public void DetectHandBone(Skeleton skeleton)
    {
        _handBoneIndex = -1;
        foreach (var name in HandBoneNames)
        {
            if (skeleton.TryGetBoneIndex(name, out int idx))
            {
                _handBoneIndex = idx;
                break;
            }
        }
    }

    public void Configure(float fitScale, TrainerGender.BodyType bodyType, string modelPath)
    {
        _isSunMoon = fitScale < 0.1f;
        _throwReleasePoint = _isSunMoon ? ThrowReleasePointSunMoon : ThrowReleasePointDefault;
        _throwDistance = (bodyType == TrainerGender.BodyType.Man)
            ? ThrowDistanceMan
            : ThrowDistanceBase;
    }

    public void Load(GraphicsDevice device, string pokeballDaePath, float fitScale, string modelPath)
    {
        _pokeball?.Dispose();
        _pokeball = new StaticModel();
        _pokeball.Load(device, pokeballDaePath);

        float modelDiameter = Math.Max(
            _pokeball.BoundsMax.X - _pokeball.BoundsMin.X,
            Math.Max(_pokeball.BoundsMax.Y - _pokeball.BoundsMin.Y,
                     _pokeball.BoundsMax.Z - _pokeball.BoundsMin.Z));

        bool isPLZA = modelPath.Contains("PZLA", StringComparison.OrdinalIgnoreCase);
        float targetDiameter = _isSunMoon ? PokeballDiameterSunMoon
            : isPLZA ? PokeballDiameterPLZA
            : PokeballDiameterDefault;

        if (modelDiameter > 0.001f && fitScale > 0.0001f)
            _pokeballScale = targetDiameter / (modelDiameter * fitScale);
        else
            _pokeballScale = 1f;

        ModelLoaderLog.Info($"[Pokeball] Loaded: diameter={modelDiameter:F3}, fitScale={fitScale:F6}, pokeballScale={_pokeballScale:F6}, handBone={_handBoneIndex}");
    }

    public void StartThrow()
    {
        _flight = FlightPhase.InHand;
    }

    /// <summary>
    /// Check if the throw animation has reached the release point.
    /// Returns true once, triggering the ball launch.
    /// </summary>
    public bool CheckRelease(ClipPlayer player)
    {
        if (_flight != FlightPhase.InHand || player.ActiveClip == null)
            return false;

        float progress = player.CurrentTime / player.ActiveClip.Duration;
        return progress >= _throwReleasePoint;
    }

    public void LaunchBall(Vector3 charPos, float rotationY, ClipPlayer player, Matrix characterWorld)
    {
        _flight = FlightPhase.FlyingOut;
        _ballFlightElapsed = 0f;
        _ballStartPos = GetHandPosition(charPos, rotationY, player, characterWorld);
        float fx = (float)Math.Sin(rotationY);
        float fz = (float)Math.Cos(rotationY);
        _ballLandPos = charPos + new Vector3(fx * _throwDistance, 0f, fz * _throwDistance);
    }

    /// <summary>
    /// Returns true on the frame the ball lands (FlyingOut → FlyingBack transition).
    /// </summary>
    public bool UpdateFlight(float dt, Vector3 charPos, float rotationY)
    {
        bool landed = false;
        if (_flight == FlightPhase.FlyingOut)
        {
            _ballFlightElapsed += dt;
            if (_ballFlightElapsed >= OutFlightDuration)
            {
                _flight = FlightPhase.FlyingBack;
                _ballFlightElapsed = 0f;
                _ballStartPos = _ballLandPos;
                landed = true;
            }
        }
        else if (_flight == FlightPhase.FlyingBack)
        {
            _ballFlightElapsed += dt;
            if (_ballFlightElapsed >= ReturnFlightDuration)
                _flight = FlightPhase.None;
        }
        return landed;
    }

    public void Reset()
    {
        _flight = FlightPhase.None;
    }

    public void Draw(GraphicsDevice device, BasicEffect effect, Matrix characterWorld,
        ClipPlayer player, Vector3 charPos, float rotationY, float fitScale)
    {
        if (_pokeball is null || _handBoneIndex < 0) return;

        Matrix ballWorld;
        float worldScale = _pokeballScale * fitScale;

        switch (_flight)
        {
            case FlightPhase.InHand:
                ballWorld = Matrix.CreateScale(_pokeballScale)
                    * player.WorldPose[_handBoneIndex]
                    * characterWorld;
                break;

            case FlightPhase.FlyingOut:
            {
                float t = MathHelper.Clamp(_ballFlightElapsed / OutFlightDuration, 0f, 1f);
                var ballPos = Vector3.Lerp(_ballStartPos, _ballLandPos, t);
                ballPos.Y += ThrowArcHeight * 4f * t * (1f - t);
                ballWorld = Matrix.CreateScale(worldScale) * Matrix.CreateTranslation(ballPos);
                break;
            }

            case FlightPhase.FlyingBack:
            {
                float t = MathHelper.Clamp(_ballFlightElapsed / ReturnFlightDuration, 0f, 1f);
                var handPos = GetHandPosition(charPos, rotationY, player, characterWorld);
                var ballPos = Vector3.Lerp(_ballStartPos, handPos, t);
                ballPos.Y += ThrowArcHeight * 0.3f * 4f * t * (1f - t);
                ballWorld = Matrix.CreateScale(worldScale) * Matrix.CreateTranslation(ballPos);
                break;
            }

            default:
                return;
        }

        effect.World = ballWorld;
        _pokeball.Draw(device, effect);
    }

    public Vector3 GetBallWorldPosition(Vector3 charPos, float rotationY,
        ClipPlayer player, Matrix characterWorld)
    {
        switch (_flight)
        {
            case FlightPhase.InHand:
                return GetHandPosition(charPos, rotationY, player, characterWorld);
            case FlightPhase.FlyingOut:
            {
                float t = MathHelper.Clamp(_ballFlightElapsed / OutFlightDuration, 0f, 1f);
                var pos = Vector3.Lerp(_ballStartPos, _ballLandPos, t);
                pos.Y += ThrowArcHeight * 4f * t * (1f - t);
                return pos;
            }
            case FlightPhase.FlyingBack:
            {
                float t = MathHelper.Clamp(_ballFlightElapsed / ReturnFlightDuration, 0f, 1f);
                var handPos = GetHandPosition(charPos, rotationY, player, characterWorld);
                var pos = Vector3.Lerp(_ballStartPos, handPos, t);
                pos.Y += ThrowArcHeight * 0.3f * 4f * t * (1f - t);
                return pos;
            }
            default:
                return GetHandPosition(charPos, rotationY, player, characterWorld);
        }
    }

    private Vector3 GetHandPosition(Vector3 charPos, float rotationY,
        ClipPlayer player, Matrix characterWorld)
    {
        if (_handBoneIndex < 0)
            return charPos + Vector3.Up * 1.5f;

        var ballWorld = Matrix.CreateScale(_pokeballScale)
            * player.WorldPose[_handBoneIndex]
            * characterWorld;
        return ballWorld.Translation;
    }

    public void Dispose()
    {
        _pokeball?.Dispose();
        _pokeball = null;
        _handBoneIndex = -1;
        _flight = FlightPhase.None;
    }
}
