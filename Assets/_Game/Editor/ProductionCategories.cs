using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    // Themed categories of four designs each: every sheet is a real category choice.
    // 13/11 partner feedback: 16 original categories (all met by Level 5) + 14 new ones introduced
    // one or two per level from Level 6 to 15, so later levels keep showing new designs (30 categories, 120 stickers).
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
            new Group("VEHICLES", "AIR AND SEA", "hot_air_balloon", "rocket", "sailboat", "submarine"),
            // 13/11 library expansion (parent = own domain; art sps_<domain>_<design>.png)
            new Group("SPACE", "SPACE", "astronaut", "ufo", "ringed_planet", "satellite"),
            new Group("MUSIC", "MUSIC", "acoustic_guitar", "drum", "trumpet", "violin"),
            new Group("TOYS", "TOYS", "teddy_bear", "rubber_duck", "yo_yo", "toy_robot"),
            new Group("SPORTS", "SPORTS", "soccer_ball", "basketball", "tennis_racket", "bowling_pin"),
            new Group("HATS", "HATS", "cowboy_hat", "top_hat", "baseball_cap", "knitted_beanie"),
            new Group("SHOES", "SHOES", "sneaker", "rain_boot", "high_heel", "flip_flop"),
            new Group("BUGS", "BUGS", "ladybug", "butterfly", "bee", "snail"),
            new Group("DINOSAURS", "DINOSAURS", "t_rex", "triceratops", "stegosaurus", "brachiosaurus"),
            new Group("DRINKS", "DRINKS", "bubble_tea", "coffee_cup", "orange_juice", "milkshake"),
            new Group("WEATHER", "WEATHER", "sun", "rain_cloud", "rainbow", "snowflake"),
            new Group("FAIRY_TALE", "FAIRY TALE", "crown", "magic_wand", "castle", "baby_dragon"),
            new Group("SCHOOL", "SCHOOL", "pencil", "backpack", "scissors", "desk_globe"),
            new Group("CAMPING", "CAMPING", "tent", "campfire", "lantern", "compass"),
            new Group("GADGETS", "GADGETS", "smartphone", "headphones", "game_controller", "instant_camera")
        };
        /// <summary>The first 16 groups are the original library (Levels 1–5 rotate through them only).</summary>
        const int OriginalGroups = 16;
        /// <summary>Level (1-based) at which each expansion group (index 16..29) is first used.</summary>
        static readonly int[] IntroLevel = { 6, 6, 7, 7, 8, 8, 9, 9, 10, 11, 12, 13, 14, 15 };
        /// <summary>Categories (= sticker count / 4) per campaign level, mirrors CampaignBalanceBuilder.Recipes.</summary>
        static readonly int[] CategoriesPerLevel = { 3, 4, 5, 6, 7, 7, 8, 8, 9, 10, 9, 10, 11, 11, 12 };
        static string StickerId(Group group, string design)
        { return "sps_" + Domain(group) + "_" + design; }
        static string Domain(Group group)
        {
            switch (group.parent)
            {
                case "ANIMALS": return "animal";
                case "PLANTS": return "plant";
                case "VEHICLES": return "vehicle";
                case "FOOD": return "food";
                default: return group.parent.ToLowerInvariant();
            }
        }
        /// <summary>Art file of an expansion sticker (used by the importer).</summary>
        public static IEnumerable<KeyValuePair<string, string>> ExpansionStickers()
        {
            for (int g = OriginalGroups; g < Groups.Length; g++)
                foreach (string design in Groups[g].designs)
                    yield return new KeyValuePair<string, string>(StickerId(Groups[g], design), design.Replace('_', ' ').ToUpperInvariant());
        }
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
            var parent = AssetDatabase.LoadAssetAtPath<CategoryDefinition>("Assets/_Game/Data/Categories/" + group.parent + ".asset");
            asset.color = parent != null ? parent.color : Color.white; // Apply() sets the sheet colour
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        // Group order for a level; the generator uses the first (sticker count / 4) groups of the pool.
        static List<int> Order(int levelIndex)
        {
            if (levelIndex < 5)
            {
                // Levels 1–5 unchanged: the original 16 groups, starting at a level-specific group so levels differ.
                var old = new List<int>();
                for (int i = 0; i < OriginalGroups; i++) old.Add((levelIndex * 5 + i) % OriginalGroups);
                return old;
            }
            // Level 6+: the groups introduced at this level first, then every group already met, least recently used first
            // (never-used before oldest), ties in list order. Replays Levels 1..levelIndex to know when each group was last used.
            var lastUsed = new int[Groups.Length];
            for (int g = 0; g < lastUsed.Length; g++) lastUsed[g] = -1;
            List<int> order = null;
            for (int l = 0; l <= levelIndex; l++)
            {
                if (l < 5) order = Order(l);
                else
                {
                    int levelNo = l + 1;
                    var fresh = Enumerable.Range(OriginalGroups, Groups.Length - OriginalGroups).Where(g => IntroLevel[g - OriginalGroups] == levelNo).ToList();
                    var seen = Enumerable.Range(0, Groups.Length).Where(g => g < OriginalGroups || IntroLevel[g - OriginalGroups] < levelNo)
                        .OrderBy(g => lastUsed[g]).ThenBy(g => g).ToList();
                    order = fresh.Concat(seen).ToList();
                }
                int used = Mathf.Min(order.Count, l < CategoriesPerLevel.Length ? CategoriesPerLevel[l] : order.Count);
                for (int k = 0; k < used; k++) lastUsed[order[k]] = l;
            }
            return order;
        }
        public static List<StickerDefinition> Pool(int levelIndex)
        {
            var pool = new List<StickerDefinition>();
            foreach (int g in Order(levelIndex))
                foreach (string design in Groups[g].designs)
                    pool.Add(AssetDatabase.LoadAssetAtPath<StickerDefinition>(ProductionStickerBuilder.Root + "/Data/" + StickerId(Groups[g], design) + ".asset"));
            if (pool.Any(d => d == null)) throw new InvalidOperationException("Missing production sticker definition.");
            return pool;
        }
        /// <summary>Readable schedule: categories used by each level (new ones marked *).</summary>
        public static string Schedule()
        {
            var sb = new System.Text.StringBuilder();
            for (int l = 0; l < CategoriesPerLevel.Length; l++)
            {
                var order = Order(l).Take(CategoriesPerLevel[l]);
                sb.AppendLine("L" + (l + 1) + ": " + string.Join(", ", order.Select(g => (g >= OriginalGroups && IntroLevel[g - OriginalGroups] == l + 1 ? "*" : "") + Groups[g].title)));
            }
            return sb.ToString();
        }
        [MenuItem("Tools/Sticker Category Game/Production Stickers/Apply 30 Categories")]
        public static void Apply()
        {
            var sheetColors = new[] { new Color(.30f, .69f, .25f), new Color(.12f, .49f, .93f), new Color(.91f, .25f, .27f), new Color(.58f, .34f, .90f), new Color(.96f, .72f, .10f) };
            for (int g = 0; g < Groups.Length; g++)
            {
                var group = Groups[g];
                var category = Asset(group);
                category.color = sheetColors[g % sheetColors.Length]; EditorUtility.SetDirty(category);
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

