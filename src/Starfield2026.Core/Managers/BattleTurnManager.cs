using Starfield2026.Core.Moves;
using System;
using System.Collections.Generic;
using Starfield2026.Core.Data;
using Starfield2026.Core.Pokemon;

namespace Starfield2026.Core.Managers;

/// <summary>
/// Simple state machine managing battle turns.
/// Queues messages and HP changes, then advances through phases.
/// </summary>
public class BattleTurnManager
{
    public enum TurnPhase
    {
        Idle,           // Waiting for player to pick Fight
        PlayerAttack,   // Showing player's attack message, applying damage
        FoeAttack,      // Showing foe's attack message, applying damage
        EXPReward,      // Awarding EXP and handling level-ups
        TurnEnd,        // Check faint, decide next turn or battle over
        BattleOver      // Victory/defeat message shown
    }

    private BattlePokemon _ally;
    private readonly BattlePokemon _foe;
    private readonly Action<string, Action?> _showMessage;
    private readonly Action _returnToMainMenu;
    private readonly Action _exitBattle;
    private readonly Action _hideMenu;

    public TurnPhase Phase { get; private set; } = TurnPhase.Idle;

    // Animation callbacks — wired by Game1 to trigger clip switches
    public Action? OnAllyAttack { get; set; }
    public Action? OnFoeAttack { get; set; }
    public Action? OnAllyFaint { get; set; }
    public Action? OnFoeFaint { get; set; }
    public Action? OnReturnToIdle { get; set; }

    private int _playerMoveIndex;
    private readonly Random _rng = new();

    public BattleTurnManager(
        BattlePokemon ally,
        BattlePokemon foe,
        Action<string, Action?> showMessage,
        Action hideMenu,
        Action returnToMainMenu,
        Action exitBattle)
    {
        _ally = ally;
        _foe = foe;
        _showMessage = showMessage;
        _hideMenu = hideMenu;
        _returnToMainMenu = returnToMainMenu;
        _exitBattle = exitBattle;
    }

    /// <summary>Replace the active ally (used for mid-battle switch-in).</summary>
    public void SetAlly(BattlePokemon newAlly)
    {
        _ally = newAlly;
        Phase = TurnPhase.Idle;
    }

    /// <summary>Start a turn after the player selects a move.</summary>
    public void StartTurn(int moveIndex)
    {
        _playerMoveIndex = moveIndex;
        _hideMenu();
        Phase = TurnPhase.PlayerAttack;
        OnAllyAttack?.Invoke();
        ExecutePlayerAttack();
    }

    private void ExecutePlayerAttack()
    {
        var bm = _ally.Moves[_playerMoveIndex];
        var moveData = MoveRegistry.GetMove(bm.MoveId);
        string moveName = moveData?.Name ?? "???";

        if (moveData != null && moveData.Power > 0)
        {
            int damage = CalculateDamage(moveData, _ally.Level);
            _foe.ApplyDamage(damage);
            _showMessage($"{_ally.Nickname} used {moveName}!", () => AfterPlayerAttack());
        }
        else
        {
            // Status move — no damage
            _showMessage($"{_ally.Nickname} used {moveName}!", () => AfterPlayerAttack());
        }
    }

    private void AfterPlayerAttack()
    {
        if (_foe.IsFainted)
        {
            OnFoeFaint?.Invoke();
            _showMessage($"Wild {_foe.Nickname} fainted!", () => AwardEXP());
            return;
        }

        Phase = TurnPhase.FoeAttack;
        OnFoeAttack?.Invoke();
        ExecuteFoeAttack();
    }

    private void AwardEXP()
    {
        Phase = TurnPhase.EXPReward;

        // Look up foe species for base EXP yield
        var foeSpecies = SpeciesRegistry.GetSpecies(_foe.SpeciesId);
        int baseYield = foeSpecies?.BaseEXPYield ?? 50;

        uint expGain = GrowthRateHelper.CalculateEXPGain(
            baseYield, _foe.Level, _ally.Level,
            isTrainerBattle: false, participantCount: 1);

        _showMessage($"{_ally.Nickname} gained {expGain} EXP. Points!", () =>
        {
            if (_ally.Source == null)
            {
                ShowVictory();
                return;
            }

            var result = _ally.Source.AddEXP(expGain);

            // Sync updated level/stats back to BattlePokemon
            _ally.Level = _ally.Source.Level;
            _ally.MaxHP = _ally.Source.MaxHP;
            _ally.CurrentHP = _ally.Source.CurrentHP;

            if (result.LevelsGained > 0)
                ShowLevelUp(result);
            else
                ShowVictory();
        });
    }

