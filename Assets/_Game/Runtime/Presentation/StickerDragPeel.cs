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
        bool held;
        PeelDragProgress progress;
        Image ghost, destination;
        Color destinationColor;
        CanvasGroup sourceGroup;
        float sourceAlpha;
        StickerPeelPresentation page;
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
            if (held || returning != null || input == null || screen == null || screen.HasModal || !Application.isPlaying) return;
            Vector2 start;
            if (!Point(e, out start) || !input.TryBeginPeel(index)) return;
            startedSession = context.Session; pointer = e.pointerId; held = true;
            var sticker = startedSession.Stickers[index];
            Vector2 size = sticker.placement.definition.FitSize(sticker.placement.size) * sticker.placement.scale;
            progress = new PeelDragProgress(start, Mathf.Min(size.x, size.y) * context.Balance.dragPeelDistanceRatio, context.Balance.dragPeelDeadZone);
            CompletedAmount = Amount = 0; DirectionDegrees = 0;
            ghost = UIFactory.Panel("Interactive peel " + index, screen.effects, view.Rect.sizeDelta * view.Rect.localScale.x, Vector2.zero, view.image.color, view.image.sprite);
            ghost.preserveAspect = view.image.preserveAspect;
            ghost.transform.position = view.Rect.position; ghost.rectTransform.localRotation = view.Rect.localRotation;
            var definition = sticker.placement.definition;
            var label = definition.showLabel ? UIFactory.Text("Label", ghost.transform, definition.displayName, 25, ghost.rectTransform.sizeDelta * .9f, Vector2.zero) : null;
            page = new StickerPeelPresentation(ghost, label, context.Balance); page.Set(0);
            var shadow = ghost.gameObject.AddComponent<Shadow>(); shadow.effectDistance = new Vector2(4, -7); shadow.effectColor = new Color(0, 0, 0, .25f);
            sourceGroup = view.GetComponent<CanvasGroup>();
            if (sourceGroup == null) sourceGroup = view.gameObject.AddComponent<CanvasGroup>();
            sourceAlpha = sourceGroup.alpha; sourceGroup.alpha = 0;
            HighlightDestination();
        }
        bool Point(PointerEventData e, out Vector2 point)
        { return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)view.transform.parent, e.position, e.pressEventCamera, out point); }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }
        public void OnBeginDrag(PointerEventData e) { Drag(e); }
        public void OnDrag(PointerEventData e) { Drag(e); }
        void Drag(PointerEventData e)
        {
            if (!held || e.pointerId != pointer) return;
            if (context.Session != startedSession || startedSession.State != GameplayState.Playing) { CancelImmediately(); return; }
            Vector2 point; if (!Point(e, out point)) return;
            Amount = progress.Update(point);
            Vector2 localDirection = Quaternion.Euler(0, 0, -view.Rect.localEulerAngles.z) * progress.Direction;
            DirectionDegrees = Mathf.Atan2(-localDirection.y, -localDirection.x) * Mathf.Rad2Deg;
            page.DirectionDegrees = DirectionDegrees; page.Set(Amount);
            if (Amount < 1) return;
            held = false; CompletedAmount = 1; RestoreDestination();
            if (!input.CommitPeel(index)) { CompletedAmount = 0; Reattach(); }
        }
        public void OnPointerUp(PointerEventData e) { if (held && e.pointerId == pointer) Reattach(); }
        public void OnEndDrag(PointerEventData e) { if (held && e.pointerId == pointer) Reattach(); }
        public void OnCancel(BaseEventData e) { CancelImmediately(); }
        void Reattach()
        {
            held = false; RestoreDestination();
            if (Amount <= .001f) { CancelImmediately(); return; }
            returning = StartCoroutine(ReturnToBoard());
        }
        IEnumerator ReturnToBoard()
        {
            float from = Amount, duration = Mathf.Max(.01f, context.Balance.dragReattachDuration), time = 0;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime; Amount = Mathf.Lerp(from, 0, Mathf.SmoothStep(0, 1, time / duration));
                page.Set(Amount); yield return null;
            }
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
            destinationColor = destination.color;
            destination.color = gallery ? new Color(.35f, 1, .45f) : new Color(1, .72f, .15f);
        }
        void RestoreDestination() { if (destination != null) destination.color = destinationColor; destination = null; }
        public void CancelImmediately()
        {
            if (returning != null) { StopCoroutine(returning); returning = null; }
            held = false; input?.CancelPeel(index); RestoreDestination();
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
