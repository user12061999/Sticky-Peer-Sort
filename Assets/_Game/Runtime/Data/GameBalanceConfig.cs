using UnityEngine;
namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/Balance")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        public float pressDuration = .045f;
        public float peelDuration = .085f;
        public float travelDuration = .24f;
        [Header("Sticker page peel")]
        public bool pagePeel = true;
        public Material pagePeelMaterial;
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
        public bool haptics;
    }
}
