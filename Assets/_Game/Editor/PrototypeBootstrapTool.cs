using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StickyPeerSort.Editor
{
    public static class PrototypeBootstrapTool
    {
        public const string Root = "Assets/_Game";
        public const string ScenePath = Root + "/Scenes/StickerCategoryPrototype.unity";
        static readonly Color Cream = new Color(.99f, .96f, .89f);

        [MenuItem("Tools/Sticker Category Game/Build Prototype")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before rebuilding.");
            foreach (string path in new[] { "Art", "Data/Categories", "Data/Stickers", "Data/Levels", "Prefabs", "Scenes", "Validation" }) Directory.CreateDirectory(Root + "/" + path);
            var round = Placeholder("whiteDot", "Circle");
            var square = Placeholder("whiteSquareCurved", "RoundedSquare");
            var panel = Placeholder("whiteSquareCurved02", "Panel");
            var balance = Asset<GameBalanceConfig>(Root + "/Data/Balance.asset");
            string[] names = { "FOOD", "ANIMALS", "PLANTS", "VEHICLES" };
            string[][] labels = {
                new[] { "food_01", "food_02", "food_03", "food_04" },
                new[] { "animal_01", "animal_02", "animal_03", "animal_04" },
                new[] { "plant_01", "plant_02", "plant_03", "plant_04" },
                new[] { "vehicle_01", "vehicle_02", "vehicle_03", "vehicle_04" }
            };
            string[][] displayNames = {
                new[] { "BURGER", "PIZZA", "FRIES", "DONUT" },
                new[] { "CAT", "PENGUIN", "ELEPHANT", "TURTLE" },
                new[] { "SUNFLOWER", "CACTUS", "TULIP", "TREE" },
                new[] { "CAR", "AIRPLANE", "BOAT", "HELICOPTER" }
            };
            Color[] colors = { Hex("FFA858"), Hex("7CB9E8"), Hex("7CCD7C"), Hex("F48FB1") };
            var categories = new CategoryDefinition[4]; var definitions = new StickerDefinition[16];
            for (int c = 0; c < 4; c++)
            {
                var cat = Asset<CategoryDefinition>(Root + "/Data/Categories/" + names[c] + ".asset");
                cat.id = names[c].ToLowerInvariant(); cat.title = names[c]; cat.color = colors[c]; categories[c] = cat; EditorUtility.SetDirty(cat);
                for (int t = 0; t < 4; t++)
                {
                    var d = Asset<StickerDefinition>(Root + "/Data/Stickers/" + labels[c][t] + ".asset");
                    d.id = labels[c][t]; d.displayName = displayNames[c][t]; d.category = cat;
                    string artPath = Root + "/Art/StickyPeerSort_Stickers16/stickers/" + labels[c][t] + ".png";
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(artPath);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
                    if (tex != null)
                    {
                        d.texture = tex; d.sprite = sprite;
                        d.tint = Color.white; d.showLabel = false; d.preserveAspect = true;
                    }
                    else if (!d.HasArtwork)
                    {
                        d.sprite = t % 2 == 0 ? round : square; d.tint = Color.Lerp(colors[c], Color.white, t * .065f);
                    }
                    StickerArtworkBaker.Bake(d);
                    definitions[c * 4 + t] = d; EditorUtility.SetDirty(d);
                }
            }
            var levels = new LevelDefinition[10];
            string[] tutorials = {
                "Pick stickers with nothing on top. Collect all four!", "Two collections are open. Match stickers by category.",
                "Future categories wait in the tray. Leave yourself space!", "Remove the top stickers first. Covered stickers cannot be picked.",
                "Plan ahead: only four stickers fit in the waiting tray.", "LOCK stickers open after their key sticker is collected.",
                "Tap ICE twice to break its layers, then collect the sticker.", "LINK partners must both be uncovered before collection.",
                "Mystery stickers reveal their identity when you tap them.", "Combine everything you learned. Hint can show a verified move."
            };
            for (int l = 1; l <= 10; l++)
            {
                string path = Root + "/Data/Levels/Level" + l.ToString("00") + ".asset";
                // Existing authored levels are retained; rebuild is safe for content iteration.
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level == null)
                {
                    level = Asset<LevelDefinition>(path); level.levelNumber = l;
                    int count = l == 1 ? 2 : l == 2 ? 3 : 4;
                    level.activeCategoryCount = l == 1 ? 1 : l <= 4 ? 2 : 3;
                    level.difficulty = l <= 2 ? "Easy" : l <= 4 ? "Planning" : l <= 7 ? "Learning" : "Challenge";
                    level.coinReward = 20 + l * 5; level.tutorial = tutorials[l - 1];
                    for (int c = 0; c < count; c++) level.objectives.Add(new CategoryObjective { category = categories[c], targetCount = 4 });
                    for (int c = 0; c < count; c++) for (int t = 0; t < 4; t++)
                    {
                        int i = c * 4 + t;
                        int col = l >= 4 ? (t + c) % 4 : t;
                        var p = new StickerPlacement { instanceId = "L" + l + "_" + i, definition = definitions[i],
                            position = new Vector2((col - 1.5f) * 210, (c - (count - 1) * .5f) * (l >= 4 ? 126 : 180)),
                            rotation = l >= 4 ? (i % 3 - 1) * 11 : (i % 3 - 1) * 4, sortingOrder = i,
                            pileId = "tray_" + col };
                        if (l >= 6 && i == 3) { p.special.kind = SpecialKind.Locked; p.special.unlockCondition = UnlockCondition.KeyCollected; p.special.dependencyId = "L" + l + "_0"; }
                        if (l >= 7 && i == 5) { p.special.kind = SpecialKind.Frozen; p.special.iceLayers = 2; }
                        if (l >= 8 && (i == 8 || i == 9)) { p.special.kind = SpecialKind.Linked; p.special.linkId = "A"; }
                        if (l >= 9 && (i == 12 || i == 14)) p.special.kind = SpecialKind.Mystery;
                        level.stickers.Add(p);
                    }
                    // Later boards force future-category stickers into the tray before early targets can be reached.
                    if (l >= 5)
                    {
                        int blockers = l == 10 ? 4 : 2;
                        for (int b = 0; b < blockers; b++)
                        {
                            var upper = level.stickers[(count - 1) * 4 + b]; var lower = level.stickers[b];
                            upper.position = lower.position; upper.rotation = lower.rotation;
                        }
                    }
                    SampleTargetSlots.Apply(level); SamplePileLayouts.Apply(level);
                    EditorUtility.SetDirty(level);
                }
                levels[l - 1] = level;
            }
            var prefab = BuildStickerPrefab(panel);
            var card = BuildPanelPrefab("CategoryCard", new Vector2(284, 280), panel, Color.white);
            var slot = BuildPanelPrefab("BufferSlot", new Vector2(128, 124), panel, Hex("E2D9C9"));
            BuildScene(levels, balance, panel, prefab, card, slot);
            if (LayerLabUIBuilder.IsAvailable) LayerLabUIBuilder.Apply(false);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); ValidateAll();
            Debug.Log("Sticker Sort prototype built: " + ScenePath);
        }
        static Color Hex(string value) { Color c; ColorUtility.TryParseHtmlString("#" + value, out c); return c; }
        static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        static Sprite Placeholder(string source, string name)
        {
            string path = Root + "/Art/" + name + ".png";
            if (!File.Exists(path)) AssetDatabase.CopyAsset("Assets/Plugins/Demigiant/DemiLib/Core/Editor/Imgs/" + source + ".png", path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Placeholder source missing: " + source);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.isReadable = true; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (name == "Panel") importer.spriteBorder = new Vector4(8, 8, 8, 8);
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static StickerView BuildStickerPrefab(Sprite panel)
        {
            var img = UIFactory.Panel("Sticker", null, new Vector2(172, 154), Vector2.zero, Color.white, panel); img.raycastTarget = true;
            var view = img.gameObject.AddComponent<StickerView>(); view.image = img;
            var shadow = img.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.18f, .15f, .12f, .2f); shadow.effectDistance = new Vector2(4, -7);
            var outline = img.gameObject.AddComponent<Outline>(); outline.effectColor = Color.white; outline.effectDistance = new Vector2(4, -4);
            view.title = UIFactory.Text("Label TMP", img.transform, "STICKER", 27, new Vector2(142, 100), Vector2.zero);
            view.badge = UIFactory.Text("Special TMP", img.transform, "", 18, new Vector2(150, 30), new Vector2(0, -46));
            var saved = PrefabUtility.SaveAsPrefabAsset(img.gameObject, Root + "/Prefabs/Sticker.prefab"); UnityEngine.Object.DestroyImmediate(img.gameObject);
            return saved.GetComponent<StickerView>();
        }
        static GameObject BuildPanelPrefab(string name, Vector2 size, Sprite sprite, Color color)
        {
            var img = UIFactory.Panel(name, null, size, Vector2.zero, color, sprite);
            var saved = PrefabUtility.SaveAsPrefabAsset(img.gameObject, Root + "/Prefabs/" + name + ".prefab"); UnityEngine.Object.DestroyImmediate(img.gameObject); return saved;
        }
        static void BuildScene(LevelDefinition[] levels, GameBalanceConfig balance, Sprite panel, StickerView sticker, GameObject card, GameObject slot)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = Cream; camera.clearFlags = CameraClearFlags.SolidColor;
            var light = new GameObject("Main Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = .5f;
            var canvas = new GameObject("Sticker Sort Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
            var background = UIFactory.Panel("Warm background", canvas.transform, Vector2.zero, Vector2.zero, Cream);
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one; background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            var ui = canvas.AddComponent<GameplayUI>(); ui.panelSprite = panel; ui.stickerPrefab = sticker; ui.cardPrefab = card; ui.slotPrefab = slot;
            ui.safeArea = UIFactory.Rect("Safe Area", canvas.transform, Vector2.zero, Vector2.zero);
            ui.safeArea.anchorMin = Vector2.zero; ui.safeArea.anchorMax = Vector2.one; ui.safeArea.offsetMin = ui.safeArea.offsetMax = Vector2.zero;
            var root = UIFactory.Rect("Portrait Layout", ui.safeArea, new Vector2(1080, 1920), Vector2.zero);
            ui.levelLabel = UIFactory.Text("Level TMP", root, "LEVEL 01", 34, new Vector2(290, 65), new Vector2(-300, 856));
            ui.levelLabel.fontStyle = FontStyles.Bold;
            ui.coins = UIFactory.Text("Coins TMP", root, "COINS  0", 27, new Vector2(280, 65), new Vector2(165, 856));
            ui.settings = UIFactory.Button("Settings", root, "II", new Vector2(90, 74), new Vector2(440, 856), Hex("EAE3D5"), panel);
            UIFactory.Text("Game title TMP", root, "STICKY PEER SORT", 43, new Vector2(900, 65), new Vector2(0, 761)).fontStyle = FontStyles.Bold;
            UIFactory.Text("Subtitle TMP", root, "a little order, a lot of joy", 25, new Vector2(900, 40), new Vector2(0, 706), Hex("8B877E"));
            ui.categories = UIFactory.Rect("Category Cards", root, new Vector2(960, 280), new Vector2(0, 501));
            ui.capacity = UIFactory.Text("Tray capacity TMP", root, "WAITING TRAY  0 / 4", 24, new Vector2(900, 45), new Vector2(0, 307));
            ui.buffer = UIFactory.Rect("Waiting Tray", root, new Vector2(960, 130), new Vector2(0, 211));
            var boardMat = UIFactory.Panel("Board mat", root, new Vector2(960, 905), new Vector2(0, -348), Hex("F2E8D7"), panel);
            ui.board = UIFactory.Rect("Sticker Board", root, new Vector2(920, 860), new Vector2(0, -348));
            ui.instruction = UIFactory.Text("Tutorial TMP", root, "Tap a sticker to start its collection.", 26, new Vector2(920, 90), new Vector2(0, 82));
            ui.progress = UIFactory.Text("Progress TMP", root, "0 / 2 COLLECTIONS", 23, new Vector2(940, 38), new Vector2(0, -760));
            ui.undo = UIFactory.Button("Undo", root, "UNDO", new Vector2(210, 88), new Vector2(-354, -866), Hex("E6DDCF"), panel);
            ui.hint = UIFactory.Button("Hint", root, "HINT", new Vector2(210, 88), new Vector2(-118, -866), Hex("F3D78F"), panel);
            ui.extra = UIFactory.Button("Extra Slot", root, "+ SLOT", new Vector2(210, 88), new Vector2(118, -866), Hex("C1DBCF"), panel);
            ui.shuffle = UIFactory.Button("Shuffle", root, "MIX", new Vector2(210, 88), new Vector2(354, -866), Hex("D7CDEE"), panel);
            ui.restart = UIFactory.Button("Restart", root, "RESTART LEVEL", new Vector2(340, 45), new Vector2(0, -939), Cream);
            ui.effects = UIFactory.Rect("Animation Layer", root, new Vector2(1080, 1920), Vector2.zero);
            var blocker = UIFactory.Panel("Modal", root, new Vector2(1080, 1920), Vector2.zero, new Color(.12f, .14f, .2f, .7f)); blocker.raycastTarget = true; ui.modal = blocker.gameObject;
            var dialog = UIFactory.Panel("Dialog", blocker.transform, new Vector2(850, 650), Vector2.zero, Cream, panel);
            ui.modalTitle = UIFactory.Text("Title TMP", dialog.transform, "PAUSED", 43, new Vector2(780, 140), new Vector2(0, 195));
            ui.modalBody = UIFactory.Text("Body TMP", dialog.transform, "", 30, new Vector2(740, 145), new Vector2(0, 50));
            ui.primary = UIFactory.Button("Primary", dialog.transform, "RESUME", new Vector2(620, 100), new Vector2(0, -95), Hex("C1DBCF"), panel);
            ui.secondary = UIFactory.Button("Secondary", dialog.transform, "RETRY", new Vector2(620, 85), new Vector2(0, -215), Hex("E6DDCF"), panel); ui.modal.SetActive(false);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var controller = new GameObject("Game Controller").AddComponent<GameController>(); controller.ui = ui; controller.levels = levels; controller.balance = balance;
            controller.audioConfig = AssetDatabase.LoadAssetAtPath<GameAudioConfig>(Root + "/Data/Audio.asset");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait; PlayerSettings.defaultScreenWidth = 1080; PlayerSettings.defaultScreenHeight = 1920;
            ui.ApplySafeArea();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList(); buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = buildScenes.ToArray();
        }
        [MenuItem("Tools/Sticker Category Game/Validate All Levels")]
        public static void ValidateAll()
        {
            var report = new System.Text.StringBuilder("Sticker Sort validation\n"); int failed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { Root + "/Data/Levels" }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            {
                var l = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid)); var errors = LevelValidator.Validate(l);
                var solution = new List<int>(); SolveResult result = SolveResult.NoSolution;
                if (errors.Count == 0) result = PuzzleSolver.Solve(new GameSession(l), 12000, out solution);
                if (result == SolveResult.Solved)
                {
                    l.solutionTrace = solution.ConvertAll(i => l.stickers[i].instanceId); EditorUtility.SetDirty(l);
                    report.AppendLine("Level " + l.levelNumber + ": SOLVED, " + solution.Count + " actions: " + string.Join(", ", l.solutionTrace));
                }
                else { failed++; l.solutionTrace.Clear(); EditorUtility.SetDirty(l); report.AppendLine("Level " + l.levelNumber + ": " + result + " " + string.Join("; ", errors)); }
            }
            File.WriteAllText(Root + "/Validation/LevelSolutions.txt", report.ToString()); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            if (failed > 0) Debug.LogError(report.ToString()); else Debug.Log(report.ToString());
        }
    }
}
