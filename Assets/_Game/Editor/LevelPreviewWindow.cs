using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    public sealed class LevelPreviewWindow : EditorWindow
    {
        const string Pending = "StickyPeerSort.PendingPreviewLevel";
        [SerializeField] int selected;
        Vector2 scroll;
        [MenuItem("Tools/Sticker Category Game/Level Preview")]
        public static void Open() { var window = GetWindow<LevelPreviewWindow>("Level Preview"); window.minSize = new Vector2(350, 420); window.Show(); }
        [InitializeOnLoadMethod] static void Initialize() { EditorApplication.update += LoadPending; }
        static void LoadPending()
        {
            int index = SessionState.GetInt(Pending, -1);
            if (index < 0 || !EditorApplication.isPlaying) return;
            var c = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (c == null || c.Session == null) return;
            SessionState.SetInt(Pending, -1); c.LoadLevel(index);
        }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Chọn level để xem trong Game View. Preview không tự lưu scene hoặc tiến trình. Play Selected cho phép chơi thử level đã chọn.", MessageType.Info);
            var c = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (c == null) { if (GUILayout.Button("Open Gameplay Scene")) EditorSceneManager.OpenScene(PrototypeBootstrapTool.ScenePath); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < c.levels.Length; i++)
            {
                var level = c.levels[i];
                if (GUILayout.Toggle(selected == i, "Level " + level.levelNumber + "   |   " + level.piles.Count + " trays   |   " + level.difficulty, "Button")) selected = i;
            }
            EditorGUILayout.EndScrollView(); selected = Mathf.Clamp(selected, 0, c.levels.Length - 1);
            if (GUILayout.Button("Preview Selected in Game View")) Preview(selected);
            if (GUILayout.Button("Play Selected"))
            {
                if (EditorApplication.isPlaying) c.LoadLevel(selected);
                else { SessionState.SetInt(Pending, selected); EditorApplication.isPlaying = true; }
            }
            if (GUILayout.Button("Select Level Asset")) Selection.activeObject = c.levels[selected];
        }
        public static void Preview(int index)
        {
            var c = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (c == null) return;
            if (EditorApplication.isPlaying) { c.LoadLevel(index); return; }
            c.ui.Initialize(new PreviewModel(c.levels[index], c.balance), new PreviewInput()); c.ui.Render();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
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
