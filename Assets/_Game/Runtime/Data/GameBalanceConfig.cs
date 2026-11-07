using UnityEngine;
namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/Balance")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        public bool tutorialsEnabled = true;
        public float pressDuration = .045f;
        public float peelDuration = .085f;
        public float travelDuration = .24f;
        [Header("Sticker page peel")]
        public bool pagePeel = true;
        public Material pagePeelMaterial;
        [Header("Interactive drag peel (Old Input EventSystem)")]
        public bool dragToPeel = true;
        [Range(.35f, 1.5f)] public float dragPeelDistanceRatio = .85f;
        [Range(.01f, .2f)] public float dragPeelDeadZone = .05f;
        [Min(.01f)] public float dragReattachDuration = .18f;
        [Header("Sticker backing paper")]
        public bool backingPaper = false;
        [Range(0, 1)] public float backingPaperOpacity = .45f;
        [Min(.1f)] public float backingPaperFallDuration = .85f;
        [Min(0)] public float backingPaperFallDistance = 230;
        public Color backingPaperTint = new Color(1, .975f, .90f, 1);
        [Min(0)] public float stickDuration = .14f;
        public float clickFailDuration = .16f;
        public float clickFailDistance = 9;
        public float categoryDuration = .24f;
        public float categoryFilledHold = .2f;
        public float categoryExitDuration = .2f;
        public float categoryEnterDuration = .24f;
        [Min(.01f)] public float keyFlightDuration = .45f;
        [Min(.01f)] public float lockOpenDuration = .28f;
        [Min(.01f)] public float specialRevealDuration = .32f;
        [Range(0, 100)] public float flightArc = 46;
        [Range(0, .25f)] public float flightPop = .1f;
        public bool celebrations = true;
        public AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public int startingBoosters = 10;
        public bool boostersEnabled = true;
        public bool haptics = true;
        [Header("Peel feel (spec: peeling and feedback)")]
        [Tooltip("Peel progress per second that counts as a fast drag (louder, denser ticks).")] [Min(.1f)] public float peelFeedbackFastSpeed = 3f;
        [Range(.01f, .3f)] public float peelTickSpacingSlow = .09f, peelTickSpacingFast = .045f;
        [Range(.02f, .5f)] public float reattachTickSpacing = .15f;
        [Tooltip("Peeling must start in this outer band of the sticker (fraction of its size). The core counts as a tap.")]
        public bool requireEdgeStart = false;
        [Range(.05f, .45f)] public float peelEdgeBand = .3f;
        [Tooltip("The flap lifts where the sticker's edge is grabbed and peels across towards the far side; curved paths still peel.")]
        public bool peelFromGrabSide = true;
        [Range(.35f, 1.5f)] public float grabPeelDistanceRatio = .7f;
        [Range(.01f, .3f)] public float grabPeelDeadZone = .1f;
        [Tooltip("A press this close to a playable sticker (board units) still picks it, unless it lands on another playable sticker.")]
        [Min(0)] public float touchSlop = 48;
        [Header("Recurring drag guide")]
        [Min(0)] public float dragGuideDelay = .45f;
        [Min(.5f)] public float dragGuideDuration = 2.6f;
    }
}
