using System;
using System.Collections.Generic;
using System.Linq;

namespace StickyPeerSort.Editor
{
    public enum LevelVerification { Invalid, Unverified, Verified, NoSolution, BudgetExceeded }

    public sealed class LevelAnalysisReport
    {
        public LevelVerification verification;
        public readonly List<string> messages = new List<string>();
        public int stickers, initiallyClickable, initiallyCovered, maxOverlapDepth, locked, mystery;
        public int peakResolvedBuffer, peakTransientBuffer, firstPeakStep;
        public readonly List<int> solution = new List<int>();
        public string solutionSource;
        public bool Verified => verification == LevelVerification.Verified;
    }

    // Read-only analysis: never regenerate boards, write traces, save assets or touch progress.
    public static class LevelAnalysis
    {
        public static LevelAnalysisReport Analyze(LevelDefinition level, bool search = false, int budget = 12000)
        {
            var report = new LevelAnalysisReport();
            try
            {
                report.messages.AddRange(LevelValidator.Validate(level));
                if (report.messages.Count > 0) { report.verification = LevelVerification.Invalid; return report; }
                var initial = new GameSession(level);
                report.stickers = initial.Stickers.Count;
                report.initiallyClickable = Enumerable.Range(0, report.stickers).Count(initial.Legal);
                report.initiallyCovered = Enumerable.Range(0, report.stickers).Count(i => BoardGeometry.Covered(initial, i));
                report.locked = level.stickers.Count(p => p.special.kind == SpecialKind.Locked);
                report.mystery = level.stickers.Count(p => p.special.kind == SpecialKind.Mystery);
                var depths = new int[report.stickers];
                foreach (int i in Enumerable.Range(0, report.stickers).OrderByDescending(i => initial.Stickers[i].order).ThenByDescending(i => i))
                {
                    depths[i] = 1;
                    for (int upper = 0; upper < report.stickers; upper++)
                        if (depths[upper] > 0 && initial.OverlapCache[i, upper]) depths[i] = Math.Max(depths[i], depths[upper] + 1);
                    report.maxOverlapDepth = Math.Max(report.maxOverlapDepth, depths[i]);
                }
                if (report.initiallyClickable == 0) report.messages.Add("No initially selectable sticker (coverage and special rules included).");
                var ids = level.stickers.Select((p, i) => new { p.instanceId, i }).ToDictionary(p => p.instanceId, p => p.i);
                var witness = new List<int>(); bool idsValid = true;
                foreach (string id in level.solutionTrace)
                {
                    int index;
                    if (id == null || !ids.TryGetValue(id, out index)) { idsValid = false; break; }
                    witness.Add(index);
                }
                string problem;
                if (idsValid && witness.Count > 0 && Verify(initial, witness, out problem))
                { report.solution.AddRange(witness); report.solutionSource = "Saved trace (replayed)"; }
                else
                {
                    if (level.solutionTrace.Count > 0) report.messages.Add("Saved trace is stale, blocked, incomplete or references a missing sticker.");
                    if (!search) { report.verification = LevelVerification.Unverified; return report; }
                    List<int> found;
                    var result = PuzzleSolver.Solve(initial, Math.Max(0, budget), out found);
                    if (result != SolveResult.Solved)
                    {
                        report.verification = result == SolveResult.NoSolution ? LevelVerification.NoSolution : LevelVerification.BudgetExceeded;
                        report.messages.Add(result == SolveResult.NoSolution ? "Search exhausted all reachable states: no winning path." : "Search budget reached. This does not prove the level is unsolvable.");
                        return report;
                    }
                    if (!Verify(initial, found, out problem)) throw new InvalidOperationException(problem);
                    report.solution.AddRange(found); report.solutionSource = "Bounded solver";
                }
                report.verification = LevelVerification.Verified;
                var replay = initial.Copy();
                for (int step = 0; step < report.solution.Count; step++)
                {
                    replay.Select(report.solution[step]);
                    if (replay.Buffer.Count > report.peakResolvedBuffer)
                    { report.peakResolvedBuffer = replay.Buffer.Count; report.firstPeakStep = step + 1; }
                    report.peakTransientBuffer = Math.Max(report.peakTransientBuffer, replay.Buffer.Count);
                    foreach (var e in replay.Events.Where(e => e.kind == PuzzleEventKind.Buffered))
                        report.peakTransientBuffer = Math.Max(report.peakTransientBuffer, e.slot + 1);
                }
            }
            catch (Exception e) { report.verification = LevelVerification.Invalid; report.messages.Add(e.Message); }
            return report;
        }

        public static bool Verify(GameSession initial, IList<int> trace, out string problem)
        {
            var replay = initial.Copy();
            for (int step = 0; step < trace.Count; step++)
                if (!replay.Select(trace[step])) { problem = "Blocked/invalid action at step " + (step + 1); return false; }
            problem = replay.State == GameplayState.Win ? "" : "Trace did not finish in Win.";
            return replay.State == GameplayState.Win;
        }
    }
}
