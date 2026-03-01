#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Starfield2026.ModelLoader.Helpers;

public sealed class PokemonParty : IDisposable
{
    private readonly PokemonSlot?[] _slots = new PokemonSlot?[6];

    private float _deployScale;
    private bool _isScalingUp;
    private bool _isScalingDown;
    private bool _fastRecall;

    private const float ScaleSpeed = 4f;
    private const float FastScaleSpeed = 12f;

    public int ActiveIndex { get; private set; }
    public int? DeployedIndex { get; private set; }

    public PokemonSlot? ActiveSlot => _slots[ActiveIndex];
    public PokemonSlot? DeployedSlot => DeployedIndex.HasValue ? _slots[DeployedIndex.Value] : null;
    public bool IsDeployed => DeployedIndex.HasValue;
    public bool IsRecalling => _isScalingDown;
    public float DeployScale => _deployScale;
    public float DeployedHeight => DeployedSlot?.RenderedHeight ?? 0f;
    public bool NeedsRecallFirst => DeployedIndex.HasValue && DeployedIndex.Value != ActiveIndex;

    public int SlotCount
    {
        get
        {
            int count = 0;
            foreach (var s in _slots)
                if (s is { IsLoaded: true }) count++;
            return count;
        }
    }

    public string ActiveDisplayName => ActiveSlot is { IsLoaded: true }
        ? ActiveSlot.DisplayName
        : "(empty)";

    public void LoadAll(GraphicsDevice device, string[] folderPaths)
    {
        Dispose();
        for (int i = 0; i < 6 && i < folderPaths.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(folderPaths[i])) continue;
            try
            {
                var slot = new PokemonSlot();
                slot.Load(device, folderPaths[i]);
                _slots[i] = slot;
            }
            catch (Exception ex)
            {
                ModelLoaderLog.Info($"[Party] Failed to load slot {i}: {ex.Message}");
            }
        }

        // Set active to first loaded slot
        ActiveIndex = 0;
        for (int i = 0; i < 6; i++)
        {
            if (_slots[i] is { IsLoaded: true })
            {
                ActiveIndex = i;
                break;
            }
        }
    }

    public void CycleNext()
    {
        int start = ActiveIndex;
        for (int i = 1; i <= 6; i++)
        {
            int idx = (start + i) % 6;
            if (_slots[idx] is { IsLoaded: true })
            {
                ActiveIndex = idx;
                return;
            }
        }
    }

    public void Deploy()
    {
        DeployedIndex = ActiveIndex;
        _deployScale = 0f;
        _isScalingUp = true;
        _isScalingDown = false;
    }

    public void StartRecall()
    {
        _isScalingDown = true;
        _isScalingUp = false;
        _fastRecall = false;
    }

    public void FastRecall()
    {
        _fastRecall = true;
    }

    public void Recall()
    {
        DeployedIndex = null;
        _deployScale = 0f;
        _isScalingDown = false;
        _isScalingUp = false;
        _fastRecall = false;
    }

    public void Update(float dt)
    {
        if (_isScalingUp)
        {
            _deployScale += ScaleSpeed * dt;
            if (_deployScale >= 1f)
            {
                _deployScale = 1f;
                _isScalingUp = false;
            }
        }
        else if (_isScalingDown)
        {
            float speed = _fastRecall ? FastScaleSpeed : ScaleSpeed;
            _deployScale -= speed * dt;
            if (_deployScale <= 0f)
                _deployScale = 0f;
        }

        DeployedSlot?.Update(dt);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix projection,
        Vector3 position, float yaw)
    {
        DeployedSlot?.Draw(device, view, projection, position, yaw, _deployScale);
    }

    public void Dispose()
    {
        for (int i = 0; i < 6; i++)
        {
            _slots[i]?.Dispose();
            _slots[i] = null;
        }
        ActiveIndex = 0;
        DeployedIndex = null;
        _deployScale = 0f;
        _isScalingDown = false;
        _isScalingUp = false;
        _fastRecall = false;
    }
}
