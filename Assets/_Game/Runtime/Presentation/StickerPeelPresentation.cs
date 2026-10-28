using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // One short-lived flight view; no rule, save, input lock or extra coroutine host.
    public sealed class StickerPeelPresentation
    {
        readonly StickerCurlMesh curl;
        readonly TextMeshProUGUI label;
        static readonly Dictionary<StickerDefinition, Rect> bounds = new Dictionary<StickerDefinition, Rect>();
        public bool Enabled => curl != null;
        public float DirectionDegrees { set { if (curl != null) curl.DirectionDegrees = value; } }
        public StickerPeelPresentation(Image image, TextMeshProUGUI label, GameBalanceConfig config, StickerDefinition definition = null)
        {
            this.label = label;
            if (!config.pagePeel || config.pagePeelMaterial == null) return;
            image.material = config.pagePeelMaterial;
            var canvas = image.canvas;
            if (canvas != null) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            curl = image.gameObject.AddComponent<StickerCurlMesh>();
            if (definition != null) curl.ArtworkBounds = VisibleBounds(definition);
        }
        static Rect VisibleBounds(StickerDefinition definition)
        {
            if (bounds.TryGetValue(definition, out var cached)) return cached;
            Vector2 min = Vector2.one, max = Vector2.zero;
            const int resolution = 48;
            for (int y = 0; y <= resolution; y++) for (int x = 0; x <= resolution; x++)
            {
                var uv = new Vector2((float)x / resolution, (float)y / resolution);
                if (definition.ContainsUV(uv)) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
            }
            var rect = min.x > max.x ? new Rect(0, 0, 1, 1) : Rect.MinMaxRect(Mathf.Max(0, min.x - 1f / resolution), Mathf.Max(0, min.y - 1f / resolution), Mathf.Min(1, max.x + 1f / resolution), Mathf.Min(1, max.y + 1f / resolution));
            bounds[definition] = rect; return rect;
        }
        public void Set(float amount)
        {
            if (curl == null) return;
            curl.Amount = amount;
            if (label != null) label.alpha = 1 - Mathf.Clamp01(amount * 2);
        }
        public IEnumerator Animate(float from, float to, float duration, Action track = null)
        {
            if (!Enabled) yield break;
            Set(from); float time = 0;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                track?.Invoke(); Set(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, time / Mathf.Max(.001f, duration))));
                yield return null;
            }
            track?.Invoke(); Set(to);
        }
    }
}
