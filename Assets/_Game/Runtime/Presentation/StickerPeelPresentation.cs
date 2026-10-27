using System;
using System.Collections;
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
        public bool Enabled => curl != null;
        public float DirectionDegrees { set { if (curl != null) curl.DirectionDegrees = value; } }
        public StickerPeelPresentation(Image image, TextMeshProUGUI label, GameBalanceConfig config)
        {
            this.label = label;
            if (!config.pagePeel || config.pagePeelMaterial == null) return;
            image.material = config.pagePeelMaterial;
            var canvas = image.canvas;
            if (canvas != null) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            curl = image.gameObject.AddComponent<StickerCurlMesh>();
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
