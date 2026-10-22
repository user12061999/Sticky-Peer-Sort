using System.Collections.Generic;
namespace StickyPeerSort
{
    public enum SolveResult { Solved, NoSolution, BudgetExceeded }
    public static class PuzzleSolver
    {
        public static SolveResult Solve(GameSession initial, int budget, out List<int> solution)
        {
            var path = new List<int>(); var seen = new HashSet<string>(); bool exceeded = false;
            bool success = Search(initial.Copy(), path, seen, ref budget, ref exceeded);
            solution = success ? path : new List<int>();
            return success ? SolveResult.Solved : exceeded ? SolveResult.BudgetExceeded : SolveResult.NoSolution;
        }
        static bool Search(GameSession s, List<int> path, HashSet<string> seen, ref int budget, ref bool exceeded)
        {
            if (s.State == GameplayState.Win) return true;
            if (s.State == GameplayState.Fail || !seen.Add(s.Key())) return false;
            if (--budget < 0) { exceeded = true; return false; }
            var choices = new List<int>();
            for (int i = 0; i < s.Stickers.Count; i++) if (s.Legal(i)) choices.Add(i);
            choices.Sort((a, b) => { int value = s.HasTarget(b).CompareTo(s.HasTarget(a)); return value != 0 ? value : a.CompareTo(b); });
            foreach (int i in choices)
            {
                var next = s.Copy(); if (!next.Select(i)) continue; path.Add(i);
                if (Search(next, path, seen, ref budget, ref exceeded)) return true;
                path.RemoveAt(path.Count - 1); if (exceeded) return false;
            }
            return false;
        }
    }
}
