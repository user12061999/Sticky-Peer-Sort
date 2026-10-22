using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Presentation only: the session resolves rules; this adapter reveals the result in order.
    public sealed class SpecialStickerPresentation
    {
        readonly GameplayUI ui;
        readonly IGameplayReadModel context;
        public SpecialStickerPresentation(GameplayUI ui, IGameplayReadModel context)
        { this.ui = ui; this.context = context; }

        public IEnumerator Unlock(int index, RectTransform keySource)
        {
            if (!ui.stickerViews.TryGetValue(index, out var view)) yield break;
            var icon = view.LockIconRect;
            if (keySource != null && ui.theme != null && ui.theme.keyIcon != null)
            {
                var key = UIFactory.Panel("Flying unlock key " + index, ui.effects, new Vector2(62, 62), Vector2.zero, Color.white, ui.theme.keyIcon);
                key.type = Image.Type.Simple; key.preserveAspect = true;
                Vector3 start = keySource.position;
                // Transfer the visible key from the completed card while the card is still present.
                keySource.gameObject.SetActive(false);
                float duration = Mathf.Max(.01f, context.Balance.keyFlightDuration), time = 0;
                key.transform.position = start;
                while (time < duration)
                {
                    time += Time.unscaledDeltaTime; float f = Mathf.SmoothStep(0, 1, time / duration);
                    key.transform.position = Vector3.Lerp(start, icon != null ? icon.position : view.Rect.position, f)
                        + ui.effects.right * Mathf.Sin(f * Mathf.PI) * 90;
                    key.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(f * Mathf.PI) * -30);
                    key.transform.localScale = Vector3.one * (1 + Mathf.Sin(f * Mathf.PI) * .22f);
                    yield return null;
                }
                key.gameObject.SetActive(false); Object.Destroy(key.gameObject);
            }
            if (icon != null)
            {
                Vector2 home = icon.anchoredPosition; float time = 0;
                float duration = Mathf.Max(.01f, context.Balance.lockOpenDuration);
                var group = icon.GetComponent<CanvasGroup>();
                if (group == null) group = icon.gameObject.AddComponent<CanvasGroup>();
                while (time < duration)
                {
                    time += Time.unscaledDeltaTime; float f = Mathf.Clamp01(time / duration);
                    icon.anchoredPosition = home + new Vector2(0, 25 * f);
                    icon.localRotation = Quaternion.Euler(0, 0, -28 * f);
                    icon.localScale = Vector3.one * (1 + Mathf.Sin(f * Mathf.PI) * .2f);
                    group.alpha = 1 - Mathf.SmoothStep(0, 1, f);
                    yield return null;
                }
                icon.anchoredPosition = home; icon.localRotation = Quaternion.identity;
                icon.localScale = Vector3.one; group.alpha = 1;
            }
            yield return Reveal(index);
            if (context.Balance.celebrations) ui.Celebration?.Collection(view.Rect);
        }

        public IEnumerator Reveal(int index)
        {
            if (!ui.stickerViews.TryGetValue(index, out var view)) yield break;
            Vector3 home = view.Rect.localScale;
            float half = Mathf.Max(.01f, context.Balance.specialRevealDuration * .5f), time = 0;
            // Keep the hidden face until the sticker turns edge-on; swap artwork at the midpoint.
            while (time < half)
            {
                time += Time.unscaledDeltaTime; float f = Mathf.SmoothStep(0, 1, time / half);
                view.Rect.localScale = new Vector3(home.x * Mathf.Lerp(1, .025f, f), home.y * (1 + .08f * f), home.z);
                yield return null;
            }
            ui.RefreshSticker(index);
            time = 0;
            view.Rect.localScale = new Vector3(home.x * .025f, home.y * 1.08f, home.z);
            while (time < half)
            {
                time += Time.unscaledDeltaTime; float f = Mathf.SmoothStep(0, 1, time / half);
                view.Rect.localScale = new Vector3(home.x * Mathf.Lerp(.025f, 1, f), home.y * Mathf.Lerp(1.08f, 1, f), home.z);
                yield return null;
            }
            view.Rect.localScale = home;
        }
    }
}
