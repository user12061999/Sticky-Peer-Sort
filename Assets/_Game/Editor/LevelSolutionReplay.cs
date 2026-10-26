using System;
using System.Collections.Generic;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    // Own an immutable level snapshot. Previous/seek restores full state, including ice and unlocks.
    public sealed class LevelSolutionReplay : IDisposable
    {
        readonly LevelDefinition snapshot;
        readonly List<int> trace;
        readonly List<GameSession> states = new List<GameSession>();
        readonly List<List<PuzzleEvent>> events = new List<List<PuzzleEvent>>();
        public int Step { get; private set; }
        public int Count => trace.Count;
        public GameSession Session => states[Step];
        public IReadOnlyList<PuzzleEvent> Events => events[Step];
        public int NextSticker => Step < Count ? trace[Step] : -1;
        public LevelSolutionReplay(LevelDefinition level, IList<int> solution)
        {
            snapshot = UnityEngine.Object.Instantiate(level); snapshot.name = level.name;
            snapshot.hideFlags = HideFlags.HideAndDontSave;
            trace = new List<int>(solution);
            var initial = new GameSession(snapshot); string problem;
            if (!LevelAnalysis.Verify(initial, trace, out problem))
            { UnityEngine.Object.DestroyImmediate(snapshot); throw new ArgumentException(problem); }
            states.Add(initial); events.Add(new List<PuzzleEvent>());
        }
        public void Seek(int step)
        {
            step = Mathf.Clamp(step, 0, Count);
            while (states.Count <= step)
            {
                var next = states[states.Count - 1].Copy();
                if (!next.Select(trace[states.Count - 1])) throw new InvalidOperationException("Replay snapshot was modified.");
                events.Add(new List<PuzzleEvent>(next.Events)); states.Add(next);
            }
            Step = step;
        }
        public void Dispose() { if (snapshot != null) UnityEngine.Object.DestroyImmediate(snapshot); }
    }
}
