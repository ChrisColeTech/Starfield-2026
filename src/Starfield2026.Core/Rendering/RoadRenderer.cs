using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.Core.Rendering;

/// <summary>
/// Renders a scrolling road with lane markings, edge lines, and rumble-strip shoulders.
/// The road is built from triangle strips (surface) and line primitives (markings).
/// Use ScrollOffset to create the illusion of forward motion.
/// </summary>
public class RoadRenderer
{
    private BasicEffect _effect = null!;

    // Road surface geometry
    private VertexPositionColor[] _roadVerts = null!;
    private short[] _roadIndices = null!;
    private int _roadTriCount;

    // Lane markings (dashed center, solid edges)
    private VertexPositionColor[] _lineVerts = null!;
    private int _lineCount;

    // Shoulder stripes
    private VertexPositionColor[] _shoulderVerts = null!;
    private short[] _shoulderIndices = null!;
    private int _shoulderTriCount;

    /// <summary>Total road width (both directions combined).</summary>
    public float RoadWidth { get; set; } = 20f;

    /// <summary>Number of lanes in each direction.</summary>
    public int LanesPerSide { get; set; } = 2;

    /// <summary>Length of each lane dash.</summary>
    public float DashLength { get; set; } = 3f;

    /// <summary>Gap between dashes.</summary>
    public float DashGap { get; set; } = 2f;

    /// <summary>Width of shoulder rumble strips on each side.</summary>
    public float ShoulderWidth { get; set; } = 3f;

    /// <summary>How far ahead/behind the road extends from center.</summary>
    public float RoadExtent { get; set; } = 300f;

    /// <summary>Scroll offset — set from car Z position using modulo for infinite scrolling.</summary>
    public float ScrollOffset { get; set; }

    /// <summary>Road surface Y position.</summary>
    public float SurfaceY { get; set; } = 0f;

    public void Initialize(GraphicsDevice device)
    {
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        BuildRoadSurface();
        BuildShoulders();
    }

    private void BuildRoadSurface()
    {
        float halfW = RoadWidth / 2f;

        // Simple quad for the road surface
        _roadVerts = new VertexPositionColor[4];
        var roadColor = new Color(45, 45, 55); // dark asphalt

        _roadVerts[0] = new VertexPositionColor(new Vector3(-halfW, SurfaceY + 0.01f, -RoadExtent), roadColor);
        _roadVerts[1] = new VertexPositionColor(new Vector3(halfW, SurfaceY + 0.01f, -RoadExtent), roadColor);
        _roadVerts[2] = new VertexPositionColor(new Vector3(-halfW, SurfaceY + 0.01f, RoadExtent), roadColor);
        _roadVerts[3] = new VertexPositionColor(new Vector3(halfW, SurfaceY + 0.01f, RoadExtent), roadColor);

        _roadIndices = new short[] { 0, 1, 2, 1, 3, 2 };
        _roadTriCount = 2;
    }

    private void BuildShoulders()
    {
        float halfW = RoadWidth / 2f;
        float stripeLen = 4f;
        int stripeCount = (int)(RoadExtent * 2f / stripeLen);
        var stripeVerts = new VertexPositionColor[stripeCount * 2 * 4]; // 2 sides, 4 verts each
        var stripeIdx = new short[stripeCount * 2 * 6]; // 2 tris each

        var color1 = new Color(180, 30, 30); // red
        var color2 = new Color(220, 220, 220); // white

        int vi = 0, ii = 0;
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1f : 1f;
            float innerX = sign * halfW;
            float outerX = sign * (halfW + ShoulderWidth);

            for (int s = 0; s < stripeCount; s++)
            {
                float z0 = -RoadExtent + s * stripeLen;
                float z1 = z0 + stripeLen;
                var c = (s % 2 == 0) ? color1 : color2;

                short baseV = (short)vi;
                stripeVerts[vi++] = new VertexPositionColor(new Vector3(innerX, SurfaceY + 0.005f, z0), c);
                stripeVerts[vi++] = new VertexPositionColor(new Vector3(outerX, SurfaceY + 0.005f, z0), c);
                stripeVerts[vi++] = new VertexPositionColor(new Vector3(innerX, SurfaceY + 0.005f, z1), c);
                stripeVerts[vi++] = new VertexPositionColor(new Vector3(outerX, SurfaceY + 0.005f, z1), c);

                stripeIdx[ii++] = baseV;
                stripeIdx[ii++] = (short)(baseV + 1);
                stripeIdx[ii++] = (short)(baseV + 2);
                stripeIdx[ii++] = (short)(baseV + 1);
                stripeIdx[ii++] = (short)(baseV + 3);
                stripeIdx[ii++] = (short)(baseV + 2);
            }
        }

