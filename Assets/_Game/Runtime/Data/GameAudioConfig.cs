using UnityEngine;
namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/Audio")]
    public sealed class GameAudioConfig : ScriptableObject
    {
        [Range(0, 1)] public float volume = .55f;
        [Range(0, 1)] public float musicVolume = .4f;
        public AudioClip bgm;
        public AudioClip peel, land, buffer, reject, collection, entrance, special, win, fail, button;
        public AudioClip freezeBreak, mysteryReveal;
    }
}
