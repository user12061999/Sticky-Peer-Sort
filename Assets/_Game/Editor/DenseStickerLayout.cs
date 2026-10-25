using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    public static class DenseStickerLayout
    {
        public static void Arrange(LevelDefinition level)
        {
            var plan = DenseLevelContent.LogicalPlan(level);
            var random = new System.Random(level.layoutSeed);
            foreach (var tray in level.piles)
            {
                var pieces = level.stickers.Where(p => p.pileId == tray.id)
                    .OrderBy(p => p.special.kind == SpecialKind.Locked ? 0 : p.special.kind == SpecialKind.Mystery ? 1 : p.special.kind == SpecialKind.Linked ? 2 : 3)
                    .ThenBy(_ => random.Next()).ToList();
                var points = Samples(tray, 28); bool[] covered = new bool[points.Count];
                var placed = new List<StickerPlacement>();
                float density = !level.generation.enabled ? 1.95f : level.generation.difficulty == LevelDifficulty.Easy ? 1.55f : level.generation.difficulty == LevelDifficulty.Hard ? 2.15f : 1.95f;
                float diameter = Mathf.Sqrt((tray.size.x - 40) * (tray.size.y - 40) / Mathf.Max(1, pieces.Count)) * density;
                foreach (var piece in pieces)
                {
                    piece.rotation = tray.rotation;
                    piece.size = Vector2.one * 100; piece.scale = 1;
                    var alpha = TrayStickerSpread.VisibleBounds(piece, tray);
                    float variation = Mathf.Lerp(.86f, 1.16f, (float)random.NextDouble());
                    piece.size *= diameter * variation / Mathf.Sqrt(alpha.width * alpha.height);
                    piece.scale = level.boardStickerSizeMultiplier;
                    Vector2 originalSize = piece.size, bestSize = piece.size;
                    Vector2 bestPosition = Vector2.zero; float bestRotation = 0, bestScore = float.MinValue;
                    var runtime = new StickerRuntime(piece);
                    for (int attempt = 0; attempt < 64; attempt++)
                    {
                        piece.size = originalSize;
                        piece.rotation = tray.rotation + Mathf.Lerp(-43, 43, (float)random.NextDouble());
                        alpha = TrayStickerSpread.VisibleBounds(piece, tray);
                        // A piece may have transparent padding outside the well; its actual art stays inside.
                        float fit = Mathf.Min(1, Mathf.Min((tray.size.x - 40) / alpha.width, (tray.size.y - 40) / alpha.height));
                        if (fit < 1) piece.size *= fit;
                        alpha = TrayStickerSpread.VisibleBounds(piece, tray);
                        Vector2 min = -tray.size * .5f + Vector2.one * 20 - alpha.min;
                        Vector2 max = tray.size * .5f - Vector2.one * 20 - alpha.max;
                        Vector2 local = new Vector2(Mathf.Lerp(min.x, max.x, (float)random.NextDouble()), Mathf.Lerp(min.y, max.y, (float)random.NextDouble()));
                        // Include edge/corner candidates alongside random poses; never assign rows or category zones.
                        if (attempt % 5 == 0) local.x = attempt % 2 == 0 ? min.x : max.x;
                        if (attempt % 7 == 0) local.y = attempt % 2 == 0 ? max.y : min.y;
                        piece.position = tray.position + Rotate(local, tray.rotation);
                        runtime.position = piece.position; runtime.rotation = piece.rotation;
                        if (!Allowed(runtime, placed)) continue;
                        float score = 0;
                        for (int p = 0; p < points.Count; p++)
                        {
                            if (covered[p] || !BoardGeometry.Contains(runtime, points[p])) continue;
                            Vector2 location = Rotate(points[p] - tray.position, -tray.rotation);
                            bool edge = Mathf.Abs(location.x) > tray.size.x * .34f || Mathf.Abs(location.y) > tray.size.y * .34f;
                            score += edge ? 2.2f : 1;
                        }
                        score += (float)random.NextDouble() * .5f;
                        if (score > bestScore) { bestScore = score; bestPosition = piece.position; bestRotation = piece.rotation; bestSize = piece.size; }
                    }
                    if (bestScore == float.MinValue) throw new InvalidOperationException("Could not place " + piece.instanceId + " without hiding a special symbol.");
                    piece.position = bestPosition; piece.rotation = bestRotation; piece.size = bestSize;
                    runtime.position = bestPosition; runtime.rotation = bestRotation;
                    for (int p = 0; p < points.Count; p++) covered[p] |= BoardGeometry.Contains(runtime, points[p]);
                    placed.Add(piece);
                }
            }
            EnsureMysteryCover(level);
            var initial = new GameSession(level);
            foreach (var mystery in level.stickers.Where(p => p.special.kind == SpecialKind.Mystery))
            {
                int index = level.stickers.IndexOf(mystery);
                if (!BoardGeometry.Covered(initial, index) || BoardGeometry.Hit(initial, mystery.position) != index)
                    throw new InvalidOperationException("A mystery sticker lost its cover or visible question center. Change seed or reduce mystery ratio.");
            }
            // Replay the independently constructed logical path through the real alpha-overlap rules.
            // Layer priority follows this path, but identity/position/angle are independently mixed.
            var replay = new GameSession(level);
            foreach (int i in plan)
                if (!replay.Select(i)) throw new InvalidOperationException("Dense layout blocked its witness at " + level.stickers[i].instanceId);
            if (replay.State != GameplayState.Win) throw new InvalidOperationException("Dense layout witness did not finish.");
            level.solutionTrace = plan.ConvertAll(i => level.stickers[i].instanceId);
        }

        static bool Allowed(StickerRuntime candidate, List<StickerPlacement> placed)
        {
            foreach (var other in placed)
            {
                if ((other.special.kind == SpecialKind.Locked || other.special.kind == SpecialKind.Mystery) && candidate.order > other.sortingOrder && BoardGeometry.Contains(candidate, other.position)) return false;
                if (candidate.placement.special.kind == SpecialKind.Linked && other.special.kind == SpecialKind.Linked && candidate.placement.special.linkId == other.special.linkId && BoardGeometry.Overlaps(candidate, new StickerRuntime(other))) return false;
            }
            return true;
        }
        public static void SetBoardSizeMultiplier(LevelDefinition level, float multiplier)
        {
            float ratio = multiplier / Mathf.Max(.01f, level.boardStickerSizeMultiplier);
            foreach (var piece in level.stickers) piece.scale *= ratio;
            level.boardStickerSizeMultiplier = multiplier;
            EnsureMysteryCover(level);
        }
        static void EnsureMysteryCover(LevelDefinition level)
        {
            foreach (var hidden in level.stickers.Where(p => p.special.kind == SpecialKind.Mystery))
            {
                int index = level.stickers.IndexOf(hidden); var state = new GameSession(level);
                if (BoardGeometry.Covered(state, index) && BoardGeometry.Hit(state, hidden.position) == index) continue;
                var candidates = level.stickers.Where(p => p.pileId == hidden.pileId && p.special.kind == SpecialKind.None && p.sortingOrder > hidden.sortingOrder)
                    .OrderBy(p => (p.position - hidden.position).sqrMagnitude).ToList();
                bool found = false;
                foreach (var cover in candidates)
                {
                    Vector2 old = cover.position;
                    foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down, new Vector2(.9f, .3f), new Vector2(-.9f, .3f) })
                    {
                        cover.position = hidden.position + Vector2.Scale(direction, cover.definition.FitSize(cover.size)) * (.53f * cover.scale);
                        TrayStickerSpread.Clamp(cover, level.piles.Find(p => p.id == cover.pileId));
                        var runtime = new StickerRuntime(cover);
                        if (level.stickers.Any(p => p != cover && p.pileId == cover.pileId && (p.special.kind == SpecialKind.Locked || p.special.kind == SpecialKind.Mystery) && p.sortingOrder < cover.sortingOrder && BoardGeometry.Contains(runtime, p.position))) continue;
                        state = new GameSession(level);
                        if (BoardGeometry.Covered(state, index) && BoardGeometry.Hit(state, hidden.position) == index) { found = true; break; }
                    }
                    if (found) break; cover.position = old;
                }
                if (!found) throw new InvalidOperationException("Mystery needs a partially covering sticker with its question center visible.");
            }
        }
        static Vector2 Rotate(Vector2 point, float degrees)
        { return (Vector2)(Quaternion.Euler(0, 0, degrees) * point); }
        static List<Vector2> Samples(BoardPileDefinition tray, int resolution)
        {
            var points = new List<Vector2>(resolution * resolution);
            Vector2 half = tray.size * .5f - Vector2.one * 24;
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
                points.Add(tray.position + Rotate(new Vector2(Mathf.Lerp(-half.x, half.x, (x + .5f) / resolution), Mathf.Lerp(-half.y, half.y, (y + .5f) / resolution)), tray.rotation));
            return points;
        }
        public static float Coverage(LevelDefinition level, BoardPileDefinition tray)
        {
            var pieces = level.stickers.Where(p => p.pileId == tray.id).Select(p => new StickerRuntime(p)).ToList();
            var points = Samples(tray, 44); int covered = points.Count(point => pieces.Any(piece => BoardGeometry.Contains(piece, point)));
            return (float)covered / points.Count;
        }
    }
}
