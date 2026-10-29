using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    // Reproducible campaign authoring; all levels are staged and replayed before assets change.
    [InitializeOnLoad]
    public static class CampaignBalanceBuilder
    {
        public const string Root = "Assets/_Game/ReferenceStyle/Data/Campaign";
        sealed class Recipe
        {
            public int count, categories, types, active, mystery, locked, bufferPeak, layout;
            public LevelDifficulty difficulty;
            public string tutorial;
            public Recipe(int count, int categories, int types, int active, int mystery, int locked, int peak, int layout, LevelDifficulty difficulty, string tutorial)
            { this.count = count; this.categories = categories; this.types = types; this.active = active; this.mystery = mystery; this.locked = locked; bufferPeak = peak; this.layout = layout; this.difficulty = difficulty; this.tutorial = tutorial; }
        }
        static readonly Recipe[] Recipes =
        {
            new Recipe(12,2,2,2,0,0,0,1,LevelDifficulty.Easy,"Peel a bright sticker and match its exact picture to a sheet."),
            new Recipe(16,3,3,2,0,0,0,0,LevelDifficulty.Easy,"Clear a sheet to reveal the next. Keep a waiting slot free."),
            new Recipe(20,3,4,2,1,0,0,2,LevelDifficulty.Easy,"Remove the cover to discover the black ? sticker."),
            new Recipe(24,4,4,3,2,0,0,1,LevelDifficulty.Normal,"Match the picture, not just the category. Plan around covered stickers."),
            new Recipe(28,4,4,3,0,2,0,0,LevelDifficulty.Normal,"Complete the sheet with a key to release chained stickers."),
            new Recipe(28,4,4,3,2,2,0,2,LevelDifficulty.Normal,"Reveal hidden pictures and clear the key sheet first."),
            new Recipe(32,4,4,2,3,2,1,1,LevelDifficulty.Normal,"Two open sheets: use the waiting tray for future pictures."),
            new Recipe(32,4,4,3,2,2,0,0,LevelDifficulty.Normal,"Three open sheets give you more room to plan."),
            new Recipe(36,4,4,2,4,3,1,2,LevelDifficulty.Normal,"Uncover the key pictures while keeping waiting space."),
            new Recipe(40,4,4,2,4,4,2,3,LevelDifficulty.Hard,"Plan two future pictures ahead. A full waiting tray ends the level."),
            new Recipe(36,4,4,3,3,2,0,1,LevelDifficulty.Normal,"A lighter round: fill the most complete matching sheet."),
            new Recipe(40,4,4,2,5,3,2,0,LevelDifficulty.Hard,"Clear covers carefully. Save space for pictures on future sheets."),
            new Recipe(44,4,4,2,5,4,2,2,LevelDifficulty.Hard,"Find the key sheet's pictures before committing waiting space."),
            new Recipe(44,4,4,3,4,3,1,1,LevelDifficulty.Normal,"Use three open sheets to work through a larger board."),
            new Recipe(48,4,4,2,6,5,3,3,LevelDifficulty.Hard,"Chapter challenge: uncover, unlock and keep one waiting slot free.")
        };
        static CampaignBalanceBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                const string request = "Temp/BalanceStickerCampaign.request";
                if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(request); Build();
            };
        }
        [MenuItem("Tools/Sticker Category Game/Build Balanced Campaign (15 Levels)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var staged = new List<LevelDefinition>();
            var report = new StringBuilder("# First chapter balance\n\nFive waiting slots; full means lose. Exact artwork targets. All winning traces are replayed without boosters. Random and target-first bots are diagnostics, not human win rates. Relief rounds: 8, 11 and 14.\n\n|Level|Stickers|Artworks|Trays|Open sheets|?|Locked|Route peak / 5|Depth|Initially clickable|Random wins / 64|Target-first wins / 64|\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            try
            {
                var templates = new[] { "Level_05_Grid4", "Level_12_Rows2", "Level_19_Mixed3", "Level_62_Pile1" }
                    .Select(n => AssetDatabase.LoadAssetAtPath<LevelDefinition>(ReferenceStyleBuilder.Root + "/Data/Levels/" + n + ".asset")).ToArray();
                if (templates.Any(t => t == null)) throw new InvalidOperationException("Missing reference layout template.");
                var pool = new List<StickerDefinition>();
                foreach (string category in new[] { "food", "animal", "plant", "vehicle" })
                    for (int t = 1; t <= 4; t++) pool.Add(AssetDatabase.LoadAssetAtPath<StickerDefinition>(ReferenceStyleBuilder.Root + "/Data/Stickers/" + category + "_" + t.ToString("00") + ".asset"));
                if (pool.Any(p => p == null)) throw new InvalidOperationException("Missing campaign artwork.");
                for (int i = 0; i < Recipes.Length; i++)
                {
                    var r = Recipes[i];
                    EditorUtility.DisplayProgressBar("Balancing campaign", "Stage and verify level " + (i + 1), (float)i / Recipes.Length);
                    var level = UnityEngine.Object.Instantiate(templates[r.layout]); staged.Add(level);
                    level.name = "Campaign_" + (i + 1).ToString("00"); level.levelNumber = i + 1;
                    level.generatedStickerCount = r.count; level.layoutSeed = 61000 + i * 101;
                    level.activeCategoryCount = r.active; level.bufferCapacity = 5; level.failOnOverflow = true;
                    level.boardStickerSizeMultiplier = .75f;
                    level.coinReward = r.difficulty == LevelDifficulty.Easy ? 25 : r.difficulty == LevelDifficulty.Normal ? 35 : 50;
                    level.tutorial = r.tutorial;
                    level.generation = new LevelGenerationConfig { enabled = true, difficulty = r.difficulty, categoryCount = r.categories, typesPerCategory = r.types,
                        mysteryPercent = 100f * r.mystery / r.count, lockedPercent = 100f * r.locked / r.count, plannedBufferPeak = r.bufferPeak, stickerPool = new List<StickerDefinition>(pool) };
                    string message;
                    if (!ConfiguredLevelGenerator.Rebuild(level, out message)) throw new InvalidOperationException(level.name + ": " + message);
                    int targetWins = TuneSeed(level, i);
                    var analysis = LevelAnalysis.Analyze(level);
                    if (!analysis.Verified || analysis.peakTransientBuffer >= level.bufferCapacity || analysis.locked != r.locked || analysis.mystery != r.mystery)
                        throw new InvalidOperationException(level.name + ": verification, ratio or buffer safety failed.");
                    report.AppendLine("|" + (i + 1) + "|" + r.count + "|" + level.stickers.Select(p => p.definition).Distinct().Count() + "|" + level.piles.Count + "|" + r.active + "|" + r.mystery + "|" + r.locked + "|" + analysis.peakTransientBuffer + "|" + analysis.maxOverlapDepth + "|" + analysis.initiallyClickable + "|" + BotWins(level, false) + "|" + targetWins + "|");
                }
                // Commit only when every staged level is valid and solvable.
                Directory.CreateDirectory(Root); AssetDatabase.Refresh();
                var assets = new List<LevelDefinition>();
                foreach (var copy in staged)
                {
                    string path = Root + "/" + copy.name + ".asset";
                    var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                    if (asset == null) { asset = ScriptableObject.CreateInstance<LevelDefinition>(); AssetDatabase.CreateAsset(asset, path); }
                    Undo.RecordObject(asset, "Balance campaign"); EditorUtility.CopySerialized(copy, asset); asset.name = copy.name;
                    EditorUtility.SetDirty(asset); assets.Add(asset);
                }
                AssetDatabase.SaveAssets();
                var controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
                if (controller != null)
                {
                    Undo.RecordObject(controller, "Use balanced campaign"); controller.levels = assets.ToArray();
                    EditorUtility.SetDirty(controller);
                    EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                }
                // Patch only the serialized level list in the other gameplay scene.
                foreach (string scene in new[] { "Assets/_Game/Scenes/SampleStickerLevel.unity", "Assets/_Game/Scenes/StickerCategoryPrototype.unity" })
                {
                    string text = File.ReadAllText(scene);
                    string list = "  levels:\n" + string.Concat(assets.Select(a => "  - {fileID: 11400000, guid: " + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(a)) + ", type: 2}\n"));
                    var pattern = new Regex(@"(?m)^  levels:\r?\n(?:  -[^\r\n]*\r?\n)*");
                    if (pattern.Matches(text).Count != 1) throw new InvalidOperationException("Expected one controller level list in " + scene);
                    File.WriteAllText(scene, pattern.Replace(text, list));
                }
                File.WriteAllText("Assets/_Game/Validation/CampaignBalance.md", report.ToString());
                var solutions = new StringBuilder();
                foreach (var level in staged) solutions.AppendLine(level.name + ": " + string.Join(",", level.solutionTrace));
                File.WriteAllText("Assets/_Game/Validation/CampaignSolutions.txt", solutions.ToString());
                File.WriteAllText("Temp/CampaignBalanceResult.txt", "PASS: 15/15 campaign winning traces verified without boosters.\n" + report);
                AssetDatabase.Refresh();
                if (controller != null) LevelPreviewWindow.Preview(0);
                Debug.Log("Balanced campaign applied: 15 verified levels. See Validation/CampaignBalance.md.");
            }
            catch (Exception e) { File.WriteAllText("Temp/CampaignBalanceResult.txt", "FAIL: " + e); Debug.LogException(e); }
            finally { EditorUtility.ClearProgressBar(); foreach (var level in staged) UnityEngine.Object.DestroyImmediate(level); }
        }
        static int TuneSeed(LevelDefinition level, int index)
        {
            // Screen large difficulty spikes with the same target-first policy and fixed seeds.
            // This is a reproducible diagnostic, never a predicted human completion rate.
            float[] targets = { 1, 1, 1, .97f, .9f, .94f, .8f, .97f, .75f, .67f, .94f, .6f, .55f, .82f, .45f };
            int wins = BotWins(level, true), bestWins = wins, seed = level.layoutSeed;
            float bestScore = Mathf.Abs(wins / 64f - targets[index]);
            if (bestScore <= .12f) return wins;
            var best = UnityEngine.Object.Instantiate(level); best.name = level.name;
            try
            {
                for (int attempt = 1; attempt <= 6 && bestScore > .12f; attempt++)
                {
                    level.layoutSeed = seed + attempt * 3571;
                    string message;
                    if (!ConfiguredLevelGenerator.Rebuild(level, out message)) continue;
                    wins = BotWins(level, true);
                    float score = Mathf.Abs(wins / 64f - targets[index]);
                    if (score >= bestScore) continue;
                    bestScore = score; bestWins = wins; EditorUtility.CopySerialized(level, best);
                }
                EditorUtility.CopySerialized(best, level); return bestWins;
            }
            finally { UnityEngine.Object.DestroyImmediate(best); }
        }
        static int BotWins(LevelDefinition level, bool targetFirst)
        {
            int wins = 0;
            var initial = new GameSession(level);
            for (int run = 0; run < 64; run++)
            {
                var session = initial.Copy(); var random = new System.Random(level.layoutSeed + run * 997);
                for (int step = 0; step < level.stickers.Count * 3 && session.State == GameplayState.Playing; step++)
                {
                    var moves = Enumerable.Range(0, level.stickers.Count).Where(session.Legal).ToList();
                    if (moves.Count == 0) break;
                    if (targetFirst && moves.Any(session.HasTarget)) moves.RemoveAll(i => !session.HasTarget(i));
                    session.Select(moves[random.Next(moves.Count)]);
                }
                if (session.State == GameplayState.Win) wins++;
            }
            return wins;
        }
    }
}
