using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionInspector : UnityEditor.Editor
    {
        string status;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
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
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        }
    }
}
