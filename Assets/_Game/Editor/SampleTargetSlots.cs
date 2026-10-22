using System.Collections.Generic;
namespace StickyPeerSort.Editor
{
    public static class SampleTargetSlots
    {
        public static void Apply(LevelDefinition level)
        {
            var assigned = new HashSet<int>();
            foreach (var objective in level.objectives)
            {
                objective.exactTargets.Clear();
                for (int i = 0; i < level.stickers.Count && objective.exactTargets.Count < objective.targetCount; i++)
                {
                    var definition = level.stickers[i].definition;
                    if (definition == null || definition.category != objective.category || assigned.Contains(i)) continue;
                    objective.exactTargets.Add(definition); assigned.Add(i);
                }
            }
        }
    }
}
