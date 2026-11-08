using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort.Editor
{
    // Re-skins the shipping gameplay scene and its prefabs with GUI Pro-SuperCasual sprites in the warm honey-wood palette:
    // black-outlined frames, ribbon popup titles, glossy pill buttons and red counter badges. Safe to run repeatedly.
    public static class SuperCasualSkinTool
    {
        const string Pack = "Assets/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/";
        const string ScenePath = "Assets/_Game/Scenes/StickerCategoryPrototype.unity";
        const string Prefabs = "Assets/_Game/ReferenceStyle/Prefabs/";
        const string ThemePath = "Assets/_Game/ReferenceStyle/ReferenceTheme.asset";
        static readonly List<Image> skinned = new List<Image>();

        static Sprite S(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Pack + path + ".png");
            if (sprite == null) throw new System.IO.FileNotFoundException("Missing GUI Pro sprite: " + Pack + path);
            return sprite;
        }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

        [MenuItem("Tools/Sticker Category Game/Apply GUI Pro-SuperCasual Skin")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before applying the skin.");
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUITheme>(ThemePath);
            AssignSprites(theme);
            SkinTrayPrefab(theme); SkinSlotPrefab(theme);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var ui = Object.FindFirstObjectByType<GameplayUI>();
            if (ui == null) throw new System.InvalidOperationException("GameplayUI not found in " + ScenePath);
            ui.theme = theme;
            skinned.Clear();
            SkinScene(ui, theme);
            Canvas.ForceUpdateCanvases(); foreach (var image in skinned) UISkin.Fit(image);
            EditorUtility.SetDirty(ui); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("GUI Pro-SuperCasual skin applied to " + ScenePath);
        }

        public static void AssignSprites(GameplayUITheme theme)
        {
            theme.outlinePanel = S("Popup/Popup_OutTitle_Bg");
            theme.innerPanel = S("Frame/CardFrame04_Bg");
            theme.titleRibbon = S("Label/Title_Ribbon01_Orange");
            theme.failRibbon = S("Label/Title_Ribbon01_Red");
            theme.badgeBurst = S("Label/Label_Badge01_Yellow");
            theme.counterBadge = S("UI_Etc/Alert_Circle_s_Bg");
            theme.primaryButton = S("Button/Button01_l_Green");
            theme.secondaryButton = S("Button/Button01_l_Yellow");
            theme.smallButton = S("Button/Button01_s_Yellow");
            theme.boosterButtons = new[] { S("Button/Button01_s_Blue"), S("Button/Button01_s_Yellow"), S("Button/Button01_s_Green"), S("Button/Button01_s_Purple") };
            EditorUtility.SetDirty(theme);
        }

        static void Inset(Transform parent, string name, float inset)
        {
            var child = parent.Find(name) as RectTransform; if (child == null) return;
            child.anchorMin = Vector2.zero; child.anchorMax = Vector2.one; child.offsetMin = Vector2.one * inset; child.offsetMax = Vector2.one * -inset;
            // Keep the corners concentric with the outlined root frame.
            UISkin.Concentric(parent.GetComponent<Image>(), child.GetComponent<Image>(), inset);
            foreach (Transform grand in child)
            {
                var g = grand.GetComponent<Image>();
                if (g != null && g.type == Image.Type.Sliced && g.sprite == child.GetComponent<Image>().sprite) g.pixelsPerUnitMultiplier = child.GetComponent<Image>().pixelsPerUnitMultiplier;
            }
        }
        static void SkinTrayPrefab(GameplayUITheme theme)
        {
            string path = Prefabs + "StickerTray.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            UISkin.Apply(root.GetComponent<Image>(), theme.outlinePanel, Hex("713B0D"));
            Inset(root.transform, "Golden bevel", 7); Inset(root.transform, "Inset shadow", 14); Inset(root.transform, "Wood well", 20);
            PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
        }
        static void SkinSlotPrefab(GameplayUITheme theme)
        {
            string path = Prefabs + "BufferSlot.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            UISkin.Apply(root.GetComponent<Image>(), theme.outlinePanel, theme.trayRim);
            Inset(root.transform, "Inset edge", 9); Inset(root.transform, "Recessed well", 13);
            var bevel = root.transform.Find("Top bevel"); if (bevel != null) Object.DestroyImmediate(bevel.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root) { var hit = Find(child, name); if (hit != null) return hit; }
            return null;
        }
        static Image Skin(Transform root, string name, Sprite sprite, Color color, Vector2? size = null, Vector2? position = null)
        {
            var t = Find(root, name); if (t == null) { Debug.LogWarning("Skin: missing " + name); return null; }
            var rect = (RectTransform)t;
            if (size.HasValue) rect.sizeDelta = size.Value;
            if (position.HasValue) rect.anchoredPosition = position.Value;
            var image = t.GetComponent<Image>(); UISkin.Apply(image, sprite, color); image.enabled = true; skinned.Add(image); return image;
        }
        static void Move(Transform root, string name, Vector2? size, Vector2 position)
        {
            var rect = Find(root, name) as RectTransform; if (rect == null) { Debug.LogWarning("Skin: missing " + name); return; }
            if (size.HasValue) rect.sizeDelta = size.Value; rect.anchoredPosition = position;
        }
        static void Hide(Transform root, string name) { var t = Find(root, name); if (t != null && t.TryGetComponent<Image>(out var image)) image.enabled = false; }
        static void Label(Button button, Color color, float min, float max, GameplayUITheme theme)
        {
            if (button == null) return;
            foreach (var text in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (string.IsNullOrEmpty(text.text) && text.name != "Label") continue;
                text.color = color; text.enableAutoSizing = true; text.fontSizeMin = min; text.fontSizeMax = max; text.fontStyle |= FontStyles.Bold;
                theme.ApplyTextStyle(text);
            }
        }

        static void SkinScene(GameplayUI ui, GameplayUITheme theme)
        {
            var root = ui.transform; var navy = Hex("1D2545"); var brown = Hex("5A2D0C");

            // Header: outlined navy capsules, big coin, yellow settings button.
            Skin(root, "Level capsule rim", theme.outlinePanel, navy, new Vector2(290, 84)); Hide(root, "Level capsule");
            Skin(root, "Coin capsule rim", theme.outlinePanel, navy, new Vector2(206, 80)); Hide(root, "Coin capsule");
            var coin = Find(root, "Coin icon") as RectTransform;
            if (coin != null) { var image = coin.GetComponent<Image>(); image.sprite = S("UI_Etc/ResourceBar_Single_Icon_Coin"); image.type = Image.Type.Simple; image.preserveAspect = true; coin.sizeDelta = new Vector2(66, 72); coin.anchoredPosition = new Vector2(-88, 2); }
            if (ui.settings != null)
            {
                Skin(root, ui.settings.name, theme.smallButton, Color.white, new Vector2(88, 88));
                var icon = Find(ui.settings.transform, "Settings icon") as RectTransform; if (icon != null) { icon.sizeDelta = new Vector2(52, 52); icon.anchoredPosition = new Vector2(0, 5); }
            }

            // Waiting tray, board and booster dock: black-outlined frames like the pack.
            Skin(root, "Waiting tray panel", theme.outlinePanel, Hex("FFD67D"));
            var surface = Find(root, "Tray surface") as RectTransform; if (surface != null) surface.sizeDelta = new Vector2(936, 118);
            Skin(root, "Board rim", theme.outlinePanel, Hex("5C300B"));
            Skin(root, "Booster dock", theme.outlinePanel, Hex("6E3E11"));

            // Boosters: glossy GUI Pro pill buttons with red counter badges.
            var boosters = new[] { ui.undo, ui.hint, ui.extra, ui.shuffle };
            for (int i = 0; i < boosters.Length; i++)
            {
                var button = boosters[i]; if (button == null) continue;
                Skin(root, button.name, theme.boosterButtons[i], Color.white, new Vector2(164, 118));
                var icon = Find(button.transform, "Booster icon") as RectTransform; if (icon != null) { icon.sizeDelta = new Vector2(66, 66); icon.anchoredPosition = new Vector2(0, 8); }
                var badge = Find(button.transform, "Inventory badge");
                if (badge != null)
                {
                    var image = badge.GetComponent<Image>(); image.sprite = theme.counterBadge; image.type = Image.Type.Simple; image.preserveAspect = true; image.color = Hex("F0433A");
                    ((RectTransform)badge).sizeDelta = new Vector2(52, 52); ((RectTransform)badge).anchoredPosition = new Vector2(66, 48);
                    var count = badge.GetComponentInChildren<TextMeshProUGUI>(true); if (count != null) { count.rectTransform.sizeDelta = new Vector2(46, 40); count.color = Color.white; }
                }
            }
            if (ui.restart != null) { Skin(root, ui.restart.name, theme.smallButton, Color.white, new Vector2(236, 58), new Vector2(0, -910)); Label(ui.restart, brown, 18, 26, theme); }

            // Breathing room (owner: UI overlapped and too tight): coin clears the settings button, slots sit inside the tray,
            // the board is a little smaller with a clear gap under the tray, booster captions sit inside the dock.
            Move(root, "Coin capsule rim", null, new Vector2(286, 845));
            Move(root, "Waiting tray panel", new Vector2(980, 164), new Vector2(0, 342));
            Move(root, "Tray surface", new Vector2(954, 140), Vector2.zero);
            Move(root, "Waiting Slots", new Vector2(940, 140), Vector2.zero);
            Move(root, "Board shadow", new Vector2(944, 852), new Vector2(0, -212));
            Move(root, "Board rim", new Vector2(930, 836), new Vector2(0, -205));
            Move(root, "Board well", new Vector2(910, 816), Vector2.zero);
            if (ui.board != null) { ui.board.anchoredPosition = new Vector2(0, -205); ui.board.localScale = Vector3.one * .95f; }
            Move(root, "Booster dock", new Vector2(1000, 196), new Vector2(0, -768));
            foreach (var button in boosters) if (button != null) ((RectTransform)button.transform).anchoredPosition = new Vector2(((RectTransform)button.transform).anchoredPosition.x, -742);

            // Popup: cream outlined panel, ribbon title (red for failures), badge burst behind the icon, pill buttons.
            Skin(root, "Modal frame", theme.outlinePanel, theme.paper, new Vector2(860, 800), Vector2.zero);
            Hide(root, "Modal paper");
            var frame = Find(root, "Modal frame");
            var ribbon = Find(frame, "Title ribbon");
            if (ribbon == null)
            {
                ribbon = UIFactory.Panel("Title ribbon", frame, Vector2.zero, Vector2.zero, Color.white, theme.titleRibbon).transform;
                var title = Find(frame, "Title TMP"); if (title != null) ribbon.SetSiblingIndex(title.GetSiblingIndex());
            }
            ui.modalRibbon = Skin(frame, "Title ribbon", theme.titleRibbon, Color.white, new Vector2(880, 160), new Vector2(0, 400));
            ui.modalRibbon.raycastTarget = false;
            var halo = Find(frame, "Badge halo") as RectTransform;
            if (halo != null)
            {
                var image = halo.GetComponent<Image>(); image.sprite = theme.badgeBurst; image.type = Image.Type.Simple; image.preserveAspect = true; image.color = Color.white;
                halo.sizeDelta = new Vector2(220, 220); halo.anchoredPosition = new Vector2(0, 205); halo.SetSiblingIndex(ribbon.GetSiblingIndex());
            }
            var badgeIcon = Find(frame, "Modal icon") as RectTransform;
            if (badgeIcon != null)
            {
                badgeIcon.sizeDelta = new Vector2(124, 124); badgeIcon.anchoredPosition = new Vector2(0, 208);
                if (halo != null) badgeIcon.SetSiblingIndex(halo.GetSiblingIndex() + 1);   // the icon sits on top of the burst
            }
            if (ui.modalTitle != null)
            {
                ui.modalTitle.rectTransform.sizeDelta = new Vector2(520, 80);   // keeps long titles off the ribbon folds ui.modalTitle.rectTransform.anchoredPosition = new Vector2(0, 418);
                ui.modalTitle.color = Color.white; ui.modalTitle.enableAutoSizing = true; ui.modalTitle.fontSizeMin = 28; ui.modalTitle.fontSizeMax = 48;
                theme.ApplyTextStyle(ui.modalTitle);
            }
            if (ui.modalBody != null)
            {
                ui.modalBody.rectTransform.sizeDelta = new Vector2(720, 150); ui.modalBody.rectTransform.anchoredPosition = new Vector2(0, 20);
                ui.modalBody.color = theme.ink; ui.modalBody.enableAutoSizing = true; ui.modalBody.fontSizeMin = 22; ui.modalBody.fontSizeMax = 32;
            }
            if (ui.primary != null) { Skin(frame, ui.primary.name, theme.primaryButton, Color.white, new Vector2(560, 124), new Vector2(0, -160)); Label(ui.primary, Color.white, 28, 44, theme); }
            if (ui.secondary != null) { Skin(frame, ui.secondary.name, theme.secondaryButton, Color.white, new Vector2(520, 108), new Vector2(0, -292)); Label(ui.secondary, brown, 24, 36, theme); }
            if (ui.sound != null) { Skin(frame, ui.sound.name, theme.smallButton, Color.white, new Vector2(196, 66), new Vector2(298, 205)); Label(ui.sound, brown, 16, 22, theme); }
        }
    }
}
