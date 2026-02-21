using Starfield2026.Core.UI;
using Starfield2026.Core.Moves;
#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Starfield2026.Core.Items;
using Starfield2026.Core.Pokemon;
using Starfield2026.Core.Input;
using Starfield2026.Core.Rendering;
using Starfield2026.Core.Screens;

namespace Starfield2026.Core.Screens.Battle;

/// <summary>
/// Manages all 2D battle UI:
///   - MessageBox for battle narration (typewriter text, advance on key)
///   - BattlePanel for battle menus (header + button grid)
///   - Overlay stack for Party/Bag screens
///   - Info bars
/// </summary>
public class BattleUIManager
{
    // Rendering deps
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private PixelFont? _uiFont;

    // Two separate components
    private readonly MessageBox _messageBox = new();
    private readonly BattlePanel _battleMenu = new();

    // Overlays
    private readonly Stack<IScreenOverlay> _overlayStack = new();
    private Party? _party;
    private PlayerInventory? _inventory;

    public bool IsOverlayActive => _overlayStack.Count > 0;
    public bool IsMenuActive => _battleMenu.IsActive;
    public Party? Party => _party;

    public void Initialize(SpriteBatch spriteBatch, Texture2D pixel,
        object? oldFontRendererKilled, object? oldKermFontKilled,
        PixelFont? uiFont = null)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _uiFont = uiFont;
    }

    public void SetPartyAndInventory(Party party, PlayerInventory inventory)
    {
        _party = party;
        _inventory = inventory;
    }

    // ── Menu setup ──

    public void SetMainMenuItems(Action onFight, Action onBag, Action onParty, Action onRun)
    {
        _messageBox.Clear();
        _battleMenu.ShowMenu("What will you do?", 2,
            new MenuItem("Fight", onFight),
            new MenuItem("Bag", onBag),
            new MenuItem("Pokemon", onParty),
            new MenuItem("Run", onRun));
    }

    public void OpenFightMenu(BattlePokemon? allyPokemon, Action closeFightMenu)
    {
        if (allyPokemon == null) return;

        var moves = allyPokemon.Moves;

        // Build simple arrays for the panel — UI widget stays domain-free
        var labels = new string[moves.Length];
        var enabled = new bool[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            var data = MoveRegistry.GetMove(moves[i].MoveId);
            labels[i] = data?.Name ?? $"Move#{moves[i].MoveId}";
            enabled[i] = moves[i].CurrentPP > 0;
        }

        _messageBox.Clear();
        _battleMenu.ShowFightMenu(labels, enabled,
            moveIndex => SelectMove(allyPokemon, moveIndex),
            closeFightMenu);
    }

    public void CloseFightMenu(Action<Action, Action, Action, Action> setupMainMenu)
    {
        // Menu will be restored by the caller via SetMainMenuItems
    }

    public void ReturnToMainMenu(Action<Action, Action, Action, Action> setupMainMenu,
        BattlePokemon? allyPokemon, Action closeFightMenu)
    {
        // Menu will be restored by the caller via SetMainMenuItems
    }

    public void ActivateMenu()
    {
        _battleMenu.IsActive = true;
    }

    public void DeactivateMenu()
    {
        _battleMenu.IsActive = false;
    }

    public void ResetMenuState()
    {
        _battleMenu.Clear();
        _messageBox.Clear();
    }

    // ── Message box (narration) ──

    public void ShowMessage(string text, Action? onFinished = null)
    {
        _battleMenu.Clear(); // hide menu while message plays
        _messageBox.Clear();
        _messageBox.Show(text);
        _messageBox.OnFinished = onFinished;
    }

    public void ClearMessage()
    {
        _messageBox.Clear();
    }

    // ── Overlays ──

    public void OpenBagScreen()
    {
        if (_inventory == null) return;
        PushOverlay(new BagScreen(_inventory, _party));
    }

    public void OpenPartyScreen()
    {
        if (_party == null) return;
        PushOverlay(new PartyScreen(_party, PartyScreenMode.BattleSwitchIn));
    }

    private void PushOverlay(IScreenOverlay overlay)
    {
        _battleMenu.IsActive = false;
        _overlayStack.Push(overlay);
    }

    public void PopOverlay(Action<int> onResult)
    {
        int switchIndex = -1;
        if (_overlayStack.Count > 0)
        {
            var overlay = _overlayStack.Pop();
            if (overlay is PartyScreen ps)
                switchIndex = ps.SelectedSwitchIndex;
        }

        if (_overlayStack.Count == 0)
            onResult(switchIndex);
    }

    public void ClearOverlays()
    {
        _overlayStack.Clear();
    }

    // ── Move selection ──

    public Action<int>? OnMoveSelected { get; set; }

    private void SelectMove(BattlePokemon allyPokemon, int moveIndex)
    {
        var bm = allyPokemon.Moves[moveIndex];
        if (bm.CurrentPP <= 0)
        {
            ShowMessage("No PP left for this move!");
            return;
        }
        bm.CurrentPP--;
        OnMoveSelected?.Invoke(moveIndex);
    }

    // ── Update ──

    public void UpdateOverlay(float dt, InputSnapshot input)
    {
        var overlay = _overlayStack.Peek();
        overlay.Update(dt, input);
    }

    public bool IsTopOverlayFinished()
    {
        return _overlayStack.Count > 0 && _overlayStack.Peek().IsFinished;
    }

    public void UpdateInput(float dt, InputSnapshot input)
    {
        bool anyKey = input.AnyKey;

        if (_battleMenu.IsActive)
        {
            // Menu handles all input
            _battleMenu.Update(input);
        }
        else if (_messageBox.IsActive)
        {
            // Message box handles typewriter + advance
            _messageBox.Update(dt, anyKey);
        }
    }

    // ── Draw ──

    public void DrawUI(int fontScale, int w, int h,
        bool introComplete, bool allySentOut,
        BattlePokemon? allyPokemon, BattlePokemon? foePokemon)
    {
        if (_spriteBatch == null || _pixel == null || _uiFont == null) return;

        int panelH = h / 4;
        int panelY = h - panelH;

        // Info bars
        int infoBarW = w / 3;
        int infoMargin = 20;
        if (introComplete && foePokemon != null)
            BattleInfoBar.DrawFoeBar(_spriteBatch, _pixel, _uiFont!,
                new Rectangle(infoMargin, infoMargin, infoBarW, panelH / 2), foePokemon, fontScale);

        if (allySentOut && allyPokemon != null)
            BattleInfoBar.DrawAllyBar(_spriteBatch, _pixel, _uiFont!,
                new Rectangle(w - infoBarW, panelY - panelH / 2 - 14, infoBarW, panelH / 2 + 10),
                allyPokemon, allyPokemon.EXPPercent, fontScale);

        // Bottom panel area — show whichever is active
        var panelRect = new Rectangle(0, panelY, w, panelH);

        if (_battleMenu.IsActive)
        {
            _battleMenu.Draw(_spriteBatch, _uiFont, _pixel, panelRect, fontScale);
        }
        else if (_messageBox.IsActive)
        {
            _messageBox.Draw(_spriteBatch, _uiFont, _pixel, panelRect, fontScale);
        }

        // Overlay on top
        if (_overlayStack.Count > 0 && _uiFont != null)
        {
            var overlay = _overlayStack.Peek();
            overlay.Draw(_spriteBatch, _pixel, _uiFont, w, h, fontScale);
        }
    }
}
