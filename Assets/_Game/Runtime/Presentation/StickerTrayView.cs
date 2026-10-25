using UnityEngine;

namespace StickyPeerSort
{
    // Reusable compartment prefab; all layout comes from the level, never the reference image.
    public sealed class StickerTrayView : MonoBehaviour
    {
        public void Bind(BoardPileDefinition pile)
        {
            name = "Sticker tray " + pile.id;
            var rect = (RectTransform)transform;
            rect.sizeDelta = pile.size; rect.anchoredPosition = pile.position;
            rect.localRotation = Quaternion.Euler(0, 0, pile.rotation);
        }
    }
}
