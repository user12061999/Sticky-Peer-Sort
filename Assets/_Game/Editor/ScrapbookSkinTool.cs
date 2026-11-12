using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort.Editor
{
    // Applies the partner's GUI v01 ("scrapbook") assets and mockup layout (1080x1920) to the shipping gameplay scene.
    // Gameplay logic and prefabs are untouched: runtime cards, holding slots and sticker trays switch on theme.scrapbook.
    // Safe to run repeatedly. To go back: untick theme.scrapbook and restore the scene from git (or re-run the SuperCasual tool).
    public static class ScrapbookSkinTool
    {
        const string Art = "Assets/_Game/Art/GUI_Scrapbook/";
        const string ScenePath = "Assets/_Game/Scenes/StickerCategoryPrototype.unity";
        const string ThemePath = "Assets/_Game/ReferenceStyle/ReferenceTheme.asset";
        const string SilhouetteMaterialPath = Art + "UISilhouette.mat";
        static readonly Color Ink = new Color32(0x16, 0x22, 0x41, 255);

        static Sprite S(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
            if (sprite == null) throw new FileNotFoundException("Missing GUI v01 sprite: " + Art + name + ".png");
            return sprite;
        }

        [MenuItem("Tools/Sticker Category Game/Apply GUI v01 Scrapbook Skin")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before applying the skin.");
            ImportSettings();
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUITheme>(ThemePath);
            AssignTheme(theme);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var ui = Object.FindFirstObjectByType<GameplayUI>();
            if (ui == null) throw new System.InvalidOperationException("GameplayUI not found in " + ScenePath);
            ui.theme = theme;
            SkinScene(ui);
            EditorUtility.SetDirty(ui); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("GUI v01 scrapbook skin applied to " + ScenePath);
        }

        // Handoff guide: Sprite (2D and UI), Single, Full Rect, PPU 100, no mipmaps, clamp, bilinear; 9-slice borders L/B/R/T.
        public static void ImportSettings()
        {
            foreach (var file in Directory.GetFiles(Art, "*.png"))
            {
                string path = file.Replace("\\", "/"), name = Path.GetFileNameWithoutExtension(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Vector4 border;
                switch (name)
                {
                    case "collection_sheet_base": border = Vector4.one * 84; break;
                    case "holding_slot_shallow": border = Vector4.one * 72; break;
                    case "sticker_surface_paper": border = Vector4.one * 48; break;
                    case "category_header_base_white": border = Vector4.one * 56; break;
                    case "ui_pill_base": border = new Vector4(80, 72, 80, 72); break;
                    default: border = Vector4.zero; break;
                }
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                bool same = importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && Mathf.Approximately(importer.spritePixelsPerUnit, 100) && !importer.mipmapEnabled && importer.wrapMode == TextureWrapMode.Clamp
                    && importer.filterMode == FilterMode.Bilinear && importer.alphaIsTransparency && settings.spriteMeshType == SpriteMeshType.FullRect
                    && importer.spriteBorder == border;
                if (same) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear; importer.alphaIsTransparency = true;
                importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.spriteBorder = border; importer.SaveAndReimport();
            }
        }

        public static void AssignTheme(GameplayUITheme theme)
        {
            theme.scrapbook = true;
            theme.sheetBase = S("collection_sheet_base"); theme.headerBase = S("category_header_base_white");
            theme.pillBase = S("ui_pill_base"); theme.holdingSlot = S("holding_slot_shallow"); theme.stickerSurface = S("sticker_surface_paper");
            theme.tutorialHand = S("tutorial_hand");
            theme.popupCoinIcon = S("coin_icon"); theme.popupSettingsIcon = S("settings_icon");
            theme.popupUndoIcon = S("booster_undo_icon"); theme.popupSlotIcon = S("booster_add_slot_icon");
            var material = AssetDatabase.LoadAssetAtPath<Material>(SilhouetteMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("StickyPeerSort/UI Silhouette");
                if (shader == null) throw new FileNotFoundException("Missing shader StickyPeerSort/UI Silhouette");
                material = new Material(shader); AssetDatabase.CreateAsset(material, SilhouetteMaterialPath);
            }
            theme.silhouetteMaterial = material;
            EditorUtility.SetDirty(theme);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root) { var hit = Find(child, name); if (hit != null) return hit; }
            return null;
        }
        // position is in mockup coordinates (origin at the 1080x1920 centre); edge pins the node to the top (1) or bottom (0) of the layout.
        static RectTransform Place(Transform root, string name, Vector2 size, Vector2 position, float edge = -1)
        {
            var rect = Find(root, name) as RectTransform; if (rect == null) { Debug.LogWarning("Scrapbook skin: missing " + name); return null; }
            Pin(rect, size, position, edge); return rect;
        }
        const float Top = 1, Bottom = 0;
        static void Pin(RectTransform rect, Vector2 size, Vector2 position, float edge)
        {
            rect.sizeDelta = size;
            if (edge >= 0) { rect.anchorMin = rect.anchorMax = new Vector2(.5f, edge); position.y -= (edge - .5f) * 1920; }
            rect.anchoredPosition = position;
        }
        static void Hide(Transform root, string name) { var t = Find(root, name); if (t != null && t.TryGetComponent<Image>(out var image)) image.enabled = false; }
        static Image Picture(RectTransform rect, Sprite sprite, float multiplier = 0)
        {
            if (rect == null) return null;
            var image = rect.GetComponent<Image>(); if (image == null) return null;
            image.sprite = sprite; image.color = Color.white; image.enabled = true;
            if (multiplier > 0) { image.type = Image.Type.Sliced; image.fillCenter = true; image.pixelsPerUnitMultiplier = multiplier; }
            else { image.type = Image.Type.Simple; image.preserveAspect = true; }
            return image;
        }
        static void Words(TMP_Text text, float size, Color color, Vector2 dimensions, Vector2 position)
        {
            if (text == null) return;
            text.rectTransform.sizeDelta = dimensions; text.rectTransform.anchoredPosition = position;
            text.color = color; text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = Mathf.Round(size * .55f); text.fontSize = size;
            if (text.font != null) text.fontSharedMaterial = text.font.material;   // flat ink as in the mockup, no outline preset
        }

        static void SkinScene(GameplayUI ui)
        {
            var root = ui.transform;

            // Studio backdrop fills the screen at any aspect (cropped, not stretched); the procedural wood grain goes.
            var backdrop = Find(root, "Amber table background") as RectTransform;
            if (backdrop != null)
            {
                var image = backdrop.GetComponent<Image>(); image.sprite = S("gameplay_background_studio"); image.type = Image.Type.Simple;
                image.preserveAspect = false; image.color = Color.white;
                var fitter = backdrop.GetComponent<AspectRatioFitter>(); if (fitter == null) fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fitter.aspectRatio = 1080f / 1920f;
            }
            var grain = Find(root, "Wood grain"); if (grain != null) grain.gameObject.SetActive(false);

            // HUD: level pill, coin pill, settings tile.
            Picture(Place(root, "Level capsule rim", new Vector2(424, 114), new Vector2(-260, 881), Top), S("ui_pill_base"), 1.5f);
            Place(root, "Level capsule", new Vector2(424, 114), Vector2.zero); Hide(root, "Level capsule");
            Words(ui.levelLabel, 84, Ink, new Vector2(390, 104), new Vector2(0, 2));
            Picture(Place(root, "Coin capsule rim", new Vector2(270, 112), new Vector2(195, 880), Top), S("ui_pill_base"), 1.5f);
            Place(root, "Coin capsule", new Vector2(270, 112), Vector2.zero); Hide(root, "Coin capsule");
            Picture(Place(root, "Coin icon", new Vector2(104, 104), new Vector2(-73, 0)), S("coin_icon"));
            Words(ui.coins, 72, Ink, new Vector2(140, 104), new Vector2(62, 0));
            if (ui.settings != null)
            {
                Picture(Place(root, ui.settings.name, new Vector2(119, 114), new Vector2(414, 881), Top), S("collection_sheet_base"), 2.4f);
                Picture(Place(ui.settings.transform, "Settings icon", new Vector2(80, 80), new Vector2(0, 1)), S("settings_icon"));
            }

            // Collection sheets row (cards themselves are built at runtime).
            if (ui.categories != null) Pin(ui.categories, new Vector2(1040, 497), new Vector2(0, 530), Top);

            // Holding slots: no tray frame, the shallow slots sit on the backdrop.
            Place(root, "Waiting tray panel", new Vector2(1040, 220), new Vector2(0, 148), Top); Hide(root, "Waiting tray panel");
            Place(root, "Tray surface", new Vector2(1040, 220), Vector2.zero); Hide(root, "Tray surface");
            if (ui.buffer != null) { ui.buffer.sizeDelta = new Vector2(1040, 220); ui.buffer.anchoredPosition = Vector2.zero; }

            // Board: the paper strips are the trays themselves; the wooden board frame goes.
            Hide(root, "Board shadow"); Hide(root, "Board rim"); Hide(root, "Board well");
            if (ui.board != null) { ui.board.anchoredPosition = new Vector2(2, -311.5f); ui.board.localScale = Vector3.one * .8f; }

            // Boosters left to right as in the mockup (owner 11/11): Undo (blue), Hint (peach), + Slot (mint), Mix (lavender).
            Hide(root, "Booster dock");
            var boosters = new[] { ui.undo, ui.hint, ui.extra, ui.shuffle };
            var bases = new[] { "button_base_blue_B", "button_base_peach_B", "button_base_mint_B", "button_base_lavender_B" };
            var icons = new[] { "booster_undo_icon", "booster_hint_icon", "booster_add_slot_icon", "booster_mix_icon" };
            var x = new[] { -366f, -122f, 121f, 365f };
            for (int i = 0; i < boosters.Length; i++)
            {
                var button = boosters[i]; if (button == null) continue;
                Picture(Place(root, button.name, new Vector2(202, 188), new Vector2(x[i], -786), Bottom), S(bases[i]));
                Picture(Place(button.transform, "Booster icon", new Vector2(128, 128), Vector2.zero), S(icons[i]));
                Picture(Place(button.transform, "Inventory badge", new Vector2(78, 60), new Vector2(66, 82)), S("ui_pill_base"), 2.8f);
                var badge = Find(button.transform, "Inventory badge");
                if (badge != null) Words(badge.GetComponentInChildren<TextMeshProUGUI>(true), 38, Ink, new Vector2(70, 56), Vector2.zero);
                var label = Find(button.transform, "Label");
                if (label != null) Words(label.GetComponent<TextMeshProUGUI>(), 42, Ink, new Vector2(218, 55), new Vector2(0, -132));
            }
            // RESTART is not in the mockup and there is no free spot for it; hidden until the owner decides (one line to restore).
            if (ui.restart != null) ui.restart.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
        }
    }
}
