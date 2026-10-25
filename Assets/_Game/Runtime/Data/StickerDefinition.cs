using UnityEngine;
namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/Sticker")]
    public sealed class StickerDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public CategoryDefinition category;
        [Header("Artwork (a texture overrides the optional Sprite)")]
        [Tooltip("Assign any transparent PNG/Texture2D. Its alpha defines the sticker silhouette; no basic shape template is needed.")]
        public Texture2D texture;
        public Sprite sprite;
        [Tooltip("Optional flat album silhouette, generated independently from the artwork alpha.")]
        public Sprite targetSilhouette;
        public bool preserveAspect = true;
        [Range(.01f, 1)] public float alphaThreshold = .15f;
        [Range(32, 512)] public int alphaMaskResolution = 256;
        public Color tint = Color.white;
        [Tooltip("Turn off after assigning production artwork if its label is unnecessary.")]
        public bool showLabel = true;
        [SerializeField, HideInInspector] Texture2D maskTexture;
        [SerializeField, HideInInspector] Sprite maskSprite;
        [SerializeField, HideInInspector] int maskWidth, maskHeight;
        [SerializeField, HideInInspector] byte[] alphaMask;
        [System.NonSerialized] Sprite generatedSprite;
        [System.NonSerialized] Texture2D generatedTexture;
        public Texture2D SourceTexture => texture != null ? texture : sprite != null ? sprite.texture : null;
        public Rect SourceRect => texture != null ? new Rect(0, 0, texture.width, texture.height) : sprite != null ? sprite.rect : Rect.zero;
        public bool HasArtwork => SourceTexture != null;
        public bool HasAlphaMask => maskTexture == SourceTexture && maskSprite == (texture != null ? null : sprite) &&
            alphaMask != null && maskWidth > 0 && maskHeight > 0 && alphaMask.Length == maskWidth * maskHeight;
        public bool CanSampleAlpha => HasAlphaMask || SourceTexture != null && SourceTexture.isReadable;
        public Sprite ArtworkSprite
        {
            get
            {
                if (texture == null) return sprite;
                if (generatedSprite == null || generatedTexture != texture)
                {
                    ReleaseGeneratedSprite(); generatedTexture = texture;
                    generatedSprite = Sprite.Create(texture, SourceRect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                    generatedSprite.name = name + " Artwork"; generatedSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                return generatedSprite;
            }
        }
        public Vector2 FitSize(Vector2 bounds)
        {
            var rect = SourceRect;
            if (!preserveAspect || rect.width <= 0 || rect.height <= 0) return bounds;
            float fit = Mathf.Min(bounds.x / rect.width, bounds.y / rect.height);
            return new Vector2(rect.width * fit, rect.height * fit);
        }
        public bool ContainsUV(Vector2 uv)
        {
            if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) return false;
            if (HasAlphaMask)
            {
                float x = Mathf.Clamp(uv.x * maskWidth - .5f, 0, maskWidth - 1), y = Mathf.Clamp(uv.y * maskHeight - .5f, 0, maskHeight - 1);
                int x0 = (int)x, y0 = (int)y, x1 = Mathf.Min(x0 + 1, maskWidth - 1), y1 = Mathf.Min(y0 + 1, maskHeight - 1);
                float a = Mathf.Lerp(Mathf.Lerp(alphaMask[y0 * maskWidth + x0], alphaMask[y0 * maskWidth + x1], x - x0),
                    Mathf.Lerp(alphaMask[y1 * maskWidth + x0], alphaMask[y1 * maskWidth + x1], x - x0), y - y0) / 255f;
                return a >= alphaThreshold;
            }
            var source = SourceTexture; if (source == null || !source.isReadable) return false;
            var rect = SourceRect;
            return StickerAlphaSampling.Sample(source, rect, uv) >= alphaThreshold;
        }
        public void SetAlphaMask(byte[] data, int width, int height)
        {
            if (data == null || data.Length != width * height) throw new System.ArgumentException("Alpha mask dimensions do not match its data.");
            alphaMask = data; maskWidth = width; maskHeight = height; maskTexture = SourceTexture; maskSprite = texture != null ? null : sprite;
        }
        void ReleaseGeneratedSprite()
        {
            if (generatedSprite != null) { if (Application.isPlaying) Destroy(generatedSprite); else DestroyImmediate(generatedSprite); }
            generatedSprite = null; generatedTexture = null;
        }
        void OnDisable() { ReleaseGeneratedSprite(); }
    }
}
