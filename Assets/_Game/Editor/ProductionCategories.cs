using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    // Sixteen themed categories of four designs each: every sheet is a real category choice,
    // and one level can hold up to 64 stickers with no design or category repeated.
    public static class ProductionCategories
    {
        public const string Root = ProductionStickerBuilder.Root + "/Categories";
        sealed class Group
        {
            public readonly string parent, title; public readonly string[] designs;
            public Group(string parent, string title, params string[] designs) { this.parent = parent; this.title = title; this.designs = designs; }
            public string Id => "category_" + title.ToLowerInvariant().Replace(' ', '_');
        }
        // Listed round robin across the four themes, so any run of groups mixes them.
        static readonly Group[] Groups =
        {
            new Group("FOOD", "SWEETS", "chocolate_cookie", "cupcake", "strawberry_cake", "strawberry_ice_cream"),
            new Group("ANIMALS", "FARM", "chick", "husky", "rabbit", "sheep"),
            new Group("PLANTS", "FLOWERS", "daisy", "hibiscus", "rose", "sunflower"),
            new Group("VEHICLES", "ON DUTY", "ambulance", "fire_engine", "police_car", "school_bus"),
            new Group("FOOD", "MEALS", "hot_dog", "ramen", "salmon_sushi", "sandwich"),
            new Group("ANIMALS", "FOREST", "deer", "fox", "raccoon", "squirrel"),
            new Group("PLANTS", "BLOOMS", "cherry_blossom", "flowering_cactus", "lavender", "lotus"),
            new Group("VEHICLES", "CITY RIDES", "bicycle", "scooter", "taxi", "train"),
            new Group("FOOD", "SNACKS", "croissant", "pancakes", "popcorn", "pretzel"),
            new Group("ANIMALS", "WILD", "hedgehog", "koala", "lion", "panda"),
            new Group("PLANTS", "HOUSEPLANTS", "bonsai", "monstera", "snake_plant", "yellow_cactus"),
            new Group("VEHICLES", "HEAVY WORK", "cement_truck", "excavator", "tractor", "tugboat"),
            new Group("FOOD", "FARM FRESH", "cheese", "grapes", "milk_carton", "watermelon"),
            new Group("ANIMALS", "SEA AND SKY", "dolphin", "owl", "seal", "whale"),
            new Group("PLANTS", "GREENERY", "bamboo", "clover", "maple_leaf", "red_mushroom"),
            new Group("VEHICLES", "AIR AND SEA", "hot_air_balloon", "rocket", "sailboat", "submarine")
        };
        static string StickerId(Group group, string design)
        { return "sps_" + (group.parent == "ANIMALS" ? "animal" : group.parent == "PLANTS" ? "plant" : group.parent == "VEHICLES" ? "vehicle" : "food") + "_" + design; }
        public static CategoryDefinition For(string stickerId)
        {
            var group = Groups.FirstOrDefault(g => g.designs.Any(d => StickerId(g, d) == stickerId));
            if (group == null) throw new InvalidOperationException("No category for sticker " + stickerId);
            return Asset(group);
        }
        static CategoryDefinition Asset(Group group)
        {
            string path = Root + "/" + group.Id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<CategoryDefinition>(path);
            if (asset != null) return asset;
            System.IO.Directory.CreateDirectory(Root);
            asset = ScriptableObject.CreateInstance<CategoryDefinition>();
            asset.id = group.Id; asset.title = group.title;
            asset.color = AssetDatabase.LoadAssetAtPath<CategoryDefinition>("Assets/_Game/Data/Categories/" + group.parent + ".asset").color;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        // Designs in group order, starting at a level-specific group so levels differ.
        public static List<StickerDefinition> Pool(int levelIndex)
        {
            var pool = new List<StickerDefinition>();
            for (int i = 0; i < Groups.Length; i++)
            {
                var group = Groups[(levelIndex * 5 + i) % Groups.Length];
                foreach (string design in group.designs)
                    pool.Add(AssetDatabase.LoadAssetAtPath<StickerDefinition>(ProductionStickerBuilder.Root + "/Data/" + StickerId(group, design) + ".asset"));
            }
            if (pool.Any(d => d == null)) throw new InvalidOperationException("Missing production sticker definition.");
            return pool;
        }
        [MenuItem("Tools/Sticker Category Game/Production Stickers/Apply 16 Categories")]
        public static void Apply()
        {
            foreach (var group in Groups)
            {
                var category = Asset(group);
                foreach (string design in group.designs)
                {
                    var sticker = AssetDatabase.LoadAssetAtPath<StickerDefinition>(ProductionStickerBuilder.Root + "/Data/" + StickerId(group, design) + ".asset");
                    if (sticker == null) throw new InvalidOperationException("Missing sticker " + StickerId(group, design));
                    sticker.category = category; EditorUtility.SetDirty(sticker);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
