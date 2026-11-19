using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Partner gesture (17/11): a press on the tray paper next to a sticker that slides into the sticker starts peeling it
    // from the point where the finger crosses its outline. This catcher sits behind the board and only answers inside
    // the drawn trays, so buttons, boosters, cards, slots and popups (all above it) keep every press they receive.
    // Stickers that cannot be peeled (covered, locked, frozen, outside a tutorial's focus) are slid over: the gesture
    // stays alive and the first playable sticker the finger reaches starts peeling (owner 17/11). A press within a playable
    // sticker's edge band (20% of its art's size outside the outline) starts peeling it straight away (owner 18/11).
    [RequireComponent(typeof(Image))]
    public sealed class BoardDragIn : MonoBehaviour, ICanvasRaycastFilter, IPointerDownHandler, IInitializePotentialDragHandler,
        IBeginDragHandler, IDragHandler, IPointerUpHandler
    {
        GameplayUI ui;
        IGameplayReadModel context;
        GameTutorialGuide tutorial;
        int pointer;
        bool armed;
        int beside = -1;   // playable sticker whose edge band the press landed in (owner 18/11)
        Vector2 last;
        // Distance between samples along the finger path, in board units: smaller than any sticker's thinnest part.
        const float Step = 6;

        public static BoardDragIn Ensure(GameplayUI ui, IGameplayReadModel context)
        {
            var parent = ui.board.parent;
            var catcher = parent.GetComponentInChildren<BoardDragIn>(true);
            if (catcher == null)
            {
                var go = new GameObject("Board drag-in catcher", typeof(RectTransform), typeof(Image), typeof(BoardDragIn));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = go.GetComponent<Image>(); image.color = Color.clear; image.raycastTarget = true;
                catcher = go.GetComponent<BoardDragIn>();
            }
            int board = ui.board.GetSiblingIndex(), own = catcher.transform.GetSiblingIndex();
            if (own != board - 1) catcher.transform.SetSiblingIndex(own < board ? board - 1 : board);   // directly behind the stickers
            catcher.ui = ui; catcher.context = context; catcher.armed = false;
            return catcher;
        }
        // Only the tray paper takes presses; everywhere else the raycast falls through as if the catcher was not there.
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera camera)
        {
            if (ui == null || context?.Session == null || !context.Balance.dragToPeel) return false;
            foreach (Transform child in ui.board)
                if (child.GetComponent<StickerView>() == null && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)child, screenPoint, camera))
                    return true;
            return false;
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || armed || !Free()) return;
            if (!BoardPoint(e, out last)) return;
            armed = true; pointer = e.pointerId;
            if (tutorial == null) tutorial = FindFirstObjectByType<GameTutorialGuide>();
            // A press just outside a playable sticker, within its edge band, grabs that edge at once, like a press on the rim.
            // It is handed over in OnInitializePotentialDrag, after the event system has picked this catcher as the target.
            beside = BoardGeometry.Hit(context.Session, last) < 0
                ? BoardGeometry.ResolveTouch(context.Session, last, context.Balance.touchSlop, context.Balance.peelEdgeBand) : -1;
        }
        public void OnInitializePotentialDrag(PointerEventData e)
        {
            e.useDragThreshold = false;
            if (armed && beside >= 0 && e.pointerId == pointer && Enter(beside, last, e)) armed = false;
            beside = -1;
        }
        public void OnBeginDrag(PointerEventData e) { Slide(e); }
        public void OnDrag(PointerEventData e) { Slide(e); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) { armed = false; beside = -1; } }
        void Slide(PointerEventData e)
        {
            if (!armed || e.pointerId != pointer) return;
            if (!Free()) { armed = false; return; }
            Vector2 point; if (!BoardPoint(e, out point)) return;
            // Walk the path since the last event, so a fast swipe cannot jump over a thin stem.
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(last, point) / Step));
            for (int s = 1; s <= steps; s++)
            {
                Vector2 q = Vector2.Lerp(last, point, (float)s / steps);
                int hit = BoardGeometry.Hit(context.Session, q);
                if (hit < 0 || !Enter(hit, q, e)) continue;
                armed = false; return;
            }
            last = point;
        }
        bool Enter(int index, Vector2 entry, PointerEventData e)
        {
            var session = context.Session;
            var sticker = session.Stickers[index];
            if (!session.Legal(index) || sticker.placement.special.kind == SpecialKind.Frozen && sticker.ice > 0) return false;
            if (tutorial != null && tutorial.IsVisible && index != tutorial.ActionStickerIndex && index != tutorial.SubjectIndex) return false;
            StickerView view;
            if (!ui.stickerViews.TryGetValue(index, out view) || view.DragPeel == null) return false;
            // The rest of this press (drags, release) now goes to the sticker, exactly as if it had been pressed.
            e.pointerPress = e.rawPointerPress = e.pointerDrag = view.gameObject;
            e.eligibleForClick = false;
            view.DragPeel.BeginAt(e, entry);
            return true;
        }
        bool Free()
        {
            return ui != null && context?.Session != null && context.Balance.dragToPeel && !ui.HasModal
                && context.Session.State == GameplayState.Playing;
        }
        bool BoardPoint(PointerEventData e, out Vector2 point)
        { return RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.board, e.position, e.pressEventCamera, out point); }
        void OnDisable() { armed = false; beside = -1; }
    }
}
