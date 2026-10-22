using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort
{
    public static class UIFactory
    {
        public static readonly Color Ink = new Color(.19f, .22f, .29f);
        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.sizeDelta = size; r.anchoredPosition = position; return r;
        }
        public static Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color, Sprite sprite = null)
        {
            var r = Rect(name, parent, size, position); var img = r.gameObject.AddComponent<Image>();
            img.color = color; img.sprite = sprite; img.type = sprite != null && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple; img.raycastTarget = false; return img;
        }
        public static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Vector2 dimensions, Vector2 position, Color? color = null)
        {
            var t = Rect(name, parent, dimensions, position).gameObject.AddComponent<TextMeshProUGUI>();
            var owner = parent != null ? parent.GetComponentInParent<GameplayUI>() : null;
            var theme = owner != null ? owner.theme : null;
            t.text = value; t.font = theme != null && theme.font != null ? theme.font : TMP_Settings.defaultFontAsset;
            t.fontSize = size; t.color = color ?? (theme != null ? theme.ink : Ink);
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
            if (theme != null) theme.ApplyTextStyle(t);
            return t;
        }
        public static Button Button(string name, Transform parent, string title, Vector2 size, Vector2 position, Color color, Sprite sprite = null)
        {
            var img = Panel(name, parent, size, position, color, sprite); img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            Text("Label", img.transform, title, 26, size - new Vector2(12, 8), Vector2.zero); return b;
        }
        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) { var o = parent.GetChild(i).gameObject; o.SetActive(false); if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
        }
    }
}
