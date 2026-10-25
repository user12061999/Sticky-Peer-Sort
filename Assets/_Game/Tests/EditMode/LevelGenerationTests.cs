using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickyPeerSort.Editor;

namespace StickyPeerSort.Tests
{
    public sealed class LevelGenerationTests
    {
        static LevelDefinition Copy(string name)
        {
            var source = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/_Game/ReferenceStyle/Data/Levels/" + name + ".asset");
            var copy = Object.Instantiate(source); copy.name = source.name; copy.generation.enabled = true;
            return copy;
        }
        [TestCase("Level_05_Grid4")]
        [TestCase("Level_12_Rows2")]
        [TestCase("Level_19_Mixed3")]
        [TestCase("Level_62_Pile1")]
        public void ConfiguredSpecialRatiosProduceVisibleSymbolsAndWinningPath(string name)
        {
            var level = Copy(name);
            try
            {
                level.generation.mysteryPercent = 10; level.generation.lockedPercent = 10;
                string message; Assert.IsTrue(ConfiguredLevelGenerator.Rebuild(level, out message), message);
                Assert.AreEqual(Mathf.RoundToInt(level.stickers.Count * .1f), level.stickers.Count(p => p.special.kind == SpecialKind.Mystery));
                Assert.AreEqual(Mathf.RoundToInt(level.stickers.Count * .1f), level.stickers.Count(p => p.special.kind == SpecialKind.Locked));
                var session = new GameSession(level);
                foreach (var mystery in level.stickers.Where(p => p.special.kind == SpecialKind.Mystery))
                {
                    int index = level.stickers.IndexOf(mystery);
                    Assert.IsTrue(BoardGeometry.Covered(session, index));
                    Assert.AreEqual(index, BoardGeometry.Hit(session, mystery.position));
                }
                foreach (string id in level.solutionTrace) Assert.IsTrue(session.Select(level.stickers.FindIndex(p => p.instanceId == id)), id);
                Assert.AreEqual(GameplayState.Win, session.State);
            }
            finally { Object.DestroyImmediate(level); }
        }
        [Test]
        public void CountsPoolSeedAndDifficultyAreFunctional()
        {
            var level = Copy("Level_62_Pile1");
            try
            {
                level.generatedStickerCount = 36; level.generation.categoryCount = 3; level.generation.typesPerCategory = 2;
                level.generation.mysteryPercent = 0; level.generation.lockedPercent = 0;
                string message; Assert.IsTrue(ConfiguredLevelGenerator.Rebuild(level, out message), message);
                Assert.AreEqual(36, level.stickers.Count); Assert.AreEqual(9, level.objectives.Count);
                Assert.AreEqual(6, level.stickers.Select(p => p.definition).Distinct().Count());
                Assert.AreEqual(3, level.objectives.Select(o => o.category).Distinct().Count());
                string first = JsonUtility.ToJson(level);
                Assert.IsTrue(ConfiguredLevelGenerator.Rebuild(level, out message), message);
                Assert.AreEqual(first, JsonUtility.ToJson(level));
                var positions = level.stickers.Select(p => p.position).ToArray();
                level.layoutSeed++;
                Assert.IsTrue(ConfiguredLevelGenerator.Rebuild(level, out message), message);
                Assert.IsFalse(positions.SequenceEqual(level.stickers.Select(p => p.position)));
                level.generation.difficulty = LevelDifficulty.Easy;
                Assert.IsTrue(ConfiguredLevelGenerator.Rebuild(level, out message), message);
                float easySize = level.stickers.Average(p => p.size.x);
                level.generation.difficulty = LevelDifficulty.Hard;
                Assert.IsTrue(ConfiguredLevelGenerator.Rebuild(level, out message), message);
                Assert.Greater(level.stickers.Average(p => p.size.x), easySize);
                // Reducing unique types preserves the pool so they can be increased again.
                level.generation.categoryCount = 4; level.generation.typesPerCategory = 4;
                Assert.IsTrue(ConfiguredLevelGenerator.Rebuild(level, out message), message);
                Assert.AreEqual(16, level.stickers.Select(p => p.definition).Distinct().Count());
            }
            finally { Object.DestroyImmediate(level); }
        }
        [Test]
        public void InvalidSettingsDoNotReplaceLastValidLayout()
        {
            var level = Copy("Level_62_Pile1");
            try
            {
                var pieces = level.stickers.Select(p => JsonUtility.ToJson(p)).ToArray();
                var trace = level.solutionTrace.ToArray();
                level.generation.mysteryPercent = 90; level.generation.lockedPercent = 90;
                string message; Assert.IsFalse(ConfiguredLevelGenerator.Rebuild(level, out message));
                StringAssert.Contains("100%", message);
                CollectionAssert.AreEqual(pieces, level.stickers.Select(p => JsonUtility.ToJson(p)).ToArray());
                CollectionAssert.AreEqual(trace, level.solutionTrace);
            }
            finally { Object.DestroyImmediate(level); }
        }
    }
}
