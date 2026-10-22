using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace StickyPeerSort
{
    // Presentation-only feedback; button actions still use the existing UI EventSystem.
    public sealed class UIButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Vector3 restingScale;
        void Awake() { restingScale = transform.localScale; }
        public void OnPointerDown(PointerEventData e)
        { if (GetComponent<Button>().interactable) transform.localScale = restingScale * .94f; }
        public void OnPointerUp(PointerEventData e) { transform.localScale = restingScale; }
        public void OnPointerExit(PointerEventData e) { transform.localScale = restingScale; }
        void OnDisable() { if (restingScale != Vector3.zero) transform.localScale = restingScale; }
    }
}
