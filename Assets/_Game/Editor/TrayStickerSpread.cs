using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    // Authoring only: distribute the existing pieces, leaving runtime rules and tray geometry intact.
    public static class TrayStickerSpread
    {
        [MenuItem("Tools/Sticker Category Game/Fill Current Scene Trays with Dense Sticker Piles")]
        public static void ApplyToCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (controller == null) throw new InvalidOperationException("Open a gameplay scene first.");
            var staged = new List<LevelDefinition>();
            try
            {
                // Solve every staged layout before committing any of the scene's level assets.
                foreach (var source in controller.levels)
                {
                    if (!AssetDatabase.GetAssetPath(source).StartsWith(ReferenceStyleBuilder.Root + "/")) throw new InvalidOperationException("Open SampleStickerLevel to rebuild its dense sample content.");
                    var copy = UnityEngine.Object.Instantiate(source); copy.name = source.name; staged.Add(copy); DenseLevelContent.Expand(copy); Arrange(copy);
                    var errors = LevelValidator.Validate(copy);
                    if (errors.Count > 0) throw new InvalidOperationException(source.name + ": " + string.Join("; ", errors));
                }
                string backup = "UserSettings/StickyPeerSortBeforeDenseTrayFill/" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                Directory.CreateDirectory(backup);
                for (int i = 0; i < staged.Count; i++)
                {
                    var source = controller.levels[i]; string path = AssetDatabase.GetAssetPath(source);
                    string previous = backup + "/" + Path.GetFileName(path);
                    if (!File.Exists(previous)) File.Copy(path, previous);
                    string name = staged[i].name; Undo.RecordObject(source, "Fill trays with dense sticker piles");
                    EditorUtility.CopySerialized(staged[i], source); source.name = name; EditorUtility.SetDirty(source);
                }
                foreach (string path in new[] { "Assets/_Game/UI/LayerLabTheme.asset", ReferenceStyleBuilder.Root + "/ReferenceTheme.asset" })
                {
                    var theme = AssetDatabase.LoadAssetAtPath<GameplayUITheme>(path);
                    if (theme == null) continue;
                    theme.coveredOpacity = .68f; EditorUtility.SetDirty(theme);
                }
                AssetDatabase.SaveAssets(); LevelPreviewWindow.Preview(0);
                Debug.Log("Dense sticker fill applied to " + staged.Count + " levels. All solved; covered darkness retained at 68%.");
            }
            finally { foreach (var copy in staged) UnityEngine.Object.DestroyImmediate(copy); }
        }

        public static void Arrange(LevelDefinition level)
        {
            alphaBounds.Clear();
            DenseStickerLayout.Arrange(level);
        }
        static Vector2 Rotate(Vector2 p, float angle)
        { float r = angle * Mathf.Deg2Rad; return new Vector2(p.x * Mathf.Cos(r) - p.y * Mathf.Sin(r), p.x * Mathf.Sin(r) + p.y * Mathf.Cos(r)); }
        static readonly Dictionary<StickerDefinition, Rect> alphaBounds = new Dictionary<StickerDefinition, Rect>();
        static Rect AlphaBounds(StickerDefinition definition)
        {
            if (alphaBounds.TryGetValue(definition, out var cached)) return cached;
            const int resolution = 96;
            Vector2 min = Vector2.one, max = Vector2.zero;
            for (int y = 0; y <= resolution; y++) for (int x = 0; x <= resolution; x++)
            {
                Vector2 uv = new Vector2((float)x / resolution, (float)y / resolution);
                if (!definition.ContainsUV(uv)) continue;
                min = Vector2.Min(min, uv); max = Vector2.Max(max, uv);
            }
            if (min.x > max.x) throw new InvalidOperationException("No opaque artwork: " + definition.name);
            min = Vector2.Max(Vector2.zero, min - Vector2.one / resolution);
            max = Vector2.Min(Vector2.one, max + Vector2.one / resolution);
            var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y); alphaBounds[definition] = rect; return rect;
        }
        internal static Rect VisibleBounds(StickerPlacement piece, BoardPileDefinition tray)
        {
            var alpha = AlphaBounds(piece.definition);
            Vector2 fit = piece.definition.FitSize(piece.size) * piece.scale;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (float x in new[] { alpha.xMin, alpha.xMax }) foreach (float y in new[] { alpha.yMin, alpha.yMax })
            {
                Vector2 point = Rotate(new Vector2((x - .5f) * fit.x, (y - .5f) * fit.y), piece.rotation - tray.rotation);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        internal static void Clamp(StickerPlacement piece, BoardPileDefinition tray)
        {
            Vector2 local = Rotate(piece.position - tray.position, -tray.rotation);
            var visible = VisibleBounds(piece, tray);
            Vector2 min = -tray.size * .5f + Vector2.one * 20 - visible.min;
            Vector2 max = tray.size * .5f - Vector2.one * 20 - visible.max;
            local.x = Mathf.Clamp(local.x, min.x, max.x); local.y = Mathf.Clamp(local.y, min.y, max.y);
            piece.position = tray.position + Rotate(local, tray.rotation);
        }
    }
}
