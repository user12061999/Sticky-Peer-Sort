using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickyPeerSort.Editor;

namespace StickyPeerSort.Tests
{
    public sealed class LevelAnalysisTests
    {
        readonly List<Object> owned = new List<Object>();
        LevelDefinition level;
        T Make<T>() where T : ScriptableObject { var item = ScriptableObject.CreateInstance<T>(); owned.Add(item); return item; }
        [SetUp] public void Setup()
        {
            level = Make<LevelDefinition>(); level.activeCategoryCount = 1; level.bufferCapacity = 3;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Circle.png");
            for (int i = 0; i < 3; i++)
            {
                var category = Make<CategoryDefinition>(); category.id = "c" + i; category.title = category.id;
                var definition = Make<StickerDefinition>(); definition.id = "d" + i; definition.category = category; definition.sprite = sprite;
                level.objectives.Add(new CategoryObjective { category = category, targetCount = 1 });
                level.stickers.Add(new StickerPlacement { instanceId = "s" + i, definition = definition, position = new Vector2(i * 300, 0), sortingOrder = i });
            }
        }
        [TearDown] public void Cleanup() { foreach (var item in owned) Object.DestroyImmediate(item); owned.Clear(); }
        [Test] public void MetricsUseRealCoverageSpecialRulesAndReplayBufferEvents()
        {
            for (int i = 0; i < 3; i++) level.stickers[i].position = new Vector2(i * 50, 0);
            level.stickers[0].special = new StickerSpecialConfig { kind = SpecialKind.Locked, dependencyId = "s2" };
            level.stickers[1].special.kind = SpecialKind.Mystery;
            level.solutionTrace.AddRange(new[] { "s2", "s1", "s0" });
            var report = LevelAnalysis.Analyze(level);
            Assert.IsTrue(report.Verified, string.Join(";", report.messages));
            Assert.AreEqual(1, report.initiallyClickable); Assert.AreEqual(2, report.initiallyCovered);
            Assert.AreEqual(3, report.maxOverlapDepth); Assert.AreEqual(1, report.locked); Assert.AreEqual(1, report.mystery);
            Assert.AreEqual(2, report.peakResolvedBuffer); Assert.AreEqual(2, report.peakTransientBuffer); Assert.AreEqual(2, report.firstPeakStep);
            // Another valid path can have a lower peak: this metric must not claim a minimum.
        }
        [Test] public void StaleTraceCanBeSearchedWithoutModifyingTheAsset()
        {
            level.solutionTrace.Add("missing"); string before = EditorJsonUtility.ToJson(level);
            Assert.AreEqual(LevelVerification.Unverified, LevelAnalysis.Analyze(level).verification);
            var report = LevelAnalysis.Analyze(level, true);
            Assert.IsTrue(report.Verified); Assert.AreEqual("Bounded solver", report.solutionSource);
            Assert.AreEqual(0, report.peakResolvedBuffer);
            Assert.AreEqual(before, EditorJsonUtility.ToJson(level));
        }
        [Test] public void IncompleteOrTrailingActionsAreNotWinningTraces()
        {
            level.solutionTrace.Add("s0"); Assert.AreEqual(LevelVerification.Unverified, LevelAnalysis.Analyze(level).verification);
            level.solutionTrace.AddRange(new[] { "s1", "s2", "s2" });
            Assert.AreEqual(LevelVerification.Unverified, LevelAnalysis.Analyze(level).verification);
        }
        [Test] public void BudgetExceededIsDifferentFromProvenNoSolution()
        {
            Assert.AreEqual(LevelVerification.BudgetExceeded, LevelAnalysis.Analyze(level, true, 0).verification);
            for (int i = 0; i < 3; i++) level.stickers[i].special = new StickerSpecialConfig { kind = SpecialKind.Locked, dependencyId = "s" + ((i + 1) % 3) };
            var blocked = LevelAnalysis.Analyze(level, true);
            Assert.AreEqual(LevelVerification.NoSolution, blocked.verification); Assert.AreEqual(0, blocked.initiallyClickable);
        }
        [Test] public void MissingAssetsAndBadDefinitionsReturnInvalidReports()
        {
            Assert.AreEqual(LevelVerification.Invalid, LevelAnalysis.Analyze(null).verification);
            level.stickers[0].definition = null;
            Assert.AreEqual(LevelVerification.Invalid, LevelAnalysis.Analyze(level).verification);
        }
        [Test] public void ReplaySeekRestoresIceCategoriesUnlocksBufferAndEventsFromOwnedSnapshot()
        {
            level.stickers[0].special = new StickerSpecialConfig { kind = SpecialKind.Frozen, iceLayers = 1 };
            level.stickers[1].special = new StickerSpecialConfig { kind = SpecialKind.Locked, unlockCondition = UnlockCondition.CategoryCompleted, dependencyId = "c0" };
            var trace = new[] { 2, 0, 0, 1 };
            using (var replay = new LevelSolutionReplay(level, trace))
            {
                level.stickers[0].position = new Vector2(9999, 0);
                Assert.AreEqual(Vector2.zero, replay.Session.Stickers[0].position);
                replay.Seek(2); Assert.AreEqual(0, replay.Session.Stickers[0].ice);
                Assert.AreEqual(1, replay.Session.Buffer.Count); Assert.IsTrue(replay.Events.Any(e => e.kind == PuzzleEventKind.IceBroken));
                string middle = replay.Session.Key();
                replay.Seek(3); Assert.IsTrue(replay.Events.Any(e => e.kind == PuzzleEventKind.Unlocked));
                replay.Seek(4); Assert.AreEqual(GameplayState.Win, replay.Session.State);
                Assert.IsTrue(replay.Events.Any(e => e.kind == PuzzleEventKind.CategoryCompleted)); Assert.IsEmpty(replay.Session.Buffer);
                replay.Seek(2); Assert.AreEqual(middle, replay.Session.Key());
                Assert.IsTrue(replay.Events.Any(e => e.kind == PuzzleEventKind.IceBroken));
                replay.Seek(0); Assert.AreEqual(1, replay.Session.Stickers[0].ice); Assert.IsEmpty(replay.Events);
                Assert.IsFalse(replay.Session.Legal(1)); Assert.AreEqual(2, replay.NextSticker);
            }
        }
        [Test] public void RejectsReplayThatWouldLoseAtFullBuffer()
        {
            level.bufferCapacity = 1;
            Assert.Throws<System.ArgumentException>(() => new LevelSolutionReplay(level, new[] { 2, 0, 1 }));
        }
    }
}
