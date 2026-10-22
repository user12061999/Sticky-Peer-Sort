using System.Collections.Generic;
using System.Linq;
namespace StickyPeerSort
{
    public static class LevelValidator
    {
        public static List<string> Validate(LevelDefinition level)
        {
            var errors = new List<string>();
            if (level == null) { errors.Add("Missing level."); return errors; }
            if (level.bufferCapacity < 1 || level.activeCategoryCount < 1 || level.objectives.Count == 0) errors.Add("Invalid buffer, active card count or empty objectives.");
            var ids = new HashSet<string>();
            foreach (var p in level.stickers)
            {
                if (string.IsNullOrEmpty(p.instanceId) || !ids.Add(p.instanceId)) errors.Add("Missing/duplicate instance ID: " + p.instanceId);
                if (p.definition == null || p.definition.category == null || !p.definition.HasArtwork) errors.Add("Missing definition/category/artwork: " + p.instanceId);
                else if (!p.definition.CanSampleAlpha) errors.Add("Bake sticker alpha mask in its Inspector: " + p.definition.name);
                if (p.scale <= 0 || p.size.x <= 0 || p.size.y <= 0) errors.Add("Invalid transform: " + p.instanceId);
            }
            foreach (var o in level.objectives)
            {
                if (o.category == null || o.targetCount < 1) { errors.Add("Invalid objective."); continue; }
                if (o.exactTargets.Count != 0 && o.exactTargets.Count != o.targetCount) errors.Add("Exact targets must match target count.");
                if (o.exactTargets.Any(t => t == null || t.category != o.category)) errors.Add("Invalid target category.");
            }
            foreach (var group in level.objectives.Where(o => o.category != null).GroupBy(o => o.category))
            {
                var available = level.stickers.Where(p => p.definition != null && p.definition.category == group.Key).ToList();
                if (available.Count != group.Sum(o => o.targetCount)) errors.Add("Sticker count differs from required targets: " + group.Key.title);
                foreach (var target in group.SelectMany(o => o.exactTargets).Where(t => t != null).GroupBy(t => t))
                    if (available.Count(p => p.definition == target.Key) < target.Count()) errors.Add("Missing exact target: " + target.Key.id);
            }
            foreach (var p in level.stickers)
            {
                if (p.definition != null && !level.objectives.Any(o => o.category == p.definition.category)) errors.Add("Sticker category has no objective: " + p.instanceId);
                var c = p.special;
                if (c.kind == SpecialKind.Locked && (c.unlockCondition == UnlockCondition.CategoryCompleted
                    ? !level.objectives.Any(o => o.category != null && o.category.id == c.dependencyId)
                    : c.dependencyId == p.instanceId || !ids.Contains(c.dependencyId))) errors.Add("Invalid lock dependency: " + p.instanceId);
                if (c.kind == SpecialKind.Linked && (string.IsNullOrEmpty(c.linkId) || level.stickers.Count(x => x.special.kind == SpecialKind.Linked && x.special.linkId == c.linkId) != 2)) errors.Add("Link must have exactly two partners: " + p.instanceId);
                if (c.kind == SpecialKind.Frozen && c.iceLayers < 1) errors.Add("Frozen sticker requires ice layers.");
            }
            return errors;
        }
    }
}
