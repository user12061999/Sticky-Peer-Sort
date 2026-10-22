using UnityEngine;
namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/Audio")]
    public sealed class GameAudioConfig : ScriptableObject
    {
        [Range(0, 1)] public float volume = .55f;
        public AudioClip peel, land, buffer, reject, collection, entrance, special, win, fail, button;
    }
}
