using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort
{
    // Visual state advances event by event, independently of the fully resolved gameplay model.
    public sealed class CategoryCardPresentation
    {
        public int categoryIndex, lane;
        public RectTransform rect;
        public CanvasGroup group;
        public Image[] targets;
        public int[] stickers;
        public TextMeshProUGUI counter;
        public Image[] progressDots;
        public Color completedColor;
        public int FilledCount { get { int n = 0; foreach (int i in stickers) if (i >= 0) n++; return n; } }
        public void Fill(int slot, int sticker, StickerDefinition definition)
        {
            stickers[slot] = sticker;
            var target = targets[slot]; UIFactory.Clear(target.transform);
            StickerVisuals.Apply(target, definition);
            if (definition.showLabel) UIFactory.Text("Sticker label", target.transform, definition.displayName, 17, target.rectTransform.sizeDelta * .95f, Vector2.zero);
            counter.text = FilledCount + " / " + stickers.Length;
            if (progressDots != null && slot < progressDots.Length) progressDots[slot].color = completedColor;
        }
    }
}
