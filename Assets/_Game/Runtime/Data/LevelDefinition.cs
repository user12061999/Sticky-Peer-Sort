using System;
using System.Collections.Generic;
using UnityEngine;
namespace StickyPeerSort
{
    public enum SpecialKind { None, Locked, Frozen, Linked, Mystery }
    public enum UnlockCondition { StickerRemoved, CategoryCompleted, KeyCollected }
    [Serializable] public sealed class StickerSpecialConfig
    {
        public SpecialKind kind;
        public UnlockCondition unlockCondition;
        public string dependencyId;
        public int iceLayers = 2;
        public string linkId;
    }
    [Serializable] public sealed class StickerPlacement
    {
        public string instanceId;
        public StickerDefinition definition;
        public Vector2 position;
        public Vector2 size = new Vector2(172, 154);
        public float rotation;
        public float scale = 1;
        public int sortingOrder;
        public string pileId;
        public StickerSpecialConfig special = new StickerSpecialConfig();
    }
    [Serializable] public sealed class CategoryObjective
    {
        public CategoryDefinition category;
        public int targetCount = 4;
        [Tooltip("Empty = any sticker in category. Otherwise one definition per target.")]
        public List<StickerDefinition> exactTargets = new List<StickerDefinition>();
    }
    [Serializable] public sealed class BoardPileDefinition
    {
        public string id;
        public Vector2 position;
        public Vector2 size = new Vector2(390, 300);
        public float rotation;
        public Color color = new Color(.84f, .76f, .65f);
    }
    [CreateAssetMenu(menuName = "Sticker Sort/Level")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public int levelNumber = 1;
        [Range(1, 4)] public int activeCategoryCount = 3;
        [Min(1)] public int bufferCapacity = 4;
        public bool failOnOverflow = true;
        public int coinReward = 25;
        public string difficulty = "Easy";
        [TextArea] public string tutorial;
        public List<CategoryObjective> objectives = new List<CategoryObjective>();
        public List<StickerPlacement> stickers = new List<StickerPlacement>();
        public List<BoardPileDefinition> piles = new List<BoardPileDefinition>();
        [HideInInspector] public List<string> solutionTrace = new List<string>();
    }
}
