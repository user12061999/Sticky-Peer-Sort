using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    public static class StickerPeelLayer
    {
        public static Vector2 SizeInOverlay(RectTransform source, RectTransform overlay)
        {
            return Vector2.Scale(source.rect.size, new Vector2(Mathf.Abs(source.lossyScale.x / overlay.lossyScale.x), Mathf.Abs(source.lossyScale.y / overlay.lossyScale.y)));
        }
        // A dedicated sort boundary also stops inherited board masks from clipping the lifted sheet.
        public static void Prepare(Image image, GameplayUI ui)
        {
            var root = ui.GetComponentInParent<Canvas>().rootCanvas;
            int order = root.sortingOrder;
            foreach (var boardCanvas in ui.board.GetComponentsInChildren<Canvas>(true))
                if (boardCanvas.sortingLayerID == root.sortingLayerID) order = Mathf.Max(order, boardCanvas.sortingOrder);
            var canvas = image.gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = image.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingLayerID = root.sortingLayerID;
            canvas.sortingOrder = order + 20;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            image.maskable = false; image.raycastTarget = false; image.transform.SetAsLastSibling();
        }
    }
}
