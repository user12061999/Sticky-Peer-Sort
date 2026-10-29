using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickyPeerSort.Editor;

namespace StickyPeerSort.Tests
{
    public sealed class CampaignBalanceTests
    {
        static LevelDefinition Load(int number) => AssetDatabase.LoadAssetAtPath<LevelDefinition>(CampaignBalanceBuilder.Root + "/Campaign_" + number.ToString("00") + ".asset");
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        public void CampaignHasLegalWinningRouteWithoutBoostersOrOverflow(int number)
        {
            var level = Load(number); Assert.IsNotNull(level);
            Assert.AreEqual(number, level.levelNumber);
            Assert.IsTrue(level.failOnOverflow);
            var report = LevelAnalysis.Analyze(level);
            Assert.IsTrue(report.Verified, string.Join(";", report.messages));
            Assert.Greater(report.initiallyClickable, 0);
            Assert.Less(report.peakTransientBuffer, level.bufferCapacity);
            Assert.AreEqual(level.stickers.Count, report.solution.Distinct().Count());
            // Symbols must be exposed and mystery identity must really be covered.
            var session = new GameSession(level);
            for (int i = 0; i < level.stickers.Count; i++)
                if (level.stickers[i].special.kind == SpecialKind.Mystery)
                { Assert.IsTrue(BoardGeometry.Covered(session, i)); Assert.AreEqual(i, BoardGeometry.Hit(session, level.stickers[i].position)); }
        }
        [Test]
        public void PlannedBufferAuthoringIsRepeatableAndRejectsAnUnsafeCapacity()
        {
            var level = Object.Instantiate(Load(10));
            try
            {
                string message;
                Assert.IsTrue(ConfiguredLevelGenerator.Rebuild(level, out message), message);
                string json = EditorJsonUtility.ToJson(level);
                Assert.IsTrue(ConfiguredLevelGenerator.Rebuild(level, out message), message);
                Assert.AreEqual(json, EditorJsonUtility.ToJson(level));
                Assert.GreaterOrEqual(LevelAnalysis.Analyze(level).peakTransientBuffer, level.generation.plannedBufferPeak);
                var trace = level.solutionTrace.ToArray();
                level.bufferCapacity = level.generation.plannedBufferPeak;
                Assert.IsFalse(ConfiguredLevelGenerator.Rebuild(level, out message));
                StringAssert.Contains("waiting slot", message);
                CollectionAssert.AreEqual(trace, level.solutionTrace);
            }
            finally { Object.DestroyImmediate(level); }
        }
    }
}
