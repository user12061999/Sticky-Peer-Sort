using UnityEngine.UI;
namespace StickyPeerSort
{
    // One image mapping for board, flights, buffer, silhouettes and filled category targets.
    public static class StickerVisuals
    {
        public static void Apply(Image image, StickerDefinition definition)
        {
            image.sprite = definition.ArtworkSprite; image.type = Image.Type.Simple;
            image.preserveAspect = definition.preserveAspect; image.color = definition.tint;
        }
    }
}
