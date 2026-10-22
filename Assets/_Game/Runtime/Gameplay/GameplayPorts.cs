using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace StickyPeerSort
{
    public interface IStickerInput { void Tap(int index); void TapAt(Vector2 boardPoint); }
    public interface IGameplayReadModel
    {
        GameSession Session { get; }
        SaveData Progress { get; }
        GameBalanceConfig Balance { get; }
    }
    public interface IGameplayCommands : IStickerInput
    {
        void Undo(); void Extra(); void Hint(); void Shuffle(); void ConfirmRestart(); void Pause();
    }
    public interface ILevelRepository { int Count { get; } LevelDefinition Get(int index); }
    public interface IGameSessionFactory { GameSession Create(LevelDefinition level); }
    public interface IPuzzleSolver { SolveResult Solve(GameSession session, int budget, out List<int> trace); }
    public interface IGameplayScreen
    {
        bool HasModal { get; }
        void Render(); void SynchronizeAfterMoves(); void HideModal();
        void RejectSticker(int index); void HighlightSticker(int index); void SetInstruction(string message);
        void ShowModal(string title, string body, string primary, Action primaryAction, string secondary, Action secondaryAction);
    }
    public sealed class MovePresentation
    {
        public readonly GameSession Snapshot;
        public readonly IReadOnlyList<PuzzleEvent> Events;
        public readonly bool BlocksInput;
        public MovePresentation(GameSession resolved)
        {
            Snapshot = resolved.Copy(); Events = new List<PuzzleEvent>(resolved.Events).AsReadOnly();
            BlocksInput = resolved.Events.Exists(e => e.kind == PuzzleEventKind.CategoryCompleted || e.kind == PuzzleEventKind.Unlocked || e.kind == PuzzleEventKind.Revealed) || resolved.State == GameplayState.Win || resolved.State == GameplayState.Fail;
        }
    }
    public interface IMovePresenter
    {
        bool IsBusy { get; } bool BlocksInput { get; } PuzzleEventKind? Phase { get; }
        event Action<PuzzleEvent> EventFinished;
        event Action Drained;
        void Enqueue(MovePresentation move); void Reset();
    }
    public interface ICoroutineRunner { void Run(IEnumerator routine); void CancelAll(); }
    public sealed class GameplayContext : IGameplayReadModel
    {
        public GameSession Session { get; internal set; }
        public SaveData Progress { get; }
        public GameBalanceConfig Balance { get; set; }
        public GameplayContext(SaveData progress, GameBalanceConfig balance) { Progress = progress; Balance = balance; }
    }
    public sealed class LevelRepository : ILevelRepository
    {
        readonly LevelDefinition[] levels;
        public int Count => levels.Length;
        public LevelRepository(LevelDefinition[] levels) { this.levels = levels ?? throw new ArgumentNullException(nameof(levels)); if (levels.Length == 0) throw new ArgumentException("At least one level is required."); }
        public LevelDefinition Get(int index) { return levels[Mathf.Clamp(index, 0, levels.Length - 1)]; }
    }
    public sealed class GameSessionFactory : IGameSessionFactory { public GameSession Create(LevelDefinition level) { return new GameSession(level); } }
    public sealed class BoundedPuzzleSolver : IPuzzleSolver
    { public SolveResult Solve(GameSession session, int budget, out List<int> trace) { return PuzzleSolver.Solve(session, budget, out trace); } }
}
