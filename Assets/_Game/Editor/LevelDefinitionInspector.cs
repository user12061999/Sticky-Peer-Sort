using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionInspector : UnityEditor.Editor
    {
        string status;
        double rebuildAt;
        bool pending;
        bool failed;
        bool generationOptions;
        void OnEnable() { EditorApplication.update += UpdatePreview; Undo.undoRedoPerformed += OnUndo; }
        void OnDisable()
        {
            EditorApplication.update -= UpdatePreview; Undo.undoRedoPerformed -= OnUndo;
            // Switching assets before the debounce expires must still commit the edited config.
            // Do not replace the newly selected level's Game View with the previous asset.
            if (!pending || target == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var level = (LevelDefinition)target;
            if (level.generation.enabled) ConfiguredLevelGenerator.Rebuild(level, out status);
        }
        void OnUndo() { if (target != null) { pending = true; rebuildAt = EditorApplication.timeSinceStartup + .4; Repaint(); } }
        void UpdatePreview()
        {
            if (!pending || EditorApplication.timeSinceStartup < rebuildAt || target == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            pending = false;
            var level = (LevelDefinition)target;
            failed = level.generation.enabled && !ConfiguredLevelGenerator.Rebuild(level, out status);
            if (failed) { Repaint(); return; }
            LevelPreviewWindow.PreviewAsset(level); Repaint();
        }
        public override void OnInspectorGUI()
        {
            var levelConfig = (LevelDefinition)target;
            bool wasEnabled = levelConfig.generation.enabled;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawSpecialRatios(levelConfig);
            DrawPropertiesExcluding(serializedObject, "m_Script", "generation", "objectives", "stickers", "solutionTrace");
            generationOptions = EditorGUILayout.Foldout(generationOptions, "Generation Options (difficulty, categories, artwork pool)", true);
            if (generationOptions)
            {
                var generation = serializedObject.FindProperty("generation");
                EditorGUILayout.PropertyField(generation.FindPropertyRelative("difficulty"));
                EditorGUILayout.PropertyField(generation.FindPropertyRelative("categoryCount"));
                EditorGUILayout.PropertyField(generation.FindPropertyRelative("typesPerCategory"));
                EditorGUILayout.PropertyField(generation.FindPropertyRelative("stickerPool"), true);
            }
            if (!serializedObject.FindProperty("generation").FindPropertyRelative("enabled").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("objectives"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("stickers"), true);
            }
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (!wasEnabled && levelConfig.generation.enabled)
                {
                    if (levelConfig.generatedStickerCount == 0) levelConfig.generatedStickerCount = levelConfig.stickers.Count;
                    if (levelConfig.generation.stickerPool.Count == 0)
                        levelConfig.generation.stickerPool = levelConfig.stickers.Select(p => p.definition).Where(d => d != null).Distinct().ToList();
                    levelConfig.generation.categoryCount = Mathf.Min(levelConfig.generation.categoryCount, levelConfig.generation.stickerPool.Where(d => d.category != null).Select(d => d.category).Distinct().Count());
                    EditorUtility.SetDirty(levelConfig);
                }
                pending = true; rebuildAt = EditorApplication.timeSinceStartup + .5;
            }
            else serializedObject.ApplyModifiedProperties();
            EditorGUILayout.LabelField(ConfiguredLevelGenerator.Summary(levelConfig), EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Rebuild & Preview Now")) { pending = true; rebuildAt = 0; UpdatePreview(); }
            if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Live regeneration is paused during Play Mode. Exit Play Mode to edit and preview assets.", MessageType.Info);
            if (GUILayout.Button("Validate and generate solution trace"))
            {
                var level = (LevelDefinition)target; var errors = LevelValidator.Validate(level);
                if (errors.Count > 0) status = string.Join("\n", errors);
                else
                {
                    List<int> trace; var result = PuzzleSolver.Solve(new GameSession(level), 12000, out trace);
                    status = result + (result == SolveResult.Solved ? ": " + trace.Count + " actions" : ". No verified solution trace generated.");
                    if (result == SolveResult.Solved) { Undo.RecordObject(level, "Generate solution trace"); level.solutionTrace = trace.ConvertAll(i => level.stickers[i].instanceId); EditorUtility.SetDirty(level); AssetDatabase.SaveAssets(); }
                }
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, failed ? MessageType.Error : MessageType.Info);
        }

        void DrawSpecialRatios(LevelDefinition level)
        {
            var generation = serializedObject.FindProperty("generation");
            var enabled = generation.FindPropertyRelative("enabled");
            var mystery = generation.FindPropertyRelative("mysteryPercent");
            var locked = generation.FindPropertyRelative("lockedPercent");
            EditorGUILayout.LabelField("Special Sticker Ratios — this level", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.PropertyField(enabled, new GUIContent("Enable Generated Content", "Generate the board from this level's settings, replacing manual placements. Required to apply these ratios."));
                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                {
                    mystery.floatValue = EditorGUILayout.Slider(new GUIContent("Mystery (?) %", "Percentage of all stickers that begin hidden behind another sticker."), mystery.floatValue, 0, 100);
                    locked.floatValue = EditorGUILayout.Slider(new GUIContent("Locked %", "Percentage of all stickers locked until the first key category is completed."), locked.floatValue, 0, 100);
                }
            }
            int count = serializedObject.FindProperty("generatedStickerCount").intValue;
            if (count == 0) count = level.stickers.Count;
            int requestedMystery = Mathf.RoundToInt(count * mystery.floatValue / 100);
            int requestedLocked = Mathf.RoundToInt(count * locked.floatValue / 100);
            EditorGUILayout.LabelField("Requested (rounded)", requestedMystery + " mystery + " + requestedLocked + " locked / " + count + " stickers");
            EditorGUILayout.LabelField("Current board", level.stickers.Count(p => p.special.kind == SpecialKind.Mystery) + " mystery + " + level.stickers.Count(p => p.special.kind == SpecialKind.Locked) + " locked / " + level.stickers.Count + " stickers");
            if (!enabled.boolValue)
                EditorGUILayout.HelpBox("Enable Generated Content to apply these percentages. Enabling replaces manual placements, including manually authored frozen/linked stickers. Current board remains unchanged until enabled.", MessageType.Info);
            else if (mystery.floatValue + locked.floatValue > 100)
                EditorGUILayout.HelpBox("Mystery + Locked must total at most 100%. Last valid board will be retained.", MessageType.Error);
            else
                EditorGUILayout.HelpBox("Percentages apply to all stickers and round to whole counts. Preview updates after 0.5 seconds. The key category remains unlocked. If packing fails, lower the ratios or change Layout Seed; the last valid board is retained.", MessageType.Info);
            EditorGUILayout.Space();
        }
    }
}
