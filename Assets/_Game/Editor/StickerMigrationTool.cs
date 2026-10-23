using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    public static class StickerMigrationTool
    {
        const string Root = "Assets/_Game";

        [MenuItem("Tools/Sticker Category Game/Migrate To 16 Production Stickers")]
        public static void Migrate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before migrating.");

            Debug.Log("<color=#2EA3FF><b>[StickerMigration]</b></color> Starting migration to 16 production stickers...");

            // 1. Ensure target folders exist
            Directory.CreateDirectory(Root + "/Data/Categories");
            Directory.CreateDirectory(Root + "/Data/Stickers");
            Directory.CreateDirectory(Root + "/Data/Levels");

            // 2. Define Category specifications
            string[] catNames = { "FOOD", "ANIMALS", "PLANTS", "VEHICLES" };
            Color[] catColors = { Hex("FFA858"), Hex("7CB9E8"), Hex("7CCD7C"), Hex("F48FB1") };

            string[][] stickerIds = {
                new[] { "food_01", "food_02", "food_03", "food_04" },
                new[] { "animal_01", "animal_02", "animal_03", "animal_04" },
                new[] { "plant_01", "plant_02", "plant_03", "plant_04" },
                new[] { "vehicle_01", "vehicle_02", "vehicle_03", "vehicle_04" }
            };

            string[][] stickerDisplayNames = {
                new[] { "BURGER", "PIZZA", "FRIES", "DONUT" },
                new[] { "CAT", "PENGUIN", "ELEPHANT", "TURTLE" },
                new[] { "SUNFLOWER", "CACTUS", "TULIP", "TREE" },
                new[] { "CAR", "AIRPLANE", "BOAT", "HELICOPTER" }
            };

            var categories = new CategoryDefinition[4];
            var definitions = new StickerDefinition[16];

            for (int c = 0; c < 4; c++)
            {
                string catPath = Root + "/Data/Categories/" + catNames[c] + ".asset";
                var cat = AssetDatabase.LoadAssetAtPath<CategoryDefinition>(catPath);
                if (cat == null)
                {
                    cat = ScriptableObject.CreateInstance<CategoryDefinition>();
                    AssetDatabase.CreateAsset(cat, catPath);
                }
                cat.id = catNames[c].ToLowerInvariant();
                cat.title = catNames[c];
                cat.color = catColors[c];
                EditorUtility.SetDirty(cat);
                categories[c] = cat;

                for (int t = 0; t < 4; t++)
                {
                    string id = stickerIds[c][t];
                    string stickerPath = Root + "/Data/Stickers/" + id + ".asset";
                    var def = AssetDatabase.LoadAssetAtPath<StickerDefinition>(stickerPath);
                    if (def == null)
                    {
                        def = ScriptableObject.CreateInstance<StickerDefinition>();
                        AssetDatabase.CreateAsset(def, stickerPath);
                    }

                    string pngPath = Root + "/Art/StickyPeerSort_Stickers16/stickers/" + id + ".png";
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);

                    if (tex == null || sprite == null)
                    {
                        Debug.LogError("Sticker texture/sprite missing at: " + pngPath);
                    }

                    def.id = id;
                    def.displayName = stickerDisplayNames[c][t];
                    def.category = cat;
                    def.texture = tex;
                    def.sprite = sprite;
                    def.tint = Color.white;
                    def.showLabel = false;
                    def.preserveAspect = true;
                    def.alphaThreshold = 0.15f;
                    def.alphaMaskResolution = 256;

                    StickerArtworkBaker.Bake(def);
                    EditorUtility.SetDirty(def);
                    definitions[c * 4 + t] = def;
                }
            }

            AssetDatabase.SaveAssets();

            // 3. Rebuild Levels 1 - 10 with the 16 stickers and 4 categories
            string[] tutorials = {
                "Pick stickers with nothing on top. Collect all four!",
                "Two collections are open. Match stickers by category.",
                "Future categories wait in the tray. Leave yourself space!",
                "Remove the top stickers first. Covered stickers cannot be picked.",
                "Plan ahead: only four stickers fit in the waiting tray.",
                "Complete the set with the KEY to unlock the locked sticker.",
                "Tap ICE twice to break its layers, then collect the sticker.",
                "LINK partners must both be uncovered before collection.",
                "Mystery stickers reveal their identity when you tap them.",
                "Combine everything you learned. Hint can show a verified move."
            };

            var levels = new LevelDefinition[10];
            for (int l = 1; l <= 10; l++)
            {
                string levelPath = Root + "/Data/Levels/Level" + l.ToString("00") + ".asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(levelPath);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(level, levelPath);
                }

                level.levelNumber = l;
                int count = l == 1 ? 2 : l == 2 ? 3 : 4;
                level.activeCategoryCount = l == 1 ? 1 : l <= 4 ? 2 : 3;
                level.bufferCapacity = 4;
                level.difficulty = l <= 2 ? "Easy" : l <= 4 ? "Planning" : l <= 7 ? "Learning" : "Challenge";
                level.coinReward = 20 + l * 5;
                level.tutorial = tutorials[l - 1];

                level.objectives.Clear();
                level.stickers.Clear();

                for (int c = 0; c < count; c++)
                {
                    level.objectives.Add(new CategoryObjective { category = categories[c], targetCount = 4 });
                }

                for (int c = 0; c < count; c++)
                {
                    for (int t = 0; t < 4; t++)
                    {
                        int i = c * 4 + t;
                        int col = l >= 4 ? (t + c) % 4 : t;
                        var p = new StickerPlacement
                        {
                            instanceId = "L" + l + "_" + i,
                            definition = definitions[i],
                            position = new Vector2((col - 1.5f) * 210, (c - (count - 1) * .5f) * (l >= 4 ? 126 : 180)),
                            rotation = l >= 4 ? (i % 3 - 1) * 11 : (i % 3 - 1) * 4,
                            sortingOrder = i,
                            pileId = "tray_" + col
                        };

                        if (l >= 6 && i == 3)
                        {
                            p.special.kind = SpecialKind.Locked;
                            p.special.unlockCondition = UnlockCondition.KeyCollected;
                            p.special.dependencyId = "L" + l + "_0";
                        }
                        if (l >= 7 && i == 5)
                        {
                            p.special.kind = SpecialKind.Frozen;
                            p.special.iceLayers = 2;
                        }
                        if (l >= 8 && (i == 8 || i == 9))
                        {
                            p.special.kind = SpecialKind.Linked;
                            p.special.linkId = "A";
                        }
                        if (l >= 9 && (i == 12 || i == 14))
                        {
                            p.special.kind = SpecialKind.Mystery;
                        }

                        level.stickers.Add(p);
                    }
                }

                SampleTargetSlots.Apply(level);
                SamplePileLayouts.Apply(level);

                var errors = LevelValidator.Validate(level);
                if (errors.Count > 0)
                {
                    Debug.LogError("Validation errors on Level " + l + ": " + string.Join(", ", errors));
                }

                List<int> trace;
                var solveResult = PuzzleSolver.Solve(new GameSession(level), 250000, out trace);
                if (solveResult == SolveResult.Solved && trace != null)
                {
                    level.solutionTrace = trace.ConvertAll(idx => level.stickers[idx].instanceId);
                }
                else
                {
                    Debug.LogError("Level " + l + " could not be solved: " + solveResult);
                }

                EditorUtility.SetDirty(level);
                levels[l - 1] = level;
            }

            AssetDatabase.SaveAssets();

            // 4. Remove obsolete placeholder category and sticker assets
            string[] oldCats = { "FRUIT", "GARDEN", "SPACE", "SWEETS", "OCEAN", "MUSIC" };
            foreach (var oldCat in oldCats)
            {
                string path = Root + "/Data/Categories/" + oldCat + ".asset";
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }

            string[] oldStickers = {
                "APPLE", "PEAR", "LEMON", "CHERRY",
                "LEAF", "BLOOM", "CACTUS", "SEED",
                "STAR", "MOON", "COMET", "SUN",
                "CAKE", "CANDY", "DONUT", "COOKIE",
                "FISH", "SHELL", "CRAB", "WHALE",
                "NOTE", "DRUM", "PIANO", "BELL"
            };
            foreach (var oldSticker in oldStickers)
            {
                string path = Root + "/Data/Stickers/" + oldSticker + ".asset";
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 5. Update scene GameController
            string scenePath = Root + "/Scenes/StickerCategoryPrototype.unity";
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath);
            }

            var controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (controller != null)
            {
                controller.levels = levels;
                EditorUtility.SetDirty(controller);
            }

            if (LayerLabUIBuilder.IsAvailable)
            {
                LayerLabUIBuilder.Apply(false);
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=#61C62D><b>[StickerMigration]</b></color> Successfully migrated all 16 stickers, 4 categories, and 10 levels!");
        }

        static Color Hex(string value)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + value, out c);
            return c;
        }
    }
}
