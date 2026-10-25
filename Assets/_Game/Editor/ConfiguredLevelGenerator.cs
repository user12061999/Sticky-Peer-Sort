using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    // Generate on a temporary object; a failed configuration never destroys the last valid board.
    public static class ConfiguredLevelGenerator
    {
        public static bool Rebuild(LevelDefinition source, out string message)
        {
            message = "";
            if (EditorApplication.isPlayingOrWillChangePlaymode) { message = "Exit Play Mode to rebuild assets."; return false; }
            var copy = UnityEngine.Object.Instantiate(source); copy.name = source.name;
            int seed = source.layoutSeed;
            try
            {
                Expand(copy);
                string failure = "";
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    try
                    {
                        copy.layoutSeed = unchecked(seed + attempt * 7919);
                        Expand(copy);
                        TrayStickerSpread.Arrange(copy);
                        var errors = LevelValidator.Validate(copy);
                        if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
                        copy.layoutSeed = seed;
                        // Derived content is rebuilt on Undo too; do not add a second Undo step
                        // after the Inspector's serialized configuration edit.
                        EditorUtility.CopySerialized(copy, source); source.name = copy.name;
                        EditorUtility.SetDirty(source);
                        message = Summary(source) + " | Verified winning path.";
                        return true;
                    }
                    catch (Exception e) { failure = e.Message; }
                }
                throw new InvalidOperationException(failure + " Try fewer special stickers, a different seed or larger trays.");
            }
            catch (Exception e) { message = e.Message + " Last valid layout retained."; return false; }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        public static string Summary(LevelDefinition level)
        {
            return level.stickers.Count + " stickers / " + level.stickers.Select(p => p.definition).Distinct().Count() + " artworks / " +
                level.objectives.Select(o => o.category).Distinct().Count() + " categories / " +
                level.stickers.Count(p => p.special.kind == SpecialKind.Mystery) + " mystery / " +
                level.stickers.Count(p => p.special.kind == SpecialKind.Locked) + " locked";
        }

        public static void Expand(LevelDefinition level)
        {
            var settings = level.generation;
            int count = level.generatedStickerCount;
            if (count < 4 || count > 256 || count % 4 != 0) throw new InvalidOperationException("Sticker count must be 4–256 and divisible by 4 (four targets per album).");
            if (settings.categoryCount < 1 || settings.typesPerCategory < 1 || settings.typesPerCategory > 32) throw new InvalidOperationException("Choose at least one category and 1–32 artworks per category.");
            if (count / 4 < settings.categoryCount) throw new InvalidOperationException("Add at least one album (4 stickers) per category.");
            if (settings.mysteryPercent < 0 || settings.lockedPercent < 0 || settings.mysteryPercent + settings.lockedPercent > 100)
                throw new InvalidOperationException("Mystery and locked percentages must be nonnegative and total at most 100%.");
            if (level.piles.Count == 0 || level.piles.Any(p => p.size.x <= 40 || p.size.y <= 40) || level.piles.Select(p => p.id).Distinct().Count() != level.piles.Count)
                throw new InvalidOperationException("Define trays with unique IDs and width/height greater than 40.");
            var pool = (settings.stickerPool.Count == 0 ? level.stickers.Select(p => p.definition) : settings.stickerPool)
                .Where(d => d != null && d.category != null).Distinct().ToList();
            var groups = pool.GroupBy(d => d.category).Take(settings.categoryCount).Select(g => g.Take(settings.typesPerCategory).ToArray()).ToArray();
            if (groups.Length != settings.categoryCount || groups.Any(g => g.Length != settings.typesPerCategory))
                throw new InvalidOperationException("Sticker Pool needs the requested number of categories and artworks in every category.");
            if ((count / 4 / settings.categoryCount) * 4 < settings.typesPerCategory)
                throw new InvalidOperationException("Increase sticker count so every category has enough instances to use all requested artworks.");
            // Persist the original pool so reducing counts does not discard artwork choices for later edits.
            settings.stickerPool = pool;
            level.difficulty = settings.difficulty.ToString();
            level.stickers.Clear(); level.objectives.Clear();
            for (int set = 0; set < count / 4; set++)
            {
                var group = groups[set % groups.Length];
                var objective = new CategoryObjective { category = group[0].category, targetCount = 4 };
                for (int t = 0; t < 4; t++)
                {
                    var definition = group[((set / groups.Length) * 4 + t) % group.Length]; objective.exactTargets.Add(definition);
                    level.stickers.Add(new StickerPlacement { instanceId = level.name + "_" + (set * 4 + t).ToString("000"), definition = definition });
                }
                level.objectives.Add(objective);
            }
            var random = new System.Random(level.layoutSeed);
            int locks = Mathf.RoundToInt(count * settings.lockedPercent / 100);
            int mysteries = Mathf.RoundToInt(count * settings.mysteryPercent / 100);
            if (locks + mysteries > count - 1) throw new InvalidOperationException("Keep at least one ordinary sticker to cover mystery stickers and start play.");
            var key = groups[0][0].category;
            var candidates = level.stickers.Where(p => p.definition.category != key).OrderBy(_ => random.Next()).ToList();
            if (locks > candidates.Count) throw new InvalidOperationException("Locked ratio is too high: the first key category must remain unlocked.");
            foreach (var p in candidates.Take(locks)) p.special = new StickerSpecialConfig { kind = SpecialKind.Locked, unlockCondition = UnlockCondition.CategoryCompleted, dependencyId = key.id };
            foreach (var p in level.stickers.Where(p => p.special.kind == SpecialKind.None).OrderBy(_ => random.Next()).Take(mysteries))
                p.special = new StickerSpecialConfig { kind = SpecialKind.Mystery };
            float area = level.piles.Sum(p => p.size.x * p.size.y);
            int[] quotas = level.piles.Select(p => Mathf.FloorToInt(count * p.size.x * p.size.y / area)).ToArray();
            for (int i = 0, remainder = count - quotas.Sum(); i < remainder; i++) quotas[i % quotas.Length]++;
            var shuffled = level.stickers.OrderBy(_ => random.Next()).ToList(); int cursor = 0;
            for (int tray = 0; tray < quotas.Length; tray++) for (int i = 0; i < quotas[tray]; i++) shuffled[cursor++].pileId = level.piles[tray].id;
        }
    }
}
