using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StickyPeerSort.Tests
{
    public sealed class TutorialGuidePlayTests
    {
        const string Key = "StickyPeerSort.ReferenceStyle.Save.v1";
        GameController controller;
        GameTutorialGuide guide;
        GameBalanceConfig balance;
        bool existed;
        string old;
        [UnitySetUp] public IEnumerator Setup()
        {
            existed = PlayerPrefs.HasKey(Key); old = PlayerPrefs.GetString(Key, "");
            yield return SceneManager.LoadSceneAsync("SampleStickerLevel"); yield return null;
            controller = Object.FindFirstObjectByType<GameController>(); guide = controller.GetComponent<GameTutorialGuide>();
            balance = Object.Instantiate(controller.balance); controller.balance = balance;
            balance.tutorialsEnabled = true;
            balance.pressDuration = balance.peelDuration = balance.travelDuration = balance.stickDuration = .001f;
            balance.categoryDuration = balance.categoryFilledHold = balance.categoryEnterDuration = balance.categoryExitDuration = .001f;
            balance.keyFlightDuration = balance.lockOpenDuration = balance.specialRevealDuration = .001f;
            controller.Save.Data.completedTutorials = 0; controller.LoadLevel(0); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (controller != null) Object.Destroy(controller.gameObject); yield return null;
            if (balance != null) Object.Destroy(balance);
            if (existed) PlayerPrefs.SetString(Key, old); else PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save();
        }
        [UnityTest] public IEnumerator PartialPeelDoesNotCompleteLessonAndDismissPersistsWithoutChangingPuzzle()
        {
            Assert.AreEqual(TutorialLesson.Peel, guide.ActiveLesson);
            Assert.IsTrue(guide.IsVisible); Assert.IsTrue(controller.CanTap);
            ScreenCapture.CaptureScreenshot("Temp/TutorialPeel.png"); yield return null;
            string before = controller.Session.Key();
            var view = controller.ui.stickerViews[guide.SubjectIndex];
            var e = new PointerEventData(EventSystem.current) { pointerId = -1, position = RectTransformUtility.WorldToScreenPoint(null, view.Rect.position) };
            view.DragPeel.Begin(e); e.position += new Vector2(8, 8); view.DragPeel.OnDrag(e); yield return null;
            Assert.IsFalse(guide.IsVisible); Assert.AreEqual(0, controller.Save.Data.completedTutorials);
            view.DragPeel.OnPointerUp(e);
            float deadline = Time.realtimeSinceStartup + 2;
            while (view.DragPeel.IsReturning && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.IsTrue(guide.IsVisible); Assert.AreEqual(before, controller.Session.Key());
            guide.Dismiss();
            Assert.AreEqual((int)TutorialLesson.Peel, SaveService.Decode(PlayerPrefs.GetString(Key)).completedTutorials);
            controller.LoadLevel(0); yield return null; Assert.AreEqual(TutorialLesson.None, guide.ActiveLesson);
        }
        [UnityTest] public IEnumerator SpecialLessonsAppearOnFirstOccurrenceAndHideDuringPause()
        {
            guide.Dismiss(); controller.LoadLevel(2); yield return null;
            Assert.AreEqual(TutorialLesson.Mystery, guide.ActiveLesson);
            ScreenCapture.CaptureScreenshot("Temp/TutorialMystery.png"); yield return null;
            Assert.AreEqual(SpecialKind.Mystery, controller.Session.Stickers[guide.SubjectIndex].placement.special.kind);
            Assert.GreaterOrEqual(guide.ActionStickerIndex, 0); Assert.IsTrue(controller.Session.Legal(guide.ActionStickerIndex));
            guide.Dismiss(); controller.LoadLevel(4); yield return null;
            Assert.AreEqual(TutorialLesson.Lock, guide.ActiveLesson);
            ScreenCapture.CaptureScreenshot("Temp/TutorialLock.png"); yield return null;
            Assert.IsTrue(controller.ui.stickerViews[guide.SubjectIndex].HasLockChains);
            controller.ui.settings.onClick.Invoke(); yield return null; Assert.IsFalse(guide.IsVisible);
            controller.ui.primary.onClick.Invoke(); yield return null; Assert.IsTrue(guide.IsVisible);
            guide.Dismiss();
            Assert.AreEqual(7, SaveService.Decode(PlayerPrefs.GetString(Key)).completedTutorials);
            controller.LoadLevel(2); yield return null; Assert.AreEqual(TutorialLesson.None, guide.ActiveLesson);
            controller.LoadLevel(4); yield return null; Assert.AreEqual(TutorialLesson.None, guide.ActiveLesson);
        }
        [UnityTest] public IEnumerator RealCollectionRevealAndUnlockCompleteTheirLessons()
        {
            int first = guide.SubjectIndex; controller.Tap(first); yield return FinishMove(); yield return null;
            Assert.AreNotEqual(0, controller.Save.Data.completedTutorials & (int)TutorialLesson.Peel);
            Assert.AreEqual(TutorialLesson.None, guide.ActiveLesson);
            foreach (int level in new[] { 2, 4 })
            {
                controller.LoadLevel(level); yield return null;
                var lesson = level == 2 ? TutorialLesson.Mystery : TutorialLesson.Lock;
                Assert.AreEqual(lesson, guide.ActiveLesson);
                foreach (string id in controller.Session.Level.solutionTrace)
                {
                    int index = controller.Session.Level.stickers.FindIndex(p => p.instanceId == id);
                    controller.Tap(index); yield return FinishMove();
                    if ((controller.Save.Data.completedTutorials & (int)lesson) != 0) break;
                }
                yield return null;
                Assert.AreNotEqual(0, controller.Save.Data.completedTutorials & (int)lesson);
                Assert.AreEqual(TutorialLesson.None, guide.ActiveLesson);
            }
            Assert.AreEqual(7, SaveService.Decode(PlayerPrefs.GetString(Key)).completedTutorials);
            balance.tutorialsEnabled = false; controller.Save.Data.completedTutorials = 0;
            controller.LoadLevel(0); yield return null; Assert.IsFalse(guide.IsVisible);
        }
        IEnumerator FinishMove()
        {
            float deadline = Time.realtimeSinceStartup + 6;
            while (controller.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(controller.IsAnimating);
        }
    }
}
