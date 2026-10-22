using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort
{
    // Cosmetic coroutines have a separate host/layer and never lock gameplay input.
    public sealed class GameCelebration : MonoBehaviour
    {
        GameplayUI ui;
        RectTransform layer;
        readonly Dictionary<RectTransform, Vector3> pulsing = new Dictionary<RectTransform, Vector3>();
        public void Initialize(GameplayUI screen)
        {
            ui = screen;
            layer = UIFactory.Rect("Celebration Layer", ui.effects.parent, new Vector2(1080, 1920), Vector2.zero);
            layer.SetAsLastSibling();
        }
        public void ResetEffects()
        {
            StopAllCoroutines();
            foreach (var pair in pulsing) if (pair.Key != null) pair.Key.localScale = pair.Value;
            pulsing.Clear(); if (layer != null) UIFactory.Clear(layer);
        }
        public void Land(RectTransform target) { if (!pulsing.ContainsKey(target)) StartCoroutine(Pop(target)); }
        IEnumerator Pop(RectTransform target)
        {
            Vector3 home = target.localScale; pulsing.Add(target, home); float elapsed = 0;
            while (elapsed < .16f && target != null)
            {
                elapsed += Time.unscaledDeltaTime; float f = Mathf.Clamp01(elapsed / .16f);
                target.localScale = home * (1 + Mathf.Sin(f * Mathf.PI) * .12f); yield return null;
            }
            if (target != null) target.localScale = home; pulsing.Remove(target);
        }
        public void Collection(RectTransform card)
        { if (layer != null) StartCoroutine(Burst(card.position, 12, .6f, false)); }
        public void Win(int reward)
        {
            if (layer == null) return;
            StartCoroutine(Burst(layer.TransformPoint(new Vector3(0, 480)), 42, 1.5f, true));
            if (reward > 0) StartCoroutine(Coins(reward));
        }
        IEnumerator Burst(Vector3 world, int count, float duration, bool confetti)
        {
            var root = UIFactory.Rect("Celebration particles", layer, Vector2.zero, Vector2.zero); root.position = world;
            var particles = new Image[count]; var velocities = new Vector2[count];
            Color[] colors = { new Color(1, .8f, .16f), new Color(1, .34f, .3f), new Color(.2f, .72f, 1), new Color(.4f, .85f, .2f), new Color(.8f, .3f, 1) };
            for (int i = 0; i < count; i++)
            {
                float angle = (i * 137.5f + 20) * Mathf.Deg2Rad;
                velocities[i] = new Vector2(Mathf.Cos(angle) * (confetti ? 420 : 180), Mathf.Sin(angle) * (confetti ? 180 : 120) + (confetti ? 220 : 80));
                particles[i] = UIFactory.Panel("Confetti", root, confetti ? new Vector2(12, 23) : new Vector2(13, 13), Vector2.zero, colors[i % colors.Length], ui.theme != null ? ui.theme.roundedPanel : ui.panelSprite);
            }
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; float f = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < count; i++)
                {
                    particles[i].rectTransform.anchoredPosition = velocities[i] * elapsed + Vector2.down * (confetti ? 210 : 120) * elapsed * elapsed;
                    particles[i].rectTransform.localRotation = Quaternion.Euler(0, 0, i * 21 + elapsed * 210);
                    var color = particles[i].color; color.a = Mathf.Clamp01((1 - f) * 3); particles[i].color = color;
                }
                yield return null;
            }
            Destroy(root.gameObject);
        }
        IEnumerator Coins(int reward)
        {
            var root = UIFactory.Rect("Coin reward", layer, Vector2.zero, new Vector2(0, 495));
            var label = UIFactory.Text("Reward TMP", root, "+" + reward + " COINS", 38, new Vector2(620, 70), Vector2.zero, Color.white);
            var coins = new Image[9]; var starts = new Vector3[9];
            for (int i = 0; i < coins.Length; i++)
            {
                coins[i] = UIFactory.Panel("Reward coin", root, new Vector2(45, 45), new Vector2((i - 4) * 27, -65 - i % 3 * 14), Color.white, ui.theme != null ? ui.theme.coinIcon : ui.panelSprite);
                coins[i].preserveAspect = true; starts[i] = coins[i].transform.position;
            }
            float elapsed = 0;
            while (elapsed < 1.1f)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < coins.Length; i++)
                {
                    float f = Mathf.Clamp01((elapsed - .2f - i * .035f) / .48f); float ease = f * f;
                    coins[i].transform.position = Vector3.Lerp(starts[i], ui.coins.transform.position, ease);
                    coins[i].rectTransform.localScale = Vector3.one * (1 - f * .75f);
                    var color = coins[i].color; color.a = f < 1 ? 1 : 0; coins[i].color = color;
                }
                label.alpha = Mathf.Clamp01((1.1f - elapsed) * 4); yield return null;
            }
            Destroy(root.gameObject);
        }
        void OnDisable() { ResetEffects(); }
    }
}
