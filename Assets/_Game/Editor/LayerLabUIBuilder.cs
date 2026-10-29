using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace StickyPeerSort.Editor
{
    public static class LayerLabUIBuilder
    {
        const string Pack = "Assets/Layer Lab/GUI Pro-SuperCasual/ResourcesData/";
        const string Components = Pack + "Sprites/Components/";
        const string ThemePath = "Assets/_Game/UI/LayerLabTheme.asset";
        public static bool IsAvailable => AssetDatabase.IsValidFolder(Pack.TrimEnd('/'));
        static GameplayUITheme theme;
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
        static Sprite Sprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Components + path);
            if (sprite == null) throw new InvalidOperationException("Layer Lab sprite missing: " + path);
            return sprite;
        }
        static GameplayUITheme LoadTheme()
        {
            Directory.CreateDirectory("Assets/_Game/UI"); AssetDatabase.Refresh();
            theme = AssetDatabase.LoadAssetAtPath<GameplayUITheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<GameplayUITheme>(); AssetDatabase.CreateAsset(theme, ThemePath);
                theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Pack + "Fonts/Sen SDF.asset");
                theme.roundedPanel = Sprite("Frame/BasicFrame_Round24.png");
                theme.cardFrame = theme.trayFrame = theme.roundedPanel;
                theme.slotFrame = Sprite("Frame/BasicFrame_Round17.png");
                theme.buttonFrame = Sprite("Button/Button_Round03_WhiteBg.png");
                theme.buttonHighlight = Sprite("Button/Button_Round03_White_Light.png");
                theme.coinIcon = Sprite("IconMisc/Icon_Coin.png"); theme.settingsIcon = Sprite("IconMisc/Icon_Setting.png");
                theme.starIcon = Sprite("IconMisc/GradeIcon_Star_l_Yellow.png"); theme.failIcon = Sprite("IconMisc/Icon_Close02.png");
                theme.collectionIcon = Sprite("IconMisc/Icon_Memo.png");
                theme.undoIcon = Sprite("Icon_PictoIcons/128/PictoIcon_Back_2.Png");
                theme.hintIcon = Sprite("Icon_PictoIcons/128/PictoIcon_Bulb.Png");
                theme.extraIcon = Sprite("Icon_PictoIcons/128/PictoIcon_Plus_1.Png");
                theme.shuffleIcon = Sprite("Icon_PictoIcons/128/PictoIcon_Change.Png");
                theme.background = Hex("E5A13D"); theme.ink = Hex("422512"); theme.muted = Hex("754218");
                theme.paper = Hex("FFF0BF"); theme.accent = Hex("169FE7"); theme.mint = Hex("61BB28");
                EditorUtility.SetDirty(theme);
            }
            if (theme.keyIcon == null) theme.keyIcon = Sprite("Icon_ItemIcons/128/ItemIcon_Key_Gold.png");
            if (theme.lockIcon == null) theme.lockIcon = Sprite("IconMisc/Icon_Lock02.png");
            if (theme.tutorialHand == null) theme.tutorialHand = Sprite("Icon_TutorialHand/256/tutorial_hand_1.png");
            EditorUtility.SetDirty(theme); WarmTextMaterials.Ensure(theme);
            return theme;
        }
        static Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color, Sprite sprite = null)
        { return UIFactory.Panel(name, parent, size, position, color, sprite != null ? sprite : theme.roundedPanel); }
        static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Vector2 dimensions, Vector2 position, Color? color = null)
        {
            var text = UIFactory.Text(name, parent, value, size, dimensions, position, color ?? theme.ink);
            text.font = theme.font; return text;
        }
        static void Outline(TextMeshProUGUI text)
        { theme.ApplyTextStyle(text); }
        static Image Icon(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position, Color? tint = null)
        {
            var image = UIFactory.Panel(name, parent, size, position, tint ?? Color.white, sprite);
            image.type = Image.Type.Simple; image.preserveAspect = true; return image;
        }
        static Button Button(string name, Transform parent, string label, Vector2 size, Vector2 position, Color color)
        {
            var button = UIFactory.Button(name, parent, label, size, position, color, theme.buttonFrame);
            var text = button.GetComponentInChildren<TextMeshProUGUI>(); text.font = theme.font; text.fontSize = 29;
            button.gameObject.AddComponent<UIButtonPressFeedback>();
            var colors = button.colors; colors.highlightedColor = Color.white; colors.pressedColor = new Color(.86f, .9f, 1); colors.disabledColor = new Color(.9f, .86f, .78f); colors.fadeDuration = .07f; button.colors = colors;
            return button;
        }
        static Button Booster(string name, Transform parent, string label, Sprite icon, float x, Color color, out TextMeshProUGUI counter)
        {
            var button = Button(name, parent, label, new Vector2(164, 112), new Vector2(x, -765), color);
            button.gameObject.AddComponent<CanvasGroup>();
            var title = button.GetComponentInChildren<TextMeshProUGUI>(); title.fontSize = 24;
            title.color = Color.white; Outline(title);
            title.rectTransform.sizeDelta = new Vector2(200, 42); title.rectTransform.anchoredPosition = new Vector2(0, -84);
            Icon("Booster icon", button.transform, icon, new Vector2(62, 62), new Vector2(0, 2));
            var badge = Panel("Inventory badge", button.transform, new Vector2(57, 43), new Vector2(64, 44), theme.ink);
            counter = Text("Count TMP", badge.transform, "10", 23, new Vector2(55, 38), Vector2.zero, Color.white);
            return button;
        }
        static GameObject PanelPrefab(string name, Sprite sprite, Color color)
        {
            var panel = UIFactory.Panel(name, null, new Vector2(280, 280), Vector2.zero, color, sprite);
            if (name == "BufferSlot")
            {
                panel.color = theme.trayRim;
                var well = Panel("Recessed well", panel.transform, Vector2.zero, Vector2.zero, theme.trayWell);
                well.rectTransform.anchorMin = Vector2.zero; well.rectTransform.anchorMax = Vector2.one;
                well.rectTransform.offsetMin = new Vector2(7, 7); well.rectTransform.offsetMax = new Vector2(-7, -7);
                var shadow = panel.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.25f, .1f, .01f, .35f); shadow.effectDistance = new Vector2(0, -5);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(panel.gameObject, "Assets/_Game/Prefabs/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(panel.gameObject); return prefab;
        }
        [MenuItem("Tools/Sticker Category Game/Redesign UI with Layer Lab")]
        public static void Apply() { Apply(true); }
        [MenuItem("Tools/Sticker Category Game/Apply Warm Arcade Palette")]
        public static void ApplyWarmPalette()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before changing the palette.");
            theme = LoadTheme();
            theme.background = Hex("E5A13D"); theme.ink = Hex("422512"); theme.muted = Hex("754218");
            theme.paper = Hex("FFF0BF"); theme.accent = Hex("169FE7"); theme.mint = Hex("61BB28");
            theme.trayRim = Hex("FFD477"); theme.trayWell = Hex("BF7E2B"); theme.emptySlot = Hex("EFCC83");
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets(); Apply(true);
        }
        public static void Apply(bool createBackup)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before redesigning the scene UI.");
            if (!IsAvailable) throw new InvalidOperationException("GUI Pro-SuperCasual is not imported.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PrototypeBootstrapTool.ScenePath) scene = EditorSceneManager.OpenScene(PrototypeBootstrapTool.ScenePath);
            var controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (controller == null || controller.levels == null || controller.levels.Length == 0) throw new InvalidOperationException("Build the gameplay prototype first.");
            if (createBackup)
            {
                Directory.CreateDirectory("Assets/_Game/Scenes/Backups"); AssetDatabase.Refresh();
                const string backup = "Assets/_Game/Scenes/Backups/StickerCategoryPrototype_BeforeLayerLab.unity";
                EditorSceneManager.SaveScene(scene);
                if (!File.Exists(backup)) AssetDatabase.CopyAsset(scene.path, backup);
            }
            theme = LoadTheme(); var oldUI = controller.ui;
            var sticker = oldUI.stickerPrefab;
            var card = PanelPrefab("CategoryCard", theme.cardFrame, theme.ink);
            var slot = PanelPrefab("BufferSlot", theme.slotFrame, theme.trayWell);
            var canvas = new GameObject("Sticker Studio - Warm Arcade", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
            var ui = canvas.AddComponent<GameplayUI>(); ui.theme = theme; ui.panelSprite = theme.roundedPanel;
            ui.stickerPrefab = sticker; ui.cardPrefab = card; ui.slotPrefab = slot;
            var bg = UIFactory.Panel("Amber table background", canvas.transform, Vector2.zero, Vector2.zero, theme.background);
            bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one; bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            ui.safeArea = UIFactory.Rect("Safe Area", canvas.transform, Vector2.zero, Vector2.zero);
            ui.safeArea.anchorMin = Vector2.zero; ui.safeArea.anchorMax = Vector2.one; ui.safeArea.offsetMin = ui.safeArea.offsetMax = Vector2.zero;
            var root = UIFactory.Rect("Portrait Layout", ui.safeArea, new Vector2(1080, 1920), Vector2.zero);
            // Warm wooden table inspired by the reference palette.
            var grain = UIFactory.Rect("Wood grain", root, new Vector2(1080, 1920), Vector2.zero).gameObject.AddComponent<UIWoodGrain>();
            grain.raycastTarget = false;
            // TOP BAR: Level capsule (center), Coin capsule (right), Settings button (far right)
            // No brand logo on top left per user request.
            var levelCapsuleRim = Panel("Level capsule rim", root, new Vector2(284, 80), new Vector2(0, 845), Hex("E5A93D"));
            var levelCapsule = Panel("Level capsule", levelCapsuleRim.transform, new Vector2(274, 70), Vector2.zero, Hex("1D2545"));
            ui.levelLabel = Text("Level TMP", levelCapsule.transform, "Level 1", 38, new Vector2(260, 60), Vector2.zero, Color.white);
            ui.levelLabel.fontStyle = FontStyles.Bold;
            ui.levelLabel.alignment = TextAlignmentOptions.Center;
            Outline(ui.levelLabel);

            var coinCapsuleRim = Panel("Coin capsule rim", root, new Vector2(204, 76), new Vector2(330, 845), Hex("E5A93D"));
            var coinCapsule = Panel("Coin capsule", coinCapsuleRim.transform, new Vector2(196, 68), Vector2.zero, Hex("1D2545"));
            Icon("Coin icon", coinCapsule.transform, theme.coinIcon, new Vector2(56, 56), new Vector2(-55, 0));
            ui.coins = Text("Coins TMP", coinCapsule.transform, "0", 32, new Vector2(110, 60), new Vector2(26, 0), Color.white);
            ui.coins.fontStyle = FontStyles.Bold;
            Outline(ui.coins);

            ui.settings = Button("Settings", root, "", new Vector2(76, 76), new Vector2(468, 845), Color.white);
            Icon("Settings icon", ui.settings.transform, theme.settingsIcon, new Vector2(52, 52), Vector2.zero);

            // Hidden HUD holders for legacy progress/album bindings (no display, prevents NRE)
            var hiddenHud = UIFactory.Rect("Hidden HUD", root, Vector2.zero, Vector2.zero);
            hiddenHud.gameObject.SetActive(false);
            ui.albumFill = Panel("Album fill", hiddenHud, Vector2.zero, Vector2.zero, theme.mint);
            ui.progress = Text("Album progress TMP", hiddenHud, "", 1, Vector2.zero, Vector2.zero);

            // CATEGORY CARDS (just below top bar)
            ui.categories = UIFactory.Rect("Category Cards", root, new Vector2(980, 320), new Vector2(0, 620));

            // WAITING TRAY (below cards)
            var trayPanel = Panel("Waiting tray panel", root, new Vector2(960, 140), new Vector2(0, 370), Hex("522A09"));
            ui.trayPanel = trayPanel;
            var trayInner = Panel("Tray surface", trayPanel.transform, new Vector2(946, 126), Vector2.zero, Hex("7D4514"));
            ui.buffer = UIFactory.Rect("Waiting Slots", trayInner.transform, new Vector2(920, 116), Vector2.zero);

            // MAIN BOARD: Sunken wooden well
            Panel("Board shadow", root, new Vector2(990, 890), new Vector2(0, -190), Hex("3A1D06"));
            var boardRim = Panel("Board rim", root, new Vector2(976, 876), new Vector2(0, -190), Hex("5C300B"));
            Panel("Board well", boardRim.transform, new Vector2(956, 856), Vector2.zero, new Color(1f, .82f, .42f, .18f));
            ui.board = UIFactory.Rect("Sticker Board", root, new Vector2(940, 840), new Vector2(0, -190));

            // BOOSTER DOCK (bottom bar)
            Panel("Booster dock", root, new Vector2(1000, 154), new Vector2(0, -780), Hex("6E3E11"));
            ui.undo = Booster("Undo", root, "UNDO", theme.undoIcon, -348, Hex("1F9CEB"), out ui.undoCount);
            ui.hint = Booster("Hint", root, "HINT", theme.hintIcon, -116, Hex("FFAC18"), out ui.hintCount);
            ui.extra = Booster("Extra Slot", root, "+ SLOT", theme.extraIcon, 116, Hex("61C62D"), out ui.extraCount);
            ui.shuffle = Booster("Shuffle", root, "MIX", theme.shuffleIcon, 348, Hex("A04BE3"), out ui.shuffleCount);
            ui.restart = UIFactory.Button("Restart", root, "RESTART", new Vector2(220, 36), new Vector2(0, -870), Hex("E5BA56"), theme.roundedPanel);
            var restartText = ui.restart.GetComponentInChildren<TextMeshProUGUI>(); restartText.font = theme.font; restartText.fontSize = 17; restartText.color = theme.ink;
            ui.restart.gameObject.AddComponent<UIButtonPressFeedback>();
            ui.effects = UIFactory.Rect("Animation Layer", root, new Vector2(1080, 1920), Vector2.zero);
            BuildModal(ui, root);
            controller.ui = ui;
            if (oldUI != null) UnityEngine.Object.DestroyImmediate(oldUI.gameObject);
            ui.ApplySafeArea();
            // Persist an authoring preview. Play mode clears it and builds the real independent model.
            ui.Initialize(new PreviewModel(controller.levels[0], controller.balance), new PreviewInput()); ui.Render();
            EditorUtility.SetDirty(controller); EditorUtility.SetDirty(ui); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Layer Lab UI applied. Gameplay data, sticker artwork and input configuration were preserved.");
        }
        static void BuildModal(GameplayUI ui, Transform root)
        {
            var dim = UIFactory.Panel("Modal", root, new Vector2(1080, 1920), Vector2.zero, new Color(.16f, .07f, .015f, .78f));
            dim.raycastTarget = true; ui.modal = dim.gameObject;
            var dialog = Panel("Modal frame", dim.transform, new Vector2(858, 790), Vector2.zero, theme.ink);
            Panel("Modal paper", dialog.transform, new Vector2(844, 770), new Vector2(0, 3), theme.paper);
            Panel("Badge halo", dialog.transform, new Vector2(205, 205), new Vector2(0, 318), Hex("FFCC4E"));
            ui.modalIcon = Icon("Modal icon", dialog.transform, theme.starIcon, new Vector2(154, 154), new Vector2(0, 322));
            ui.sound = Button("Sound toggle", dialog.transform, "SOUND: ON", new Vector2(190, 55), new Vector2(300, 320), Hex("FFCA43"));
            ui.sound.GetComponentInChildren<TextMeshProUGUI>().fontSize = 19;
            ui.modalTitle = Text("Title TMP", dialog.transform, "NICE SORTING!", 43, new Vector2(750, 130), new Vector2(0, 157));
            ui.modalBody = Text("Body TMP", dialog.transform, "Your collection is complete.", 29, new Vector2(710, 160), new Vector2(0, 18), theme.muted);
            ui.primary = Button("Primary", dialog.transform, "NEXT LEVEL", new Vector2(640, 112), new Vector2(0, -143), Hex("67CE29"));
            ui.secondary = Button("Secondary", dialog.transform, "RETRY", new Vector2(640, 94), new Vector2(0, -277), Hex("FFCA43"));
            ui.modal.SetActive(false);
        }
        sealed class PreviewModel : IGameplayReadModel
        {
            public GameSession Session { get; }
            public SaveData Progress { get; } = new SaveData();
            public GameBalanceConfig Balance { get; }
            public PreviewModel(LevelDefinition level, GameBalanceConfig balance) { Session = new GameSession(level); Balance = balance; }
        }
        sealed class PreviewInput : IStickerInput { public void Tap(int index) { } public void TapAt(Vector2 point) { } }
    }
}
