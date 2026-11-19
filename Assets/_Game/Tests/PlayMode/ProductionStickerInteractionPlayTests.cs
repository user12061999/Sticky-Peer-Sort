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
            Assert.AreEqual(120, catalog.Length); // 13/11: 64 original + 56 expansion stickers (30 categories)
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
                // Alpha accuracy is checked without the touch margin and the edge band, which deliberately reach past the art.
                float slop=balance.touchSlop, band=balance.peelEdgeBand; balance.touchSlop=0; balance.peelEdgeBand=0;
                Assert.IsFalse(view.IsRaycastLocationValid(ScreenPoint(transparent),null),definition.id+" transparent padding blocks input");
                balance.touchSlop=slop; balance.peelEdgeBand=band; yield return null;
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
        [UnityTest] public IEnumerator GrabbingJustOutsideTheEdgeAndDraggingAcrossPeelsFromThatEdge()
        {
            LoadFixture(); yield return null;
            var view=controller.ui.stickerViews[0]; var center=controller.Session.Stickers[0].position; var edge=center;
            while(BoardGeometry.Contains(controller.Session.Stickers[0],edge+Vector2.left)) edge+=Vector2.left;
            var grab=edge+Vector2.left*10;
            Assert.AreEqual(-1,BoardGeometry.Hit(controller.Session,grab),"The grab starts outside the artwork");
            var e=Press(0,grab); Assert.IsTrue(view.DragPeel.IsPeeling);
            Drag(view,e,center); Assert.Greater(view.DragPeel.Amount,.3f,"Dragging to the middle is well into the peel");
            Assert.Less(view.DragPeel.Amount,1);
            Assert.AreEqual(180,Mathf.Abs(Mathf.DeltaAngle(0,view.DragPeel.DirectionDegrees)),1,"The flap lifts on the grabbed left edge");
            Drag(view,e,center+(center-grab)); yield return Finish();
            Assert.AreEqual(StickerLocation.Collected,controller.Session.Stickers[0].location);
        }
        [UnityTest] public IEnumerator ADragFromTheMiddleOfAStickerDoesNotPeelIt()
        {
            // Owner 10/11: only a drag that starts on the rim peels; the middle only shows the guide.
            LoadFixture(); yield return null;
            var view=controller.ui.stickerViews[0]; var center=controller.Session.Stickers[0].position; string before=controller.Session.Key();
            Assert.IsFalse(BoardGeometry.NearEdge(controller.Session.Stickers[0],center,balance.peelEdgeBand));
            var e=Press(0,center); Assert.IsFalse(view.DragPeel.IsPeeling,"A press in the middle must not grab the sticker");
            Drag(view,e,center+Vector2.left*1000); Assert.IsFalse(view.DragPeel.IsPeeling,"Dragging from the middle must not start a peel");
            ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.pointerUpHandler); yield return null;
            Assert.AreNotEqual(StickerLocation.Collected,controller.Session.Stickers[0].location);
            Assert.AreEqual(before,controller.Session.Key()); Assert.IsTrue(controller.CanTap);
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
            Vector2 center, outward; Assert.IsTrue(BoardGeometry.TryEdgePoint(controller.Session,1,balance.peelEdgeBand,out center,out outward));
            var blocked=Press(1,center); Assert.IsFalse(second.DragPeel.IsPeeling,"Only one peel owns input");
            ExecuteEvents.Execute(first.gameObject,held,ExecuteEvents.pointerUpHandler);
            Assert.IsTrue(first.DragPeel.IsReturning); Assert.IsTrue(controller.CanTap,"Releasing must free input at once");
            Drag(second,blocked,center+Vector2.left*20); Assert.IsTrue(second.DragPeel.IsPeeling,"The same drag starts once input is free");
            ExecuteEvents.Execute(second.gameObject,blocked,ExecuteEvents.pointerUpHandler);
            while(first.DragPeel.IsReturning||second.DragPeel.IsReturning) yield return null;
        }
        // 17/11 drag review fix 1: a second finger on the sticker being peeled must not take the peel over.
        [UnityTest] public IEnumerator ASecondFingerOnAPeelingStickerDoesNotTakeThePeelOver()
        {
            LoadFixture(); yield return null;
            var view=controller.ui.stickerViews[0]; Vector2 start; var first=Begin(0,out start);
            Drag(view,first,start+Vector2.left*30); float amount=view.DragPeel.Amount; Assert.Greater(amount,0);
            var second=new PointerEventData(EventSystem.current){pointerId=7,button=PointerEventData.InputButton.Left,position=ScreenPoint(start)};
            ExecuteEvents.Execute(view.gameObject,second,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(view.gameObject,second,ExecuteEvents.pointerUpHandler);
            Assert.IsTrue(view.DragPeel.IsPeeling,"Lifting the second finger must not end the first finger's peel");
            Drag(view,first,start+Vector2.left*45); Assert.Greater(view.DragPeel.Amount,amount,"The first finger still drives the peel");
            ExecuteEvents.Execute(view.gameObject,first,ExecuteEvents.pointerUpHandler);
            Assert.IsFalse(view.DragPeel.IsPeeling); Assert.IsTrue(controller.CanTap,"Lifting the first finger frees the board");
            float deadline=Time.realtimeSinceStartup+2; while(view.DragPeel.IsReturning&&Time.realtimeSinceStartup<deadline) yield return null;
        }
        // Fix 2: a press that misses the artwork goes to the closest playable sticker, not the first one found on the right.
        [UnityTest] public IEnumerator APressBetweenTwoStickersGoesToTheCloserOne()
        {
            var level=LoadFixture(); yield return null;
            var a=controller.Session.Stickers[0]; var b=controller.Session.Stickers[1];
            Vector2 rightA=a.position; while(BoardGeometry.Contains(a,rightA+Vector2.right)) rightA+=Vector2.right;
            Vector2 leftB=b.position; while(BoardGeometry.Contains(b,leftB+Vector2.left)) leftB+=Vector2.left;
            float inset=b.position.x-leftB.x;
            level.stickers[1].position=new Vector2(rightA.x+30+inset,a.position.y); controller.LoadLevel(0); yield return null;
            var session=controller.Session; Assert.IsTrue(session.Legal(0)); Assert.IsTrue(session.Legal(1));
            var p=rightA+Vector2.right*8;
            Assert.AreEqual(-1,BoardGeometry.Hit(session,p),"The press is in the gap");
            Assert.AreEqual(1,BoardGeometry.Hit(session,p+Vector2.right*24),"The right neighbour is within the old first sample");
            Assert.AreEqual(0,BoardGeometry.ResolveTouch(session,p,balance.touchSlop),"8 px from sticker 0, 22 px from sticker 1");
            Assert.AreEqual(1,BoardGeometry.ResolveTouch(session,rightA+Vector2.right*24,balance.touchSlop),"Closer to sticker 1 now");
        }
        // Fix 3: a press that cannot start a peel (another sticker owns input) gets a light shake when released.
        [UnityTest] public IEnumerator ARefusedPressShakesTheStickerWhenReleased()
        {
            LoadFixture(); yield return null;
            var first=controller.ui.stickerViews[0]; var second=controller.ui.stickerViews[1];
            Vector2 start; var held=Begin(0,out start); Drag(first,held,start+Vector2.left*30);
            Vector2 edge, outward; Assert.IsTrue(BoardGeometry.TryEdgePoint(controller.Session,1,balance.peelEdgeBand,out edge,out outward));
            var refused=Press(1,edge); Assert.IsFalse(second.DragPeel.IsPeeling);
            Assert.IsFalse(second.IsShowingClickFail,"No shake while the finger is still down: the drag may still start");
            ExecuteEvents.Execute(second.gameObject,refused,ExecuteEvents.pointerUpHandler);
            Assert.IsTrue(second.IsShowingClickFail,"A press that never peeled is answered");
            Assert.IsTrue(first.DragPeel.IsPeeling,"The other peel is untouched");
            ExecuteEvents.Execute(first.gameObject,held,ExecuteEvents.pointerUpHandler);
            float deadline=Time.realtimeSinceStartup+2; while((first.DragPeel.IsReturning||second.IsShowingClickFail)&&Time.realtimeSinceStartup<deadline) yield return null;
        }
        // 17/11 partner case 3: start on the tray paper beside a sticker and slide into it; the peel starts where the finger enters.
        PointerEventData PressPaper(Vector2 local)
        {
            Canvas.ForceUpdateCanvases();
            var e=new PointerEventData(EventSystem.current){pointerId=-1,button=PointerEventData.InputButton.Left,position=ScreenPoint(local),pressPosition=ScreenPoint(local)};
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(e,hits);
            Assert.IsNotEmpty(hits,"Nothing takes a press on the tray paper");
            Assert.IsNotNull(hits[0].gameObject.GetComponent<BoardDragIn>(),"The paper press went to "+hits[0].gameObject.name);
            var go=hits[0].gameObject; e.pointerCurrentRaycast=e.pointerPressRaycast=hits[0];
            ExecuteEvents.Execute(go,e,ExecuteEvents.pointerDownHandler); e.pointerPress=go; e.pointerDrag=go;
            ExecuteEvents.Execute(go,e,ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(go,e,ExecuteEvents.beginDragHandler); e.dragging=true;
            return e;
        }
        void Slide(PointerEventData e, Vector2 local)
        { e.position=ScreenPoint(local); ExecuteEvents.Execute(e.pointerDrag,e,ExecuteEvents.dragHandler); }
        static Vector2 LeftOfArt(StickerRuntime s, float gap)
        { Vector2 p=s.position; while(BoardGeometry.Contains(s,p+Vector2.left)) p+=Vector2.left; return p+Vector2.left*gap; }
        [UnityTest] public IEnumerator ADragFromTheTrayPaperIntoAStickerPeelsItFromWhereTheFingerEnters()
        {
            LoadFixture(); yield return null;
            var session=controller.Session; var view=controller.ui.stickerViews[0]; var s=session.Stickers[0];
            float gap=Mathf.Max(60,BoardGeometry.EdgeReach(s,balance.peelEdgeBand)+20);
            Vector2 start=LeftOfArt(s,gap);
            Assert.AreEqual(-1,BoardGeometry.ResolveTouch(session,start,balance.touchSlop,balance.peelEdgeBand),"The press is beyond the touch margin and the edge band");
            var e=PressPaper(start);
            Slide(e,start+Vector2.right*(gap-10)); Assert.IsFalse(view.DragPeel.IsPeeling,"Nothing peels before the finger reaches the art");
            Slide(e,start+Vector2.right*(gap+6)); Assert.IsTrue(view.DragPeel.IsPeeling,"Entering the outline starts the peel");
            Assert.AreSame(view.gameObject,e.pointerDrag,"The rest of the press belongs to the sticker");
            Assert.Less(view.DragPeel.Amount,.1f,"The peel counts from the entry point, not from the press");
            Slide(e,start+Vector2.right*(gap+60)); Assert.Greater(view.DragPeel.Amount,.1f);
            ExecuteEvents.Execute(e.pointerPress,e,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(e.pointerDrag,e,ExecuteEvents.endDragHandler);
            Assert.IsFalse(view.DragPeel.IsPeeling); Assert.IsTrue(controller.CanTap,"Lifting the finger frees the board");
            float deadline=Time.realtimeSinceStartup+2; while(view.DragPeel.IsReturning&&Time.realtimeSinceStartup<deadline) yield return null;
        }
        // Owner 17/11: a sticker that cannot move is slid over; the drag carries on to the next playable sticker.
        [UnityTest] public IEnumerator ADragInSlidesOverABlockedStickerAndPeelsThePlayableOneBehindIt()
        {
            var level=LoadFixture();
            level.stickers[1].position=level.stickers[0].position+new Vector2(50,0); controller.LoadLevel(0); yield return null;
            var session=controller.Session; Assert.IsFalse(session.Legal(0),"Sticker 0 is covered"); Assert.IsTrue(session.Legal(1));
            float gap=Mathf.Max(60,Mathf.Max(BoardGeometry.EdgeReach(session.Stickers[0],balance.peelEdgeBand),BoardGeometry.EdgeReach(session.Stickers[1],balance.peelEdgeBand))+20);
            Vector2 start=LeftOfArt(session.Stickers[0],gap);
            Assert.AreEqual(-1,BoardGeometry.ResolveTouch(session,start,balance.touchSlop,balance.peelEdgeBand));
            var e=PressPaper(start); var covered=controller.ui.stickerViews[0]; var top=controller.ui.stickerViews[1];
            float x=6; bool overBlocked=false;
            for(;x<=300+gap&&!top.DragPeel.IsPeeling;x+=6)
            {
                Slide(e,start+Vector2.right*x);
                if(BoardGeometry.Hit(session,start+Vector2.right*x)==0) overBlocked=true;
            }
            Assert.IsTrue(overBlocked,"The finger crossed the covered sticker first");
            Assert.IsFalse(covered.DragPeel.IsPeeling,"The covered sticker never peels");
            Assert.IsTrue(top.DragPeel.IsPeeling,"The drag carries on into the playable sticker");
            Assert.AreSame(top.gameObject,e.pointerDrag);
            ExecuteEvents.Execute(e.pointerPress,e,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(e.pointerDrag,e,ExecuteEvents.endDragHandler);
            Assert.IsTrue(controller.CanTap);
            float deadline=Time.realtimeSinceStartup+2; while(top.DragPeel.IsReturning&&Time.realtimeSinceStartup<deadline) yield return null;
        }
        [UnityTest] public IEnumerator TheDragInCatcherNeverTakesPressesFromButtonsOrOffThePaper()
        {
            LoadFixture(); yield return null; Canvas.ForceUpdateCanvases();
            var ui=controller.ui; var catcher=Object.FindFirstObjectByType<BoardDragIn>(); Assert.IsNotNull(catcher);
            Assert.AreEqual(ui.board.GetSiblingIndex()-1,catcher.transform.GetSiblingIndex(),"The catcher sits right behind the stickers");
            foreach(var button in ui.GetComponentsInChildren<Button>())
            {
                if(!button.gameObject.activeInHierarchy) continue;
                var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
                var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(e,hits);
                if(hits.Count>0) Assert.IsNull(hits[0].gameObject.GetComponent<BoardDragIn>(),button.name+" lost its press to the board");
            }
            Vector3[] corners=new Vector3[4]; ui.categories.GetWorldCorners(corners);
            var card=RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);
            Assert.IsFalse(catcher.IsRaycastLocationValid(card,null),"Above the trays the catcher lets presses through");
            controller.LoadLevel(0); yield return null;
            Assert.AreEqual(1,Object.FindObjectsByType<BoardDragIn>(FindObjectsSortMode.None).Length,"Reloading keeps one catcher");
        }
        PointerEventData PressAt(Vector2 local)
        {
            Canvas.ForceUpdateCanvases();
            var e=new PointerEventData(EventSystem.current){pointerId=-1,button=PointerEventData.InputButton.Left,position=ScreenPoint(local),pressPosition=ScreenPoint(local)};
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(e,hits);
            Assert.IsNotEmpty(hits,"Nothing takes the press");
            var go=hits[0].gameObject; e.pointerCurrentRaycast=e.pointerPressRaycast=hits[0];
            var pressed=ExecuteEvents.ExecuteHierarchy(go,e,ExecuteEvents.pointerDownHandler); e.pointerPress=pressed??go;
            e.pointerDrag=ExecuteEvents.GetEventHandler<IDragHandler>(go);
            if(e.pointerDrag!=null) ExecuteEvents.Execute(e.pointerDrag,e,ExecuteEvents.initializePotentialDrag);
            if(e.pointerDrag!=null){ExecuteEvents.Execute(e.pointerDrag,e,ExecuteEvents.beginDragHandler); e.dragging=true;}
            return e;
        }
        void LiftFinger(PointerEventData e)
        {
            if(e.pointerPress!=null) ExecuteEvents.Execute(e.pointerPress,e,ExecuteEvents.pointerUpHandler);
            if(e.pointerDrag!=null) ExecuteEvents.Execute(e.pointerDrag,e,ExecuteEvents.endDragHandler);
        }
        // Owner 18/11: the edge band is measured on the drawn artwork, not on its box with the transparent margin, so a
        // press in the middle of the art, and a drag from there, never peels (it only asks for the guide).
        [UnityTest] public IEnumerator APressInTheMiddleOfTheArtDoesNotPeelEvenWhenDragged()
        {
            LoadFixture(); yield return null; yield return null;
            var session=controller.Session; var s=session.Stickers[0]; var view=controller.ui.stickerViews[0];
            Vector2 size=s.placement.definition.FitSize(s.placement.size)*s.placement.scale;
            Vector2 centre=Vector2.zero; bool found=false;
            for(float y=-.1f;y<=.1f&&!found;y+=.02f) for(float x=-.1f;x<=.1f&&!found;x+=.02f)
            {
                Vector2 p=s.position+new Vector2(x*size.x,y*size.y);
                if(BoardGeometry.Contains(s,p)&&!BoardGeometry.NearEdge(s,p,balance.peelEdgeBand)){centre=p;found=true;}
            }
            Assert.IsTrue(found,"The sticker has a middle that is not edge");
            var e=PressAt(centre); Assert.AreSame(view.gameObject,e.pointerPress,"The press belongs to the sticker");
            Assert.IsFalse(view.DragPeel.IsPeeling,"A press in the middle does not peel");
            for(int i=1;i<=10;i++) Slide(e,centre+Vector2.right*12*i);
            Assert.IsFalse(view.DragPeel.IsPeeling,"Dragging from the middle does not peel either");
            LiftFinger(e);
        }
        // Owner 18/11: a press just outside the art, within the edge band (20% of the art's smaller side), grabs the edge at
        // once, as a press on the rim does; past the band a press peels nothing until the finger slides into a sticker.
        [UnityTest] public IEnumerator APressJustOutsideTheArtWithinTheEdgeBandPeelsAtOnce()
        {
            LoadFixture(); yield return null; yield return null;
            var session=controller.Session; var s=session.Stickers[0]; var view=controller.ui.stickerViews[0];
            float reach=Mathf.Max(balance.touchSlop,BoardGeometry.EdgeReach(s,balance.peelEdgeBand));   // whichever reaches further
            Vector2 near=LeftOfArt(s,reach*.85f);
            Assert.AreEqual(0,BoardGeometry.ResolveTouch(session,near,balance.touchSlop,balance.peelEdgeBand));
            var e=PressAt(near);
            Assert.IsTrue(view.DragPeel.IsPeeling,"A press "+(reach*.85f).ToString("F0")+" units outside the art starts peeling");
            Assert.AreSame(view.gameObject,e.pointerDrag,"The rest of the press belongs to the sticker");
            Slide(e,near+Vector2.right*80); Assert.Greater(view.DragPeel.Amount,0f);
            LiftFinger(e);
            float deadline=Time.realtimeSinceStartup+2; while(view.DragPeel.IsReturning&&Time.realtimeSinceStartup<deadline) yield return null;
            var far=PressAt(LeftOfArt(s,reach+20));
            Assert.IsFalse(view.DragPeel.IsPeeling,"Past the band a press peels nothing by itself");
            LiftFinger(far);
        }
        // 17/11 partner feedback (daisy picture): beside the stem, under the petals, the artwork has a notch. Presses there,
        // in the notch or on the art next to it, used to fall in the old rule's dead middle (an ellipse in the sticker's box).
        // The edge now follows the drawn outline, so both peel.
        [UnityTest] public IEnumerator PressesBesideAndOnTheDaisyStemPeel()
        {
            LoadFixture(catalog.First(d=>d.id=="sps_plant_daisy")); yield return null;
            var session=controller.Session; var s=session.Stickers[0]; var view=controller.ui.stickerViews[0];
            Vector2 size=s.placement.definition.FitSize(s.placement.size)*s.placement.scale;
            Vector2 notch=Vector2.zero, art=Vector2.zero; bool found=false;
            for(float y=-.05f;y>-.3f&&!found;y-=.01f) for(float x=-.29f;x<0&&!found;x+=.01f)
            {
                var n=new Vector2(x/.3f,y/.3f); if(n.sqrMagnitude>=1) continue;   // inside the old dead middle only
                Vector2 p=s.position+new Vector2(x*size.x,y*size.y);
                if(BoardGeometry.Contains(s,p)||BoardGeometry.Hit(session,p)>=0) continue;
                Vector2 q=p; while(!BoardGeometry.Contains(s,q)&&q.x<s.position.x) q+=Vector2.right;
                if(!BoardGeometry.Contains(s,q)) continue;
                notch=p; art=q+Vector2.right*3; found=BoardGeometry.Contains(s,art);
            }
            Assert.IsTrue(found,"The daisy has a notch beside its stem inside the old dead middle");
            foreach(var point in new[]{notch,art})
            {
                var e=new PointerEventData(EventSystem.current){pointerId=-1,button=PointerEventData.InputButton.Left,position=ScreenPoint(point)};
                var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(e,hits);
                Assert.IsNotEmpty(hits); Assert.AreSame(view.gameObject,hits[0].gameObject,"The press beside the stem belongs to the daisy");
                ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.pointerDownHandler);
                Assert.IsTrue(view.DragPeel.IsPeeling,(point==notch?"In the notch":"On the stem side")+": the press starts peeling");
                ExecuteEvents.Execute(view.gameObject,e,ExecuteEvents.pointerUpHandler);
                float deadline=Time.realtimeSinceStartup+2; while(view.DragPeel.IsReturning&&Time.realtimeSinceStartup<deadline) yield return null;
            }
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
        [UnityTest] public IEnumerator HintBoosterShowsAHandOnTheChosenStickerUntilADragStarts()
        {
            balance.boostersEnabled = true; var tips = controller.GetComponent<GameTipsGuide>(); tips.enabled = true;
            LoadFixture(); yield return null;
            controller.ui.Notify("No verified path found. Undo or restart to try another order.");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.IsTrue(tips.ToastVisible, "Booster failure message must be visible without the legacy instruction label.");
            StringAssert.Contains("No verified path", tips.ToastMessage);
            controller.Save.Data.hint = 2; controller.Hint(); yield return null;
            int index = tips.BoosterHintIndex;
            Assert.GreaterOrEqual(index, 0, "Hint booster showed nothing.");
            Assert.IsTrue(controller.Session.Legal(index)); Assert.AreEqual(1, controller.Save.Data.hint);
            var pointer = GameObject.Find("Holding area pointer");
            Assert.IsNotNull(pointer); Assert.IsNotNull(pointer.transform.Find("Hand"));
            yield return new WaitForSecondsRealtime(4f);
            Assert.AreEqual(index, tips.BoosterHintIndex, "Hint must stay until the player acts.");
            PeelSignals.Started(index); yield return null;
            Assert.AreEqual(-1, tips.BoosterHintIndex);
        }
    }
}
