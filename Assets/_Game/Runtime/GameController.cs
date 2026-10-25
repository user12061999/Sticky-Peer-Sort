using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
namespace StickyPeerSort
{
    // Scene composition root and backwards-compatible scene API; no puzzle rules or animation logic.
    public sealed class GameController : MonoBehaviour, ICoroutineRunner
    {
        public GameplayUI ui;
        public LevelDefinition[] levels;
        public GameAudioConfig audioConfig;
        [Tooltip("Leave empty for the main game. Sample scenes may use an isolated progress key.")]
        public string progressKey;
        [SerializeField, FormerlySerializedAs("balance")] GameBalanceConfig balanceConfig;
        GameplayContext context;
        GameplayCoordinator gameplay;
        MovePresentationPlayer presentation;
        IGameFeedback feedback = new SilentFeedback();
        public GameBalanceConfig balance
        {
            get => balanceConfig;
            set { balanceConfig = value; if (context != null) context.Balance = value; }
        }
        public GameSession Session => context?.Session;
        public SaveService Save { get; private set; }
        public bool IsAnimating => presentation != null && presentation.IsBusy;
        public bool CanTap => gameplay != null && gameplay.CanTap;
        public PuzzleEventKind? PresentationPhase => presentation?.Phase;
        public event Action<PuzzleEvent> PresentationEventFinished;
        public IGameFeedback Feedback
        {
            get => feedback;
            set { feedback = value ?? new SilentFeedback(); if (presentation != null) presentation.Feedback = feedback; }
        }
        void Start()
        {
            Application.targetFrameRate = 60; Application.runInBackground = true; Screen.orientation = ScreenOrientation.Portrait;
            Save = new SaveService(balance.startingBoosters, progressKey);
            context = new GameplayContext(Save.Data, balance);
            var audio = GetComponent<GameAudioFeedback>() ?? gameObject.AddComponent<GameAudioFeedback>();
            audio.Initialize(audioConfig, ui); ui.AudioFeedback = audio;
            if (feedback is SilentFeedback) feedback = audio;
            var celebration = GetComponent<GameCelebration>() ?? gameObject.AddComponent<GameCelebration>();
            celebration.Initialize(ui); ui.Celebration = celebration;
            presentation = new MovePresentationPlayer(ui, context, this) { Feedback = feedback };
            var boosters = new GameplayBoosters(Save, new BoundedPuzzleSolver());
            gameplay = new GameplayCoordinator(context, new LevelRepository(levels), new GameSessionFactory(), Save, boosters, presentation, ui);
            ui.Initialize(context, gameplay); ui.BindCommands(gameplay);
            presentation.EventFinished += ForwardPresentationEvent;
            gameplay.LoadLevel(0);
        }
        void ForwardPresentationEvent(PuzzleEvent e) { PresentationEventFinished?.Invoke(e); }
        public void LoadLevel(int index) { gameplay.LoadLevel(index); }
        public void Tap(int index) { gameplay.Tap(index); }
        public void TapAt(Vector2 point) { gameplay.TapAt(point); }
        public void Undo() { gameplay.Undo(); }
        public void Extra() { gameplay.Extra(); }
        public void Hint() { gameplay.Hint(); }
        public void Shuffle() { gameplay.Shuffle(); }
        public void Run(IEnumerator routine) { StartCoroutine(routine); }
        public void CancelAll() { StopAllCoroutines(); }
        void OnApplicationPause(bool paused) { gameplay?.Suspend(paused); }
        void OnDestroy()
        {
            if (presentation != null) presentation.EventFinished -= ForwardPresentationEvent;
            if (ui != null) ui.UnbindCommands(); gameplay?.Dispose(); Save?.Flush();
        }
    }
}
