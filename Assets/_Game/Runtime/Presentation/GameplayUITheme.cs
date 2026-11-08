using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/UI Theme")]
    public sealed class GameplayUITheme : ScriptableObject
    {
        public TMP_FontAsset font;
        public Sprite tutorialHand;
        [Header("TextMeshPro SDF presets")]
        public Material headingTextMaterial, lightTextMaterial, darkTextMaterial;
        public void ApplyTextStyle(TMP_Text text)
        {
            if (text == null || text.font != font) return;
            bool light = text.color.grayscale > .75f;
            var preset = light ? text.fontSize >= 38 ? headingTextMaterial : lightTextMaterial : darkTextMaterial;
            if (preset == null) return;
            text.fontSharedMaterial = preset;
            text.extraPadding = true;
            text.UpdateMeshPadding();
        }
        public Sprite roundedPanel, cardFrame, trayFrame, slotFrame, buttonFrame, buttonHighlight;
        public Sprite coinIcon, settingsIcon, starIcon, failIcon, collectionIcon;
        public Sprite undoIcon, hintIcon, extraIcon, shuffleIcon;
        public Sprite keyIcon, lockIcon;
        [Header("GUI Pro-SuperCasual skin (optional; empty = plain rounded panels)")]
        public Sprite outlinePanel, innerPanel, titleRibbon, failRibbon, badgeBurst, counterBadge;
        public Sprite primaryButton, secondaryButton, smallButton;
        public Sprite[] boosterButtons;
        public bool HasSkin => outlinePanel != null;
        [Header("Board sticker states")]
        public Color blockedStickerTint = new Color(.6f, .6f, .6f, 1);
        [Range(.5f, .95f)] public float mysteryQuestionSize = .78f;
        [Header("Reference composition (opt-in for sample scene)")]
        public bool referenceComposition;
        public Sprite woodSurface;
        [Header("Covered stickers")]
        [Range(0, .8f)] public float coveredOpacity = .68f;
        [Range(0, 4)] public float coveredSoftness = 1.5f;
        public Color background = new Color(.91f, .95f, 1);
        public Color ink = new Color(.15f, .21f, .34f);
        public Color muted = new Color(.44f, .53f, .67f);
        public Color paper = new Color(1, .99f, .96f);
        public Color accent = new Color(.27f, .54f, 1);
        public Color mint = new Color(.29f, .78f, .65f);
        public Color trayRim = new Color(1, .8f, .38f);
        public Color trayWell = new Color(.74f, .41f, .12f);
        public Color emptySlot = new Color(.94f, .75f, .43f);
        public Color CategoryColor(Color source)
        {
            Color.RGBToHSV(source, out var h, out var s, out var v);
            // Green/yellow need a deeper value to keep white titles legible.
            return Color.HSVToRGB(h, Mathf.Max(.8f, s), h > .12f && h < .48f ? .68f : .9f);
        }
    }

    public static class UISkin
    {
        // Applies a 9-sliced GUI Pro sprite and shrinks its borders (pixelsPerUnitMultiplier) when the rect is smaller than them,
        // so the same outlined frame works for a 40 px badge and an 800 px popup.
        public static Image Apply(Image image, Sprite sprite, Color color)
        {
            if (image == null || sprite == null) return image;
            image.sprite = sprite; image.color = color; image.type = Image.Type.Sliced; image.fillCenter = true;
            Fit(image); return image;
        }
        public static void Fit(Image image)
        {
            if (image == null || image.sprite == null) return;
            var size = image.rectTransform.rect.size;
            if (size.x <= 0 || size.y <= 0) size = image.rectTransform.sizeDelta;
            var b = image.sprite.border; float m = 1;
            if (b.x + b.z > 0 && size.x > 0) m = Mathf.Max(m, (b.x + b.z) / (size.x * .95f));
            if (b.y + b.w > 0 && size.y > 0) m = Mathf.Max(m, (b.y + b.w) / (size.y * .95f));
            image.pixelsPerUnitMultiplier = m;
        }
        // Visible corner radius of each rounded sprite at pixelsPerUnitMultiplier 1 (measured from the textures).
        public static float SpriteRadius(Sprite sprite)
        {
            if (sprite == null) return 0;
            switch (sprite.name)
            {
                case "Popup_OutTitle_Bg": return 48;
                case "CardFrame04_Bg": return 34;
                case "BasicFrame_Round24": return 24;
            }
            var b = sprite.border; return .83f * Mathf.Min(Mathf.Min(b.x, b.z), Mathf.Min(b.y, b.w));
        }
        public static float Radius(Image image)
        {
            return image == null || image.sprite == null ? 0 : SpriteRadius(image.sprite) / Mathf.Max(.01f, image.pixelsPerUnitMultiplier);
        }
        // Rounds an inset layer so its corners run parallel to the frame around it: radius = outer radius - inset.
        public static void Concentric(Image outer, Image inner, float inset)
        {
            if (outer == null || inner == null || inner.sprite == null) return;
            float target = Mathf.Max(4, Radius(outer) - inset);
            float m = SpriteRadius(inner.sprite) / target;
            var size = inner.rectTransform.rect.size; if (size.x <= 0 || size.y <= 0) size = inner.rectTransform.sizeDelta;
            var b = inner.sprite.border;
            if (size.x > 0 && b.x + b.z > 0) m = Mathf.Max(m, (b.x + b.z) / size.x);
            if (size.y > 0 && b.y + b.w > 0) m = Mathf.Max(m, (b.y + b.w) / size.y);
            inner.pixelsPerUnitMultiplier = m;
        }
    }
}
