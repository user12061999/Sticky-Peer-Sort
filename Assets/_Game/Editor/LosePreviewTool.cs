using UnityEditor;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    public static class LosePreviewTool
    {
        static readonly int[] Path = { 1, 2, 4, 7, 5, 8, 10, 13, 12, 11 };
        static GameController controller;
        static int step;
        static double next;
        [MenuItem("Tools/Sticker Category Game/Preview Lose - Level 3 (Play Mode)")]
        public static void Preview()
        {
            Stop(); controller = Object.FindFirstObjectByType<GameController>();
            if (controller == null || controller.Session == null) return;
            int index = System.Array.FindIndex(controller.levels, level => level.levelNumber == 3);
            if (index < 0) { Stop(); return; }
            controller.LoadLevel(index); step = 0; next = EditorApplication.timeSinceStartup + .35;
            EditorApplication.update += Tick;
        }
        [MenuItem("Tools/Sticker Category Game/Preview Lose - Level 3 (Play Mode)", true)]
        static bool CanPreview()
        {
            var game = Object.FindFirstObjectByType<GameController>();
            return EditorApplication.isPlaying && !EditorApplication.isPaused && game != null && System.Array.Exists(game.levels, level => level.levelNumber == 3);
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || controller == null || controller.Session == null) { Stop(); return; }
            if (controller.IsAnimating || EditorApplication.timeSinceStartup < next) return;
            if (controller.Session.State == GameplayState.Fail || step >= Path.Length)
            {
                Debug.Log("Lose preview: " + controller.Session.State + ", tray " + controller.Session.Buffer.Count + "/" + controller.Session.Capacity + ". Undo and Retry are playable."); Stop(); return;
            }
            if (!controller.CanTap || !controller.Session.Legal(Path[step]))
            { Debug.LogWarning("Lose preview interrupted. Restart the preview after changing the level layout."); Stop(); return; }
            controller.Tap(Path[step++]); next = EditorApplication.timeSinceStartup + .45;
        }
        static void Stop() { EditorApplication.update -= Tick; controller = null; }
    }
}
