using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    public static class DenseLevelContent
    {
        public static void Expand(LevelDefinition level)
        {
            if (level.generation.enabled) { ConfiguredLevelGenerator.Expand(level); return; }
            int variant = Array.IndexOf(new[] { 5, 12, 19, 62 }, level.levelNumber);
            if (variant < 0) throw new InvalidOperationException("Open the Reference Style sample scene to rebuild its content.");
            level.name = ReferenceStyleBuilder.Names[variant];
            int count = level.generatedStickerCount > 0 ? level.generatedStickerCount : new[] { 32, 40, 48, 64 }[variant];
            if (count < 16 || count % 4 != 0) throw new InvalidOperationException("Generated Sticker Count must be at least 16 and divisible by 4.");
            level.generatedStickerCount = count;
            if (level.layoutSeed == 0) level.layoutSeed = 3251 + level.levelNumber * 101;
            var definitions = new List<StickerDefinition>();
            foreach (string category in new[] { "food", "animal", "plant", "vehicle" }) for (int t = 1; t <= 4; t++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<StickerDefinition>(ReferenceStyleBuilder.Root + "/Data/Stickers/" + category + "_" + t.ToString("00") + ".asset");
                if (definition == null) throw new InvalidOperationException("Missing sample artwork definition."); definitions.Add(definition);
            }
            level.objectives.Clear(); level.stickers.Clear();
            for (int set = 0; set < count / 4; set++)
            {
                int category = set % 4;
                var objective = new CategoryObjective { category = definitions[category * 4].category, targetCount = 4 };
                for (int t = 0; t < 4; t++)
                {
                    var definition = definitions[category * 4 + t]; objective.exactTargets.Add(definition);
                    level.stickers.Add(new StickerPlacement { instanceId = level.name + "_" + (set * 4 + t).ToString("000") + "_" + definition.id,
                        definition = definition, size = new Vector2(220, 220) });
                }
                level.objectives.Add(objective);
            }
            if (variant == 1)
            {
                level.stickers[8].special = new StickerSpecialConfig { kind = SpecialKind.Linked, linkId = "A" };
                level.stickers[9].special = new StickerSpecialConfig { kind = SpecialKind.Linked, linkId = "A" };
            }
            if (variant == 2) level.stickers[6].special = new StickerSpecialConfig { kind = SpecialKind.Frozen, iceLayers = 2 };
            if (variant == 3)
            {
                level.stickers[7].special = new StickerSpecialConfig { kind = SpecialKind.Locked, unlockCondition = UnlockCondition.CategoryCompleted, dependencyId = definitions[0].category.id };
                level.stickers[10].special = new StickerSpecialConfig { kind = SpecialKind.Mystery };
            }
            // Allocate by tray area, then mix identities independently of spatial placement.
            float area = level.piles.Sum(p => p.size.x * p.size.y);
            int[] quotas = level.piles.Select(p => Mathf.FloorToInt(count * p.size.x * p.size.y / area)).ToArray();
            int remaining = count - quotas.Sum();
            for (int i = 0; i < remaining; i++) quotas[i % quotas.Length]++;
            var random = new System.Random(level.layoutSeed);
            var indices = Enumerable.Range(0, count).OrderBy(_ => random.Next()).ToList();
            int[] used = new int[quotas.Length]; int[,] categories = new int[quotas.Length, 4];
            foreach (int i in indices)
            {
                int category = definitions.IndexOf(level.stickers[i].definition) / 4, chosen = -1; float score = float.MaxValue;
                foreach (int tray in Enumerable.Range(0, quotas.Length).OrderBy(_ => random.Next()))
                {
                    if (used[tray] >= quotas[tray]) continue;
                    float value = (float)categories[tray, category] / quotas[tray] + .05f * used[tray] / quotas[tray];
                    if (value < score) { score = value; chosen = tray; }
                }
                level.stickers[i].pileId = level.piles[chosen].id; used[chosen]++; categories[chosen, category]++;
            }
            if (variant == 1 && level.stickers[8].pileId == level.stickers[9].pileId)
            {
                var other = level.stickers.First(p => p.pileId != level.stickers[8].pileId && p.special.kind == SpecialKind.None);
                string owner = other.pileId; other.pileId = level.stickers[9].pileId; level.stickers[9].pileId = owner;
            }
        }

        public static List<int> LogicalPlan(LevelDefinition level)
        {
            var flat = UnityEngine.Object.Instantiate(level);
            try
            {
                for (int i = 0; i < flat.stickers.Count; i++) { flat.stickers[i].position = new Vector2(i * 1000, 0); flat.stickers[i].size = Vector2.one * 100; flat.stickers[i].rotation = 0; }
                var session = new GameSession(flat); var trace = new List<int>(); var random = new System.Random(level.layoutSeed + 751);
                int mystery = level.generation.enabled ? -1 : flat.stickers.FindIndex(p => p.special.kind == SpecialKind.Mystery);
                int pending = -1;
                int parked = 0;
                while (session.State == GameplayState.Playing && trace.Count < flat.stickers.Count * 4)
                {
                    var choices = Enumerable.Range(0, flat.stickers.Count).Where(i => session.Legal(i) && session.HasTarget(i)).ToList();
                    if (level.generation.enabled && trace.Count < 4 && choices.Any(i => flat.stickers[i].special.kind == SpecialKind.None))
                        choices.RemoveAll(i => flat.stickers[i].special.kind != SpecialKind.None);
                    foreach (int linked in Enumerable.Range(0, flat.stickers.Count).Where(i => flat.stickers[i].special.kind == SpecialKind.Linked && session.Stickers[i].location == StickerLocation.Board))
                        choices.RemoveAll(i => i != linked && flat.stickers[i].definition == flat.stickers[linked].definition);
                    if (mystery >= 0 && session.Stickers[mystery].location == StickerLocation.Board)
                    {
                        choices.RemoveAll(i => i != mystery && flat.stickers[i].definition == flat.stickers[mystery].definition);
                        if (trace.Count >= 4 && choices.Contains(mystery)) choices = new List<int> { mystery };
                    }
                    if (pending >= 0 && session.Stickers[pending].location == StickerLocation.Board) choices = new List<int> { pending };
                    if (choices.Count == 0) throw new InvalidOperationException("No logical authoring path for " + level.name);
                    int chosen = choices[random.Next(choices.Count)]; session.Select(chosen); trace.Add(chosen);
                    // Campaign authoring can interleave future-sheet pieces into the witness.
                    // Their layer priority makes these buffer decisions part of the real board.
                    if (level.generation.enabled && pending < 0 && trace.Count >= 4 && parked < level.generation.plannedBufferPeak)
                    {
                        var future = Enumerable.Range(0, flat.stickers.Count)
                            .Where(i => session.Stickers[i].location == StickerLocation.Board && session.Legal(i) && !session.HasTarget(i) && flat.stickers[i].special.kind == SpecialKind.None).ToList();
                        while (future.Count > 0 && parked < level.generation.plannedBufferPeak)
                        {
                            int buffered = future[random.Next(future.Count)]; future.Remove(buffered);
                            if (!session.Select(buffered) || session.State == GameplayState.Fail) throw new InvalidOperationException("Buffer planning exhausted the waiting tray.");
                            trace.Add(buffered); parked++;
                        }
                    }
                    pending = -1;
                    if (session.Stickers[chosen].location == StickerLocation.Board) pending = chosen;
                    else if (flat.stickers[chosen].special.kind == SpecialKind.Linked)
                        pending = flat.stickers.FindIndex(p => p != flat.stickers[chosen] && p.special.kind == SpecialKind.Linked && p.special.linkId == flat.stickers[chosen].special.linkId && session.Stickers[flat.stickers.IndexOf(p)].location == StickerLocation.Board);
                }
                if (session.State != GameplayState.Win) throw new InvalidOperationException("Authoring plan did not win.");
                if (level.generation.enabled && parked < level.generation.plannedBufferPeak) throw new InvalidOperationException("Not enough future-sheet stickers for the planned buffer route.");
                var unique = trace.Distinct().ToList();
                for (int rank = 0; rank < unique.Count; rank++) level.stickers[unique[rank]].sortingOrder = unique.Count - rank;
                return trace;
            }
            finally { UnityEngine.Object.DestroyImmediate(flat); }
        }
    }
}
