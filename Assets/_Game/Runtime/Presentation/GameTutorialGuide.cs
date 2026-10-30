using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Temporary coaching with circular dark spotlight veil around guided stickers.
    public sealed class GameTutorialGuide : MonoBehaviour
    {
        GameController controller;
        SaveService save;
        GameplayUI ui;
        GameSession session;
        RectTransform root, bubble, hand;
        CanvasGroup group;
        TutorialSpotlightGraphic spotlightVeil;
        RectTransform destinationTarget;
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
            actionView = null; keyTarget = null; destinationTarget = null;
            if (ActionStickerIndex >= 0) ui.stickerViews.TryGetValue(ActionStickerIndex, out actionView);
            var special = session.Stickers[SubjectIndex].placement.special;
            if (lesson == TutorialLesson.Lock)
            {
                ActionStickerIndex = -1; actionView = null;
                if (special.unlockCondition == UnlockCondition.CategoryCompleted)
                {
                    foreach (var pair in ui.categoryViews)
                        if (session.Categories[pair.Key].objective.category.id == special.dependencyId)
                        {
                            keyTarget = pair.Value.rect.Find("Unlock key") as RectTransform;
                            if (keyTarget == null) keyTarget = pair.Value.rect;
                            break;
                        }
                }
                else
                {
                    ActionStickerIndex = session.Level.stickers.FindIndex(p => p.instanceId == special.dependencyId);
                    if (ActionStickerIndex >= 0) ui.stickerViews.TryGetValue(ActionStickerIndex, out actionView);
                }
            }
            int targetCategory = -1, targetSlot = -1;
            if (lesson == TutorialLesson.Peel && session.TryGetTarget(SubjectIndex, out targetCategory, out targetSlot) && ui.categoryViews.TryGetValue(targetCategory, out var card))
                destinationTarget = card.targets[targetSlot].rectTransform;
            else if (lesson == TutorialLesson.Lock && keyTarget != null)
                destinationTarget = keyTarget;

            ActiveLesson = lesson; started = Time.unscaledTime;
            root = UIFactory.Rect("First-time tutorial " + lesson, ui.effects.parent, new Vector2(1080, 1920), Vector2.zero);
            root.SetAsLastSibling();
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;

            var guideRect = (actionView != null ? actionView.Rect : subjectView.Rect);

            // 1. Fullscreen dark overlay with circular spotlight cutouts
            var veilGo = UIFactory.Rect("Dark Spotlight Veil", root, new Vector2(4000, 4000), Vector2.zero);
            spotlightVeil = veilGo.gameObject.AddComponent<TutorialSpotlightGraphic>();
            spotlightVeil.color = new Color(0f, 0f, 0f, 0.78f);
            spotlightVeil.SetTarget(guideRect, spotlightVeil.rectTransform, 26f);
            if (destinationTarget != null) spotlightVeil.SetSecondaryTarget(destinationTarget, spotlightVeil.rectTransform, 18f);
            else spotlightVeil.ClearCircle2();

            // 2. Prominent, high-contrast tutorial card dialog
            var outerRim = UIFactory.Panel("Tutorial gold rim", root, new Vector2(926, 252), Vector2.zero, new Color(0.96f, 0.74f, 0.28f, 1f), ui.theme != null ? ui.theme.roundedPanel : ui.panelSprite);
            bubble = outerRim.rectTransform;
            var shadow = outerRim.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.55f);
            shadow.effectDistance = new Vector2(0, -8);

            var cardInner = UIFactory.Panel("Tutorial speech card", outerRim.transform, new Vector2(918, 244), Vector2.zero, new Color(0.08f, 0.12f, 0.23f, 0.98f), ui.theme != null ? ui.theme.roundedPanel : ui.panelSprite);

            string title = lesson == TutorialLesson.Peel ? "PEEL & MATCH" : lesson == TutorialLesson.Mystery ? "MYSTERY STICKER" : "UNLOCK WITH A KEY";
            string body = lesson == TutorialLesson.Peel ? "Hold and drag a bright sticker to peel it.\nMatch its picture to the glowing sheet.\nRelease early to stick it back."
                : lesson == TutorialLesson.Mystery ? "Peel away the sticker covering the ?.\nIts hidden picture will be revealed."
                : special.unlockCondition == UnlockCondition.CategoryCompleted ? "Complete the sheet with the key.\nIts key unlocks the chained stickers."
                : "Remove the required sticker first\nto release these chains.";

            // Lesson icon badge
            var badge = UIFactory.Panel("Lesson badge", cardInner.transform, new Vector2(60, 60), new Vector2(-395, 68), new Color(0.96f, 0.74f, 0.28f, 0.22f), ui.theme != null ? ui.theme.roundedPanel : ui.panelSprite);
            Sprite badgeSprite = lesson == TutorialLesson.Lock ? (ui.theme != null ? ui.theme.keyIcon : null) : (ui.theme != null ? ui.theme.tutorialHand : null);
            if (badgeSprite != null)
            {
                var icon = UIFactory.Panel("Badge icon", badge.transform, new Vector2(44, 44), Vector2.zero, Color.white, badgeSprite);
                icon.preserveAspect = true;
            }
            else if (lesson == TutorialLesson.Mystery)
            {
                var q = UIFactory.Text("Badge question mark", badge.transform, "?", 36, new Vector2(44, 44), Vector2.zero, new Color(1f, 0.88f, 0.35f, 1f));
                q.fontStyle = FontStyles.Bold;
            }

            var heading = UIFactory.Text("Tutorial title TMP", cardInner.transform, title, 36, new Vector2(650, 50), new Vector2(-25, 68), new Color(1f, 0.88f, 0.35f, 1f));
            heading.alignment = TextAlignmentOptions.Left;
            heading.fontStyle = FontStyles.Bold;

            var text = UIFactory.Text("Tutorial explanation TMP", cardInner.transform, body, 29, new Vector2(680, 114), new Vector2(-60, -20), Color.white);
            text.alignment = TextAlignmentOptions.Left;
            text.fontStyle = FontStyles.Normal;

            var button = UIFactory.Button("Dismiss tutorial", cardInner.transform, lesson == TutorialLesson.Peel ? "SKIP" : "GOT IT", new Vector2(165, 58), new Vector2(345, -60), new Color(0.28f, 0.74f, 0.16f, 1f), ui.theme != null ? ui.theme.roundedPanel : ui.panelSprite);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 25;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            if (ui.theme != null) ui.theme.ApplyTextStyle(label);
            button.onClick.AddListener(Dismiss);

            subjectGlow = DestinationPreviewGlow.Show(subjectView.image, subjectView.Definition, false);
            if (destinationTarget != null && lesson == TutorialLesson.Peel && targetCategory >= 0 && ui.categoryViews.TryGetValue(targetCategory, out var destCard))
                destinationGlow = DestinationPreviewGlow.Show(destCard.targets[targetSlot], subjectView.Definition, true);

            if (ui.theme != null && ui.theme.tutorialHand != null)
            {
                var pointer = UIFactory.Panel("Tutorial drag hand", root, Vector2.one * 120, Vector2.zero, Color.white, ui.theme.tutorialHand);
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
            float desiredY = point.y < -150 ? point.y + 265 : point.y - 265;
            bubble.anchoredPosition = new Vector2(0, CardY(desiredY));

            var guideRect = (actionView != null ? actionView.Rect : subjectView.Rect);
            if (spotlightVeil != null && guideRect != null)
            {
                spotlightVeil.SetTarget(guideRect, spotlightVeil.rectTransform, 26f);
                if (destinationTarget != null) spotlightVeil.SetSecondaryTarget(destinationTarget, spotlightVeil.rectTransform, 18f);
            }

            if (hand == null) return;
            bool drag = ActiveLesson == TutorialLesson.Peel || ActiveLesson == TutorialLesson.Mystery && actionView != null;
            float f = Mathf.Clamp01((t % 2.1f - .35f) / 1.1f);
            float travel = drag ? Mathf.SmoothStep(0, 1, f) * 160 : Mathf.Sin(t * 4) * 8;
            hand.anchoredPosition = (Vector2)point + new Vector2(33 + (drag ? travel * .25f : 0), -38 + travel);
            hand.localScale = Vector3.one * (1 - Mathf.Sin(f * Mathf.PI) * .08f);
        }
        float CardY(float preferred)
        {
            var subject = Bounds(subjectView.Rect);
            var action = actionView != null ? Bounds(actionView.Rect) : subject;
            var gallery = Bounds(ui.categories); var waiting = Bounds(ui.buffer);
            float best = 0, score = float.MaxValue;
            foreach (float candidate in new[] { preferred, waiting.center.y, (waiting.yMin + Bounds(ui.board).yMax) * .5f, 0, -320, -540, 560 })
            {
                float y = Mathf.Clamp(candidate, -600, 620);
                var rect = new Rect(-464, y - 127, 928, 254);
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
            root = bubble = hand = destinationTarget = null;
            spotlightVeil = null;
            subjectView = actionView = null; keyTarget = null;
            ActiveLesson = TutorialLesson.None; SubjectIndex = ActionStickerIndex = -1;
        }
        void OnDisable() { Clear(); }
        void OnDestroy() { if (controller != null) controller.PresentationEventFinished -= OnEvent; }
    }
}
