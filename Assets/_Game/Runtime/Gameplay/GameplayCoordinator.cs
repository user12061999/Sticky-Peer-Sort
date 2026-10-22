using System;
using UnityEngine;
namespace StickyPeerSort
{
    // Application flow. Dependencies are ports; this class does not know about Unity UI or coroutines.
    public sealed class GameplayCoordinator : IGameplayCommands, IDisposable
    {
        readonly GameplayContext context;
        readonly ILevelRepository levels;
        readonly IGameSessionFactory sessions;
        readonly IProgressStore progress;
        readonly IGameplayBoosters boosters;
        readonly IMovePresenter presenter;
        readonly IGameplayScreen screen;
        int levelIndex;
        GameSession Session => context.Session;
        public bool CanTap => Session != null && !presenter.BlocksInput && !screen.HasModal && Session.State == GameplayState.Playing;
        bool Available => CanTap && !presenter.IsBusy;
        public GameplayCoordinator(GameplayContext context, ILevelRepository levels, IGameSessionFactory sessions,
            IProgressStore progress, IGameplayBoosters boosters, IMovePresenter presenter, IGameplayScreen screen)
        {
            this.context = context; this.levels = levels; this.sessions = sessions; this.progress = progress;
            this.boosters = boosters; this.presenter = presenter; this.screen = screen;
            presenter.Drained += OnPresentationDrained;
        }
        public void LoadLevel(int index)
        {
            presenter.Reset(); levelIndex = Mathf.Clamp(index, 0, levels.Count - 1);
            context.Session = sessions.Create(levels.Get(levelIndex)); screen.HideModal(); screen.Render();
        }
        public void Tap(int index)
        {
            if (!CanTap) return;
            if (!Session.Select(index)) { screen.RejectSticker(index); return; }
            presenter.Enqueue(new MovePresentation(Session));
        }
        public void TapAt(Vector2 point) { if (CanTap) Tap(BoardGeometry.Hit(Session, point)); }
        public void Undo() { if (!presenter.IsBusy && boosters.Undo(Session, context.Balance)) { screen.HideModal(); screen.Render(); } }
        public void Extra() { if (!presenter.IsBusy && boosters.Extra(Session, context.Balance)) screen.Render(); }
        public void Hint()
        {
            if (!Available) return; int index;
            if (boosters.Hint(Session, context.Balance, out index)) screen.HighlightSticker(index);
            else screen.SetInstruction("No verified path found. Undo or restart to try another order.");
        }
        public void Shuffle()
        {
            if (!Available) return; GameSession shuffled;
            if (boosters.Shuffle(Session, context.Balance, out shuffled)) { context.Session = shuffled; screen.Render(); }
            else screen.SetInstruction("No safe shuffle found. Try a hint.");
        }
        void OnPresentationDrained()
        {
            screen.SynchronizeAfterMoves();
            if (Session.State == GameplayState.Win)
            {
                bool reward = progress.Complete(Session.Level); screen.SynchronizeAfterMoves();
                screen.ShowModal("COLLECTION COMPLETE!", reward ? "+" + Session.Level.coinReward + " coins. Album updated." : "Great sorting! Collection already in your album.",
                    levelIndex < levels.Count - 1 ? "NEXT LEVEL" : "PLAY AGAIN", () => LoadLevel(levelIndex < levels.Count - 1 ? levelIndex + 1 : 0), "RETRY", () => LoadLevel(levelIndex));
            }
            else if (Session.State == GameplayState.Fail)
                screen.ShowModal("TRAY OVERFLOW!", Session.Buffer.Count + " stickers need waiting space, but the tray has only " + Session.Capacity + " slots.\nUndo the last move or retry.", "RETRY", () => LoadLevel(levelIndex), "UNDO", UndoFromFail);
        }
        void UndoFromFail()
        {
            screen.HideModal(); Undo();
            if (Session.State == GameplayState.Fail) screen.ShowModal("NO UNDO LEFT", "Restart for a fresh tray.", "RETRY", () => LoadLevel(levelIndex), "+ SLOT", ExtraFromFail);
        }
        void ExtraFromFail()
        {
            if (!context.Balance.boostersEnabled || progress.Data.extraSlot <= 0)
            { screen.ShowModal("NO EXTRA SLOTS LEFT", "Restart to try another order.", "RETRY", () => LoadLevel(levelIndex), "UNDO", UndoFromFail); return; }
            screen.HideModal(); Extra();
        }
        public void ConfirmRestart()
        {
            if (!Available) return; Session.Pause(true);
            screen.ShowModal("RESTART LEVEL?", "Your current board will be reset.", "RESTART", () => LoadLevel(levelIndex), "KEEP PLAYING", Resume);
        }
        public void Pause()
        {
            if (!Available) return; Session.Pause(true);
            screen.ShowModal("PAUSED", "Album: " + progress.Data.highestCompleted + " / " + levels.Count + " levels.\nUnlocked level: " + progress.Data.unlockedLevel,
                "RESUME", Resume, "CONTINUE SAVED", () => LoadLevel(Mathf.Min(progress.Data.unlockedLevel - 1, levels.Count - 1)));
        }
        void Resume() { screen.HideModal(); Session.Pause(false); }
        public void Suspend(bool paused) { progress.Flush(); if (paused && Available) Pause(); }
        public void Dispose() { presenter.Drained -= OnPresentationDrained; presenter.Reset(); }
    }
}
