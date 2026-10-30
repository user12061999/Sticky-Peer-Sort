using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Cosmetic only: independent from the sticker's destination, move queue and gameplay state.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class StickerBackingPaper : MonoBehaviour
    {
        CanvasGroup group;
        PaperFlutterMesh paperMesh;
        RectTransform rect;
        GameBalanceConfig balance;
        Vector2 home;
        Quaternion rotation;
        float elapsed, drift;
        public bool IsFalling { get; private set; }

        public static StickerBackingPaper Create(StickerView source, GameplayUI ui, GameBalanceConfig balance, int index)
        {
            if (balance == null || !balance.backingPaper) return null;
            var definition = source.image.sprite;
            var placement = source.Rect;
            // Reuse the sticker's exact alpha and layout; there is no rectangular carrier or margin.
            var paper = UIFactory.Panel("Sticker backing paper " + index, ui.effects, StickerPeelLayer.SizeInOverlay(placement, ui.effects), Vector2.zero,
                balance.backingPaperTint, definition);
            paper.preserveAspect = source.image.preserveAspect;
            if (balance.pagePeelMaterial != null) paper.material = balance.pagePeelMaterial;
            paper.type = Image.Type.Simple; StickerPeelLayer.Prepare(paper, ui);
            paper.GetComponent<Canvas>().sortingOrder--;
            paper.transform.position = placement.position; paper.transform.rotation = placement.rotation;
            var flutter = paper.gameObject.AddComponent<PaperFlutterMesh>();
            flutter.PaperFace = true;
            var shadow = paper.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.18f, .12f, .05f, .22f); shadow.effectDistance = new Vector2(4, -6);
            var result = paper.gameObject.AddComponent<StickerBackingPaper>();
            result.group = paper.GetComponent<CanvasGroup>(); result.group.alpha = 0;
            result.paperMesh = flutter;
            result.rect = paper.rectTransform; result.balance = balance; result.drift = index % 2 == 0 ? 1 : -1;
            return result;
        }
        public void SetPeel(float amount) { if (!IsFalling && group != null) group.alpha = balance.backingPaperOpacity * Mathf.SmoothStep(0, 1, amount / .12f); }
        public void Release()
        {
            if (IsFalling) return;
            IsFalling = true; elapsed = 0; home = rect.anchoredPosition; rotation = rect.localRotation; group.alpha = balance.backingPaperOpacity;
        }
        public void Discard() { gameObject.SetActive(false); Destroy(gameObject); }
        void Update()
        {
            if (!IsFalling) return;
            elapsed += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(elapsed / Mathf.Max(.1f, balance.backingPaperFallDuration));
            rect.anchoredPosition = home + new Vector2(drift * (Mathf.Sin(f * Mathf.PI * 3.5f) * 22 + 38 * f),
                -balance.backingPaperFallDistance * (.35f * f + .65f * f * f) + 6 * Mathf.Sin(f * Mathf.PI));
            rect.localRotation = rotation * Quaternion.Euler(0, 0, drift * (18 * Mathf.Sin(f * Mathf.PI * 3) + 22 * f));
            rect.localScale = new Vector3(1 - .16f * Mathf.Pow(Mathf.Sin(f * Mathf.PI * 2.5f), 2), 1 - .07f * Mathf.Pow(Mathf.Sin(f * Mathf.PI * 2), 2), 1);
            float bend = .065f * Mathf.Sin(f * Mathf.PI);
            paperMesh.SetFlutter(f * 15, bend);
            group.alpha = balance.backingPaperOpacity * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1, f)));
            if (f >= 1) Discard();
        }
        public static void Clear(RectTransform effects)
        { foreach (var paper in effects.GetComponentsInChildren<StickerBackingPaper>()) paper.Discard(); }
    }
}
