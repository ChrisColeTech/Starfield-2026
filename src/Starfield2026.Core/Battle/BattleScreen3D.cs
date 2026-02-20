#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
// using PokemonGreen.Assets;
using Starfield2026.Core.Input;
using Starfield2026.Core.Items;
using Starfield2026.Core.Pokemon;
using Starfield2026.Core.UI;
using Starfield2026.Core.UI.Fonts;

namespace Starfield2026.Core.Battle;

/// <summary>
/// Thin orchestrator for the 3D battle screen.
/// Delegates to <see cref="BattleCamera"/>, <see cref="BattleSceneRenderer"/>,
/// and <see cref="BattleUIManager"/>.
/// </summary>
public class BattleScreen3D
{
    private readonly BattleCamera _camera = new();
    private readonly BattleSceneRenderer _renderer = new();
    private readonly BattleUIManager _ui = new();

    // Battle state
    private BattlePokemon? _allyPokemon;
    private BattlePokemon? _foePokemon;
    private BattleTurnManager? _turnManager;
    private bool _zoomStarted;
    private bool _introComplete;
    private bool _allySentOut;

    // Switch-in state
    private enum SwitchPhase { None, Recalling, Deploying }
    private SwitchPhase _switchPhase;
    private int _switchPartyIndex = -1;

    public bool InBattle { get; private set; }
    public bool HasLoadedModels => _renderer.HasLoadedModels;
    public SkeletalModelData? AllyModel => _renderer.AllyModel;
    public SkeletalModelData? FoeModel => _renderer.FoeModel;

    /// <summary>Called when the battle ends.</summary>
    public Action? OnBattleExit { get; set; }

    // ── Initialization ──

    public void Initialize(SpriteBatch spriteBatch, Texture2D pixel,
        KermFontRenderer? kermFontRenderer, KermFont? kermFont,
        SpriteFont? fallbackFont = null)
    {
        _ui.Initialize(spriteBatch, pixel, kermFontRenderer, kermFont, fallbackFont);
    }

    public void SetPartyAndInventory(Party party, PlayerInventory inventory)
    {
        _ui.SetPartyAndInventory(party, inventory);
    }

    public void LoadBattleModels(GraphicsDevice device, string basePath)
    {
        _renderer.LoadBattleModels(device, basePath);
    }

    public void SetBattleBackground(BattleBackground bg)
    {
        _renderer.SetBackground(bg);
    }

    public void SetPokemonModels(SkeletalModelData? ally, SkeletalModelData? foe)
    {
        _renderer.SetPokemonModels(ally, foe);
    }

    // ── Battle Entry ──

    public void EnterBattle(BattleBackground bg = BattleBackground.TallGrass)
    {
        // Create real PartyPokemon so EXP/level-up system works
        var allyParty = Pokemon.PartyPokemon.Create(4, 5, Pokemon.Gender.Male);  // Charmander Lv5
        allyParty.MoveIds = new[] { 2, 3, 5, 4 };  // Scratch, Growl, Ember, Leer
        allyParty.MovePPs = new[] { 35, 40, 25, 30 };

        var foeParty = Pokemon.PartyPokemon.Create(16, 3, Pokemon.Gender.Female); // Pidgey Lv3
        foeParty.MoveIds = new[] { 1, 8 };  // Tackle, Sand Attack
        foeParty.MovePPs = new[] { 35, 15 };

        var ally = BattlePokemon.FromParty(allyParty);
        var foe = BattlePokemon.FromParty(foeParty);
        EnterBattle(ally, foe, bg);
    }

    public void EnterBattle(BattlePokemon ally, BattlePokemon foe,
        BattleBackground bg = BattleBackground.TallGrass)
    {
        InBattle = true;
        _allyPokemon = ally;
        _foePokemon = foe;
        _renderer.SetBackground(bg);

        // Turn manager
        _turnManager = new BattleTurnManager(
            _allyPokemon, _foePokemon,
            showMessage: (msg, onDone) => _ui.ShowMessage(msg, onDone),
            hideMenu: () => _ui.DeactivateMenu(),
            returnToMainMenu: () =>
            {
                SetupMenuCallbacks();
                _ui.ReturnToMainMenu(
                    (f, b, p, r) => _ui.SetMainMenuItems(f, b, p, r),
                    _allyPokemon, CloseFightMenu);
            },
            exitBattle: ExitBattle);

        // Wire animation callbacks
        _turnManager.OnAllyAttack = () => _renderer.AllyModel?.PlayIndex(1);
        _turnManager.OnFoeAttack = () => _renderer.FoeModel?.PlayIndex(1);
        _turnManager.OnAllyFaint = () => _renderer.AllyModel?.PlayIndex(2);
        _turnManager.OnFoeFaint = () => _renderer.FoeModel?.PlayIndex(2);
        _turnManager.OnReturnToIdle = () =>
        {
            _renderer.AllyModel?.PlayIndex(0);
            _renderer.FoeModel?.PlayIndex(0);
        };

        // Wire move selection to turn manager
        _ui.OnMoveSelected = moveIndex => _turnManager?.StartTurn(moveIndex);

        // Randomize placeholder cubes for camera testing
        _renderer.RandomizePlaceholderCubes();

        // Camera setup — account for both Pokemon heights
        float foeHeight = _renderer.ComputeFoeHeight();
        float allyHeight = _renderer.ComputeAllyHeight();
        _camera.Reset(foeHeight, allyHeight);

        // Reset state
        _zoomStarted = false;
        _introComplete = false;
        _allySentOut = false;

        SetupMenuCallbacks();
        _ui.ResetMenuState();

        // Deploy foe immediately, ally stays hidden until "Go!"
        _renderer.DeployFoe();

        // Start intro sequence
        _ui.ShowMessage($"Wild {_foePokemon.Nickname.ToUpper()} appeared!", () =>
        {
            _zoomStarted = true;
            _camera.StartZoom();
        });
    }

