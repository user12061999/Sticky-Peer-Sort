using UnityEngine;

namespace StickyPeerSort
{
    // Input-independent gesture math. Reversing towards the start reverses the peel.
    public sealed class PeelDragProgress
    {
        readonly Vector2 origin;
        readonly float distance, deadZone;
        bool directed;
        public Vector2 Direction { get; private set; } = Vector2.left;
        public float Amount { get; private set; }
        public PeelDragProgress(Vector2 origin, float distance, float deadZoneFraction)
        { this.origin = origin; this.distance = Mathf.Max(1, distance); deadZone = this.distance * Mathf.Clamp01(deadZoneFraction); }
        public float Update(Vector2 point)
        {
            Vector2 delta = point - origin;
            if (!directed && delta.magnitude > deadZone) { Direction = delta.normalized; directed = true; }
            Amount = directed ? Mathf.Clamp01(Vector2.Dot(delta, Direction) / distance) : 0;
            return Amount;
        }
    }
}
