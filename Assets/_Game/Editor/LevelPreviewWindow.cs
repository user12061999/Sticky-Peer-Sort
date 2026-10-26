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
        UnityEditor.Editor levelEditor;
        LevelReviewPanel review;
        public static event Action<LevelDefinition> PreviewRefreshed;
        void OnEnable() { review = new LevelReviewPanel(Repaint); EditorApplication.update += TickReview; }
        void TickReview() { review?.Tick(); }
        void OnDisable() { EditorApplication.update -= TickReview; review?.Dispose(); if (levelEditor != null) DestroyImmediate(levelEditor); }
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
            if (c.levels == null || c.levels.Length == 0) { EditorGUILayout.HelpBox("Assign levels to GameController first.", MessageType.Warning); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < c.levels.Length; i++)
            {
                var level = c.levels[i];
                if (level == null) { EditorGUILayout.LabelField("Missing level at index " + i); continue; }
                if (GUILayout.Toggle(selected == i, "Level " + level.levelNumber + "   |   " + level.piles.Count + " trays   |   " + level.difficulty, "Button") && selected != i) { selected = i; Preview(i); }
            }
            selected = Mathf.Clamp(selected, 0, c.levels.Length - 1);
            if (c.levels[selected] == null) { EditorGUILayout.EndScrollView(); return; }
            if (GUILayout.Button("Preview Selected in Game View")) Preview(selected);
            if (GUILayout.Button("Play Selected"))
            {
                if (EditorApplication.isPlaying) c.LoadLevel(selected);
                else { SessionState.SetInt(Pending, selected); EditorApplication.isPlaying = true; }
            }
            if (GUILayout.Button("Select Level Asset")) Selection.activeObject = c.levels[selected];
            EditorGUILayout.Space();
            review?.Draw(c, c.levels[selected]);
            EditorGUILayout.Space();
            UnityEditor.Editor.CreateCachedEditor(c.levels[selected], typeof(LevelDefinitionInspector), ref levelEditor);
            // The same authoring inspector is available here and on the level asset.
            if (levelEditor != null) levelEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
        public static void PreviewAsset(LevelDefinition level)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var c = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (c == null) return;
            int index = Array.IndexOf(c.levels, level);
            if (index >= 0) Preview(index, false);
        }
        public static void Preview(int index, bool focusGameView = true)
        {
            var c = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (c == null) return;
            if (c.levels == null || index < 0 || index >= c.levels.Length || c.levels[index] == null) return;
            if (EditorApplication.isPlaying) { c.LoadLevel(index); return; }
            PreviewRefreshed?.Invoke(c.levels[index]);
            if (LevelAnalysis.Analyze(c.levels[index]).verification == LevelVerification.Invalid)
            { ClearPreview(c, "Invalid level — see Level Analysis for details."); return; }
            DisplaySession(c, new GameSession(c.levels[index]));
            if (focusGameView) EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
        public static void DisplaySession(GameController controller, GameSession session)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || controller == null || controller.ui == null) return;
            controller.ui.Initialize(new PreviewModel(session, controller.balance), new PreviewInput()); controller.ui.Render();
        }
        public static void ClearPreview(GameController controller, string message)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || controller == null || controller.ui == null) return;
            var ui = controller.ui;
            UIFactory.Clear(ui.board); UIFactory.Clear(ui.categories); UIFactory.Clear(ui.buffer);
            ui.stickerViews.Clear(); ui.categoryViews.Clear(); ui.waitingViews.Clear();
            ui.Initialize(null, new PreviewInput()); ui.HideModal(); ui.SetInstruction(message);
        }
        sealed class PreviewModel : IGameplayReadModel
        {
            public GameSession Session { get; }
            public SaveData Progress { get; } = new SaveData();
            public GameBalanceConfig Balance { get; }
            public PreviewModel(GameSession session, GameBalanceConfig balance) { Session = session; Balance = balance; }
        }
        sealed class PreviewInput : IStickerInput { public void Tap(int index) { } public void TapAt(Vector2 point) { } }
    }
}
