using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace StickyPeerSort.Tests
{
    public sealed class PuzzleTests
    {
        readonly List<Object> owned = new List<Object>();
        LevelDefinition level;
        T Make<T>() where T : ScriptableObject { var item = ScriptableObject.CreateInstance<T>(); owned.Add(item); return item; }
        [SetUp] public void Setup()
        {
            level = Make<LevelDefinition>(); level.activeCategoryCount = 1; level.bufferCapacity = 1;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Circle.png");
            for (int i = 0; i < 3; i++)
            {
                var cat = Make<CategoryDefinition>(); cat.id = "cat" + i; cat.title = cat.id;
                var def = Make<StickerDefinition>(); def.id = "def" + i; def.category = cat; def.sprite = sprite;
                level.objectives.Add(new CategoryObjective { category = cat, targetCount = 1 });
                level.stickers.Add(new StickerPlacement { instanceId = "s" + i, definition = def, position = new Vector2((i - 1) * 220, 0), sortingOrder = i });
            }
        }
        [TearDown] public void Cleanup() { foreach (var item in owned) Object.DestroyImmediate(item); owned.Clear(); }
        [Test] public void MatchingStickerFillsTarget() { var s = new GameSession(level); Assert.IsTrue(s.Select(0)); Assert.AreEqual(0, s.Categories[0].targets[0]); Assert.AreEqual(StickerLocation.Collected, s.Stickers[0].location); }
        [Test] public void FutureStickerPreservesIdentityInBuffer() { var s = new GameSession(level); s.Select(2); CollectionAssert.AreEqual(new[] { 2 }, s.Buffer); Assert.AreSame(level.stickers[2].definition, s.Stickers[s.Buffer[0]].placement.definition); }
        [Test] public void CompletionActivatesNextCategory() { var s = new GameSession(level); s.Select(0); Assert.IsTrue(s.Categories[1].active); Assert.IsFalse(s.Categories[0].active); }
        [Test] public void BufferedStickerAutoTransfers() { var s = new GameSession(level); s.Select(1); s.Select(0); Assert.IsEmpty(s.Buffer); Assert.IsTrue(s.Categories[1].Complete); Assert.IsTrue(s.Categories[2].active); }
        [Test] public void CascadeResolvesInStableOrderBeforeResult()
        {
            level.bufferCapacity = 2; var s = new GameSession(level); s.Select(2); s.Select(1); s.Select(0);
            Assert.AreEqual(GameplayState.Win, s.State); Assert.IsEmpty(s.Buffer);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, s.Events.Where(e => e.kind == PuzzleEventKind.Collected).Select(e => e.sticker));
        }
        [Test] public void FullTrayRemainsPlayableUntilExceeded() { var s = new GameSession(level); s.Select(2); Assert.AreEqual(GameplayState.Playing, s.State); s.Select(1); Assert.AreEqual(GameplayState.Fail, s.State); }
        [Test] public void FullTrayCanResolveWithoutPrematureFailure() { var s = new GameSession(level); s.Select(1); s.Select(0); Assert.AreEqual(GameplayState.Playing, s.State); Assert.IsEmpty(s.Buffer); }
        [Test] public void WinEmittedExactlyOnce() { var s = new GameSession(level); int wins = 0; s.StateChanged += state => { if (state == GameplayState.Win) wins++; }; s.Select(0); s.Select(1); s.Select(2); Assert.IsFalse(s.Select(2)); Assert.AreEqual(1, wins); }
        [Test] public void PartiallyCoveredStickerIsBlockedEvenOnItsVisiblePart()
        {
            level.stickers[0].position = Vector2.zero; level.stickers[1].position = new Vector2(50, 0);
            level.stickers[2].position = new Vector2(400, 0);
            var s = new GameSession(level); Assert.AreEqual(1, BoardGeometry.Hit(s, new Vector2(25, 0))); Assert.AreEqual(0, BoardGeometry.Hit(s, new Vector2(-60, 0))); Assert.IsTrue(BoardGeometry.Exposed(s, 0));
            string before = s.Key(); Assert.IsTrue(BoardGeometry.Covered(s, 0)); Assert.IsFalse(s.Legal(0));
            Assert.IsFalse(s.Select(0)); Assert.IsFalse(s.SelectAt(new Vector2(-60, 0))); Assert.AreEqual(before, s.Key());
            Assert.IsTrue(s.Select(1)); Assert.IsFalse(BoardGeometry.Covered(s, 0)); Assert.IsTrue(s.Select(0));
        }
        [Test] public void EqualSortingOrdersUseTheSameTieBreakForCoverageAndHits()
        {
            level.stickers[0].position = level.stickers[1].position; level.stickers[0].sortingOrder = level.stickers[1].sortingOrder;
            var s = new GameSession(level); Assert.AreEqual(1, BoardGeometry.Hit(s, level.stickers[1].position)); Assert.IsTrue(BoardGeometry.Covered(s, 0)); Assert.IsFalse(s.Select(0));
        }
        [Test] public void TransparentCornerDoesNotConsumeTap() { var s = new GameSession(level); Assert.IsFalse(BoardGeometry.Contains(s.Stickers[0], level.stickers[0].position + new Vector2(84, 75))); }
        [Test] public void LockedStickerWaitsForRemovedDependency()
        {
            level.stickers[0].special.kind = SpecialKind.Locked; level.stickers[0].special.dependencyId = "s1";
            var s = new GameSession(level); Assert.IsFalse(s.Select(0)); s.Select(1); Assert.IsTrue(s.Select(0));
        }
        [Test] public void CategoryAndKeyUnlockConditionsWork()
        {
            var config = level.stickers[1].special; config.kind = SpecialKind.Locked; config.unlockCondition = UnlockCondition.CategoryCompleted; config.dependencyId = "cat0";
            var s = new GameSession(level); Assert.IsFalse(s.Select(1)); s.Select(0); Assert.IsTrue(s.Select(1));
            config.unlockCondition = UnlockCondition.KeyCollected; config.dependencyId = "s0"; s = new GameSession(level); Assert.IsFalse(s.Select(1)); s.Select(0); Assert.IsTrue(s.Select(1));
        }
        [Test] public void IceBreaksBeforeCollection()
        {
            level.stickers[0].special.kind = SpecialKind.Frozen; var s = new GameSession(level);
            s.Select(0); Assert.AreEqual(1, s.Stickers[0].ice); Assert.AreEqual(StickerLocation.Board, s.Stickers[0].location);
            s.Select(0); Assert.AreEqual(0, s.Stickers[0].ice); s.Select(0); Assert.AreEqual(StickerLocation.Collected, s.Stickers[0].location);
        }
        [Test] public void LinkedPartnersRequireExposureWithoutRemovingEachOther()
        {
            foreach (int i in new[] { 0, 1 }) { level.stickers[i].special.kind = SpecialKind.Linked; level.stickers[i].special.linkId = "A"; }
            level.stickers[2].position = level.stickers[1].position;
            var s = new GameSession(level); Assert.IsFalse(s.Select(0)); s.Select(2); Assert.IsTrue(s.Select(0)); Assert.AreEqual(StickerLocation.Board, s.Stickers[1].location); Assert.IsTrue(s.Select(1));
        }
        [Test] public void MysteryRevealsThenCollects() { level.stickers[0].special.kind = SpecialKind.Mystery; var s = new GameSession(level); s.Select(0); Assert.IsTrue(s.Stickers[0].revealed); Assert.AreEqual(StickerLocation.Collected, s.Stickers[0].location); }
        [Test] public void MysteryRevealsWhenCoverIsRemovedAndUndoHidesItAgain()
        {
            level.stickers[0].special.kind = SpecialKind.Mystery;
            level.stickers[1].position = level.stickers[0].position + new Vector2(100, 0); level.stickers[1].sortingOrder = 10;
            var s = new GameSession(level); Assert.IsFalse(s.Stickers[0].revealed); Assert.IsFalse(s.Legal(0));
            s.Select(1); Assert.IsTrue(s.Stickers[0].revealed); Assert.AreEqual(StickerLocation.Board, s.Stickers[0].location);
            Assert.IsTrue(s.Events.Exists(e => e.kind == PuzzleEventKind.Revealed && e.sticker == 0));
            s.Undo(); Assert.IsFalse(s.Stickers[0].revealed);
        }
        [Test] public void KeySetMustBeCompleteBeforeLockOpens()
        {
            level.objectives[0].targetCount = 2;
            level.stickers[2].definition = level.stickers[0].definition;
            var special = level.stickers[1].special; special.kind = SpecialKind.Locked;
            special.unlockCondition = UnlockCondition.CategoryCompleted; special.dependencyId = "cat0";
            var s = new GameSession(level); s.Select(0); Assert.IsFalse(s.Legal(1));
            s.Select(2); Assert.IsTrue(s.Legal(1)); Assert.IsTrue(s.Events.Exists(e => e.kind == PuzzleEventKind.Unlocked && e.sticker == 1));
            s.Undo(); Assert.IsFalse(s.Legal(1));
        }
        [Test] public void UndoRestoresFullLogicalStateAndSpecials()
        {
            level.stickers[0].special.kind = SpecialKind.Frozen; var s = new GameSession(level); string before = s.Key();
            s.Select(0); Assert.IsTrue(s.Undo()); Assert.AreEqual(before, s.Key()); s.Select(2); s.Select(1); Assert.AreEqual(GameplayState.Fail, s.State); Assert.IsTrue(s.Undo()); Assert.AreEqual(GameplayState.Playing, s.State);
        }
        [Test] public void SnapshotsDoNotMutateSourceOrEventHistory()
        {
            var s = new GameSession(level); s.Select(2); var events = s.Events.ToArray(); var copy = s.Copy(); copy.Select(0);
            CollectionAssert.AreEqual(events, s.Events); Assert.AreEqual(StickerLocation.Board, s.Stickers[0].location); Assert.AreEqual(0, level.solutionTrace.Count);
        }
        [Test] public void RestartIsDeterministic() { var s = new GameSession(level); string before = s.Key(); s.Select(0); s.AddSlot(); Assert.AreEqual(before, new GameSession(level).Key()); }
        [Test] public void InvalidLevelsAreRejected() { level.stickers[1].instanceId = "s0"; level.bufferCapacity = 0; level.stickers[0].special.kind = SpecialKind.Linked; Assert.GreaterOrEqual(LevelValidator.Validate(level).Count, 3); }
        [Test] public void ExactTargetIdentityIsRespected()
        {
            level.stickers[1].definition.category = level.objectives[0].category; level.objectives.RemoveAt(1); level.objectives[0].targetCount = 2;
            level.objectives[0].exactTargets.Add(level.stickers[1].definition); level.objectives[0].exactTargets.Add(level.stickers[0].definition);
            var s = new GameSession(level); s.Select(0); Assert.AreEqual(0, s.Categories[0].targets[1]); s.Select(1); Assert.AreEqual(1, s.Categories[0].targets[0]);
        }
        [Test] public void ExtraSlotAndUndoRestoreCapacity() { var s = new GameSession(level); s.AddSlot(); Assert.AreEqual(2, s.Capacity); s.Undo(); Assert.AreEqual(1, s.Capacity); }
        [Test] public void ShufflePreservesIdentityAndUndoRestoresTransforms()
        {
            var s = new GameSession(level); var positions = s.Stickers.Select(x => x.position).ToArray();
            s.Shuffle(37); CollectionAssert.AreEquivalent(positions, s.Stickers.Select(x => x.position));
            for (int i = 0; i < s.Stickers.Count; i++) Assert.AreSame(level.stickers[i].definition, s.Stickers[i].placement.definition);
            Assert.IsTrue(s.Undo()); CollectionAssert.AreEqual(positions, s.Stickers.Select(x => x.position));
        }
        [Test] public void BoundedSolverReportsBudgetWithoutClaimingUnsolvable()
        {
            List<int> trace; Assert.AreEqual(SolveResult.BudgetExceeded, PuzzleSolver.Solve(new GameSession(level), 0, out trace)); Assert.IsEmpty(trace);
        }
        [Test] public void SolverOnlySuggestsLegalWinningMoves() { var s = new GameSession(level); List<int> trace; Assert.AreEqual(SolveResult.Solved, PuzzleSolver.Solve(s, 500, out trace)); foreach (int i in trace) { Assert.IsTrue(s.Legal(i)); s.Select(i); } Assert.AreEqual(GameplayState.Win, s.State); }
        [Test] public void PauseRejectsInput() { var s = new GameSession(level); s.Pause(true); Assert.IsFalse(s.Select(0)); s.Pause(false); Assert.IsTrue(s.Select(0)); }
        [Test] public void SaveRoundTripAndCompletionRewardsAreIdempotent()
        {
            const string key = "StickyPeerSort.Save.v1"; string old = PlayerPrefs.GetString(key, ""); bool existed = PlayerPrefs.HasKey(key);
            try { PlayerPrefs.SetString(key, ""); var save = new SaveService(); level.coinReward = 42; Assert.IsTrue(save.Complete(level)); Assert.IsFalse(save.Complete(level)); var loaded = new SaveService(); Assert.AreEqual(42, loaded.Data.coins); Assert.AreEqual(2, loaded.Data.unlockedLevel); Assert.AreEqual(1, loaded.Data.highestCompleted); Assert.AreEqual(1, SaveService.Decode("invalid").version); }
            finally { if (existed) PlayerPrefs.SetString(key, old); else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        }
        [Test] public void SampleMysteriesHaveVisibleQuestionCentersWhileCovered()
        {
            foreach (int number in new[] { 9, 10 })
            {
                var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/_Game/Data/Levels/Level" + number.ToString("00") + ".asset");
                var session = new GameSession(asset);
                for (int i = 0; i < session.Stickers.Count; i++)
                    if (session.Stickers[i].placement.special.kind == SpecialKind.Mystery)
                    {
                        Assert.IsTrue(BoardGeometry.Covered(session, i)); Assert.IsFalse(session.Stickers[i].revealed);
                        Assert.AreEqual(i, BoardGeometry.Hit(session, session.Stickers[i].position), "The question-mark center must remain visible.");
                    }
            }
        }
        [Test] public void SampleTargetsHaveFixedArtworkAndVariedTraysContainTheirStickers()
        {
            var counts = new HashSet<int>();
            for (int number = 1; number <= 10; number++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/_Game/Data/Levels/Level" + number.ToString("00") + ".asset");
                counts.Add(asset.piles.Count);
                foreach (var objective in asset.objectives)
                {
                    Assert.AreEqual(objective.targetCount, objective.exactTargets.Count);
                    foreach (var target in objective.exactTargets) Assert.IsNotNull(target.ArtworkSprite);
                }
                foreach (var p in asset.stickers)
                {
                    var pile = asset.piles.Find(x => x.id == p.pileId); Assert.IsNotNull(pile);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Vector2 point = new Vector2((corner % 2 == 0 ? -.5f : .5f) * p.size.x, (corner < 2 ? -.5f : .5f) * p.size.y) * p.scale;
                        Vector2 world = p.position + (Vector2)(Quaternion.Euler(0, 0, p.rotation) * point);
                        Vector2 local = Quaternion.Euler(0, 0, -pile.rotation) * (world - pile.position);
                        Assert.LessOrEqual(Mathf.Abs(local.x), pile.size.x * .5f - 14, "Sticker outside tray: L" + number + " " + p.instanceId);
                        Assert.LessOrEqual(Mathf.Abs(local.y), pile.size.y * .5f - 14, "Sticker outside tray: L" + number + " " + p.instanceId);
                    }
                }
            }
            Assert.GreaterOrEqual(counts.Count, 5, "The sample levels need distinct compartment counts.");
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        public void SampleLevelHasValidatedReplayableSolution(int number)
        {
            var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/_Game/Data/Levels/Level" + number.ToString("00") + ".asset");
            Assert.IsNotNull(asset); Assert.IsEmpty(LevelValidator.Validate(asset)); Assert.IsNotEmpty(asset.solutionTrace);
            var s = new GameSession(asset);
            foreach (string id in asset.solutionTrace) { int index = asset.stickers.FindIndex(p => p.instanceId == id); Assert.IsTrue(s.Select(index), "Invalid trace action: " + id); }
            Assert.AreEqual(GameplayState.Win, s.State); Assert.IsEmpty(s.Buffer);
        }
    }
}
