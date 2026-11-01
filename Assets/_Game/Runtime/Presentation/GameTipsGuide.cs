using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Spec "Tutorial" 2–5 and "Recurring drag guide". Non-blocking: nothing here selects a sticker or changes rules.
    // - Level 1 tips (once each, saved): auto move to the sheet, sheet completion/replacement, holding area, auto collection
    //   from the holding area. If the player has not used the holding area after the first sheet swap, a hand points at a
    //   sticker whose sheet is not shown yet.
    // - Recurring drag guide: every level until its first successful peel, a standalone tap on an available sticker shows the
    //   hand + trail at that sticker's edge after a short delay; any drag start hides it or cancels the pending one.
    public sealed class GameTipsGuide : MonoBehaviour
    {
        GameController controller;
        SaveService save;
        GameplayUI ui;
        GameTutorialGuide tutorial;
        GameSession session;
        bool firstPeelDone;
        int pendingTap = -1; float pendingAt;
        RectTransform hintRoot, hintHand; readonly List<Image> trail = new List<Image>(); int hintIndex = -1; float hintStarted; bool hintPersistent;
        Vector2 hintOutward;
        RectTransform toastRoot; CanvasGroup toastGroup; TextMeshProUGUI toastText; float toastUntil;
        readonly Queue<KeyValuePair<string, float>> queued = new Queue<KeyValuePair<string, float>>();   // tips never overwrite each other
        readonly HashSet<int> waited = new HashSet<int>();
        readonly Vector3[] corners = new Vector3[4];
        public bool DragGuideVisible => hintRoot != null && hintRoot.gameObject.activeSelf && !hintPersistent;
        public bool ToastVisible => toastRoot != null && toastGroup.alpha > .01f;
        public string ToastMessage => toastText != null ? toastText.text : "";

        public void Initialize(GameController owner, SaveService progress)
        {
            Unsubscribe();
            controller = owner; save = progress; ui = owner.ui; tutorial = owner.GetComponent<GameTutorialGuide>();
            owner.PresentationEventFinished += OnEvent;
            PeelSignals.StandaloneTap += OnTap; PeelSignals.DragStarted += OnDragStarted; PeelSignals.PeelCompleted += OnPeelCompleted;
        }
        void Unsubscribe()
        {
            if (controller != null) controller.PresentationEventFinished -= OnEvent;
            PeelSignals.StandaloneTap -= OnTap; PeelSignals.DragStarted -= OnDragStarted; PeelSignals.PeelCompleted -= OnPeelCompleted;
        }
        void OnDestroy() { Unsubscribe(); }

        bool Done(TutorialLesson lesson) => (save.Data.completedTutorials & (int)lesson) != 0;
        bool LevelOne => session != null && session.Level.levelNumber == 1 && controller.balance.tutorialsEnabled;

        void Update()
        {
            if (controller == null || controller.Session == null) return;
            if (session != controller.Session) { session = controller.Session; firstPeelDone = false; pendingTap = -1; waited.Clear(); HideHint(); }
            bool busy = ui.HasModal || session.State != GameplayState.Playing || (tutorial != null && tutorial.IsVisible);
            if (pendingTap >= 0 && Time.unscaledTime >= pendingAt)
            {
                int index = pendingTap; pendingTap = -1;
                if (!busy && !firstPeelDone && session.Legal(index)) ShowHint(index, false);
            }
            if (hintRoot != null)
            {
                bool expired = !hintPersistent && Time.unscaledTime - hintStarted > controller.balance.dragGuideDuration;
                if (expired || busy || hintIndex < 0 || !session.Legal(hintIndex)) HideHint();
                else AnimateHint();
            }
            // Level 1 shows its third sheet as soon as the first one completes, so the holding lesson is offered right after the first
            // automatic move, while a sticker without a displayed sheet still exists.
            if (!busy && hintRoot == null && LevelOne && Done(TutorialLesson.AutoMove) && !Done(TutorialLesson.Holding) && !controller.IsAnimating && !ToastVisible)
                PointAtHoldingCandidate();
            if (queued.Count > 0 && (toastRoot == null || Time.unscaledTime >= toastUntil && toastGroup.alpha <= .01f)) { var nextTip = queued.Dequeue(); Show(nextTip.Key, nextTip.Value); }
            if (toastRoot != null)
            {
                float target = Time.unscaledTime < toastUntil && !(tutorial != null && tutorial.IsVisible) ? 1 : 0;
                toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, target, Time.unscaledDeltaTime * 5);
            }
        }

        // ---------- recurring drag guide ----------
        void OnTap(int index)
        {
            if (controller != null && session != controller.Session) { session = controller.Session; firstPeelDone = false; waited.Clear(); HideHint(); }
            if (session == null || firstPeelDone || !session.Legal(index)) return;
            pendingTap = index; pendingAt = Time.unscaledTime + controller.balance.dragGuideDelay;
            if (hintRoot != null && !hintPersistent) HideHint();
        }
        void OnDragStarted(int index) { pendingTap = -1; if (hintRoot != null) HideHint(); }
        void OnPeelCompleted(int index) { firstPeelDone = true; pendingTap = -1; HideHint(); }

        void ShowHint(int index, bool persistent)
        {
            HideHint();
            if (ui.theme == null || ui.theme.tutorialHand == null || !ui.stickerViews.ContainsKey(index)) return;
            hintIndex = index; hintPersistent = persistent; hintStarted = Time.unscaledTime;
            hintRoot = UIFactory.Rect(persistent ? "Holding area pointer" : "Recurring drag guide", ui.effects.parent, new Vector2(1080, 1920), Vector2.zero);
            hintRoot.SetAsLastSibling();
            var group = hintRoot.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            trail.Clear();
            for (int i = 0; i < 5; i++)
            {
                var dot = UIFactory.Panel("Trail " + i, hintRoot, Vector2.one * (26 - i * 3), Vector2.zero, new Color(1, 1, 1, .0f), ui.theme.roundedPanel != null ? ui.theme.roundedPanel : ui.panelSprite);
                dot.raycastTarget = false; trail.Add(dot);
            }
            var pointer = UIFactory.Panel("Hand", hintRoot, Vector2.one * 120, Vector2.zero, Color.white, ui.theme.tutorialHand);
            pointer.preserveAspect = true; pointer.raycastTarget = false; hintHand = pointer.rectTransform;
            AnimateHint();
        }
        void AnimateHint()
        {
            Vector2 edge, direction;
            BoardGeometry.TryEdgePoint(session, hintIndex, controller.balance.peelEdgeBand, out edge, out direction);
            Vector2 start = hintRoot.InverseTransformPoint(ui.board.TransformPoint(edge));
            hintOutward = ((Vector2)hintRoot.InverseTransformVector(ui.board.TransformVector(direction))).normalized;
            float t = Time.unscaledTime - hintStarted;
            float f = Mathf.Clamp01((t % 1.6f - .25f) / .9f), travel = Mathf.SmoothStep(0, 1, f) * 170;
            hintHand.anchoredPosition = start + new Vector2(33, -38) + hintOutward * travel;
            hintHand.localScale = Vector3.one * (1 - Mathf.Sin(f * Mathf.PI) * .08f);
            for (int i = 0; i < trail.Count; i++)
            {
                float back = travel - i * 28;
                trail[i].rectTransform.anchoredPosition = start + hintOutward * Mathf.Max(0, back);
                trail[i].color = new Color(1, 1, 1, back > 0 && f < 1 ? .55f * (1 - i / (float)trail.Count) : 0);
            }
        }
        void HideHint()
        {
            if (hintRoot != null) { hintRoot.gameObject.SetActive(false); Destroy(hintRoot.gameObject); }
            hintRoot = hintHand = null; trail.Clear(); hintIndex = -1; hintPersistent = false;
        }

        // Step 4: point at a peelable sticker whose sheet is not on screen, so its trip to the holding area can be shown.
        void PointAtHoldingCandidate()
        {
            if (session.Buffer.Count >= session.Capacity - 1) return;
            for (int i = 0; i < session.Stickers.Count; i++)
                if (session.Stickers[i].location == StickerLocation.Board && session.Legal(i) && !session.HasTarget(i))
                {
                    ShowHint(i, true);
                    Toast("No sheet for this one yet? Peel it anyway.\nIt waits in the holding area.", 6);
                    return;
                }
        }

        // ---------- level-1 tips ----------
        void OnEvent(PuzzleEvent e)
        {
            if (!LevelOne || save == null) return;
            switch (e.kind)
            {
                case PuzzleEventKind.Buffered:
                    waited.Add(e.sticker);
                    if (!Done(TutorialLesson.Holding))
                    {
                        Complete(TutorialLesson.Holding); if (hintPersistent) HideHint();
                        if (toastGroup != null) { toastUntil = 0; toastGroup.alpha = 0; }   // replace the pointer tip immediately
                        Toast("Stickers without a sheet wait here.\nIf all 4 holding slots fill up, you lose!", 3.4f);
                    }
                    break;
                case PuzzleEventKind.Collected:
                    if (waited.Remove(e.sticker))
                    {
                        if (!Done(TutorialLesson.HoldingCollect)) { Complete(TutorialLesson.HoldingCollect); Toast("Its sheet appeared, so the waiting\nsticker jumped in automatically!", 3.2f); }
                    }
                    else if (!Done(TutorialLesson.AutoMove)) { Complete(TutorialLesson.AutoMove); Toast("Peeled stickers fly to their\nmatching sheet automatically!", 2.8f); }
                    break;
                case PuzzleEventKind.CategoryCompleted:
                    if (!Done(TutorialLesson.SheetSwap)) { Complete(TutorialLesson.SheetSwap); Toast("4 matching stickers complete a sheet.\nA new sheet slides in!", 3.2f); }
                    break;
            }
        }
        void Complete(TutorialLesson lesson) { save.CompleteTutorial(lesson); }

        void Toast(string message, float seconds)
        {
            if (toastRoot != null && Time.unscaledTime < toastUntil && toastText.text != message) { queued.Enqueue(new KeyValuePair<string, float>(message, seconds)); return; }
            Show(message, seconds);
        }
        void Show(string message, float seconds)
        {
            if (toastRoot == null)
            {
                toastRoot = UIFactory.Rect("Tutorial tip", ui.effects.parent, new Vector2(900, 150), Vector2.zero);
                toastGroup = toastRoot.gameObject.AddComponent<CanvasGroup>(); toastGroup.blocksRaycasts = false; toastGroup.alpha = 0;
                var sprite = ui.theme != null ? ui.theme.roundedPanel : ui.panelSprite;
                var rim = UIFactory.Panel("Tip rim", toastRoot, new Vector2(900, 150), Vector2.zero, new Color(.96f, .74f, .28f, 1), sprite); rim.raycastTarget = false;
                var card = UIFactory.Panel("Tip card", rim.transform, new Vector2(890, 140), Vector2.zero, new Color(.08f, .12f, .23f, .96f), sprite); card.raycastTarget = false;
                toastText = UIFactory.Text("Tip text", card.transform, "", 34, new Vector2(840, 130), Vector2.zero, Color.white);
                toastText.alignment = TextAlignmentOptions.Center; toastText.fontStyle = FontStyles.Bold;
                if (ui.theme != null) ui.theme.ApplyTextStyle(toastText);
            }
            toastRoot.SetAsLastSibling();
            toastText.text = message; toastUntil = Time.unscaledTime + seconds;
            ui.board.GetWorldCorners(corners);
            float top = toastRoot.parent.InverseTransformPoint(corners[1]).y;
            toastRoot.anchoredPosition = new Vector2(0, top - 95);   // just inside the top of the board, below sheets and holding area
        }
    }
}
