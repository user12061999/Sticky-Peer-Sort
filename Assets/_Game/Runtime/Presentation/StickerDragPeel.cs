using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Old Input Module pointer events drive an uncommitted visual ghost. Only a full peel selects.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class StickerDragPeel : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler,
        IDragHandler, IEndDragHandler, IPointerUpHandler, ICancelHandler
    {
        StickerView view;
        GameplayUI screen;
        IGameplayReadModel context;
        IStickerPeelInput input;
        GameSession startedSession;
        int index, pointer;
        bool held, pressPending;
        PeelDragProgress progress;
        Image ghost, destination;
        DestinationPreviewGlow destinationGlow;
        CanvasGroup sourceGroup;
        float sourceAlpha;
        StickerPeelPresentation page;
        StickerBackingPaper backing;
        public StickerBackingPaper TakeBackingPaper() { var paper = backing; backing = null; return paper; }
        Coroutine returning;
        public bool IsPeeling => held;
        public bool IsReturning => returning != null;
        public float Amount { get; private set; }
        public float CompletedAmount { get; private set; }
        public float DirectionDegrees { get; private set; }

        public void Bind(StickerView view, IGameplayReadModel context, IStickerInput input, int index)
        {
            CancelImmediately();
            this.view = view; this.context = context; this.input = input as IStickerPeelInput; this.index = index;
            screen = view.GetComponentInParent<GameplayUI>();
        }
        public void Begin(PointerEventData e)
        {
            pressPending = true; pointer = e.pointerId;
            TryStart(e);
        }
        // A press during a sheet change is remembered, so the same drag starts peeling once input is free.
        void TryStart(PointerEventData e)
        {
            if (held || input == null || screen == null || screen.HasModal || !Application.isPlaying) return;
            if (returning != null) CancelImmediately();
            Vector2 start;
            if (!Point(e, out start) || !input.TryBeginPeel(index)) return;
            startedSession = context.Session; pointer = e.pointerId; held = true; pressPending = false;
            var sticker = startedSession.Stickers[index];
            Vector2 size = sticker.placement.definition.FitSize(sticker.placement.size) * sticker.placement.scale;
            var balance = context.Balance;
            progress = balance.peelFromGrabSide && TryGrabSide(sticker, start, size, balance.peelCornerThreshold, out var inward)
                ? new PeelDragProgress(start, Mathf.Min(size.x, size.y) * balance.grabPeelDistanceRatio, balance.grabPeelDeadZone, inward)
                : new PeelDragProgress(start, Mathf.Min(size.x, size.y) * balance.dragPeelDistanceRatio, balance.dragPeelDeadZone);
            CompletedAmount = Amount = 0; DirectionDegrees = 0;
            ghost = UIFactory.Panel("Interactive peel " + index, screen.effects, StickerPeelLayer.SizeInOverlay(view.Rect, screen.effects), Vector2.zero, view.image.color, view.image.sprite);
            ghost.preserveAspect = view.image.preserveAspect;
            StickerPeelLayer.Prepare(ghost, screen);
            ghost.transform.position = view.Rect.position; ghost.transform.rotation = view.Rect.rotation;
            var definition = sticker.placement.definition;
            var label = definition.showLabel ? UIFactory.Text("Label", ghost.transform, definition.displayName, 25, ghost.rectTransform.sizeDelta * .9f, Vector2.zero) : null;
            page = new StickerPeelPresentation(ghost, label, context.Balance, definition);
            UpdateDirection(); page.Set(0);
            var shadow = ghost.gameObject.AddComponent<Shadow>(); shadow.effectDistance = new Vector2(4, -7); shadow.effectColor = new Color(0, 0, 0, .25f);
            backing = StickerBackingPaper.Create(view, screen, context.Balance, index);
            sourceGroup = view.GetComponent<CanvasGroup>();
            if (sourceGroup == null) sourceGroup = view.gameObject.AddComponent<CanvasGroup>();
            sourceAlpha = sourceGroup.alpha; sourceGroup.alpha = 0;
            HighlightDestination();
            screen.PeelFeel?.Begin(); PeelSignals.Started(index);
        }
        void UpdateDirection()
        {
            Vector2 localDirection = Quaternion.Euler(0, 0, -view.Rect.localEulerAngles.z) * progress.Direction;
            DirectionDegrees = Mathf.Atan2(-localDirection.y, -localDirection.x) * Mathf.Rad2Deg;
            page.DirectionDegrees = DirectionDegrees;
        }
        // Which side was grabbed: a corner when the press is far out on both axes, else the nearest edge.
        // Returns the board-space drag direction that peels from there (towards the opposite side).
        static bool TryGrabSide(StickerRuntime sticker, Vector2 boardPoint, Vector2 size, float cornerThreshold, out Vector2 inward)
        {
            inward = Vector2.zero;
            if (size.x <= 0 || size.y <= 0) return false;
            float r = sticker.rotation * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            Vector2 d = boardPoint - sticker.position;
            Vector2 n = new Vector2((d.x * c + d.y * s) / size.x, (-d.x * s + d.y * c) / size.y) * 2;
            if (n.sqrMagnitude < .04f) return false;
            Vector2 side = Mathf.Abs(n.x) > cornerThreshold && Mathf.Abs(n.y) > cornerThreshold ? new Vector2(Mathf.Sign(n.x) * size.x, Mathf.Sign(n.y) * size.y)
                : Mathf.Abs(n.x) >= Mathf.Abs(n.y) ? new Vector2(Mathf.Sign(n.x), 0) : new Vector2(0, Mathf.Sign(n.y));
            side.Normalize();
            inward = -new Vector2(side.x * c - side.y * s, side.x * s + side.y * c);
            return true;
        }
        bool Point(PointerEventData e, out Vector2 point)
        { return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)view.transform.parent, e.position, e.pressEventCamera, out point); }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }
        public void OnBeginDrag(PointerEventData e) { Drag(e); }
        public void OnDrag(PointerEventData e) { Drag(e); }
        void Drag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            if (!held && pressPending) TryStart(e);
            if (!held) return;
            if (context.Session != startedSession || startedSession.State != GameplayState.Playing) { CancelImmediately(); return; }
            Vector2 point; if (!Point(e, out point)) return;
            float before = Amount;
            Amount = progress.Update(point);
            screen.PeelFeel?.Progress(before, Amount);
            UpdateDirection(); page.Set(Amount);
            if (backing != null) backing.SetPeel(Amount);
            if (Amount < 1) return;
            held = false; CompletedAmount = 1; RestoreDestination();
            if (!input.CommitPeel(index)) { CompletedAmount = 0; Reattach(); }
            else { screen.PeelFeel?.Completed(); PeelSignals.Completed(index); }
        }
        public void OnPointerUp(PointerEventData e) { pressPending = false; if (held && e.pointerId == pointer) Reattach(); }
        public void OnEndDrag(PointerEventData e) { pressPending = false; if (held && e.pointerId == pointer) Reattach(); }
        public void OnCancel(BaseEventData e) { if (held) Reattach(); }
        void Reattach()
        {
            held = false; RestoreDestination();
            // The board is free the moment the finger lifts; the flap only finishes settling visually.
            input?.CancelPeel(index);
            screen.PeelFeel?.Reattached(Amount);
            if (Amount < .04f) PeelSignals.Tap(index);   // pressed and released without peeling = a standalone tap
            returning = StartCoroutine(ReturnToBoard());
        }
        IEnumerator ReturnToBoard()
        {
            float from = Amount, duration = Mathf.Max(.01f, context.Balance.dragReattachDuration), time = 0;
            while (from > .001f && time < duration)
            {
                time += Time.unscaledDeltaTime; Amount = Mathf.Lerp(from, 0, Mathf.SmoothStep(0, 1, time / duration));
                page.Set(Amount); if (backing != null) backing.SetPeel(Amount);
                yield return null;
            }
            Amount = 0; page.Set(0);
            if (backing != null) backing.SetPeel(0);
            yield return null; // Render a fully flat sticker above the board before handing back to its source.
            returning = null; CancelImmediately();
        }
        void HighlightDestination()
        {
            int category, target;
            bool gallery = startedSession.TryGetTarget(index, out category, out target);
            if (gallery && screen.categoryViews.TryGetValue(category, out var card))
                destination = card.targets[target];
            else if (startedSession.Buffer.Count < screen.buffer.childCount)
                destination = screen.buffer.GetChild(startedSession.Buffer.Count).GetComponent<Image>();
            if (destination == null) return;
            destinationGlow = DestinationPreviewGlow.Show(destination, startedSession.Stickers[index].placement.definition, gallery);
        }
        void RestoreDestination() { if (destinationGlow != null) destinationGlow.Hide(); destinationGlow = null; destination = null; }
        public void CancelImmediately()
        {
            if (returning != null) { StopCoroutine(returning); returning = null; }
            held = pressPending = false; input?.CancelPeel(index); RestoreDestination();
            if (backing != null) { backing.Discard(); backing = null; }
            if (sourceGroup != null) sourceGroup.alpha = sourceAlpha;
            if (ghost != null) { ghost.gameObject.SetActive(false); Destroy(ghost.gameObject); }
            ghost = null; page = null; Amount = CompletedAmount = 0;
        }
        void Update()
        {
            if ((held || returning != null) && (context.Session != startedSession || startedSession.State != GameplayState.Playing)) CancelImmediately();
        }
        void OnApplicationPause(bool paused) { if (paused) CancelImmediately(); }
        void OnApplicationFocus(bool focused) { if (!focused) CancelImmediately(); }
        void OnDisable() { CancelImmediately(); }
    }
}
