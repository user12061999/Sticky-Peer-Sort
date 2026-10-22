using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    public static class GameFeelTuningTool
    {
        const string Root = "Assets/_Game";
        [MenuItem("Tools/Sticker Category Game/Apply Game Feel and Level Curve")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            string backup = "UserSettings/StickyPeerSortBeforeFeelTuning"; Directory.CreateDirectory(backup);
            foreach (string path in Directory.GetFiles(Root + "/Data/Levels", "*.asset")) Backup(path, backup);
            Backup(Root + "/Data/Balance.asset", backup);
            var balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(Root + "/Data/Balance.asset");
            balance.pressDuration = .045f; balance.peelDuration = .085f; balance.travelDuration = .24f;
            balance.clickFailDuration = .16f; balance.clickFailDistance = 9;
            balance.categoryFilledHold = .2f; balance.categoryDuration = .24f;
            balance.categoryExitDuration = .20f; balance.categoryEnterDuration = .24f;
            balance.flightArc = 46; balance.flightPop = .1f; balance.celebrations = true;
            balance.easing = AnimationCurve.EaseInOut(0, 0, 1, 1); EditorUtility.SetDirty(balance);
            string[] tutorials = {
                "Pick the top stickers. Collect all four to finish a set!",
                "Two sets are open. Match colors and clear a set to open the next.",
                "A sticker from a future set is on top. Use the waiting tray!",
                "Two future stickers are on top. Plan which open set to clear first.",
                "Plan two waiting stickers ahead. Keep a route to an open set.",
                "Collect the key sticker first, then the LOCK sticker opens.",
                "ICE needs two taps to break, then one tap to collect.",
                "Both LINK partners must be uncovered. Save room for waiting stickers.",
                "Mystery stickers reveal on tap. Plan around three waiting stickers.",
                "Four waiting slots, no spare room. Clear open sets before taking more!"
            };
            var report = new StringBuilder("# Level tuning report\n\nVerified without boosters. Seeded random-legal play is a diagnostic, not a human difficulty score.\n\n| Level | Stickers | Open sets | Tray | Forced future covers | Solution peak tray | Actions | Random wins / 128 | Focus |\n|---|---|---|---|---|---|---|---|---|\n");
            int[] pressure = { 0, 0, 1, 2, 2, 2, 2, 3, 3, 4 };
            for (int l = 1; l <= 10; l++)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Root + "/Data/Levels/Level" + l.ToString("00") + ".asset");
                level.activeCategoryCount = l == 1 ? 1 : l <= 4 ? 2 : 3; level.bufferCapacity = 4;
                level.difficulty = l <= 2 ? "Easy" : l <= 4 ? "Planning" : l <= 7 ? "Learning" : "Challenge";
                level.coinReward = 20 + l * 5; level.tutorial = tutorials[l - 1];
                SampleTargetSlots.Apply(level); SamplePileLayouts.Apply(level);
                var errors = LevelValidator.Validate(level); List<int> trace;
                var result = PuzzleSolver.Solve(new GameSession(level), 250000, out trace);
                if (errors.Count != 0 || result != SolveResult.Solved) throw new InvalidOperationException("Level " + l + ": " + result + " " + string.Join(";", errors));
                level.solutionTrace = trace.ConvertAll(i => level.stickers[i].instanceId);
                int peak = 0; var replay = new GameSession(level);
                foreach (int i in trace) { if (!replay.Select(i)) throw new InvalidOperationException("Invalid trace"); peak = Mathf.Max(peak, replay.Buffer.Count); }
                if (replay.State != GameplayState.Win) throw new InvalidOperationException("Trace did not win");
                int wins = 0;
                for (int seed = 0; seed < 128; seed++)
                {
                    var random = new System.Random(1103 + seed); var session = new GameSession(level);
                    for (int step = 0; step < 100 && session.State == GameplayState.Playing; step++)
                    {
                        var legal = new List<int>(); for (int i = 0; i < session.Stickers.Count; i++) if (session.Legal(i)) legal.Add(i);
                        if (legal.Count == 0) break; session.Select(legal[random.Next(legal.Count)]);
                    }
                    if (session.State == GameplayState.Win) wins++;
                }
                report.AppendLine("| " + l + " | " + level.stickers.Count + " | " + level.activeCategoryCount + " | 4 | " + pressure[l - 1] + " | " + peak + " | " + trace.Count + " | " + wins + " | " + level.difficulty + " |");
                EditorUtility.SetDirty(level);
            }
            File.WriteAllText(Root + "/Validation/LevelTuning.md", report.ToString());
            var audio = AssetDatabase.LoadAssetAtPath<GameAudioConfig>(Root + "/Data/Audio.asset");
            if (audio == null) { audio = ScriptableObject.CreateInstance<GameAudioConfig>(); AssetDatabase.CreateAsset(audio, Root + "/Data/Audio.asset"); }
            audio.peel = Clip("peel"); audio.land = Clip("land"); audio.buffer = Clip("buffer"); audio.reject = Clip("reject");
            audio.collection = Clip("collection"); audio.entrance = Clip("entrance"); audio.special = Clip("special");
            audio.win = Clip("win"); audio.fail = Clip("fail"); audio.button = Clip("button"); EditorUtility.SetDirty(audio);
            var controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            controller.audioConfig = audio; EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets(); LayerLabUIBuilder.Apply(false); PrototypeBootstrapTool.ValidateAll();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log(report.ToString());
        }
        static AudioClip Clip(string name)
        {
            string path = Root + "/Audio/" + name + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing generated sound: " + path); return clip;
        }
        static void Backup(string path, string folder)
        { string dest = folder + "/" + Path.GetFileName(path); if (!File.Exists(dest)) File.Copy(path, dest); }
    }
}
