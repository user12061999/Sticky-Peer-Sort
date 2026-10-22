using UnityEngine;
namespace StickyPeerSort
{
    [CreateAssetMenu(menuName = "Sticker Sort/Category")]
    public sealed class CategoryDefinition : ScriptableObject
    {
        public string id;
        public string title;
        public Color color = Color.white;
    }
}
