#nullable enable
using System;
using Microsoft.Xna.Framework;

namespace Starfield2026.Core.Camera;

// ── Camera Settings ──
// LookAt:        where the camera points at
// StartPosition: close-up on foe at battle start
// EndPosition:   pulled-back final position
// ZoomDuration:  seconds for the zoom-out animation
// ArcHeight:     vertical arc during zoom (0 = straight line)
// FOV:           field of view in degrees

/// <summary>
/// Battle camera. Starts at StartPosition, curves out to EndPosition.
/// </summary>
public class BattleCamera
{
    private const float LookAtX = 0f;
    private const float LookAtY = 2f;
    private const float LookAtZ = -6f;

    private const float StartX = 0f;
    private const float StartY = 7.0f;
    private const float StartZ = 8f;

    private const float EndX = 15f;
    private const float EndY = 7f;
    private const float EndZ = 25f;

    private const float ZoomDuration = 0.5f;
    private const float ZoomPerUnit = 2f;
    private const float ArcHeight = 3f;
    private const float FOVDegrees = 26f;

    // ── State ──
    private Vector3 _position;
    private float _t = 1f;

    private static readonly Vector3 LookAtPos = new(LookAtX, LookAtY, LookAtZ);
    private Vector3 StartPos = new(StartX, StartY, StartZ);
    private static readonly Vector3 EndPos = new(EndX, EndY, EndZ);

    public Vector3 Position => _position;
    public bool IsAnimating => _t < 1f;
    public float FOV => MathHelper.ToRadians(FOVDegrees);

    /// <summary>Reset camera to start position.</summary>
   public void Reset(float foeHeight, float allyHeight)
    {
        float tallest = Math.Max(foeHeight, allyHeight);
        float extraZ = Math.Max(0f, tallest - 3f) * ZoomPerUnit;
        StartPos = new(StartX, StartY, StartZ + extraZ);
        _position =StartPos;
        _t = 1f;
    }

    /// <summary>Begin the zoom-out from start to end position.</summary>
    public void StartZoom()
    {
        _t = 0f;
    }

    /// <summary>
    /// Advance the animation. Returns true on the frame the zoom completes.
    /// </summary>
    public bool Update(float dt)
    {
        if (_t >= 1f)
            return false;

        _t += dt / ZoomDuration;
        if (_t >= 1f)
        {
            _t = 1f;
            _position = EndPos;
            return true;
        }

        // Ease-out
        float ease = 1f - (1f - _t) * (1f - _t);

        // Lerp with vertical arc
        _position = Vector3.Lerp(StartPos, EndPos, ease);
        _position.Y += ArcHeight * MathF.Sin(ease * MathF.PI);

        return false;
    }

    /// <summary>Get the view matrix.</summary>
    public Matrix GetViewMatrix()
    {
        return Matrix.CreateLookAt(_position, LookAtPos, Vector3.Up);
    }
}
