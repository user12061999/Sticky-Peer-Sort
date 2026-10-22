using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    public static class BoardLayoutUpgradeTool
    {
        [MenuItem("Tools/Sticker Category Game/Apply Exact Sticker Targets and Varied Trays")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            const string folder = "Assets/_Game/Data/Levels/";
            const string backup = "UserSettings/StickyPeerSortBeforeVariedTrays";
            Directory.CreateDirectory(backup);
            string[] names = { "Three staggered trays", "Four square compartments", "Three planning piles", "Two wide trays", "Four tilted compartments", "Three horizontal trays", "One large scattered tray", "Wide tray and four small compartments", "Six small compartments", "Tall tray and three side compartments" };
            var report = new StringBuilder("# Current level layout and tuning report\n\nExact texture targets, four waiting slots, and solver-verified wins without boosters. Seeded random-legal play is diagnostic, not human difficulty.\n\n| Level | Layout | Trays | Stickers | Open sets | Solution peak tray | Actions | Random wins / 128 |\n|---|---|---|---|---|---|---|---|\n");
            var pending = new List<LevelDefinition>();
            // Stage and validate every board before modifying saved assets.
            for (int number = 1; number <= 10; number++)
            {
                string path = folder + "Level" + number.ToString("00") + ".asset";
                string copy = backup + "/" + Path.GetFileName(path); if (!File.Exists(copy)) File.Copy(path, copy);
                var source = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                var level = UnityEngine.Object.Instantiate(source); pending.Add(level);
                SampleTargetSlots.Apply(level); SamplePileLayouts.Apply(level);
                var errors = LevelValidator.Validate(level); List<int> solution;
                var solved = PuzzleSolver.Solve(new GameSession(level), 250000, out solution);
                if (errors.Count > 0 || solved != SolveResult.Solved) throw new InvalidOperationException("Level " + number + ": " + solved + " " + string.Join("; ", errors));
                level.solutionTrace = solution.ConvertAll(i => level.stickers[i].instanceId);
                var replay = new GameSession(level); int peak = 0;
                foreach (int i in solution) { if (!replay.Select(i)) throw new InvalidOperationException("Illegal trace"); peak = Mathf.Max(peak, replay.Buffer.Count); }
                int wins = 0;
                for (int seed = 0; seed < 128; seed++)
                {
                    var session = new GameSession(level); var random = new System.Random(1103 + seed);
                    for (int step = 0; step < 100 && session.State == GameplayState.Playing; step++)
                    {
                        var legal = new List<int>(); for (int i = 0; i < session.Stickers.Count; i++) if (session.Legal(i)) legal.Add(i);
                        if (legal.Count == 0) break; session.Select(legal[random.Next(legal.Count)]);
                    }
                    if (session.State == GameplayState.Win) wins++;
                }
                report.AppendLine("| " + number + " | " + names[number - 1] + " | " + level.piles.Count + " | " + level.stickers.Count + " | " + level.activeCategoryCount + " | " + peak + " | " + solution.Count + " | " + wins + " |");
            }
            for (int i = 0; i < pending.Count; i++)
            {
                var source = AssetDatabase.LoadAssetAtPath<LevelDefinition>(folder + "Level" + (i + 1).ToString("00") + ".asset");
                string originalName = source.name; EditorUtility.CopySerialized(pending[i], source); source.name = originalName;
                EditorUtility.SetDirty(source); UnityEngine.Object.DestroyImmediate(pending[i]);
            }
            File.WriteAllText("Assets/_Game/Validation/LevelTuning.md", report.ToString());
            AssetDatabase.SaveAssets(); PrototypeBootstrapTool.ValidateAll(); LayerLabUIBuilder.Apply(false);
            Debug.Log(report.ToString());
        }
    }
}
