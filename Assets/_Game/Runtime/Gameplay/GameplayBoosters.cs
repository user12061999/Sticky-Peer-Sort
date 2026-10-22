using System.Collections.Generic;
namespace StickyPeerSort
{
    // Inventory and booster rules have no dependency on a Canvas, animation, or scene controller.
    public interface IGameplayBoosters
    {
        bool Undo(GameSession session, GameBalanceConfig balance);
        bool Extra(GameSession session, GameBalanceConfig balance);
        bool Hint(GameSession session, GameBalanceConfig balance, out int index);
        bool Shuffle(GameSession session, GameBalanceConfig balance, out GameSession shuffled);
    }
    public sealed class GameplayBoosters : IGameplayBoosters
    {
        readonly IProgressStore progress;
        readonly IPuzzleSolver solver;
        public GameplayBoosters(IProgressStore progress, IPuzzleSolver solver) { this.progress = progress; this.solver = solver; }
        public bool Undo(GameSession session, GameBalanceConfig balance)
        {
            if (!balance.boostersEnabled || progress.Data.undo <= 0 || !session.Undo()) return false;
            progress.Data.undo--; progress.Flush(); return true;
        }
        public bool Extra(GameSession session, GameBalanceConfig balance)
        {
            if (!balance.boostersEnabled || progress.Data.extraSlot <= 0 || !session.AddSlot()) return false;
            progress.Data.extraSlot--; progress.Flush(); return true;
        }
        public bool Hint(GameSession session, GameBalanceConfig balance, out int index)
        {
            index = -1; if (!balance.boostersEnabled || progress.Data.hint <= 0) return false;
            List<int> trace;
            if (solver.Solve(session, 2000, out trace) != SolveResult.Solved || trace.Count == 0 || !session.Legal(trace[0])) return false;
            index = trace[0]; progress.Data.hint--; progress.Flush(); return true;
        }
        public bool Shuffle(GameSession session, GameBalanceConfig balance, out GameSession shuffled)
        {
            shuffled = session; if (!balance.boostersEnabled || progress.Data.shuffle <= 0) return false;
            var candidate = session.Copy(); candidate.Shuffle(37 + session.Stickers.FindAll(s => s.location == StickerLocation.Collected).Count);
            List<int> trace;
            if (solver.Solve(candidate, 2000, out trace) != SolveResult.Solved) return false;
            shuffled = candidate; progress.Data.shuffle--; progress.Flush(); return true;
        }
    }
}
