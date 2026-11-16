using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/UI Theme")]
    public sealed class GameplayUITheme : ScriptableObject
    {
        public TMP_FontAsset font;
        [Header("Typography v01 (partner spec): Nunito static 900/800, TMP weight Regular, no synthetic Bold")]
        public TMP_FontAsset fontBlack, fontExtraBold;
        public Material underlayMaterial;   // ExtraBold + Normal Underlay: white text on tinted headers and pills
        public bool Nunito => fontBlack != null && fontExtraBold != null;
        public FontStyles Strong => Nunito ? FontStyles.Normal : FontStyles.Bold;
        // Puts a text on one of the two Nunito faces with that face's own material (never shared across atlases).
        public void Face(TMP_Text t, bool black)
        {
            if (t == null || !Nunito) return;
            var face = black ? fontBlack : fontExtraBold;
            t.font = face; t.fontSharedMaterial = face.material;
            t.fontStyle &= ~(FontStyles.Bold | FontStyles.Italic); t.fontWeight = FontWeight.Regular;
            t.extraPadding = true;
        }
        // Spec text block: one line, centred, kerning on, all spacing 0, Ellipsis; Auto Size only when min > 0.
        public void Type(TMP_Text t, bool black, float size, Color color, float min = 0, bool underlay = false, bool wrap = false, float margin = 0)
        {
            if (t == null || !Nunito) return;
            Face(t, black);
            if (underlay && !black && underlayMaterial != null) t.fontSharedMaterial = underlayMaterial;
            t.color = color; t.fontSize = size; t.enableVertexGradient = false;
            t.enableAutoSizing = min > 0 && min < size;
            if (t.enableAutoSizing) { t.fontSizeMin = min; t.fontSizeMax = size; }
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.characterSpacing = 0; t.wordSpacing = 0; t.lineSpacing = 0; t.paragraphSpacing = 0;
            t.characterWidthAdjustment = 0; t.lineSpacingAdjustment = 0;
            var features = t.fontFeatures; if (features != null && !features.Contains(UnityEngine.TextCore.OTL_FeatureTag.kern)) { features.Add(UnityEngine.TextCore.OTL_FeatureTag.kern); t.fontFeatures = features; }
            t.alignment = TextAlignmentOptions.Center; t.margin = new Vector4(margin, 0, margin, 0);
            t.rectTransform.localScale = Vector3.one;
            t.UpdateMeshPadding();
        }
        // Category header Tint, usage priority order (not a fixed category mapping). Applied once through Image.color.
        public Color[] headerPalette =
        {
            new Color32(0xC7, 0x62, 0x59, 255), new Color32(0x39, 0x8C, 0x92, 255), new Color32(0x73, 0x7D, 0xB8, 255),
            new Color32(0xAE, 0x64, 0x88, 255), new Color32(0x4E, 0x7D, 0xA1, 255), new Color32(0x63, 0x83, 0x6D, 255),
            new Color32(0xA7, 0x6F, 0x49, 255), new Color32(0x89, 0x70, 0x9E, 255), new Color32(0x81, 0x81, 0x4C, 255),
            new Color32(0x91, 0x6E, 0x64, 255),
        };
        public bool HasHeaderPalette => headerPalette != null && headerPalette.Length > 0;
        public Color HeaderTintAt(int priority)
        {
            int n = headerPalette.Length; return headerPalette[((priority % n) + n) % n];
        }
        public Sprite tutorialHand;
        [Header("TextMeshPro SDF presets")]
        public Material headingTextMaterial, lightTextMaterial, darkTextMaterial;
        public void ApplyTextStyle(TMP_Text text)
        {
            if (text == null) return;
            if (Nunito && (text.font == fontBlack || text.font == fontExtraBold))
            {
                // The old Sen presets belong to another atlas; Nunito text keeps its own face material (or the underlay preset).
                var m = text.fontSharedMaterial;
                if (m == null || (m != underlayMaterial && m.GetTexture(ShaderUtilities.ID_MainTex) != text.font.atlasTexture)) text.fontSharedMaterial = text.font.material;
                text.fontStyle &= ~FontStyles.Bold; text.extraPadding = true; text.UpdateMeshPadding();
                return;
            }
            if (text.font != font) return;
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
        [Header("Scrapbook GUI v01 (partner mockup). On = new layout; off = previous skin")]
        public bool scrapbook;
        public Sprite sheetBase, headerBase, pillBase, holdingSlot, stickerSurface;
        public Sprite popupCoinIcon, popupSettingsIcon, popupUndoIcon, popupSlotIcon;   // GUI v01 icons reused on popups
        public Material silhouetteMaterial;   // solid fill from sticker alpha: empty collection slots
        public Color scrapbookInk = new Color32(0x16, 0x22, 0x41, 255);
        public Color silhouetteColor = new Color32(197, 199, 188, 255);
        public Color scrapbookRim = new Color32(0xE6, 0xD1, 0xAB, 255);    // card outer rim, sampled from the mockup
        public Color scrapbookPill = new Color32(0xFB, 0xEB, 0xCB, 255);   // cream count pill
        public bool Scrapbook => scrapbook && sheetBase != null;
        // Header tint: the four colors given by the GUI handoff, otherwise the project color softened to the same pastel band.
        public Color HeaderTint(string title, Color source)
        {
            switch ((title ?? "").Trim().ToUpperInvariant())
            {
                case "FOOD": return new Color32(0xFB, 0x7C, 0x69, 255);
                case "ANIMALS": return new Color32(0x46, 0xB0, 0xB8, 255);
                case "VEHICLES": return new Color32(0xEA, 0xC4, 0x6C, 255);
                case "PLANTS": return new Color32(0x9D, 0xC2, 0x8F, 255);
            }
            Color.RGBToHSV(source, out var h, out var s, out var v);
            return Color.HSVToRGB(h, Mathf.Clamp(s, .45f, .65f), Mathf.Clamp(v, .72f, .9f));
        }
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
