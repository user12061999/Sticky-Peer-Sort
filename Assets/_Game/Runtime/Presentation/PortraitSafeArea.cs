using UnityEngine;

namespace StickyPeerSort
{
    // Re-fit on device rotation, window resize or changes to system safe-area insets.
    public sealed class PortraitSafeArea : MonoBehaviour
    {
        public GameplayUI view;
        Vector2 size;
        Rect area;
        void LateUpdate()
        {
            var next = new Vector2(Screen.width, Screen.height);
            if (view == null || (size == next && area == Screen.safeArea)) return;
            size = next; area = Screen.safeArea; view.ApplySafeArea();
        }
    }
}