    private void SetupMenuCallbacks()
    {
        _ui.SetMainMenuItems(
            onFight: OpenFightMenu,
            onBag: () => _ui.OpenBagScreen(),
            onParty: () => _ui.OpenPartyScreen(),
            onRun: TryRun);
    }

    private void OpenFightMenu()
    {
        _ui.OpenFightMenu(_allyPokemon, CloseFightMenu);
    }

    private void CloseFightMenu()
    {
        _ui.CloseFightMenu((f, b, p, r) => _ui.SetMainMenuItems(f, b, p, r));
        SetupMenuCallbacks();
    }

    private void TryRun()
    {
        _ui.DeactivateMenu();
        _ui.ShowMessage("You got away safely!", ExitBattle);
    }

    private void ExitBattle()
    {
        OnBattleExit?.Invoke();
    }

    /// <summary>Called by the transition system after the fade completes.</summary>
    public void CleanupBattle()
    {
        InBattle = false;
        _renderer.ClearPokemonModels();
        _allyPokemon = null;
        _foePokemon = null;
        _turnManager = null;
        _ui.ClearOverlays();
    }

    // ── Update ──

    public void Update(float dt, InputSnapshot input, double totalSeconds)
    {
        // Handle overlay (Bag/Pokemon screens)
        if (_ui.IsOverlayActive)
        {
            _ui.UpdateOverlay(dt, input);
            if (_ui.IsTopOverlayFinished())
                _ui.PopOverlay(ReturnFromOverlay);

            // Still animate camera/models/HP while overlay is up
            _camera.Update(dt);
            _allyPokemon?.UpdateDisplayHP(dt);
            _foePokemon?.UpdateDisplayHP(dt);
            _renderer.UpdateModels(dt, totalSeconds);
            return;
        }

        // Camera animation
        bool zoomJustCompleted = _camera.Update(dt);
        if (zoomJustCompleted && _zoomStarted)
        {
            _zoomStarted = false;
            _introComplete = true;
            _renderer.DeployAlly();
            _ui.ShowMessage($"Go! {_allyPokemon?.Nickname.ToUpper()}!", () =>
            {
                _allySentOut = true;
                _ui.ActivateMenu();
                _ui.ShowMessage("What will you do?");
            });
        }

        // HP bars
        _allyPokemon?.UpdateDisplayHP(dt);
        _foePokemon?.UpdateDisplayHP(dt);

        // Model animations
        _renderer.UpdateModels(dt, totalSeconds);

        // Switch-in state machine
        if (_switchPhase == SwitchPhase.Recalling && _renderer.IsAllyRecalled)
        {
            CompleteSwitchDeploy();
        }
        else if (_switchPhase == SwitchPhase.Deploying && _renderer.IsAllyDeployed)
        {
            _switchPhase = SwitchPhase.None;
            SetupMenuCallbacks();
            _ui.ReturnToMainMenu(
                (f, b, p, r) => _ui.SetMainMenuItems(f, b, p, r),
                _allyPokemon, CloseFightMenu);
        }

        // UI input
        _ui.UpdateInput(dt, input);
    }

    private Party? GetParty() => _ui.Party;

    private void ReturnFromOverlay(int switchIndex)
    {
        if (switchIndex >= 0 && _allyPokemon != null)
        {
            BeginSwitch(switchIndex);
            return;
        }

        SetupMenuCallbacks();
        _ui.ReturnToMainMenu(
            (f, b, p, r) => _ui.SetMainMenuItems(f, b, p, r),
            _allyPokemon, CloseFightMenu);
    }

    private void BeginSwitch(int partyIndex)
    {
        _switchPartyIndex = partyIndex;
        _switchPhase = SwitchPhase.Recalling;

        // Sync current ally back to party
        _allyPokemon?.SyncToParty();

        // Show recall message and start recall animation simultaneously
        _ui.DeactivateMenu();
        _ui.ShowMessage($"Come back, {_allyPokemon?.Nickname.ToUpper()}!");
        _renderer.RecallAlly();
    }

    private void CompleteSwitchDeploy()
    {
        var party = GetParty();
        if (party == null || _switchPartyIndex < 0 || _switchPartyIndex >= party.Count)
        {
            _switchPhase = SwitchPhase.None;
            return;
        }

        // Create new BattlePokemon from the selected party member
        var newAlly = BattlePokemon.FromParty(party[_switchPartyIndex]);
        _allyPokemon = newAlly;

        // Rewire turn manager with new ally
        _turnManager?.SetAlly(newAlly);

        // Show deploy message and start deploy animation simultaneously
        _switchPhase = SwitchPhase.Deploying;
        _ui.ShowMessage($"Go! {newAlly.Nickname.ToUpper()}!");
        _renderer.DeployAlly();
    }

    // ── Drawing ──

    public void Draw3DScene()
    {
        var device = _renderer.Device;
        if (device != null)
            Draw3DScene(device);
    }

    public void Draw3DScene(GraphicsDevice device)
    {
        var view = _camera.GetViewMatrix();
        _renderer.Draw(device, view, _camera.FOV);
    }

    public void DrawUI(int fontScale, int w, int h)
    {
        _ui.DrawUI(fontScale, w, h, _introComplete, _allySentOut, _allyPokemon, _foePokemon);
    }
}
