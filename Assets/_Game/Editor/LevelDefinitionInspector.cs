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
            EditorGUILayout.HelpBox("Enable Generation to configure content. Changes rebuild after a short pause and refresh Game View in Edit Mode. Percentages round to whole stickers; four stickers per album. Easy/Normal/Hard changes overlap density. Invalid settings retain the last valid board.", MessageType.Info);
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, "m_Script", "objectives", "stickers", "solutionTrace");
            if (!levelConfig.generation.enabled)
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
    }
}
