using TMPro;
using UnityEngine;
namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/UI Theme")]
    public sealed class GameplayUITheme : ScriptableObject
    {
        public TMP_FontAsset font;
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
}
