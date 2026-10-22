using UnityEngine;
namespace StickyPeerSort
{
    public static class StickerAlphaSampling
    {
        // Clamp inside the sprite's own rect: Repeat textures and adjacent atlas cells must not
        // bleed opaque pixels into the opposite transparent edge of an irregular sticker.
        public static float Sample(Texture2D texture, Rect region, Vector2 uv)
        {
            int minX = Mathf.RoundToInt(region.x), minY = Mathf.RoundToInt(region.y);
            int maxX = Mathf.RoundToInt(region.xMax) - 1, maxY = Mathf.RoundToInt(region.yMax) - 1;
            float x = Mathf.Clamp(region.x + uv.x * region.width - .5f, minX, maxX);
            float y = Mathf.Clamp(region.y + uv.y * region.height - .5f, minY, maxY);
            int x0 = (int)x, y0 = (int)y, x1 = Mathf.Min(x0 + 1, maxX), y1 = Mathf.Min(y0 + 1, maxY);
            return Mathf.Lerp(Mathf.Lerp(texture.GetPixel(x0, y0).a, texture.GetPixel(x1, y0).a, x - x0),
                Mathf.Lerp(texture.GetPixel(x0, y1).a, texture.GetPixel(x1, y1).a, x - x0), y - y0);
        }
    }
}
