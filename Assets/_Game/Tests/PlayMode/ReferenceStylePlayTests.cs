using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StickyPeerSort.Tests
{
    public sealed class ReferenceStylePlayTests
    {
        const string DemoKey = "StickyPeerSort.ReferenceStyle.Save.v1";
        GameController controller;
        GameBalanceConfig balance;
        bool existed;
        string old;
        [UnitySetUp] public IEnumerator SetUp()
        {
            existed = PlayerPrefs.HasKey(DemoKey); old = PlayerPrefs.GetString(DemoKey, "");
            yield return SceneManager.LoadSceneAsync("SampleStickerLevel"); yield return null;
            controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller); Assert.AreEqual(DemoKey, controller.progressKey);
            balance = Object.Instantiate(controller.balance); controller.balance = balance;
            balance.pressDuration = balance.peelDuration = balance.travelDuration = balance.categoryDuration = .001f;
            balance.categoryFilledHold = balance.categoryEnterDuration = balance.categoryExitDuration = .001f;
            balance.keyFlightDuration = balance.lockOpenDuration = balance.specialRevealDuration = .001f;
            balance.stickDuration = .001f;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (controller != null) Object.Destroy(controller.gameObject); yield return null;
            if (balance != null) Object.Destroy(balance);
            if (existed) PlayerPrefs.SetString(DemoKey, old); else PlayerPrefs.DeleteKey(DemoKey); PlayerPrefs.Save();
        }
        IEnumerator Finish()
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (controller.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(controller.IsAnimating);
        }
        [UnityTest] public IEnumerator AllFourSampleTracesWinThroughRealPresentation()
        {
            string mainBefore = PlayerPrefs.GetString("StickyPeerSort.Save.v1", "");
            for (int n = 0; n < 4; n++)
            {
                controller.LoadLevel(n); var level = controller.Session.Level;
                Assert.AreEqual(level.piles.Count, controller.ui.board.GetComponentsInChildren<StickerTrayView>().Length);
                foreach (var card in controller.ui.categoryViews.Values)
                    for (int slot = 0; slot < 4; slot++)
                        Assert.AreSame(level.objectives[card.categoryIndex].exactTargets[slot].targetSilhouette, card.targets[slot].sprite);
                foreach (string id in level.solutionTrace)
                {
                    int index = level.stickers.FindIndex(p => p.instanceId == id);
                    controller.Tap(index); yield return Finish();
                }
                Assert.AreEqual(GameplayState.Win, controller.Session.State, level.name);
                Assert.IsTrue(controller.ui.HasModal); Assert.IsEmpty(controller.ui.stickerViews);
                Assert.IsEmpty(controller.ui.waitingViews);
            }
            Assert.AreEqual(mainBefore, PlayerPrefs.GetString("StickyPeerSort.Save.v1", ""));
        }
        [UnityTest] public IEnumerator LandingReplacesTheSilhouetteWithMatchingArtwork()
        {
            var level = controller.Session.Level; int index = level.stickers.FindIndex(p => p.instanceId == level.solutionTrace[0]);
            var definition = level.stickers[index].definition;
            Assert.AreEqual(.75f, controller.ui.stickerViews[index].Rect.localScale.x, .001f);
            int category = level.objectives.FindIndex(o => o.category == definition.category);
            int slot = level.objectives[category].exactTargets.IndexOf(definition);
            Assert.AreSame(definition.targetSilhouette, controller.ui.categoryViews[category].targets[slot].sprite);
            controller.Tap(index); yield return Finish();
            var target = controller.ui.categoryViews[category].targets[slot];
            Assert.AreSame(definition.ArtworkSprite, target.sprite); Assert.AreEqual(definition.tint, target.color);
            Assert.IsTrue(target.preserveAspect);
        }
    }
}
