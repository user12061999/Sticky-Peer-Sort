using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort.Editor
{
    public static class ReferenceStyleBuilder
    {
        public const string Root = "Assets/_Game/ReferenceStyle";
        public const string ScenePath = "Assets/_Game/Scenes/SampleStickerLevel.unity";
        public static readonly string[] Names = { "Level_05_Grid4", "Level_12_Rows2", "Level_19_Mixed3", "Level_62_Pile1" };
        static GameplayUITheme theme;
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

        [MenuItem("Tools/Sticker Category Game/Reference Style/Generate Sample Scene and Four Levels")]
        public static void Generate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before generating sample assets.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var folder in new[] { "Art", "Data/Stickers", "Data/Levels", "Prefabs", "Validation" }) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            // A copy preserves the existing camera, light, audio/input configuration and composition root.
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(PrototypeBootstrapTool.ScenePath, ScenePath))
                throw new IOException("Could not create sample scene from the existing gameplay scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (controller == null) throw new InvalidOperationException("The source scene requires a GameController.");
            theme = AssetDatabase.LoadAssetAtPath<GameplayUITheme>(Root + "/ReferenceTheme.asset");
            if (theme == null)
            {
                theme = UnityEngine.Object.Instantiate(controller.ui.theme);
                theme.name = "ReferenceTheme"; AssetDatabase.CreateAsset(theme, Root + "/ReferenceTheme.asset");
            }
            theme.referenceComposition = true; theme.background = Hex("E6A141"); theme.paper = Hex("FFF1CC");
            if (theme.tutorialHand == null) theme.tutorialHand = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/Icon_TutorialHand/256/tutorial_hand_1.png");
            theme.trayRim = Hex("FFD07A"); theme.trayWell = Hex("C6812E"); theme.coveredOpacity = .68f; theme.coveredSoftness = .7f;
            theme.woodSurface = GenerateWood(); EditorUtility.SetDirty(theme);
            var definitions = LoadArtwork(); var levels = new LevelDefinition[4];
            for (int variant = 0; variant < 4; variant++) levels[variant] = CreateLevel(variant, definitions);
            var originalSticker = controller.ui.stickerPrefab;
            var piece = UnityEngine.Object.Instantiate(originalSticker); piece.name = "StickerPiece";
            var piecePrefab = PrefabUtility.SaveAsPrefabAsset(piece.gameObject, Root + "/Prefabs/StickerPiece.prefab").GetComponent<StickerView>();
            UnityEngine.Object.DestroyImmediate(piece.gameObject);
            var old = controller.ui; var ui = BuildView(piecePrefab);
            controller.ui = ui; controller.levels = levels; controller.progressKey = "StickyPeerSort.ReferenceStyle.Save.v1";
            // Sample tuning is isolated too; preserve the user's current gameplay balance.
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root + "/Data/ReferenceBalance.asset");
            if (balance == null) { balance = UnityEngine.Object.Instantiate(controller.balance); AssetDatabase.CreateAsset(balance, Root + "/Data/ReferenceBalance.asset"); }
            controller.balance = balance;
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            ui.ApplySafeArea();
            // Save the reusable view before adding the editor-only preview children.
            PrefabUtility.SaveAsPrefabAsset(ui.gameObject, Root + "/Prefabs/StickerLevelView.prefab");
            Preview(ui, levels[0], balance);
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var build = EditorBuildSettings.scenes.ToList();
            if (!build.Any(s => s.path == ScenePath)) { build.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = build.ToArray(); }
            LevelPreviewWindow.Open();
            Debug.Log("Reference style sample ready: " + ScenePath + "; four validated and solved levels; existing gameplay scene unchanged.");
        }

        static StickerDefinition[] LoadArtwork()
        {
            var result = new StickerDefinition[16];
            string[] categories = { "food", "animal", "plant", "vehicle" };
            for (int c = 0; c < 4; c++) for (int t = 0; t < 4; t++)
            {
                string id = categories[c] + "_" + (t + 1).ToString("00");
                var source = AssetDatabase.LoadAssetAtPath<StickerDefinition>("Assets/_Game/Data/Stickers/" + id + ".asset");
                if (source == null || !source.HasArtwork) throw new InvalidOperationException("Missing existing artwork: " + id);
                string path = Root + "/Data/Stickers/" + id + ".asset";
                var copy = AssetDatabase.LoadAssetAtPath<StickerDefinition>(path);
                if (copy == null) { copy = UnityEngine.Object.Instantiate(source); copy.name = id; AssetDatabase.CreateAsset(copy, path); }
                copy.showLabel = false; copy.tint = Color.white; copy.preserveAspect = true;
                StickerArtworkBaker.Bake(copy);
                copy.targetSilhouette = GenerateSilhouette(copy); EditorUtility.SetDirty(copy); result[c * 4 + t] = copy;
            }
            return result;
        }

        static Sprite GenerateSilhouette(StickerDefinition definition)
        {
            string path = Root + "/Art/" + definition.id + "_silhouette.png";
            if (!File.Exists(path))
            {
                const int size = 192; var mask = new bool[size * size]; var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) mask[y * size + x] = definition.ContainsUV(new Vector2((x + .5f) / size, (y + .5f) / size));
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    if (!mask[y * size + x]) continue;
                    bool edge = false;
                    for (int dy = -3; dy <= 3 && !edge; dy++) for (int dx = -3; dx <= 3; dx++)
                    {
                        int sx = x + dx, sy = y + dy;
                        if (sx < 0 || sx >= size || sy < 0 || sy >= size || !mask[sy * size + sx]) { edge = true; break; }
                    }
                    pixels[y * size + x] = edge ? Hex("FFFCEE") : Hex("F1BC70");
                }
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false); texture.SetPixels32(pixels); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            }
            return ImportSprite(path, false);
        }

        static Sprite GenerateWood()
        {
            string path = Root + "/Art/HoneyWood.png";
            if (!File.Exists(path))
            {
                const int size = 256; var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float grain = Mathf.Sin(y * .66f + Mathf.Sin(x * .035f) * .8f) * .018f
                        + Mathf.PerlinNoise(x * .024f, y * .36f) * .042f;
                    float light = .035f * Mathf.Cos((float)y / size * Mathf.PI * 2);
                    pixels[y * size + x] = new Color(.88f + grain + light, .54f + grain + light, .20f + grain * .7f + light, 1);
                }
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false); texture.SetPixels(pixels); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            }
            return ImportSprite(path, true);
        }
        static Sprite ImportSprite(string path, bool repeat)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.isReadable = false;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static LevelDefinition CreateLevel(int variant, StickerDefinition[] definitions)
        {
            string path = Root + "/Data/Levels/" + Names[variant] + ".asset";
            // Existing authored sample data is retained on subsequent Generate runs.
            var existing = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path); if (existing != null) return existing;
            var level = ScriptableObject.CreateInstance<LevelDefinition>(); level.name = Names[variant];
            level.levelNumber = new[] { 5, 12, 19, 62 }[variant]; level.activeCategoryCount = 3;
            level.bufferCapacity = variant == 3 ? 5 : 4; level.coinReward = 0;
            level.difficulty = new[] { "Overlap introduction", "Row planning", "Mixed compartments", "Special pile" }[variant];
            level.tutorial = variant == 3 ? "Clear FOOD for the key. Reveal ? by removing its cover." : "Pick a bright sticker. Complete a set to open the next album.";
            for (int c = 0; c < 4; c++)
            {
                var objective = new CategoryObjective { category = definitions[c * 4].category, targetCount = 4 };
                for (int t = 0; t < 4; t++) objective.exactTargets.Add(definitions[c * 4 + t]); level.objectives.Add(objective);
            }
            var anchors = new Vector2[4]; var owners = new int[4];
            if (variant == 0)
            {
                for (int t = 0; t < 4; t++)
                {
                    anchors[t] = new Vector2(t % 2 == 0 ? -230 : 230, t < 2 ? 210 : -210); owners[t] = t;
                    AddTray(level, anchors[t], new Vector2(430, 394));
                }
            }
            else if (variant == 1)
            {
                AddTray(level, new Vector2(0, 210), new Vector2(890, 380)); AddTray(level, new Vector2(0, -210), new Vector2(890, 380));
                for (int t = 0; t < 4; t++) { anchors[t] = new Vector2(t % 2 == 0 ? -220 : 220, t < 2 ? 210 : -210); owners[t] = t / 2; }
            }
            else if (variant == 2)
            {
                AddTray(level, new Vector2(-230, 215), new Vector2(430, 380)); AddTray(level, new Vector2(230, 215), new Vector2(430, 380));
                AddTray(level, new Vector2(0, -205), new Vector2(890, 390));
                for (int t = 0; t < 4; t++) { anchors[t] = new Vector2(t % 2 == 0 ? -230 : 230, t < 2 ? 215 : -205); owners[t] = t < 2 ? t : 2; }
            }
            else
            {
                AddTray(level, Vector2.zero, new Vector2(780, 810));
                for (int t = 0; t < 4; t++) anchors[t] = new Vector2(t % 2 == 0 ? -135 : 135, t < 2 ? 180 : -170);
            }
            Vector2[] offsets = { new Vector2(-52, -35), new Vector2(42, -32), new Vector2(-40, 42), new Vector2(42, 42) };
            for (int c = 0; c < 4; c++) for (int t = 0; t < 4; t++)
            {
                var p = new StickerPlacement { instanceId = Names[variant] + "_" + definitions[c * 4 + t].id,
                    definition = definitions[c * 4 + t], position = anchors[t] + offsets[c],
                    size = variant == 3 ? new Vector2(240, 230) : new Vector2(216, 208),
                    rotation = (t % 2 == 0 ? -1 : 1) * (5 + c * 3), sortingOrder = (4 - c) * 20 + t, pileId = "tray_" + owners[t] };
                level.stickers.Add(p);
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
                // A visible lock at the foot of the pile, away from FOOD's path to its key.
                level.stickers[7].position = new Vector2(220, -280); level.stickers[7].size = new Vector2(220, 210); level.stickers[7].rotation = 0; level.stickers[7].sortingOrder = 120;
                level.stickers[3].position = new Vector2(85, -40);
                level.stickers[10].special = new StickerSpecialConfig { kind = SpecialKind.Mystery };
                level.stickers[10].position = new Vector2(-225, -205); level.stickers[10].rotation = 0;
                level.stickers[2].position = new Vector2(-110, -170); level.stickers[2].rotation = 0;
            }
            DenseLevelContent.Expand(level); TrayStickerSpread.Arrange(level);
            var errors = LevelValidator.Validate(level); if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            AssetDatabase.CreateAsset(level, path); return level;
        }
        static void AddTray(LevelDefinition level, Vector2 position, Vector2 size)
        { level.piles.Add(new BoardPileDefinition { id = "tray_" + level.piles.Count, position = position, size = size, color = Hex("C6812E") }); }

        static Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        { return UIFactory.Panel(name, parent, size, position, color, theme.roundedPanel); }
        static void Stretch(RectTransform rect, float inset = 0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset; }
        static void SavePrefab(GameObject obj, string name) { PrefabUtility.SaveAsPrefabAsset(obj, Root + "/Prefabs/" + name + ".prefab"); }
        static Image Wood(Transform parent, string name, float inset, Color tint)
        {
            var wood = UIFactory.Panel(name, parent, Vector2.zero, Vector2.zero, tint, theme.woodSurface);
            wood.type = Image.Type.Tiled;
            Stretch(wood.rectTransform, inset); return wood;
        }
        static StickerTrayView TrayPrefab()
        {
            var rim = Panel("StickerTray", null, new Vector2(430, 394), Vector2.zero, Hex("713B0D"));
            var light = Panel("Golden bevel", rim.transform, Vector2.zero, Vector2.zero, Hex("FFCE72")); Stretch(light.rectTransform, 3);
            var inner = Panel("Inset shadow", rim.transform, Vector2.zero, Vector2.zero, Hex("985413")); Stretch(inner.rectTransform, 11);
            var well = Panel("Wood well", rim.transform, Vector2.zero, Vector2.zero, Hex("D8963B")); Stretch(well.rectTransform, 17);
            var mask = well.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = true;
            Wood(well.transform, "Well grain", 0, new Color(1, 1, 1, .46f));
            var shade = Panel("Upper inset shade", well.transform, Vector2.zero, Vector2.zero, new Color(.28f, .12f, .02f, .12f));
            shade.rectTransform.anchorMin = new Vector2(0, .88f); shade.rectTransform.anchorMax = Vector2.one; shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            var shadow = rim.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.3f, .13f, .02f, .28f); shadow.effectDistance = new Vector2(0, -5);
            rim.gameObject.AddComponent<StickerTrayView>(); SavePrefab(rim.gameObject, "StickerTray"); UnityEngine.Object.DestroyImmediate(rim.gameObject);
            return AssetDatabase.LoadAssetAtPath<StickerTrayView>(Root + "/Prefabs/StickerTray.prefab");
        }
        static GameObject BufferPrefab()
        {
            var rim = Panel("BufferSlot", null, new Vector2(164, 164), Vector2.zero, theme.trayRim);
            var edge = Panel("Inset edge", rim.transform, Vector2.zero, Vector2.zero, Hex("8B4B11")); Stretch(edge.rectTransform, 7);
            var well = Panel("Recessed well", rim.transform, Vector2.zero, Vector2.zero, theme.trayWell); Stretch(well.rectTransform, 11);
            var shadow = rim.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.3f, .13f, .02f, .28f); shadow.effectDistance = new Vector2(0, -6);
            var highlight = Panel("Top bevel", rim.transform, Vector2.zero, Vector2.zero, new Color(1, .96f, .77f, .6f));
            highlight.rectTransform.anchorMin = new Vector2(.12f, .94f); highlight.rectTransform.anchorMax = new Vector2(.88f, .96f); highlight.rectTransform.offsetMin = highlight.rectTransform.offsetMax = Vector2.zero;
            SavePrefab(rim.gameObject, "BufferSlot"); UnityEngine.Object.DestroyImmediate(rim.gameObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BufferSlot.prefab");
        }

        static GameplayUI BuildView(StickerView piecePrefab)
        {
            var canvas = new GameObject("StickerLevelView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
            var ui = canvas.AddComponent<GameplayUI>(); ui.theme = theme; ui.panelSprite = theme.roundedPanel; ui.stickerPrefab = piecePrefab;
            ui.trayPrefab = TrayPrefab(); ui.slotPrefab = BufferPrefab();
            var card = Panel("CategoryAlbumCard", null, new Vector2(296, 318), Vector2.zero, Color.white);
            SavePrefab(card.gameObject, "CategoryAlbumCard"); UnityEngine.Object.DestroyImmediate(card.gameObject);
            ui.cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CategoryAlbumCard.prefab");
            var bg = UIFactory.Panel("Honey wood table", canvas.transform, Vector2.zero, Vector2.zero, Color.white, theme.woodSurface); Stretch(bg.rectTransform);
            bg.type = Image.Type.Tiled;
            ui.safeArea = UIFactory.Rect("Safe Area", canvas.transform, Vector2.zero, Vector2.zero); Stretch(ui.safeArea);
            var root = UIFactory.Rect("Portrait Layout", ui.safeArea, new Vector2(1080, 1920), Vector2.zero);
            var header = UIFactory.Rect("LevelHeader", root, new Vector2(1000, 100), new Vector2(0, 850));
            var capsule = Panel("Level capsule gold", header, new Vector2(280, 80), Vector2.zero, Hex("FFC949"));
            var navy = Panel("Level capsule navy", capsule.transform, new Vector2(268, 68), Vector2.zero, Hex("202844"));
            ui.levelLabel = UIFactory.Text("Level TMP", navy.transform, "LEVEL 5", 37, new Vector2(260, 60), Vector2.zero, Color.white);
            var coin = Panel("Currency gold", header, new Vector2(222, 76), new Vector2(365, 0), Hex("FFC949"));
            var coinWell = Panel("Currency navy", coin.transform, new Vector2(210, 64), Vector2.zero, Hex("202844"));
            var icon = UIFactory.Panel("Coin icon", coinWell.transform, new Vector2(51, 51), new Vector2(-72, 0), Color.white, theme.coinIcon); icon.preserveAspect = true;
            ui.coins = UIFactory.Text("Coins TMP", coinWell.transform, "0", 32, new Vector2(140, 56), new Vector2(20, 0), Color.white);
            ui.settings = UIFactory.Button("Settings", header, "", new Vector2(76, 76), new Vector2(-430, 0), Hex("FFF0CA"), theme.buttonFrame);
            var settings = UIFactory.Panel("Settings icon", ui.settings.transform, new Vector2(49, 49), Vector2.zero, Color.white, theme.settingsIcon); settings.preserveAspect = true;
            SavePrefab(header.gameObject, "LevelHeader");
            ui.categories = UIFactory.Rect("Category albums", root, new Vector2(980, 330), new Vector2(0, 607));
            ui.buffer = UIFactory.Rect("Buffer row", root, new Vector2(940, 164), new Vector2(0, 334));
            ui.board = UIFactory.Rect("StickerBoard", root, new Vector2(940, 840), new Vector2(0, -246)); SavePrefab(ui.board.gameObject, "StickerBoard");
            var dock = UIFactory.Rect("Booster dock", root, new Vector2(960, 130), new Vector2(0, -810));
            ui.undo = Booster(dock, "Undo", "UNDO", theme.undoIcon, -330, Hex("22A5E9"), out ui.undoCount);
            ui.hint = Booster(dock, "Hint", "HINT", theme.hintIcon, -110, Hex("FFA81B"), out ui.hintCount);
            ui.extra = Booster(dock, "Extra", "+ SLOT", theme.extraIcon, 110, Hex("71BC29"), out ui.extraCount);
            ui.shuffle = Booster(dock, "Shuffle", "MIX", theme.shuffleIcon, 330, Hex("A351E5"), out ui.shuffleCount);
            ui.restart = UIFactory.Button("Restart", root, "RESTART", new Vector2(220, 38), new Vector2(0, -920), Hex("FFD67B"), theme.roundedPanel);
            ui.restart.GetComponentInChildren<TextMeshProUGUI>().fontSize = 19;
            var hidden = UIFactory.Rect("Hidden progress bindings", root, Vector2.zero, Vector2.zero);
            ui.progress = UIFactory.Text("Album progress", hidden, "", 1, Vector2.zero, Vector2.zero);
            ui.albumFill = Panel("Album fill", hidden, Vector2.zero, Vector2.zero, Color.white); hidden.gameObject.SetActive(false);
            ui.effects = UIFactory.Rect("Animation Layer", root, new Vector2(1080, 1920), Vector2.zero);
            BuildModal(ui, root);
            canvas.AddComponent<PortraitSafeArea>().view = ui;
            return ui;
        }
        static Button Booster(Transform dock, string name, string label, Sprite sprite, float x, Color tint, out TextMeshProUGUI counter)
        {
            var button = UIFactory.Button(name, dock, "", new Vector2(142, 98), new Vector2(x, 0), tint, theme.buttonFrame);
            button.gameObject.AddComponent<UIButtonPressFeedback>(); button.gameObject.AddComponent<CanvasGroup>();
            var icon = UIFactory.Panel("Icon", button.transform, new Vector2(54, 54), Vector2.zero, Color.white, sprite); icon.preserveAspect = true;
            UIFactory.Text("Booster label", button.transform, label, 22, new Vector2(190, 35), new Vector2(0, -69), Color.white);
            var count = Panel("Inventory", button.transform, new Vector2(48, 38), new Vector2(58, 43), Hex("58351C"));
            counter = UIFactory.Text("Count TMP", count.transform, "10", 22, new Vector2(46, 36), Vector2.zero, Color.white); return button;
        }
        static void BuildModal(GameplayUI ui, Transform root)
        {
            var dim = UIFactory.Panel("Modal", root, new Vector2(1080, 1920), Vector2.zero, new Color(.16f, .07f, .015f, .78f)); dim.raycastTarget = true; ui.modal = dim.gameObject;
            var dialog = Panel("Modal paper", dim.transform, new Vector2(840, 760), Vector2.zero, theme.paper);
            ui.modalIcon = UIFactory.Panel("Icon", dialog.transform, new Vector2(130, 130), new Vector2(0, 268), Color.white, theme.starIcon); ui.modalIcon.preserveAspect = true;
            ui.modalTitle = UIFactory.Text("Title", dialog.transform, "", 40, new Vector2(750, 110), new Vector2(0, 125), theme.ink);
            ui.modalBody = UIFactory.Text("Body", dialog.transform, "", 29, new Vector2(720, 170), new Vector2(0, -8), theme.ink);
            ui.primary = UIFactory.Button("Primary", dialog.transform, "", new Vector2(620, 94), new Vector2(0, -155), Hex("69C82B"), theme.buttonFrame);
            ui.secondary = UIFactory.Button("Secondary", dialog.transform, "", new Vector2(620, 80), new Vector2(0, -268), Hex("FFC24F"), theme.buttonFrame);
            ui.sound = UIFactory.Button("Sound", dialog.transform, "SOUND: ON", new Vector2(185, 50), new Vector2(285, 305), Hex("FFD67B"), theme.buttonFrame);
            ui.sound.GetComponentInChildren<TextMeshProUGUI>().fontSize = 18; ui.modal.SetActive(false);
        }
        public static void Preview(GameplayUI ui, LevelDefinition level, GameBalanceConfig balance)
        { ui.Initialize(new PreviewModel(level, balance), new PreviewInput()); ui.Render(); }
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
