using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace StickyPeerSort
{
    public sealed class StickerView : MonoBehaviour, IPointerDownHandler, ICanvasRaycastFilter
    {
        public Image image;
        public TextMeshProUGUI title, badge;
        public RectTransform Rect { get { return (RectTransform)transform; } }
        IGameplayReadModel context;
        IStickerInput input;
        RectTransform board;
        int index;
        Coroutine feedback;
        Color restingColor;
        Image lockIcon;
        public RectTransform LockIconRect => lockIcon != null ? lockIcon.rectTransform : null;
        Image coveredShade;
        Image specialBadgePlate;
        public bool IsCoveredShadeVisible => coveredShade != null && coveredShade.gameObject.activeSelf;
        public void SetCovered(bool covered)
        {
            if (coveredShade != null) coveredShade.gameObject.SetActive(covered);
        }
        static readonly LockedRule LockRule = new LockedRule();
        public bool IsShowingClickFail { get; private set; }
        public void ClickFail(GameBalanceConfig config)
        {
            if (feedback != null) StopCoroutine(feedback);
            Rect.anchoredPosition = context.Session.Stickers[index].position;
            feedback = StartCoroutine(FailFeedback(config));
        }
        IEnumerator FailFeedback(GameBalanceConfig config)
        {
            IsShowingClickFail = true;
            Vector2 home = context.Session.Stickers[index].position; Color original = restingColor;
            float duration = Mathf.Max(.01f, config.clickFailDuration), elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; float f = Mathf.Clamp01(elapsed / duration);
                Rect.anchoredPosition = home + Vector2.right * Mathf.Sin(f * Mathf.PI * 6) * config.clickFailDistance * (1 - f);
                image.color = Color.Lerp(original, new Color(1, .35f, .35f), Mathf.Sin(f * Mathf.PI) * .7f);
                yield return null;
            }
            Rect.anchoredPosition = home; image.color = original; IsShowingClickFail = false; feedback = null;
        }
        public void Bind(IGameplayReadModel owner, IStickerInput stickerInput, int stickerIndex)
        {
            context = owner; input = stickerInput; board = (RectTransform)transform.parent; index = stickerIndex;
            var s = owner.Session.Stickers[index]; var d = s.placement.definition;
            var screen = GetComponentInParent<GameplayUI>();
            if (screen != null && screen.theme != null && screen.theme.font != null) title.font = badge.font = screen.theme.font;
            Rect.anchoredPosition = s.position; Rect.sizeDelta = s.placement.size; Rect.localScale = Vector3.one * s.placement.scale;
            Rect.localRotation = Quaternion.Euler(0, 0, s.rotation);
            StickerVisuals.Apply(image, d);
            bool mystery = s.placement.special.kind == SpecialKind.Mystery && !s.revealed;
            bool locked = s.placement.special.kind == SpecialKind.Locked && LockRule.Evaluate(owner.Session, index) == SpecialAction.Block;
            if (lockIcon == null)
            { lockIcon = UIFactory.Panel("Lock icon", transform, new Vector2(53, 53), Vector2.zero, Color.white); lockIcon.preserveAspect = true; }
            lockIcon.gameObject.SetActive(locked);
            lockIcon.rectTransform.sizeDelta = Vector2.one * Mathf.Min(s.placement.size.x, s.placement.size.y) * .52f;
            lockIcon.rectTransform.anchoredPosition = Vector2.zero;
            if (screen != null && screen.theme != null) lockIcon.sprite = screen.theme.lockIcon;
            title.text = mystery ? "?" : d.showLabel ? d.displayName : "";
            title.fontSize = mystery ? 86 : 27;
            title.color = mystery ? Color.white : UIFactory.Ink;
            if (screen != null && screen.theme != null)
            {
                title.color = mystery ? Color.white : screen.theme.ink;
                screen.theme.ApplyTextStyle(title); screen.theme.ApplyTextStyle(badge);
            }
            title.rectTransform.sizeDelta = s.placement.size * .83f;
            switch (s.placement.special.kind)
            {
                case SpecialKind.Locked: badge.text = ""; break;
                case SpecialKind.Frozen: badge.text = s.ice > 0 ? "ICE " + s.ice : ""; break;
                case SpecialKind.Linked: badge.text = "LINK " + s.placement.special.linkId; break;
                case SpecialKind.Mystery: badge.text = ""; break;
                default: badge.text = ""; break;
            }
            badge.rectTransform.anchoredPosition = new Vector2(0, -s.placement.size.y * .31f);
            badge.rectTransform.sizeDelta = new Vector2(s.placement.size.x * .95f, 32);
            badge.fontSize = locked ? 12 : 17;
            bool referenceStyle = screen != null && screen.theme != null && screen.theme.referenceComposition;
            bool specialBadge = referenceStyle && ((s.placement.special.kind == SpecialKind.Frozen && s.ice > 0) || s.placement.special.kind == SpecialKind.Linked);
            if (specialBadge && specialBadgePlate == null)
                specialBadgePlate = UIFactory.Panel("Special badge plate", transform, new Vector2(116, 34), Vector2.zero, Color.white, screen.panelSprite);
            if (specialBadgePlate != null) specialBadgePlate.gameObject.SetActive(specialBadge);
            if (specialBadge)
            {
                specialBadgePlate.color = s.placement.special.kind == SpecialKind.Frozen ? new Color(.06f, .48f, .78f) : new Color(.55f, .16f, .7f);
                specialBadgePlate.rectTransform.anchoredPosition = badge.rectTransform.anchoredPosition;
                badge.color = Color.white; badge.fontSize = 20; badge.transform.SetAsLastSibling(); screen.theme.ApplyTextStyle(badge);
            }
            if (s.placement.special.kind == SpecialKind.Frozen && s.ice > 0) image.color = Color.Lerp(d.tint, new Color(.4f, .86f, 1), .65f);
            if (mystery)
            {
                image.sprite = d.ArtworkSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = referenceStyle ? new Color(.56f, .16f, .85f) : new Color(.12f, .16f, .28f);
                title.transform.SetAsLastSibling();
            }
            if (locked)
            {
                image.sprite = d.ArtworkSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.Lerp(d.tint, Color.black, .5f);
                title.text = "";
                lockIcon.transform.SetAsLastSibling();
            }
            restingColor = image.color;
            if (coveredShade == null)
            {
                coveredShade = UIFactory.Panel("Covered black veil", transform, Vector2.zero, Vector2.zero, Color.black);
                coveredShade.rectTransform.anchorMin = Vector2.zero; coveredShade.rectTransform.anchorMax = Vector2.one;
                coveredShade.rectTransform.offsetMin = coveredShade.rectTransform.offsetMax = Vector2.zero;
                coveredShade.transform.SetAsFirstSibling(); // Dim artwork; keep ? and lock symbols readable.
                coveredShade.gameObject.AddComponent<SoftCoveredShade>();
            }
            coveredShade.sprite = image.sprite; coveredShade.type = image.type; coveredShade.preserveAspect = image.preserveAspect;
            coveredShade.color = new Color(0, 0, 0, screen != null && screen.theme != null ? screen.theme.coveredOpacity : .46f);
            coveredShade.GetComponent<SoftCoveredShade>().softness = screen != null && screen.theme != null ? screen.theme.coveredSoftness : 1.5f;
            SetCovered(BoardGeometry.Covered(owner.Session, index));
        }
        public bool IsRaycastLocationValid(Vector2 point, Camera camera)
        {
            if (context == null) return false;
            Vector2 p; RectTransformUtility.ScreenPointToLocalPointInRectangle(board, point, camera, out p);
            return BoardGeometry.Contains(context.Session.Stickers[index], p);
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            Vector2 p; RectTransformUtility.ScreenPointToLocalPointInRectangle(board, e.position, e.pressEventCamera, out p);
            if (BoardGeometry.Hit(context.Session, p) == index) input.TapAt(p);
        }
    }
}
