using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // The alpha silhouette supplies the contour; no rectangular frame or solid recolor is used.
    [RequireComponent(typeof(Image))]
    public sealed class DestinationPreviewGlow : MonoBehaviour
    {
        public const int ContourPadding = 18;
        const float WidthMultiplier = 3;
        Texture2D texture;
        Sprite sprite;
        public Image OutlineImage { get; private set; }
        public static DestinationPreviewGlow Show(Image target, StickerDefinition definition, bool gallery)
        {
            var rect = UIFactory.Rect("Sticker contour glow", target.transform, Vector2.zero, Vector2.zero);
            var glow = rect.gameObject.AddComponent<DestinationPreviewGlow>();
            glow.Build(definition);
            Vector2 size = target.rectTransform.rect.size - (gallery ? Vector2.zero : new Vector2(10, 19));
            bool preserve = gallery ? target.preserveAspect : definition.preserveAspect;
            var source = definition.SourceRect;
            if (preserve && source.width > 0 && source.height > 0)
            {
                float fit = Mathf.Min(size.x / source.width, size.y / source.height);
                size = new Vector2(source.width, source.height) * fit;
            }
            rect.sizeDelta = Vector2.Scale(size, new Vector2((float)glow.texture.width / (glow.texture.width - ContourPadding * 2), (float)glow.texture.height / (glow.texture.height - ContourPadding * 2)));
            glow.OutlineImage = glow.GetComponent<Image>(); glow.OutlineImage.sprite = glow.sprite;
            glow.OutlineImage.raycastTarget = false; glow.OutlineImage.maskable = false; glow.OutlineImage.preserveAspect = false;
            glow.Update();
            return glow;
        }
        public void Hide() { gameObject.SetActive(false); Destroy(gameObject); }
        void Update()
        {
            if (OutlineImage == null) return;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            OutlineImage.color = new Color(.12f, .95f, .26f, .4f + .22f * pulse);
        }
        void Build(StickerDefinition definition)
        {
            const int padding = ContourPadding, resolution = 192;
            var source = definition.SourceRect; float scale = resolution / Mathf.Max(1, Mathf.Max(source.width, source.height));
            int artWidth = Mathf.Max(8, Mathf.RoundToInt(source.width * scale)), artHeight = Mathf.Max(8, Mathf.RoundToInt(source.height * scale));
            int width = artWidth + padding * 2, height = artHeight + padding * 2;
            var distance = new float[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                distance[y * width + x] = definition.ContainsUV(new Vector2((x - padding + .5f) / artWidth, (y - padding + .5f) / artHeight)) ? 0 : 10000;
            // Two-pass chamfer distance gives a soft, narrow ring around arbitrary PNG alpha.
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (x > 0) distance[i] = Mathf.Min(distance[i], distance[i - 1] + 1);
                if (y > 0) distance[i] = Mathf.Min(distance[i], distance[i - width] + 1);
                if (x > 0 && y > 0) distance[i] = Mathf.Min(distance[i], distance[i - width - 1] + 1.4142f);
                if (x + 1 < width && y > 0) distance[i] = Mathf.Min(distance[i], distance[i - width + 1] + 1.4142f);
            }
            for (int y = height - 1; y >= 0; y--) for (int x = width - 1; x >= 0; x--)
            {
                int i = y * width + x;
                if (x + 1 < width) distance[i] = Mathf.Min(distance[i], distance[i + 1] + 1);
                if (y + 1 < height) distance[i] = Mathf.Min(distance[i], distance[i + width] + 1);
                if (x + 1 < width && y + 1 < height) distance[i] = Mathf.Min(distance[i], distance[i + width + 1] + 1.4142f);
                if (x > 0 && y + 1 < height) distance[i] = Mathf.Min(distance[i], distance[i + width - 1] + 1.4142f);
            }
            var pixels = new Color[distance.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                float d = distance[i] / WidthMultiplier;
                float alpha = d == 0 ? 0 : Mathf.Max(1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.4f, 2.8f, d)), .18f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.8f, 5.2f, d))));
                pixels[i] = new Color(1, 1, 1, alpha);
            }
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Sticker alpha contour", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels); texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
        }
        void OnDestroy()
        {
            if (sprite != null) { if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite); }
            if (texture != null) { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
        }
    }
}