    private void ShowLevelUp(LevelUpResult result)
    {
        _showMessage($"{_ally.Nickname} grew to Lv. {_ally.Level}!", () =>
        {
            ShowNewMoves(result, 0);
        });
    }

    private void ShowNewMoves(LevelUpResult result, int index)
    {
        if (index >= result.NewMoveIds.Count)
        {
            // Done showing moves, check evolution
            if (result.PendingEvolution != null)
                ShowEvolution(result.PendingEvolution);
            else
                ShowVictory();
            return;
        }

        int moveId = result.NewMoveIds[index];
        var moveData = MoveRegistry.GetMove(moveId);
        string moveName = moveData?.Name ?? "???";

        _showMessage($"{_ally.Nickname} learned {moveName}!", () =>
        {
            ShowNewMoves(result, index + 1);
        });
    }

    private void ShowEvolution(Data.EvolutionData evo)
    {
        string oldName = _ally.Nickname;
        var newSpecies = SpeciesRegistry.GetSpecies(evo.ToSpeciesId);
        string newName = newSpecies?.Name ?? "???";

        // Perform the evolution
        _ally.Source!.Evolve(evo.ToSpeciesId);

        // Sync to BattlePokemon
        _ally.Level = _ally.Source.Level;
        _ally.MaxHP = _ally.Source.MaxHP;
        _ally.CurrentHP = _ally.Source.CurrentHP;

        _showMessage($"{oldName} evolved into {newName}!", () => ShowVictory());
    }

    private void ShowVictory()
    {
        Phase = TurnPhase.BattleOver;
        _showMessage("You win!", () => _exitBattle());
    }

    private void ExecuteFoeAttack()
    {
        // Pick a random foe move that has PP
        var usable = new List<int>();
        for (int i = 0; i < _foe.Moves.Length; i++)
        {
            if (_foe.Moves[i].CurrentPP > 0)
                usable.Add(i);
        }

        if (usable.Count == 0)
        {
            // Foe has no moves with PP — struggle equivalent
            _showMessage($"Wild {_foe.Nickname} has no moves left!", () => AfterFoeAttack());
            return;
        }

        int foeIdx = usable[_rng.Next(usable.Count)];
        var foeBm = _foe.Moves[foeIdx];
        foeBm.CurrentPP--;
        var foeMove = MoveRegistry.GetMove(foeBm.MoveId);
        string foeName = foeMove?.Name ?? "???";

        if (foeMove != null && foeMove.Power > 0)
        {
            int damage = CalculateDamage(foeMove, _foe.Level);
            _ally.ApplyDamage(damage);
            _showMessage($"Wild {_foe.Nickname} used {foeName}!", () => AfterFoeAttack());
        }
        else
        {
            _showMessage($"Wild {_foe.Nickname} used {foeName}!", () => AfterFoeAttack());
        }
    }

    private void AfterFoeAttack()
    {
        if (_ally.IsFainted)
        {
            Phase = TurnPhase.BattleOver;
            OnAllyFaint?.Invoke();
            _showMessage($"{_ally.Nickname} fainted!", () =>
            {
                _showMessage("You blacked out!", () => _exitBattle());
            });
            return;
        }

        Phase = TurnPhase.Idle;
        OnReturnToIdle?.Invoke();
        _returnToMainMenu();
    }

    /// <summary>Simplified damage formula.</summary>
    private int CalculateDamage(MoveData move, int level)
    {
        int damage = (move.Power * level / 5 + 2) / 3;
        // Add a small random factor (+/- 15%)
        float roll = 0.85f + (float)_rng.NextDouble() * 0.15f;
        damage = (int)(damage * roll);
        return Math.Max(1, damage);
    }
}
