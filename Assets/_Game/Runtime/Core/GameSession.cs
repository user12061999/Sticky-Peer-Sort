using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace StickyPeerSort
{
    public enum GameplayState { Loading, Ready, Playing, ResolvingMove, ResolvingCategory, CheckingResult, Win, Fail, Paused }
    public enum StickerLocation { Board, Buffer, Collected }
    public sealed class StickerRuntime
    {
        public readonly StickerPlacement placement;
        public StickerLocation location;
        public int ice, order;
        public bool revealed;
        public Vector2 position;
        public float rotation;
        public StickerRuntime(StickerPlacement p) { placement = p; ice = p.special.iceLayers; position = p.position; rotation = p.rotation; order = p.sortingOrder; }
        public StickerRuntime Copy() { return (StickerRuntime)MemberwiseClone(); }
    }
    public sealed class CategoryRuntime
    {
        public readonly CategoryObjective objective;
        public int[] targets;
        public bool active;
        public bool Complete { get { for (int i = 0; i < targets.Length; i++) if (targets[i] < 0) return false; return true; } }
        public int Count { get { int n = 0; foreach (int i in targets) if (i >= 0) n++; return n; } }
        public CategoryRuntime(CategoryObjective o) { objective = o; targets = new int[o.targetCount]; for (int i = 0; i < targets.Length; i++) targets[i] = -1; }
        public CategoryRuntime Copy() { return new CategoryRuntime(objective) { targets = (int[])targets.Clone(), active = active }; }
    }
    public enum PuzzleEventKind { Peel, IceBroken, Revealed, Collected, Buffered, CategoryCompleted, CategoryActivated, Unlocked, Win, Fail }
    public struct PuzzleEvent
    {
        public PuzzleEventKind kind;
        public int sticker, category, slot;
        public PuzzleEvent(PuzzleEventKind k, int s = -1, int c = -1, int t = -1) { kind = k; sticker = s; category = c; slot = t; }
    }
    public sealed class GameSession
    {
        public LevelDefinition Level { get; private set; }
        public List<StickerRuntime> Stickers { get; private set; } = new List<StickerRuntime>();
        public List<CategoryRuntime> Categories { get; private set; } = new List<CategoryRuntime>();
        public List<int> Buffer { get; private set; } = new List<int>();
        public List<PuzzleEvent> Events = new List<PuzzleEvent>();
        public GameplayState State { get; private set; }
        public int Capacity { get; private set; }
        public bool[,] OverlapCache { get; private set; }
        public event Action<GameplayState> StateChanged;
        readonly StickerRuleEngine rules = new StickerRuleEngine();
        GameSession undo;
        GameplayState resumeState;
        public bool CanUndo { get { return undo != null; } }
        public GameSession(LevelDefinition level)
        {
            State = GameplayState.Loading; Level = level; Capacity = level.bufferCapacity;
            foreach (var p in level.stickers) Stickers.Add(new StickerRuntime(p));
            OverlapCache = BoardGeometry.BuildOverlapCache(this);
            foreach (var o in level.objectives) Categories.Add(new CategoryRuntime(o));
            for (int i = 0; i < Math.Min(level.activeCategoryCount, Categories.Count); i++) Categories[i].active = true;
            RevealUncovered(false);
            SetState(GameplayState.Ready); SetState(GameplayState.Playing);
        }
        void SetState(GameplayState value) { if (State == value) return; State = value; StateChanged?.Invoke(value); }
        public void Pause(bool pause) { if (pause) { resumeState = State; SetState(GameplayState.Paused); } else SetState(resumeState); }
        public bool Legal(int i)
        {
            return i >= 0 && i < Stickers.Count && Stickers[i].location == StickerLocation.Board && !BoardGeometry.Covered(this, i) && rules.Evaluate(this, i) != SpecialAction.Block;
        }
        public bool Select(int i)
        { return SelectInternal(i, false); }
        public bool SelectAt(Vector2 boardPoint)
        { return SelectInternal(BoardGeometry.Hit(this, boardPoint), true); }
        bool SelectInternal(int i, bool hitVerified)
        {
            if (State != GameplayState.Playing || i < 0 || i >= Stickers.Count || Stickers[i].location != StickerLocation.Board ||
                BoardGeometry.Covered(this, i) || rules.Evaluate(this, i) == SpecialAction.Block) return false;
            undo = Copy(); Events.Clear(); SetState(GameplayState.ResolvingMove);
            var s = Stickers[i];
            var action = rules.Evaluate(this, i);
            if (action == SpecialAction.DamageIce)
            { s.ice--; Events.Add(new PuzzleEvent(PuzzleEventKind.IceBroken, i)); SetState(GameplayState.Playing); return true; }
            var locked = new List<int>();
            for (int k = 0; k < Stickers.Count; k++) if (Stickers[k].location == StickerLocation.Board && Stickers[k].placement.special.kind == SpecialKind.Locked && rules.Evaluate(this, k) == SpecialAction.Block) locked.Add(k);
            if (s.placement.special.kind == SpecialKind.Mystery && !s.revealed) { s.revealed = true; Events.Add(new PuzzleEvent(PuzzleEventKind.Revealed, i)); }
            Events.Add(new PuzzleEvent(PuzzleEventKind.Peel, i));
            if (!Collect(i)) { s.location = StickerLocation.Buffer; Buffer.Add(i); Events.Add(new PuzzleEvent(PuzzleEventKind.Buffered, i, -1, Buffer.Count - 1)); }
            Resolve();
            RevealUncovered(true);
            foreach (int k in locked) if (rules.Evaluate(this, k) == SpecialAction.Collect) Events.Add(new PuzzleEvent(PuzzleEventKind.Unlocked, k));
            SetState(GameplayState.CheckingResult);
            bool win = Buffer.Count == 0 && Categories.TrueForAll(c => c.Complete) && Stickers.TrueForAll(x => x.location == StickerLocation.Collected);
            if (win) { Events.Add(new PuzzleEvent(PuzzleEventKind.Win)); SetState(GameplayState.Win); }
            else if (Level.failOnOverflow && Buffer.Count >= Capacity) { Events.Add(new PuzzleEvent(PuzzleEventKind.Fail)); SetState(GameplayState.Fail); }
            else SetState(GameplayState.Playing);
            return true;
        }
        public bool HasTarget(int i) { int c, t; return FindTarget(i, out c, out t); }
        public bool TryGetTarget(int i, out int category, out int target) { return FindTarget(i, out category, out target); }
        void RevealUncovered(bool emit)
        {
            for (int i = 0; i < Stickers.Count; i++)
            {
                var sticker = Stickers[i];
                if (sticker.location != StickerLocation.Board || sticker.placement.special.kind != SpecialKind.Mystery || sticker.revealed || BoardGeometry.Covered(this, i)) continue;
                sticker.revealed = true; if (emit) Events.Add(new PuzzleEvent(PuzzleEventKind.Revealed, i));
            }
        }
        bool FindTarget(int i, out int category, out int target)
        {
            var d = Stickers[i].placement.definition;
            category = target = -1;
            int mostFilled = -1;
            for (int c = 0; c < Categories.Count; c++)
            {
                var cat = Categories[c];
                if (!cat.active || cat.objective.category != d.category) continue;
                for (int t = 0; t < cat.targets.Length; t++)
                    if (cat.targets[t] < 0 && (cat.objective.exactTargets.Count == 0 || cat.objective.exactTargets[t] == d))
                    {
                        // Consider only sheets with an exact free target, then prefer the most filled.
                        // Equal counts retain objective order; the first matching slot wins within a sheet.
                        if (cat.Count > mostFilled) { category = c; target = t; mostFilled = cat.Count; }
                        break;
                    }
            }
            return category >= 0;
        }
        bool Collect(int i)
        {
            int c, t; if (!FindTarget(i, out c, out t)) return false;
            Categories[c].targets[t] = i; Stickers[i].location = StickerLocation.Collected;
            Events.Add(new PuzzleEvent(PuzzleEventKind.Collected, i, c, t)); return true;
        }
        void Resolve()
        {
            bool changed;
            do
            {
                changed = false; SetState(GameplayState.ResolvingCategory);
                for (int c = 0; c < Categories.Count; c++) if (Categories[c].active && Categories[c].Complete)
                {
                    Categories[c].active = false; Events.Add(new PuzzleEvent(PuzzleEventKind.CategoryCompleted, -1, c)); changed = true;
                    for (int n = 0; n < Categories.Count; n++) if (!Categories[n].active && !Categories[n].Complete)
                    { Categories[n].active = true; Events.Add(new PuzzleEvent(PuzzleEventKind.CategoryActivated, -1, n)); break; }
                }
                for (int b = 0; b < Buffer.Count;)
                    if (Collect(Buffer[b])) { Buffer.RemoveAt(b); changed = true; } else b++;
            } while (changed);
        }
        public bool Undo() { if (undo == null) return false; var previous = undo; Restore(previous); undo = null; Events.Clear(); return true; }
        public bool AddSlot() { if (State != GameplayState.Playing && State != GameplayState.Fail) return false; undo = Copy(); Capacity++; SetState(GameplayState.Playing); return true; }
        public bool Shuffle(int seed)
        {
            if (State != GameplayState.Playing) return false;
            undo = Copy(); var random = new System.Random(seed); var indices = new List<int>();
            for (int i = 0; i < Stickers.Count; i++) if (Stickers[i].location == StickerLocation.Board && Stickers[i].placement.special.kind == SpecialKind.None) indices.Add(i);
            for (int k = indices.Count - 1; k > 0; k--)
            {
                var a = Stickers[indices[k]]; var b = Stickers[indices[random.Next(k + 1)]];
                Vector2 pos = a.position; a.position = b.position; b.position = pos;
                float rot = a.rotation; a.rotation = b.rotation; b.rotation = rot;
                int order = a.order; a.order = b.order; b.order = order;
            }
            OverlapCache = BoardGeometry.BuildOverlapCache(this); RevealUncovered(false); return true;
        }
        public GameSession Copy()
        {
            var copy = (GameSession)MemberwiseClone(); copy.Stickers = Stickers.ConvertAll(s => s.Copy()); copy.Categories = Categories.ConvertAll(c => c.Copy());
            copy.Buffer = new List<int>(Buffer); copy.Events = new List<PuzzleEvent>(); copy.undo = null; copy.StateChanged = null; return copy;
        }
        void Restore(GameSession copy) { Stickers = copy.Stickers.ConvertAll(s => s.Copy()); Categories = copy.Categories.ConvertAll(c => c.Copy()); Buffer = new List<int>(copy.Buffer); Capacity = copy.Capacity; OverlapCache = copy.OverlapCache; SetState(copy.State); }
        public string Key()
        {
            var b = new StringBuilder(); b.Append(Capacity).Append('|');
            foreach (var s in Stickers) b.Append((int)s.location).Append(',').Append(s.ice).Append(',').Append(s.revealed ? 1 : 0).Append(';');
            foreach (var c in Categories) { b.Append(c.active ? 'a' : 'i'); foreach (int t in c.targets) b.Append(t).Append(','); }
            b.Append('|'); foreach (int i in Buffer) b.Append(i).Append(','); return b.ToString();
        }
    }
}
