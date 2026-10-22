using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace StickyPeerSort
{
    // Pointer feedback stays independent of gameplay command listeners (modal buttons replace them).
    public sealed class UIButtonSound : MonoBehaviour, IPointerClickHandler
    {
        GameAudioFeedback audioFeedback;
        public void Initialize(GameAudioFeedback value) { audioFeedback = value; }
        public void OnPointerClick(PointerEventData e)
        { if (e.button == PointerEventData.InputButton.Left && GetComponent<Button>().interactable) audioFeedback.Click(); }
    }
}
