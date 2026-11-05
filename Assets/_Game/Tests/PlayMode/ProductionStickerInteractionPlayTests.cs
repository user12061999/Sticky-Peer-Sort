using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StickyPeerSort.Tests
{
    public sealed class ProductionStickerInteractionPlayTests
    {
        const string Key = "StickyPeerSort.ReferenceStyle.Save.v1";
        GameController controller;
        GameBalanceConfig balance;
        StickerDefinition[] catalog;
        bool existed;
        string previous;
        readonly List<LevelDefinition> fixtures = new List<LevelDefinition>();
        [UnitySetUp] public IEnumerator Setup()
        {
            existed = PlayerPrefs.HasKey(Key); previous = PlayerPrefs.GetString(Key, "");
            yield return GameplayTestScene.LoadReference(); yield return null;
            controller = Object.FindFirstObjectByType<GameController>(); Assert.IsNotNull(controller);
            catalog = controller.levels.SelectMany(l => l.generation.stickerPool).Where(d => d.id.StartsWith("sps_")).Distinct().OrderBy(d => d.id).ToArray();
            Assert.AreEqual(64, catalog.Length);
            balance = Object.Instantiate(controller.balance); controller.balance = balance;
            balance.tutorialsEnabled = false;
            balance.pressDuration = balance.peelDuration = balance.travelDuration = balance.stickDuration = .02f;
            balance.categoryFilledHold = balance.categoryExitDuration = balance.categoryEnterDuration = .02f;
            balance.keyFlightDuration = balance.lockOpenDuration = balance.specialRevealDuration = .02f;
            balance.dragReattachDuration = .03f;
            controller.GetComponent<GameTutorialGuide>().enabled = false;
            controller.GetComponent<GameTipsGuide>().enabled = false;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (controller != null) Object.Destroy(controller.gameObject); yield return null;
            if (balance != null) Object.Destroy(balance);
            foreach (var level in fixtures) Object.Destroy(level); fixtures.Clear();
            if (existed) PlayerPrefs.SetString(Key, previous); else PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save();
        }
        LevelDefinition LoadFixture(StickerDefinition first = null)
        {
            if (first == null) first = catalog[0];
            var group = catalog.Where(d => d.category == first.category).ToList();
            int offset = group.IndexOf(first);
            var level = ScriptableObject.CreateInstance<LevelDefinition>(); fixtures.Add(level);
            level.name = "Production artwork interaction fixture"; level.levelNumber = 1;
            level.activeCategoryCount = 1; level.bufferCapacity = 5; level.failOnOverflow = true;
            level.boardStickerSizeMultiplier = .75f;
            level.piles.Add(new BoardPileDefinition { id = "qa", size = new Vector2(890,800) });
            for (int sheet = 0; sheet < 3; sheet++)
            {
                var objective = new CategoryObjective { category = first.category, targetCount = 4 }; level.objectives.Add(objective);
                for (int slot = 0; slot < 4; slot++)
                {
                    int i = sheet*4+slot; var d = group[(offset+i)%group.Count]; objective.exactTargets.Add(d);
                    level.stickers.Add(new StickerPlacement { instanceId = "qa_"+i, definition = d, size = new Vector2(172,154), scale = .75f,
                        position = new Vector2((i%4-1.5f)*200,240-(i/4)*240), sortingOrder = i, pileId = "qa" });
                }
            }
            controller.levels[0] = level; controller.LoadLevel(0); Canvas.ForceUpdateCanvases(); return level;
        }
        Vector2 ScreenPoint(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, controller.ui.board.TransformPoint(local));
        PointerEventData Begin(int index, out Vector2 start)
        {
            Canvas.ForceUpdateCanvases(); Vector2 outward;
            Assert.IsTrue(BoardGeometry.TryEdgePoint(controller.Session,index,balance.peelEdgeBand,out start,out outward), "No grabbable edge: "+controller.Session.Stickers[index].placement.definition.id);
            var e = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left, position = ScreenPoint(start) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(e,hits);
            Assert.IsNotEmpty(hits, "No UI raycast for " + controller.Session.Stickers[index].placement.definition.id + "; depth=" + controller.ui.stickerViews[index].image.depth + "; culled=" + controller.ui.stickerViews[index].image.canvasRenderer.cull);
            Assert.AreEqual(controller.ui.stickerViews[index].gameObject,hits[0].gameObject,"Pointer hit a different UI object.");
            ExecuteEvents.Execute(hits[0].gameObject,e,ExecuteEvents.pointerDownHandler);
            Assert.IsTrue(controller.ui.stickerViews[index].DragPeel.IsPeeling); return e;
        }
        void Drag(StickerView view, PointerEventData e, Vector2 point)
        { e.position = ScreenPoint(point); ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.dragHandler); }
        IEnumerator Finish()
        {
            float deadline = Time.realtimeSinceStartup+6;
            while (controller.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsFalse(controller.IsAnimating); yield return null;
        }
        IEnumerator Collect(int index)
        {
            var view = controller.ui.stickerViews[index]; Vector2 start;
            var e = Begin(index,out start); Drag(view,e,start+Vector2.left*1000);
            var flight = controller.ui.effects.Find("Flying sticker " + index);
            Assert.IsNotNull(flight); Assert.IsFalse(flight.GetComponent<Image>().maskable);
            var boardCanvas = controller.ui.board.GetComponent<Canvas>();
            if (boardCanvas != null) Assert.Greater(flight.GetComponent<Canvas>().sortingOrder, boardCanvas.sortingOrder);
            yield return Finish();
        }
        [UnityTest] public IEnumerator All64HaveAlphaAccurateEdgeInputCancelAndExactArtworkLanding()
        {
            Assert.IsInstanceOf<StandaloneInputModule>(EventSystem.current.currentInputModule);
            foreach (var definition in catalog)
            {
                LoadFixture(definition); yield return null;
                Assert.IsTrue(definition.HasAlphaMask,definition.id); Assert.IsFalse(definition.ContainsUV(new Vector2(.01f,.01f)));
                var view = controller.ui.stickerViews[0]; Assert.AreSame(definition.ArtworkSprite,view.image.sprite);
                Vector2 transparent = controller.Session.Stickers[0].position + new Vector2(-.49f,-.49f)*154*.75f;
                float slop=balance.touchSlop; balance.touchSlop=0;
                Assert.IsFalse(view.IsRaycastLocationValid(ScreenPoint(transparent),null),definition.id+" transparent padding blocks input");
                balance.touchSlop=slop; yield return null;
                string before = controller.Session.Key(); Vector2 start; var e = Begin(0,out start);
                Drag(view,e,start+Vector2.left*30); Assert.Greater(view.DragPeel.Amount,0); Assert.Less(view.DragPeel.Amount,1);
                Assert.AreEqual(before,controller.Session.Key());
                Drag(view,e,start); Assert.AreEqual(0,view.DragPeel.Amount,.001f);
                Drag(view,e,start+Vector2.left*30); ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.pointerUpHandler);
                float deadline = Time.realtimeSinceStartup+2;
                while(view.DragPeel.IsReturning && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.IsFalse(view.DragPeel.IsReturning); Assert.AreEqual(before,controller.Session.Key());
                Assert.AreEqual(1,view.GetComponent<CanvasGroup>().alpha); Assert.IsTrue(controller.CanTap);
                yield return Collect(0);
                Assert.AreEqual(1,controller.ui.categoryViews[0].FilledCount,definition.id);
                Assert.AreSame(definition.ArtworkSprite,controller.ui.categoryViews[0].targets[0].sprite);
                Assert.AreEqual(StickerLocation.Collected,controller.Session.Stickers[0].location); Assert.IsEmpty(controller.Session.Buffer);
            }
        }
        PointerEventData Press(int index, Vector2 local)
        {
            Canvas.ForceUpdateCanvases();
            var e = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left, position = ScreenPoint(local) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(e,hits);
            Assert.IsNotEmpty(hits,"Press found no sticker");
            Assert.AreEqual(controller.ui.stickerViews[index].gameObject,hits[0].gameObject,"Press selected the wrong object");
            ExecuteEvents.Execute(hits[0].gameObject,e,ExecuteEvents.pointerDownHandler); return e;
        }
        [UnityTest] public IEnumerator ADragFromTheMiddleOfAStickerPeelsIt()
        {
            LoadFixture(); yield return null;
            var view=controller.ui.stickerViews[0]; var center=controller.Session.Stickers[0].position;
            var e=Press(0,center); Assert.IsTrue(view.DragPeel.IsPeeling);
            Drag(view,e,center+Vector2.left*1000); yield return Finish();
            Assert.AreEqual(StickerLocation.Collected,controller.Session.Stickers[0].location);
        }
        [UnityTest] public IEnumerator APressJustOutsideTheArtworkStillPicksTheStickerButAFarOneDoesNot()
        {
            LoadFixture(); yield return null;
            var center=controller.Session.Stickers[0].position; var edge=center;
            while(BoardGeometry.Contains(controller.Session.Stickers[0],edge+Vector2.left)) edge+=Vector2.left;
            var near=edge+Vector2.left*(balance.touchSlop*.4f);
            Assert.AreEqual(-1,BoardGeometry.Hit(controller.Session,near));
            var e=Press(0,near); Assert.IsTrue(controller.ui.stickerViews[0].DragPeel.IsPeeling);
            ExecuteEvents.Execute(controller.ui.stickerViews[0].gameObject,e,ExecuteEvents.pointerUpHandler);
            Assert.AreEqual(-1,BoardGeometry.ResolveTouch(controller.Session,edge+Vector2.left*(balance.touchSlop*3),balance.touchSlop));
            yield return null;
        }
        [UnityTest] public IEnumerator APlayableStickerUnderTheFingerIsNeverLostToANeighbour()
        {
            var level=LoadFixture();
            level.stickers[1].position=level.stickers[0].position+Vector2.right*60; controller.LoadLevel(0); yield return null;
            var session=controller.Session;
            Assert.IsTrue(session.Legal(1));
            var onTop=session.Stickers[1].position;
            Assert.AreEqual(1,BoardGeometry.ResolveTouch(session,onTop,balance.touchSlop));
            Assert.AreEqual(1,BoardGeometry.Hit(session,onTop));
        }
        [UnityTest] public IEnumerator ReleasingFreesTheBoardAtOnceAndABlockedPressStartsWithTheSameDrag()
        {
            LoadFixture(); yield return null;
            var first=controller.ui.stickerViews[0]; var second=controller.ui.stickerViews[1];
            Vector2 start; var held=Begin(0,out start);
            Drag(first,held,start+Vector2.left*30);
            var center=controller.Session.Stickers[1].position;
            var blocked=Press(1,center); Assert.IsFalse(second.DragPeel.IsPeeling,"Only one peel owns input");
            ExecuteEvents.Execute(first.gameObject,held,ExecuteEvents.pointerUpHandler);
            Assert.IsTrue(first.DragPeel.IsReturning); Assert.IsTrue(controller.CanTap,"Releasing must free input at once");
            Drag(second,blocked,center+Vector2.left*20); Assert.IsTrue(second.DragPeel.IsPeeling,"The same drag starts once input is free");
            ExecuteEvents.Execute(second.gameObject,blocked,ExecuteEvents.pointerUpHandler);
            while(first.DragPeel.IsReturning||second.DragPeel.IsReturning) yield return null;
        }
        [UnityTest] public IEnumerator SameCategoryWrongDesignGoesToBufferAndFiveOccupiedSlotsLose()
        {
            var fixture=LoadFixture();
            // One design requested four times; every later sticker is another design of the category.
            var other=catalog.First(d=>d.category==fixture.stickers[0].definition.category&&d!=fixture.stickers[0].definition);
            for(int i=0;i<4;i++) fixture.objectives[0].exactTargets[i]=fixture.stickers[0].definition;
            for(int i=4;i<12;i++) fixture.stickers[i].definition=other;
            controller.LoadLevel(0); yield return null;
            Assert.AreSame(controller.Session.Stickers[0].placement.definition.category,controller.Session.Stickers[4].placement.definition.category);
            Assert.IsFalse(controller.Session.HasTarget(4),"Category alone must not be sufficient.");
            for(int i=4;i<=8;i++)
            {
                var definition=controller.Session.Stickers[i].placement.definition;
                yield return Collect(i);
                Assert.AreSame(definition.ArtworkSprite,controller.ui.waitingViews[i].sprite);
                Assert.AreEqual(i-3,controller.Session.Buffer.Count);
                Assert.AreEqual(i==8 ? GameplayState.Fail : GameplayState.Playing,controller.Session.State);
            }
            Assert.AreEqual(5,controller.ui.waitingViews.Count); Assert.IsTrue(controller.ui.HasModal);
            controller.Undo(); yield return null;
            Assert.AreEqual(GameplayState.Playing,controller.Session.State); Assert.AreEqual(4,controller.Session.Buffer.Count);
        }
        [UnityTest] public IEnumerator CoveredMysteryCannotPeelAndRevealsOnlyAfterCoverLeaves()
        {
            var level=LoadFixture();
            level.stickers[0].special.kind=SpecialKind.Mystery;
            level.stickers[1].position=level.stickers[0].position; controller.LoadLevel(0); yield return null;
            var hidden=controller.ui.stickerViews[0];
            Assert.IsTrue(BoardGeometry.Covered(controller.Session,0)); Assert.IsFalse(controller.Session.Legal(0));
            Assert.AreEqual(Color.black,hidden.image.color); Assert.AreEqual("?",hidden.title.text); Assert.IsTrue(hidden.IsCoveredShadeVisible);
            string before=controller.Session.Key(); controller.Tap(0); yield return null;
            Assert.AreEqual(before,controller.Session.Key()); Assert.IsTrue(hidden.IsShowingClickFail);
            Assert.AreEqual(Color.black,hidden.image.color);
            yield return new WaitForSecondsRealtime(balance.clickFailDuration+.05f);
            yield return Collect(1);
            Assert.IsTrue(controller.Session.Stickers[0].revealed); Assert.IsFalse(hidden.IsCoveredShadeVisible);
            Assert.AreSame(level.stickers[0].definition.ArtworkSprite,hidden.image.sprite); Assert.AreNotEqual("?",hidden.title.text);
        }
        [UnityTest] public IEnumerator KeySheetUnlocksChainedArtworkAndDragStaysAboveMaskedBoard()
        {
            var level=LoadFixture(); level.stickers[4].special=new StickerSpecialConfig { kind=SpecialKind.Locked,unlockCondition=UnlockCondition.CategoryCompleted,dependencyId=level.objectives[0].category.id };
            controller.LoadLevel(0); yield return null;
            var locked=controller.ui.stickerViews[4]; Assert.IsTrue(locked.HasLockChains); Assert.IsFalse(controller.Session.Legal(4));
            string before=controller.Session.Key(); controller.Tap(4); Assert.AreEqual(before,controller.Session.Key());
            var canvas=controller.ui.board.gameObject.AddComponent<Canvas>(); canvas.overrideSorting=true; canvas.sortingOrder=1200;
            controller.ui.board.gameObject.AddComponent<GraphicRaycaster>();
            controller.ui.board.gameObject.AddComponent<RectMask2D>();
            yield return null; // A newly nested canvas needs its first render before Graphic.depth is valid.
            var view=controller.ui.stickerViews[0]; Vector2 start; var e=Begin(0,out start);
            Drag(view,e,start+Vector2.left*30);
            var ghost=controller.ui.effects.Find("Interactive peel 0"); Assert.IsNotNull(ghost);
            Assert.Greater(ghost.GetComponent<Canvas>().sortingOrder,canvas.sortingOrder); Assert.IsFalse(ghost.GetComponent<Image>().maskable);
            Assert.Greater(ghost.GetComponent<StickerCurlMesh>().Amount, 0);
            ScreenCapture.CaptureScreenshot("Temp/ProductionPeelPartial.png"); yield return null;
            ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.pointerUpHandler);
            Assert.Greater(ghost.GetComponent<Canvas>().sortingOrder,canvas.sortingOrder);
            while(view.DragPeel.IsReturning) yield return null;
            for(int i=0;i<4;i++) yield return Collect(i);
            Assert.IsFalse(locked.HasLockChains); Assert.IsFalse(locked.LockIconRect.gameObject.activeSelf);
            Assert.IsTrue(controller.Session.Legal(4)); Assert.AreSame(level.stickers[4].definition.ArtworkSprite,locked.image.sprite);
            yield return Collect(4); Assert.AreEqual(1,controller.ui.categoryViews[1].FilledCount);
        }
    }
}
