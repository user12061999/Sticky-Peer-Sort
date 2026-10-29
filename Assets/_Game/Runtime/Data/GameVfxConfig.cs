using UnityEngine;

namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/VFX")]
    public sealed class GameVfxConfig : ScriptableObject
    {
        public GameObject landing, collection, reveal, win;
        [Min(1)] public float landingScale = 24, collectionScale = 70, revealScale = 40, winScale = 120;
        [Range(1, 24)] public int maxConcurrentEffects = 8;
        [Min(.1f)] public float maxLifetime = 4;
        public Material alphaMaterial, additiveMaterial;
    }
}
