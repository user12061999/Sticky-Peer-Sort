using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickyPeerSort.Editor;

namespace StickyPeerSort.Tests
{
    public sealed class ReferenceStyleTests
    {
        [Test] public void FourSamplesAreIndependentValidAndSolvableWithExpectedTrays()
        {
            int[] counts = { 4, 2, 3, 1 };
            int[] stickers = { 32, 40, 48, 64 };
            for (int n = 0; n < 4; n++)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(ReferenceStyleBuilder.Root + "/Data/Levels/" + ReferenceStyleBuilder.Names[n] + ".asset");
                Assert.IsNotNull(level); Assert.IsEmpty(LevelValidator.Validate(level), level.name);
                Assert.AreEqual(counts[n], level.piles.Count); Assert.AreEqual(n == 3 ? 5 : 4, level.bufferCapacity);
                Assert.AreEqual(3, level.activeCategoryCount); Assert.AreEqual(stickers[n], level.stickers.Count);
                Assert.AreEqual(stickers[n] / 4, level.objectives.Count);
                var session = new GameSession(level); bool overlap = false;
                for (int i = 0; i < session.Stickers.Count; i++) overlap |= BoardGeometry.Covered(session, i);
                Assert.IsTrue(overlap, level.name + " must contain real alpha overlap.");
                foreach (string id in level.solutionTrace)
                    Assert.IsTrue(session.Select(level.stickers.FindIndex(p => p.instanceId == id)), level.name + ": " + id);
                Assert.AreEqual(GameplayState.Win, session.State);
                foreach (var sticker in level.stickers)
                    StringAssert.StartsWith(ReferenceStyleBuilder.Root, AssetDatabase.GetAssetPath(sticker.definition));
            }
        }
        [Test] public void VisibleSampleArtworkStaysInItsCompartmentDespiteTransparentPadding()
        {
            foreach (string name in ReferenceStyleBuilder.Names)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(ReferenceStyleBuilder.Root + "/Data/Levels/" + name + ".asset");
                foreach (var sticker in level.stickers)
                {
                    var pile = level.piles.Find(p => p.id == sticker.pileId); Assert.IsNotNull(pile);
                    Vector2 fit = sticker.definition.FitSize(sticker.size) * sticker.scale;
                    for (int x = 0; x <= 48; x++) for (int y = 0; y <= 48; y++)
                    {
                        var uv = new Vector2(x / 48f, y / 48f);
                        if (!sticker.definition.ContainsUV(uv)) continue;
                        var local = (Vector2)(Quaternion.Euler(0, 0, sticker.rotation) * new Vector3((uv.x - .5f) * fit.x, (uv.y - .5f) * fit.y));
                        local += sticker.position - pile.position;
                        local = (Vector2)(Quaternion.Euler(0, 0, -pile.rotation) * local);
                        Assert.LessOrEqual(Mathf.Abs(local.x), pile.size.x * .5f - 15, name + ": " + sticker.instanceId + " x");
                        Assert.LessOrEqual(Mathf.Abs(local.y), pile.size.y * .5f - 15, name + ": " + sticker.instanceId + " y");
                    }
                }
            }
        }
        [Test] public void MysteryQuestionCenterIsVisibleWhileItsFaceIsCovered()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(ReferenceStyleBuilder.Root + "/Data/Levels/Level_62_Pile1.asset");
            var session = new GameSession(level); int index = level.stickers.FindIndex(s => s.special.kind == SpecialKind.Mystery);
            Assert.IsFalse(session.Stickers[index].revealed); Assert.IsTrue(BoardGeometry.Covered(session, index));
            Assert.AreEqual(index, BoardGeometry.Hit(session, session.Stickers[index].position));
        }
        [Test] public void ReducedBoardStickersKeepMixedCategoriesAndRealOverlap()
        {
            foreach (string name in ReferenceStyleBuilder.Names)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(ReferenceStyleBuilder.Root + "/Data/Levels/" + name + ".asset");
                var session = new GameSession(level);
                Assert.AreEqual(.75f, level.boardStickerSizeMultiplier);
                foreach (var piece in level.stickers) Assert.AreEqual(.75f, piece.scale, .001f);
                foreach (var tray in level.piles)
                {
                    var categories = new HashSet<CategoryDefinition>();
                    bool overlap = false; int count = 0;
                    for (int i = 0; i < level.stickers.Count; i++)
                    {
                        var piece = level.stickers[i]; if (piece.pileId != tray.id) continue;
                        categories.Add(piece.definition.category); count++;
                        overlap |= BoardGeometry.Covered(session, i);
                    }
                    Assert.GreaterOrEqual(count, 8); Assert.GreaterOrEqual(categories.Count, 3);
                    Assert.GreaterOrEqual(DenseStickerLayout.Coverage(level, tray), .4f, name + " / " + tray.id + " has lost its pile.");
                    Assert.IsTrue(overlap, name + " / " + tray.id + " must retain overlapping silhouettes.");
                }
            }
        }
        [Test] public void DemoProgressDoesNotWriteMainSave()
        {
            const string main = "StickyPeerSort.Save.v1", demo = "StickyPeerSort.Test.ReferenceStyle.Save.v1";
            string before = PlayerPrefs.GetString(main, ""); bool existed = PlayerPrefs.HasKey(demo); string old = PlayerPrefs.GetString(demo, "");
            try
            {
                var save = new SaveService(3, demo); save.Data.coins = 123; save.Flush();
                Assert.AreEqual(before, PlayerPrefs.GetString(main, "")); Assert.AreEqual(123, new SaveService(3, demo).Data.coins);
            }
            finally { if (existed) PlayerPrefs.SetString(demo, old); else PlayerPrefs.DeleteKey(demo); PlayerPrefs.Save(); }
        }
    }
}
