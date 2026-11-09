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
        bool held, pressPending, straining, destinationPending;
        float rawAmount, strainTime;
        // While a collection sheet is replaced the flap stops here, just short of detaching, until the new card is in.
        const float SheetChangeHold = .82f;
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
            progress = balance.peelFromGrabSide && TryGrabEdge(sticker, start, size, out var inward, out var across)
                ? new PeelDragProgress(start, Mathf.Clamp(across, Mathf.Min(size.x, size.y) * .5f, Mathf.Max(size.x, size.y)) * balance.grabPeelDistanceRatio, balance.grabPeelDeadZone, inward)
                : new PeelDragProgress(start, Mathf.Min(size.x, size.y) * balance.dragPeelDistanceRatio, balance.dragPeelDeadZone);
            CompletedAmount = Amount = rawAmount = 0; DirectionDegrees = 0; straining = false;
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
            HighlightDestination(); destinationPending = destination == null;
            screen.PeelFeel?.Begin(); PeelSignals.Started(index);
        }
        void UpdateDirection()
        {
            Vector2 localDirection = Quaternion.Euler(0, 0, -view.Rect.localEulerAngles.z) * progress.Direction;
            DirectionDegrees = Mathf.Atan2(-localDirection.y, -localDirection.x) * Mathf.Rad2Deg;
            page.DirectionDegrees = DirectionDegrees;
        }
        // Partner gesture: grab the sticker at its edge, even just outside it, and drag across it.
        // The flap lifts at the grabbed point and peels towards the far side; a press near the centre
        // has no clear edge, so there the first drag movement sets the direction instead.
        const float CentreGrab = .35f;
        static bool TryGrabEdge(StickerRuntime sticker, Vector2 boardPoint, Vector2 size, out Vector2 inward, out float across)
        {
            inward = Vector2.zero; across = 0;
            if (size.x <= 0 || size.y <= 0) return false;
            float r = sticker.rotation * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            Vector2 d = boardPoint - sticker.position;
            Vector2 n = new Vector2((d.x * c + d.y * s) / size.x, (-d.x * s + d.y * c) / size.y) * 2;
            if (n.magnitude < CentreGrab) return false;
            inward = -d.normalized; across = 2 * d.magnitude;
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
            rawAmount = progress.Update(point);
            UpdateDirection(); Apply();
        }
        // During a sheet change the commit must wait for the new card, so the flap holds at SheetChangeHold and strains
        // (wobble + one light pulse). Once the sheet is in, a flap still held far enough detaches on its own.
        void Apply()
        {
            float before = Amount;
            bool waiting = input.CommitBlocked;
            Amount = waiting ? Mathf.Min(rawAmount, SheetChangeHold) : rawAmount;
            SetStraining(waiting && rawAmount >= SheetChangeHold);
            screen.PeelFeel?.Progress(before, Amount);
            page.Set(Amount);
            if (backing != null) backing.SetPeel(Amount);
            if (Amount < 1) return;
            held = false; SetStraining(false); CompletedAmount = 1; RestoreDestination();
            if (!input.CommitPeel(index)) { CompletedAmount = 0; Reattach(); }
            else { screen.PeelFeel?.Completed(); PeelSignals.Completed(index); }
        }
        void SetStraining(bool on)
        {
            if (on == straining) return;
            straining = on; strainTime = 0;
            if (on && context.Balance.haptics) GameHaptics.Pulse(12, 70, true);
            if (!on && ghost != null) ghost.transform.rotation = view.Rect.rotation;
        }
        public bool IsHeldBySheetChange => straining;
        public void OnPointerUp(PointerEventData e) { pressPending = false; if (held && e.pointerId == pointer) Reattach(); }
        public void OnEndDrag(PointerEventData e) { pressPending = false; if (held && e.pointerId == pointer) Reattach(); }
        public void OnCancel(BaseEventData e) { if (held) Reattach(); }
        void Reattach()
        {
            held = false; SetStraining(false); RestoreDestination();
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
            held = pressPending = straining = destinationPending = false; rawAmount = 0; input?.CancelPeel(index); RestoreDestination();
            if (backing != null) { backing.Discard(); backing = null; }
            if (sourceGroup != null) sourceGroup.alpha = sourceAlpha;
            if (ghost != null) { ghost.gameObject.SetActive(false); Destroy(ghost.gameObject); }
            ghost = null; page = null; Amount = CompletedAmount = 0;
        }
        void Update()
        {
            if ((held || returning != null) && (context.Session != startedSession || startedSession.State != GameplayState.Playing)) { CancelImmediately(); return; }
            if (!held) return;
            if (straining)
            {
                strainTime += Time.unscaledDeltaTime;
                if (ghost != null) ghost.transform.rotation = view.Rect.rotation * Quaternion.Euler(0, 0, Mathf.Sin(strainTime * 38) * 2.2f);
            }
            if (input.CommitBlocked) return;
            // The new sheet is in: show where the sticker will land, then release a flap that was holding at the stop.
            if (destinationPending) { destinationPending = false; HighlightDestination(); }
            if (Amount < rawAmount) Apply();
        }
        void OnApplicationPause(bool paused) { if (paused) CancelImmediately(); }
        void OnApplicationFocus(bool focused) { if (!focused) CancelImmediately(); }
        void OnDisable() { CancelImmediately(); }
    }
}
