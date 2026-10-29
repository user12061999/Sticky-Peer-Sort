using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Temporary, non-blocking coaching. Only its dismiss button receives raycasts.
    public sealed class GameTutorialGuide : MonoBehaviour
    {
        GameController controller;
        SaveService save;
        GameplayUI ui;
        GameSession session;
        RectTransform root, bubble, hand;
        CanvasGroup group;
        StickerView subjectView, actionView;
        RectTransform keyTarget;
        DestinationPreviewGlow subjectGlow, destinationGlow;
        float started;
        readonly Vector3[] corners = new Vector3[4];
        public TutorialLesson ActiveLesson { get; private set; }
        public int SubjectIndex { get; private set; } = -1;
        public int ActionStickerIndex { get; private set; } = -1;
        public bool IsVisible => root != null && root.gameObject.activeSelf;

        public void Initialize(GameController owner, SaveService progress)
        {
            if (controller != null) controller.PresentationEventFinished -= OnEvent;
            controller = owner; save = progress; ui = owner.ui;
            owner.PresentationEventFinished += OnEvent;
        }
        void Update()
        {
            if (controller == null || save == null || controller.Session == null) return;
            if (!controller.balance.tutorialsEnabled) { Clear(); return; }
            if (session != controller.Session) { Clear(); session = controller.Session; }
            var next = TutorialGuidePolicy.Next(session, save.Data.completedTutorials);
            if (next != ActiveLesson) Clear();
            bool interacting = controller.IsAnimating || ui.HasModal || session.State != GameplayState.Playing;
            foreach (var view in ui.stickerViews.Values)
                if (view.DragPeel != null && (view.DragPeel.IsPeeling || view.DragPeel.IsReturning)) { interacting = true; break; }
            if (root != null) SetVisible(!interacting);
            if (interacting || next == TutorialLesson.None) return;
            if (root != null && subjectView == null) Clear();
            if (root == null && !Build(next)) return;
            Animate();
        }
        bool Build(TutorialLesson lesson)
        {
            SubjectIndex = TutorialGuidePolicy.Subject(session, lesson);
            if (SubjectIndex < 0 || !ui.stickerViews.TryGetValue(SubjectIndex, out subjectView)) return false;
            ActionStickerIndex = lesson == TutorialLesson.Mystery ? TutorialGuidePolicy.CoverToRemove(session, SubjectIndex) : SubjectIndex;
            actionView = null; keyTarget = null;
            if (ActionStickerIndex >= 0) ui.stickerViews.TryGetValue(ActionStickerIndex, out actionView);
            var special = session.Stickers[SubjectIndex].placement.special;
            if (lesson == TutorialLesson.Lock)
            {
                ActionStickerIndex = -1; actionView = null;
                if (special.unlockCondition == UnlockCondition.CategoryCompleted)
                {
                    foreach (var pair in ui.categoryViews)
                        if (session.Categories[pair.Key].objective.category.id == special.dependencyId)
                        { keyTarget = pair.Value.rect.Find("Unlock key") as RectTransform; if (keyTarget == null) keyTarget = pair.Value.rect; break; }
                }
                else
                {
                    ActionStickerIndex = session.Level.stickers.FindIndex(p => p.instanceId == special.dependencyId);
                    if (ActionStickerIndex >= 0) ui.stickerViews.TryGetValue(ActionStickerIndex, out actionView);
                }
            }
            ActiveLesson = lesson; started = Time.unscaledTime;
            root = UIFactory.Rect("First-time tutorial " + lesson, ui.effects.parent, new Vector2(1080, 1920), Vector2.zero);
            root.SetAsLastSibling(); group = root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = true;
            var panel = UIFactory.Panel("Tutorial speech card", root, new Vector2(820, 194), Vector2.zero, new Color(.12f, .16f, .28f), ui.theme != null ? ui.theme.roundedPanel : ui.panelSprite);
            bubble = panel.rectTransform;
            var shadow = panel.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .3f); shadow.effectDistance = new Vector2(0, -6);
            string title = lesson == TutorialLesson.Peel ? "PEEL & MATCH" : lesson == TutorialLesson.Mystery ? "MYSTERY STICKER" : "UNLOCK WITH A KEY";
            string body = lesson == TutorialLesson.Peel ? "Hold and drag a bright sticker to peel it.\nMatch its picture to the glowing sheet.\nRelease early to stick it back."
                : lesson == TutorialLesson.Mystery ? "Peel away the sticker covering the ?.\nIts hidden picture will be revealed."
                : special.unlockCondition == UnlockCondition.CategoryCompleted ? "Complete the sheet with the key.\nIts key unlocks the chained stickers."
                : "Remove the required sticker first\nto release these chains.";
            var heading = UIFactory.Text("Tutorial title TMP", bubble, title, 31, new Vector2(680, 45), new Vector2(-30, 57), new Color(1, .85f, .34f));
            heading.alignment = TextAlignmentOptions.Left;
            var text = UIFactory.Text("Tutorial explanation TMP", bubble, body, 26, new Vector2(640, 92), new Vector2(-50, -10), Color.white);
            text.alignment = TextAlignmentOptions.Left;
            var button = UIFactory.Button("Dismiss tutorial", bubble, lesson == TutorialLesson.Peel ? "SKIP" : "GOT IT", new Vector2(125, 42), new Vector2(322, -67), new Color(.3f, .74f, .18f), ui.theme != null ? ui.theme.roundedPanel : ui.panelSprite);
            var label = button.GetComponentInChildren<TextMeshProUGUI>(); label.fontSize = 21; label.color = Color.white;
            if (ui.theme != null) ui.theme.ApplyTextStyle(label);
            button.onClick.AddListener(Dismiss);
            subjectGlow = DestinationPreviewGlow.Show(subjectView.image, subjectView.Definition, false);
            if (lesson == TutorialLesson.Peel && session.TryGetTarget(SubjectIndex, out int category, out int slot) && ui.categoryViews.TryGetValue(category, out var card))
                destinationGlow = DestinationPreviewGlow.Show(card.targets[slot], subjectView.Definition, true);
            if (ui.theme != null && ui.theme.tutorialHand != null)
            {
                var pointer = UIFactory.Panel("Tutorial drag hand", root, Vector2.one * 112, Vector2.zero, Color.white, ui.theme.tutorialHand);
                pointer.preserveAspect = true; hand = pointer.rectTransform;
            }
            SetVisible(true); return true;
        }
        Vector3 Point()
        {
            if (keyTarget != null) return keyTarget.position;
            if (actionView != null)
            {
                var piece = session.Stickers[ActionStickerIndex];
                var fit = piece.placement.definition.FitSize(piece.placement.size);
                // Aim at opaque, clickable artwork rather than transparent sprite padding.
                for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
                {
                    var local = new Vector2((x - 2) * fit.x * .12f, (y - 2) * fit.y * .12f);
                    var world = actionView.Rect.TransformPoint(local);
                    var boardPoint = (Vector2)ui.board.InverseTransformPoint(world);
                    if (BoardGeometry.Hit(session, boardPoint) == ActionStickerIndex) return world;
                }
                return actionView.Rect.position;
            }
            return subjectView.Rect.position;
        }
        void Animate()
        {
            var point = root.InverseTransformPoint(Point()); float t = Time.unscaledTime - started;
            float desiredY = point.y < -200 ? point.y + 225 : point.y - 225;
            bubble.anchoredPosition = new Vector2(0, CardY(desiredY));
            if (hand == null) return;
            bool drag = ActiveLesson == TutorialLesson.Peel || ActiveLesson == TutorialLesson.Mystery && actionView != null;
            float f = Mathf.Clamp01((t % 2.1f - .35f) / 1.1f);
            float travel = drag ? Mathf.SmoothStep(0, 1, f) * 160 : Mathf.Sin(t * 4) * 8;
            hand.anchoredPosition = (Vector2)point + new Vector2(33 + (drag ? travel * .25f : 0), -38 + travel);
            hand.localScale = Vector3.one * (1 - Mathf.Sin(f * Mathf.PI) * .08f);
        }
        float CardY(float preferred)
        {
            // Protect the demonstrated sticker, its cover and the sheet artwork.
            // Empty waiting wells can temporarily sit behind a lesson; occupied wells stay readable.
            var subject = Bounds(subjectView.Rect);
            var action = actionView != null ? Bounds(actionView.Rect) : subject;
            var gallery = Bounds(ui.categories); var waiting = Bounds(ui.buffer);
            float best = 0, score = float.MaxValue;
            foreach (float candidate in new[] { preferred, waiting.center.y, (waiting.yMin + Bounds(ui.board).yMax) * .5f, 0, -300, -520, 550 })
            {
                float y = Mathf.Clamp(candidate, -610, 640);
                var rect = new Rect(-410, y - 97, 820, 194);
                float value = Overlap(rect, subject) * 50 + Overlap(rect, action) * 50 + Overlap(rect, gallery) * 20
                    + Overlap(rect, waiting) * (session.Buffer.Count > 0 ? 10 : .1f) + Mathf.Abs(y - preferred) * .01f;
                if (value < score) { best = y; score = value; }
            }
            return best;
        }
        Rect Bounds(RectTransform rect)
        {
            rect.GetWorldCorners(corners); var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min;
            foreach (var corner in corners) { var point = (Vector2)root.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        static float Overlap(Rect a, Rect b)
        { return Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) * Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin)); }
        void SetVisible(bool visible)
        {
            if (root != null) root.gameObject.SetActive(visible);
            if (subjectGlow != null) subjectGlow.gameObject.SetActive(visible);
            if (destinationGlow != null) destinationGlow.gameObject.SetActive(visible);
        }
        void OnEvent(PuzzleEvent e)
        {
            if (save == null || controller == null || !controller.balance.tutorialsEnabled) return;
            var lesson = e.kind == PuzzleEventKind.Collected && controller.Session.Level.levelNumber == 1 ? TutorialLesson.Peel
                : e.kind == PuzzleEventKind.Revealed ? TutorialLesson.Mystery : e.kind == PuzzleEventKind.Unlocked ? TutorialLesson.Lock : TutorialLesson.None;
            if (lesson == TutorialLesson.None) return;
            save.CompleteTutorial(lesson); if (ActiveLesson == lesson) Clear();
        }
        public void Dismiss()
        { if (ActiveLesson != TutorialLesson.None) save.CompleteTutorial(ActiveLesson); Clear(); }
        public void Clear()
        {
            if (subjectGlow != null) subjectGlow.Hide();
            if (destinationGlow != null) destinationGlow.Hide();
            subjectGlow = destinationGlow = null;
            if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            root = bubble = hand = null; subjectView = actionView = null; keyTarget = null;
            ActiveLesson = TutorialLesson.None; SubjectIndex = ActionStickerIndex = -1;
        }
        void OnDisable() { Clear(); }
        void OnDestroy() { if (controller != null) controller.PresentationEventFinished -= OnEvent; }
    }
}
