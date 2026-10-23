using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    public static class SamplePileLayouts
    {
        // Deterministic authoring, not a runtime shuffle: generated levels can be validated and replayed.
        public static void Apply(LevelDefinition level)
        {
            if (level.levelNumber >= 6)
            {
                level.stickers[3].special = new StickerSpecialConfig();
                level.stickers[7].special = new StickerSpecialConfig { kind = SpecialKind.Locked, unlockCondition = UnlockCondition.CategoryCompleted, dependencyId = level.objectives[0].category.id };
                level.tutorial = level.levelNumber == 6 ? "Complete the set with the KEY to unlock the locked sticker." : level.tutorial;
            }
            if (level.levelNumber >= 9) level.tutorial = "Hidden ? stickers reveal when uncovered. Clear the KEY set to unlock.";
            level.piles.Clear();
            var anchors = new List<Vector2>(); var owners = new List<int>();
            int variant = Mathf.Clamp(level.levelNumber, 1, 10);
            if (variant == 1 || variant == 3)
            {
                Add(level, anchors, owners, new Vector2(-225, 160), new Vector2(420, 336), -7);
                Add(level, anchors, owners, new Vector2(225, 120), new Vector2(420, 336), 9);
                Add(level, anchors, owners, new Vector2(-25, -200), new Vector2(420, 336), -4);
            }
            else if (variant == 2 || variant == 5)
            {
                for (int row = 0; row < 2; row++) for (int col = 0; col < 2; col++)
                    Add(level, anchors, owners, new Vector2(col == 0 ? -220 : 220, row == 0 ? 175 : -175), new Vector2(420, 336), variant == 5 ? (col == 0 ? -2 : 2) : 0);
            }
            else if (variant == 4)
            {
                Add(level, anchors, owners, new Vector2(0, 175), new Vector2(860, 336), 0, new[] { new Vector2(-210, 0), new Vector2(210, 0) });
                Add(level, anchors, owners, new Vector2(0, -175), new Vector2(860, 336), 0, new[] { new Vector2(-210, 0), new Vector2(210, 0) });
            }
            else if (variant == 6)
            {
                for (int row = 0; row < 3; row++)
                    Add(level, anchors, owners, new Vector2(0, 230 - row * 230), new Vector2(860, 226), 0, new[] { new Vector2(-210, 0), new Vector2(210, 0) });
            }
            else if (variant == 7)
            {
                Add(level, anchors, owners, Vector2.zero, new Vector2(880, 700), 0,
                    new[] { new Vector2(-205, 160), new Vector2(205, 160), new Vector2(-205, -160), new Vector2(205, -160) });
            }
            else if (variant == 8)
            {
                Add(level, anchors, owners, new Vector2(0, 230), new Vector2(860, 226), 0, new[] { new Vector2(-210, 0), new Vector2(210, 0) });
                for (int row = 0; row < 2; row++) for (int col = 0; col < 2; col++)
                    Add(level, anchors, owners, new Vector2(col == 0 ? -220 : 220, -row * 230), new Vector2(420, 226), 0);
            }
            else if (variant == 9)
            {
                for (int row = 0; row < 3; row++) for (int col = 0; col < 2; col++)
                    Add(level, anchors, owners, new Vector2(col == 0 ? -220 : 220, 230 - row * 230), new Vector2(420, 226), 0);
            }
            else
            {
                Add(level, anchors, owners, new Vector2(-220, 0), new Vector2(420, 700), 0,
                    new[] { new Vector2(0, 230), Vector2.zero, new Vector2(0, -230) });
                for (int row = 0; row < 3; row++)
                    Add(level, anchors, owners, new Vector2(220, 230 - row * 230), new Vector2(420, 226), 0);
            }
            Vector2[] offsets = { new Vector2(-67, -39), new Vector2(59, -28), new Vector2(-43, 47), new Vector2(57, 45),
                new Vector2(-73, 2), new Vector2(13, -43), new Vector2(-7, 38), new Vector2(50, -4) };
            float[] angles = { -23, 19, -12, 27, 9, -28, 14, -7 };
            float[] spread = { 1.12f, 1.08f, 1.03f, .96f, .90f, .92f, .93f, .88f, .85f, .82f };
            bool shallow = variant == 6 || variant >= 8;
            for (int i = 0; i < level.stickers.Count; i++)
            {
                int anchor = i % anchors.Count, layer = i / anchors.Count;
                var data = level.piles[owners[anchor]]; var p = level.stickers[i];
                Vector2 offset = offsets[layer % offsets.Length] * spread[variant - 1];
                if (shallow) offset.y *= .13f;
                float r = data.rotation * Mathf.Deg2Rad;
                p.position = anchors[anchor] + new Vector2(offset.x * Mathf.Cos(r) - offset.y * Mathf.Sin(r), offset.x * Mathf.Sin(r) + offset.y * Mathf.Cos(r));
                p.size = shallow ? new Vector2(210, 172) : new Vector2(240, 216); p.scale = 1;
                p.rotation = angles[(layer + anchor) % angles.Length] * (shallow ? .22f : 1) + data.rotation;
                p.sortingOrder = i; p.pileId = data.id;
                // Keep the full rotated sticker rectangle inside its authored wooden compartment.
                Vector2 relative = p.position - data.position;
                Vector2 local = new Vector2(relative.x * Mathf.Cos(r) + relative.y * Mathf.Sin(r), -relative.x * Mathf.Sin(r) + relative.y * Mathf.Cos(r));
                float turn = (p.rotation - data.rotation) * Mathf.Deg2Rad;
                Vector2 extent = new Vector2(Mathf.Abs(Mathf.Cos(turn)) * p.size.x + Mathf.Abs(Mathf.Sin(turn)) * p.size.y,
                    Mathf.Abs(Mathf.Sin(turn)) * p.size.x + Mathf.Abs(Mathf.Cos(turn)) * p.size.y) * .5f;
                Vector2 room = data.size * .5f - new Vector2(16, 16) - extent;
                local.x = Mathf.Clamp(local.x, -Mathf.Max(0, room.x), Mathf.Max(0, room.x));
                local.y = Mathf.Clamp(local.y, -Mathf.Max(0, room.y), Mathf.Max(0, room.y));
                p.position = data.position + new Vector2(local.x * Mathf.Cos(r) - local.y * Mathf.Sin(r), local.x * Mathf.Sin(r) + local.y * Mathf.Cos(r));
            }
            int[] pressure = { 0, 0, 1, 2, 2, 2, 2, 3, 3, 4 };
            int blockers = pressure[Mathf.Clamp(level.levelNumber - 1, 0, 9)];
            if (blockers > 0)
            {
                for (int i = 0; i < blockers; i++)
                {
                    var lower = level.stickers[i]; var upper = level.stickers[level.stickers.Count - 4 + i];
                    upper.position = lower.position; upper.rotation = lower.rotation; upper.pileId = lower.pileId;
                }
            }
            // A removal order authors the layer hierarchy. Future stickers start on top where
            // buffer planning is introduced; locks and linked partners retain a legal solution.
            var removalOrder = new List<int>();
            if (blockers > 0)
            {
                for (int i = 0; i < blockers; i++) removalOrder.Add(level.stickers.Count - 4 + i);
            }
            for (int i = 0; i < level.stickers.Count; i++) if (!removalOrder.Contains(i)) removalOrder.Add(i);
            for (int rank = 0; rank < removalOrder.Count; rank++) level.stickers[removalOrder[rank]].sortingOrder = level.stickers.Count - rank;
            if (level.levelNumber >= 6)
            {
                var locked = level.stickers[7]; var tray = level.piles.Find(x => x.id == locked.pileId);
                locked.size = shallow ? new Vector2(210, 172) : new Vector2(240, 216);
                locked.position = tray.position + (tray.size.x < 500 ? new Vector2(65, 0) : level.levelNumber == 7 ? new Vector2(0, -20) : Vector2.zero);
                locked.rotation = tray.rotation; locked.sortingOrder = level.stickers.Count + 1;
                if (tray.size.x >= 500)
                    foreach (var other in level.stickers)
                        if (other != locked && other.pileId == tray.id && Mathf.Abs(other.position.x - locked.position.x) < 190 && Mathf.Abs(other.position.y - locked.position.y) < 180)
                            other.position = new Vector2(tray.position.x + (other.position.x < tray.position.x ? -260 : 260), other.position.y);
            }
            // Keep the question-mark half exposed under exactly one ordinary cover.
            foreach (var hidden in level.stickers)
            {
                if (hidden.special.kind != SpecialKind.Mystery) continue;
                var tray = level.piles.Find(x => x.id == hidden.pileId);
                float y = hidden.position.y;
                StickerPlacement cover = null;
                foreach (var other in level.stickers)
                    if (other != hidden && other.pileId == hidden.pileId && other.special.kind == SpecialKind.None && Mathf.Abs(other.position.y - y) < 100)
                    { cover = other; break; }
                if (cover == null) continue;
                hidden.position = new Vector2(tray.position.x - 70, y); hidden.rotation = 0;
                foreach (var other in level.stickers)
                    if (other != hidden && other.pileId == hidden.pileId && Mathf.Abs(other.position.y - y) < 130)
                    { other.position = new Vector2(tray.position.x + 80, other.position.y); other.rotation = 0; }
                cover.position = new Vector2(tray.position.x + 35, y); cover.sortingOrder = level.stickers.Count + 3;
            }
        }
        static void Add(LevelDefinition level, List<Vector2> anchors, List<int> owners, Vector2 position, Vector2 size, float angle, Vector2[] groups = null)
        {
            int owner = level.piles.Count;
            level.piles.Add(new BoardPileDefinition { id = "tray_" + owner, position = position, size = size, rotation = angle, color = new Color(.86f, .77f, .65f) });
            if (groups == null) { anchors.Add(position); owners.Add(owner); }
            else foreach (var group in groups) { anchors.Add(position + group); owners.Add(owner); }
        }
        [MenuItem("Tools/Sticker Category Game/Apply Overlapping Sample Piles")]
        public static void ApplyAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { PrototypeBootstrapTool.Root + "/Data/Levels" }))
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Undo.RecordObject(level, "Arrange overlapping sticker piles"); Apply(level); EditorUtility.SetDirty(level);
            }
            AssetDatabase.SaveAssets(); PrototypeBootstrapTool.ValidateAll();
        }
    }
}