        _shoulderVerts = stripeVerts;
        _shoulderIndices = stripeIdx;
        _shoulderTriCount = stripeCount * 2 * 2;
    }

    private void BuildLaneLines(float scrollFrac)
    {
        float halfW = RoadWidth / 2f;
        float laneWidth = RoadWidth / (LanesPerSide * 2);
        var edgeColor = new Color(255, 255, 255, 200);
        var dashColor = new Color(255, 255, 200, 180);

        var lines = new System.Collections.Generic.List<VertexPositionColor>();
        float y = SurfaceY + 0.02f;

        // Solid edge lines (left and right edge of road)
        lines.Add(new VertexPositionColor(new Vector3(-halfW, y, -RoadExtent), edgeColor));
        lines.Add(new VertexPositionColor(new Vector3(-halfW, y, RoadExtent), edgeColor));
        lines.Add(new VertexPositionColor(new Vector3(halfW, y, -RoadExtent), edgeColor));
        lines.Add(new VertexPositionColor(new Vector3(halfW, y, RoadExtent), edgeColor));

        // Center divider (solid double yellow)
        var yellowColor = new Color(255, 200, 0, 200);
        lines.Add(new VertexPositionColor(new Vector3(-0.15f, y, -RoadExtent), yellowColor));
        lines.Add(new VertexPositionColor(new Vector3(-0.15f, y, RoadExtent), yellowColor));
        lines.Add(new VertexPositionColor(new Vector3(0.15f, y, -RoadExtent), yellowColor));
        lines.Add(new VertexPositionColor(new Vector3(0.15f, y, RoadExtent), yellowColor));

        // Dashed lane lines
        float dashCycle = DashLength + DashGap;
        float dashOffset = scrollFrac % dashCycle;

        for (int lane = 1; lane < LanesPerSide; lane++)
        {
            float x1 = -halfW + lane * laneWidth;
            float x2 = halfW - lane * laneWidth;

            for (float z = -RoadExtent - dashOffset; z < RoadExtent; z += dashCycle)
            {
                float z0 = z;
                float z1 = z + DashLength;
                if (z0 > RoadExtent || z1 < -RoadExtent) continue;
                z0 = Math.Max(z0, -RoadExtent);
                z1 = Math.Min(z1, RoadExtent);

                lines.Add(new VertexPositionColor(new Vector3(x1, y, z0), dashColor));
                lines.Add(new VertexPositionColor(new Vector3(x1, y, z1), dashColor));
                lines.Add(new VertexPositionColor(new Vector3(x2, y, z0), dashColor));
                lines.Add(new VertexPositionColor(new Vector3(x2, y, z1), dashColor));
            }
        }

        _lineVerts = lines.ToArray();
        _lineCount = _lineVerts.Length / 2;
    }

    /// <summary>Draw the road at the current scroll offset.</summary>
    public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
    {
        // Compute scroll fraction for shoulder stripe tiling
        float shoulderCycle = 4f; // stripe length
        float scrollFrac = ScrollOffset % shoulderCycle;
        var scrollTranslation = new Vector3(0, 0, scrollFrac);

        // Rebuild dashed lines each frame (cheap — just line verts)
        BuildLaneLines(ScrollOffset);

        // --- Road surface ---
        _effect.World = Matrix.CreateTranslation(scrollTranslation);
        _effect.View = view;
        _effect.Projection = projection;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                _roadVerts, 0, _roadVerts.Length,
                _roadIndices, 0, _roadTriCount);
        }

        // --- Shoulders ---
        if (_shoulderVerts != null && _shoulderVerts.Length > 0)
        {
            _effect.World = Matrix.CreateTranslation(scrollTranslation);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _shoulderVerts, 0, _shoulderVerts.Length,
                    _shoulderIndices, 0, _shoulderTriCount);
            }
        }

        // --- Lane markings ---
        if (_lineVerts != null && _lineCount > 0)
        {
            _effect.World = Matrix.CreateTranslation(scrollTranslation);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList, _lineVerts, 0, _lineCount);
            }
        }
    }
}
