using UnityEngine;

namespace StickyPeerSort
{
    // Input-independent gesture math. Reversing towards the start reverses the peel.
    public sealed class PeelDragProgress
    {
        readonly Vector2 origin;
        readonly float distance, deadZone;
        bool directed, anchored;
        public Vector2 Direction { get; private set; } = Vector2.left;
        public float Amount { get; private set; }
        public PeelDragProgress(Vector2 origin, float distance, float deadZoneFraction)
        { this.origin = origin; this.distance = Mathf.Max(1, distance); deadZone = this.distance * Mathf.Clamp01(deadZoneFraction); }
        // Peel anchored at the grabbed edge/corner: the curl direction is fixed (inward, away from that side) and any drag
        // counts, full speed inward, half speed outward, so a curved finger path still peels.
        public PeelDragProgress(Vector2 origin, float distance, float deadZoneFraction, Vector2 inward) : this(origin, distance, deadZoneFraction)
        { Direction = inward.normalized; directed = anchored = true; }
        public float Update(Vector2 point)
        {
            Vector2 delta = point - origin;
            if (anchored)
            {
                float length = delta.magnitude;
                if (length <= deadZone) return Amount = 0;
                float facing = Vector2.Dot(delta / length, Direction);
                return Amount = Mathf.Clamp01((length - deadZone) * Mathf.Lerp(.5f, 1, (facing + 1) * .5f) / (distance - deadZone));
            }
            if (!directed && delta.magnitude > deadZone) { Direction = delta.normalized; directed = true; }
            Amount = directed ? Mathf.Clamp01(Vector2.Dot(delta, Direction) / distance) : 0;
            return Amount;
        }
    }
}
