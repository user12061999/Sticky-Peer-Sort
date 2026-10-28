using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StickyPeerSort.Tests
{
    public sealed class GameplaySmokeTests
    {
        GameController controller;
        GameBalanceConfig original, fast;
        bool hadSave;
        string previousSave;
        readonly List<LevelDefinition> fixtures = new List<LevelDefinition>();
        readonly List<Object> artworkObjects = new List<Object>();
        const string SaveKey = "StickyPeerSort.Save.v1";
        [UnitySetUp] public IEnumerator OpenScene()
        {
            hadSave = PlayerPrefs.HasKey(SaveKey); previousSave = PlayerPrefs.GetString(SaveKey); PlayerPrefs.DeleteKey(SaveKey);
            Application.runInBackground = true;
            yield return SceneManager.LoadSceneAsync(0); yield return null;
            controller = Object.FindFirstObjectByType<GameController>(); Assert.IsNotNull(controller);
            if (controller.ui.modal.activeSelf) controller.ui.primary.onClick.Invoke();
            original = controller.balance; fast = Object.Instantiate(original); controller.balance = fast;
            fast.pressDuration = fast.peelDuration = fast.travelDuration = fast.categoryDuration = .001f;
            fast.categoryFilledHold = fast.categoryExitDuration = fast.categoryEnterDuration = .001f;
            fast.keyFlightDuration = fast.lockOpenDuration = fast.specialRevealDuration = .001f;
            fast.stickDuration = .001f;
            fast.backingPaper = false; // Most fixtures isolate move sequencing; dedicated paper tests enable it.
            Assert.AreEqual(GameplayState.Playing, controller.Session.State);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (controller != null) { controller.balance = original; Object.Destroy(controller.gameObject); }
            yield return null; // Dispose and flush the test controller before restoring the user's save.
            if (fast != null) Object.Destroy(fast);
            foreach (var fixture in fixtures) Object.Destroy(fixture); fixtures.Clear();
            for (int i = artworkObjects.Count - 1; i >= 0; i--) Object.Destroy(artworkObjects[i]); artworkObjects.Clear();
            if (hadSave) PlayerPrefs.SetString(SaveKey, previousSave); else PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); yield return null;
        }
        IEnumerator WaitForAnimation()
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (controller.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(controller.IsAnimating, "Presentation did not finish.");
        }
        [UnityTest] public IEnumerator PointerRaycastCollectsAndRejectsDoubleTap()
        {
            Canvas.ForceUpdateCanvases(); var view = controller.ui.stickerViews[0];
            Vector2 local = VisiblePoint(0);
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, controller.ui.board.TransformPoint(local)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits); Assert.AreEqual(view.gameObject, hits[0].gameObject);
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            Assert.AreEqual(0, controller.Session.Categories[0].Count, "Pointer-down must not select before the full peel.");
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, controller.ui.board.TransformPoint(local + Vector2.left * 1000));
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.dragHandler);
            controller.Tap(0); Assert.AreEqual(1, controller.Session.Categories[0].Count);
            yield return WaitForAnimation(); Assert.AreEqual(7, controller.ui.stickerViews.Count);
        }
        [UnityTest] public IEnumerator BoardHasNoDestinationBadgesAndSpecialsStayConcealed()
        {
            LoadFlatLevel(0); var level = controller.levels[0]; level.activeCategoryCount = 1; level.bufferCapacity = 3;
            level.stickers[1].position = level.stickers[0].position + new Vector2(40, 0); level.stickers[1].sortingOrder = -1;
            level.stickers[6].special.kind = SpecialKind.Mystery;
            level.stickers[6].position = level.stickers[5].position + new Vector2(-60, 0); level.stickers[6].sortingOrder = -2;
            level.stickers[7].special = new StickerSpecialConfig { kind = SpecialKind.Locked, dependencyId = level.stickers[0].instanceId };
            controller.LoadLevel(0); yield return null; Canvas.ForceUpdateCanvases();
            var views = controller.ui.stickerViews;
            Assert.AreEqual(StickerBoardState.Available, views[0].BoardState);
            Assert.AreEqual(StickerBoardState.Blocked, views[1].BoardState);
            Assert.Less(views[1].image.color.grayscale, views[0].image.color.grayscale);
            Assert.AreEqual(StickerBoardState.Available, views[4].BoardState);
            foreach (int i in new[] { 0, 4 }) Assert.IsNull(views[i].GetComponentInChildren<StickerBoardAdornment>(true), "Ordinary stickers must not allocate a check/clock graphic.");
            Assert.AreEqual(StickerBoardState.Mystery, views[6].BoardState); Assert.AreEqual(Color.black, views[6].image.color);
            Assert.GreaterOrEqual(views[6].title.fontSize, Mathf.Min(level.stickers[6].size.x, level.stickers[6].size.y) * .7f);
            Assert.IsTrue(views[7].HasLockChains); Assert.IsTrue(views[7].LockIconRect.gameObject.activeSelf);
            foreach (int i in new[] { 7 })
            {
                var graphic = views[i].GetComponentInChildren<StickerBoardAdornment>();
                Assert.IsNotNull(graphic); Assert.IsFalse(graphic.raycastTarget);
                var mesh = graphic.canvasRenderer.GetMesh();
                Assert.IsNotNull(mesh);
                Assert.Greater(mesh.vertexCount, 0, "Lock chains need a rendered mesh.");
            }
            controller.Tap(6); yield return null; Assert.AreEqual(Color.black, views[6].image.color, "Rejected mystery taps must not expose its texture.");
            controller.Tap(0); yield return WaitForAnimation();
            Assert.AreEqual(StickerBoardState.Available, controller.ui.stickerViews[1].BoardState);
            Assert.IsFalse(controller.ui.stickerViews[7].HasLockChains);
            Assert.IsFalse(controller.ui.stickerViews[7].LockIconRect.gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator TargetHighlightSurvivesBadgeRemovalForWaitingAndGallery()
        {
            LoadFlatLevel(0); controller.levels[0].activeCategoryCount = 1; controller.LoadLevel(0);
            var view = controller.ui.stickerViews[4];
            Assert.AreEqual(StickerBoardState.Available, view.BoardState); Assert.IsFalse(controller.Session.HasTarget(4));
            var waitingTarget = controller.ui.buffer.GetChild(0).GetComponent<UnityEngine.UI.Image>();
            Color waitingColor = waitingTarget.color; Vector2 start; var pointer = BeginPeel(4, out start);
            Assert.AreEqual(waitingColor, waitingTarget.color, "The destination artwork must keep its original tint.");
            Assert.IsNotNull(waitingTarget.GetComponentInChildren<DestinationPreviewGlow>());
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerUpHandler); Assert.AreEqual(waitingColor, waitingTarget.color);
            while (view.DragPeel.IsReturning) yield return null;
            foreach (int i in new[] { 0, 1, 2, 3 }) { controller.Tap(i); yield return WaitForAnimation(); }
            int category, slot; Assert.IsTrue(controller.Session.TryGetTarget(4, out category, out slot));
            var target = controller.ui.categoryViews[category].targets[slot]; Color targetColor = target.color;
            pointer = BeginPeel(4, out start); Assert.AreEqual(targetColor, target.color);
            var glow = target.GetComponentInChildren<DestinationPreviewGlow>();
            Assert.IsNotNull(glow, "A sticker contour should preview the target without filling it.");
            Assert.Greater(glow.OutlineImage.color.g, glow.OutlineImage.color.r);
            Assert.Greater(glow.OutlineImage.color.g, glow.OutlineImage.color.b);
            Assert.IsFalse(glow.OutlineImage.raycastTarget);
            var contour = glow.OutlineImage.sprite.texture;
            Assert.AreEqual(0, contour.GetPixel(0, 0).a, "No rectangular corner is allowed.");
            Assert.AreEqual(0, contour.GetPixel(contour.width - 1, contour.height - 1).a);
            bool outlineFound = false;
            var definition = controller.Session.Stickers[4].placement.definition;
            for (int y = 0; y < contour.height; y += 3) for (int x = 0; x < contour.width; x += 3)
            {
                float alpha = contour.GetPixel(x, y).a; outlineFound |= alpha > 0;
                int padding = DestinationPreviewGlow.ContourPadding;
                var uv = new Vector2((x - padding + .5f) / (contour.width - padding * 2), (y - padding + .5f) / (contour.height - padding * 2));
                if (definition.ContainsUV(uv)) Assert.AreEqual(0, alpha, "Highlight must not cover the sticker's opaque artwork.");
            }
            Assert.IsTrue(outlineFound);
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerUpHandler); Assert.AreEqual(targetColor, target.color);
            Assert.AreEqual(StickerBoardState.Available, view.BoardState);
            Assert.IsNull(view.GetComponentInChildren<StickerBoardAdornment>(true));
        }
        PointerEventData BeginPeel(int index, out Vector2 start)
        {
            Canvas.ForceUpdateCanvases(); start = VisiblePoint(index);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, controller.ui.board.TransformPoint(start)) };
            ExecuteEvents.Execute(controller.ui.stickerViews[index].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            return pointer;
        }
        void MovePeel(StickerView view, PointerEventData pointer, Vector2 start, float amount, Vector2? direction = null)
        {
            var p = controller.Session.Stickers[controller.Session.Level.stickers.FindIndex(s => s.instanceId == view.name.Substring("Sticker ".Length))].placement;
            float distance = Mathf.Min(p.definition.FitSize(p.size).x, p.definition.FitSize(p.size).y) * p.scale * fast.dragPeelDistanceRatio;
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, controller.ui.board.TransformPoint(start + (direction ?? Vector2.left) * distance * amount));
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.dragHandler);
        }
        [UnityTest] public IEnumerator PartialDragBackAndEarlyReleaseReattachWithoutChangingGameState()
        {
            LoadFlatLevel(0); var view = controller.ui.stickerViews[0]; string before = controller.Session.Key(); Vector2 start;
            var pointer = BeginPeel(0, out start); Assert.IsTrue(view.DragPeel.IsPeeling); Assert.IsFalse(controller.CanTap);
            MovePeel(view, pointer, start, .45f); Assert.AreEqual(.45f, view.DragPeel.Amount, .001f);
            MovePeel(view, pointer, start, .1f); Assert.AreEqual(.1f, view.DragPeel.Amount, .001f);
            MovePeel(view, pointer, start, 0); ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            while (view.DragPeel.IsReturning) yield return null;
            Assert.IsTrue(controller.CanTap); Assert.AreEqual(before, controller.Session.Key());
            Assert.AreEqual(1, view.GetComponent<CanvasGroup>().alpha); Assert.IsFalse(controller.Session.CanUndo);
            pointer = BeginPeel(0, out start); MovePeel(view, pointer, start, .55f);
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.IsTrue(view.DragPeel.IsReturning); Assert.IsFalse(controller.CanTap);
            float deadline = Time.realtimeSinceStartup + 2;
            while (view.DragPeel.IsReturning && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.AreEqual(before, controller.Session.Key()); Assert.IsTrue(controller.CanTap); Assert.IsEmpty(controller.Session.Buffer);
            Assert.AreEqual(0, controller.ui.effects.childCount); Assert.AreEqual(0, view.DragPeel.Amount);
        }
        [UnityTest] public IEnumerator FullDragHandsFoldToGalleryFlightOnceAndThenCanBeUndone()
        {
            LoadFlatLevel(0); fast.peelDuration = .4f; fast.travelDuration = .25f;
            var view = controller.ui.stickerViews[0]; Vector2 start; var pointer = BeginPeel(0, out start);
            MovePeel(view, pointer, start, .5f); Assert.AreEqual(0, controller.Session.Categories[0].Count);
            MovePeel(view, pointer, start, 1.1f);
            Assert.AreEqual(1, controller.Session.Categories[0].Count); Assert.IsFalse(controller.ui.stickerViews.ContainsKey(0));
            var flying = controller.ui.effects.Find("Flying sticker 0"); Assert.IsNotNull(flying);
            Assert.IsTrue(flying.GetComponent<Canvas>().overrideSorting);
            Assert.IsFalse(flying.GetComponent<UnityEngine.UI.Image>().maskable);
            Assert.AreEqual(1, flying.GetComponent<StickerCurlMesh>().Amount, .001f, "A completed drag must not restart the peel animation.");
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            yield return WaitForAnimation(); Assert.AreEqual(1, controller.ui.categoryViews[0].FilledCount);
            controller.Undo(); Assert.IsTrue(controller.ui.stickerViews.ContainsKey(0)); Assert.AreEqual(0, controller.Session.Categories[0].Count);
        }
        [UnityTest] public IEnumerator FullDragWithoutMatchingSheetLandsInWaitingArea()
        {
            LoadFlatLevel(0, 1); var view = controller.ui.stickerViews[4]; Vector2 start; var pointer = BeginPeel(4, out start);
            Assert.AreEqual(StickerBoardState.Available, view.BoardState); Assert.IsFalse(controller.Session.HasTarget(4));
            MovePeel(view, pointer, start, .5f, Vector2.up); Assert.AreEqual(-90, view.DragPeel.DirectionDegrees, .001f);
            Assert.IsEmpty(controller.Session.Buffer);
            MovePeel(view, pointer, start, 1.1f, Vector2.up);
            Assert.AreEqual(1, controller.Session.Buffer.Count);
            yield return WaitForAnimation(); Assert.IsTrue(controller.ui.waitingViews.ContainsKey(4));
            Assert.AreEqual(0, controller.Session.Categories[0].Count);
        }
        [UnityTest] public IEnumerator PartialDragKeepsCoversAndRejectsOtherPointersThenRestartCancelsIt()
        {
            LoadFlatLevel(0); var level = controller.levels[0];
            level.stickers[1].position = level.stickers[0].position + new Vector2(40, 0); level.stickers[1].sortingOrder = -1;
            controller.LoadLevel(0); var view = controller.ui.stickerViews[0]; Vector2 start; var pointer = BeginPeel(0, out start);
            MovePeel(view, pointer, start, .5f); string before = controller.Session.Key();
            Assert.IsTrue(BoardGeometry.Covered(controller.Session, 1)); Assert.IsTrue(controller.ui.stickerViews[1].IsCoveredShadeVisible);
            controller.Tap(2); Assert.AreEqual(before, controller.Session.Key());
            int undo = controller.Save.Data.undo; controller.Undo(); Assert.AreEqual(undo, controller.Save.Data.undo);
            pointer.pointerId = 9; MovePeel(view, pointer, start, 1.5f); Assert.AreEqual(.5f, view.DragPeel.Amount, .001f);
            controller.LoadLevel(0); yield return null;
            Assert.IsTrue(controller.CanTap); Assert.AreEqual(0, controller.ui.effects.childCount); Assert.IsTrue(BoardGeometry.Covered(controller.Session, 1));
            view = controller.ui.stickerViews[0]; pointer = BeginPeel(0, out start); MovePeel(view, pointer, start, .4f);
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.cancelHandler);
            while (view.DragPeel.IsReturning) yield return null;
            yield return null;
            Assert.IsTrue(controller.CanTap); Assert.AreEqual(0, controller.ui.effects.childCount);
        }
        [UnityTest] public IEnumerator PeeledStickerAndReturnStayAboveSortedMaskedBoardUntilFlat()
        {
            LoadFlatLevel(0); var canvas = controller.ui.board.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 80;
            controller.ui.board.localScale = Vector3.one * 1.15f;
            controller.ui.board.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var view = controller.ui.stickerViews[0]; Vector2 start; var pointer = BeginPeel(0, out start);
            MovePeel(view, pointer, start, .65f);
            var ghost = controller.ui.effects.Find("Interactive peel 0"); Assert.IsNotNull(ghost);
            Assert.AreEqual(view.Rect.rect.width * view.Rect.lossyScale.x, ((RectTransform)ghost).rect.width * ghost.lossyScale.x, .01f);
            Assert.Greater(ghost.GetComponent<Canvas>().sortingOrder, canvas.sortingOrder);
            Assert.IsFalse(ghost.GetComponent<UnityEngine.UI.Image>().maskable);
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.IsTrue(view.DragPeel.IsReturning); Assert.AreEqual(0, view.GetComponent<CanvasGroup>().alpha);
            Assert.Greater(ghost.GetComponent<Canvas>().sortingOrder, canvas.sortingOrder);
            while (view.DragPeel.IsReturning) yield return null;
            Assert.AreEqual(1, view.GetComponent<CanvasGroup>().alpha); Assert.AreEqual(0, controller.Session.Categories[0].Count);
        }
        [UnityTest] public IEnumerator BackingPaperShowsBelowPartialPeelAndCancelsWithoutDropping()
        {
            LoadFlatLevel(0); fast.backingPaper = true;
            var view = controller.ui.stickerViews[0]; string before = controller.Session.Key(); Vector2 start;
            var pointer = BeginPeel(0, out start); MovePeel(view, pointer, start, .55f);
            var paper = controller.ui.effects.Find("Sticker backing paper 0").GetComponent<StickerBackingPaper>();
            Assert.IsFalse(paper.IsFalling);
            Assert.AreEqual(fast.backingPaperOpacity, paper.GetComponent<CanvasGroup>().alpha, .001f);
            Assert.Less(paper.GetComponent<CanvasGroup>().alpha, 1);
            Assert.AreSame(view.image.sprite, paper.GetComponent<UnityEngine.UI.Image>().sprite, "The paper must use the sticker's own alpha silhouette.");
            Assert.AreEqual(view.Rect.rect.width * view.Rect.lossyScale.x, ((RectTransform)paper.transform).rect.width * paper.transform.lossyScale.x, .01f);
            Assert.Less(paper.GetComponent<Canvas>().sortingOrder, controller.ui.effects.Find("Interactive peel 0").GetComponent<Canvas>().sortingOrder);
            foreach (var graphic in paper.GetComponentsInChildren<UnityEngine.UI.Graphic>()) Assert.IsFalse(graphic.raycastTarget);
            Assert.AreEqual(before, controller.Session.Key());
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            while (view.DragPeel.IsReturning) yield return null;
            yield return null;
            Assert.IsTrue(paper == null); Assert.AreEqual(before, controller.Session.Key()); Assert.IsTrue(controller.CanTap);
        }
        [UnityTest] public IEnumerator ReleasedBackingFallsWithoutBlockingCollectionThenExpiresAndResets()
        {
            LoadFlatLevel(0, 1); fast.backingPaper = true; fast.backingPaperFallDuration = .8f; fast.backingPaperFallDistance = 180;
            var view = controller.ui.stickerViews[4]; Vector2 start; var pointer = BeginPeel(4, out start);
            MovePeel(view, pointer, start, .5f);
            var paper = controller.ui.effects.Find("Sticker backing paper 4").GetComponent<StickerBackingPaper>();
            Vector3 home = paper.transform.localPosition; MovePeel(view, pointer, start, 1.1f);
            Assert.IsTrue(paper.IsFalling); Assert.AreEqual(1, controller.Session.Buffer.Count);
            yield return WaitForAnimation();
            Assert.IsTrue(controller.ui.waitingViews.ContainsKey(4)); Assert.IsTrue(controller.CanTap);
            string after = controller.Session.Key(); yield return new WaitForSecondsRealtime(.18f);
            Assert.Less(paper.transform.localPosition.y, home.y); Assert.AreEqual(after, controller.Session.Key());
            ScreenCapture.CaptureScreenshot("Temp/BackingPaperFallTest.png"); yield return null;
            float deadline = Time.realtimeSinceStartup + 2;
            while (paper != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(paper == null); Assert.IsTrue(controller.CanTap);
            view = controller.ui.stickerViews[0]; pointer = BeginPeel(0, out start); MovePeel(view, pointer, start, 1.1f);
            Assert.IsNotEmpty(controller.ui.effects.GetComponentsInChildren<StickerBackingPaper>());
            controller.LoadLevel(0); yield return null;
            Assert.IsEmpty(controller.ui.effects.GetComponentsInChildren<StickerBackingPaper>(true)); Assert.IsTrue(controller.CanTap);
        }
        sealed class LandingFeedback : IGameFeedback
        {
            public int landings;
            public GameController controller;
            public System.Action onLanding;
            public void Play(PuzzleEventKind kind)
            {
                if (kind != PuzzleEventKind.Collected) return;
                Assert.AreEqual(1, controller.ui.categoryViews[0].FilledCount, "Landing sound must follow the visible slot commit."); landings++; onLanding?.Invoke();
            }
            public void Haptic() { }
        }
        [UnityTest] public IEnumerator KeyCardClearsBeforeLockedStickerOpens()
        {
            LoadFlatLevel(5);
            var special = controller.levels[5].stickers[7].special;
            Assert.AreEqual(UnlockCondition.CategoryCompleted, special.unlockCondition);
            Assert.IsNotNull(controller.ui.categoryViews[0].rect.Find("Unlock key"));
            controller.Tap(0); yield return WaitForAnimation(); Assert.IsFalse(controller.Session.Legal(7));
            foreach (int index in new[] { 1, 2, 3 }) { controller.Tap(index); yield return WaitForAnimation(); }
            Assert.IsTrue(controller.Session.Legal(7)); Assert.IsFalse(controller.ui.stickerViews[7].transform.Find("Lock icon").gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator KeyFliesFromFilledCardBeforeExitAndRestartCancelsEffects()
        {
            LoadFlatLevel(5); fast.keyFlightDuration = .3f; fast.lockOpenDuration = .15f;
            foreach (int index in new[] { 0, 1, 2 }) { controller.Tap(index); yield return WaitForAnimation(); }
            var locked = controller.ui.stickerViews[7]; var hiddenFace = locked.image.sprite;
            controller.Tap(3);
            float deadline = Time.realtimeSinceStartup + 3;
            Transform flying = null;
            while (flying == null && Time.realtimeSinceStartup < deadline)
            { flying = controller.ui.effects.Find("Flying unlock key 7"); yield return null; }
            Assert.IsNotNull(flying); Assert.IsFalse(controller.CanTap);
            Assert.AreEqual(4, controller.ui.categoryViews[0].FilledCount);
            Assert.IsTrue(locked.LockIconRect.gameObject.activeSelf);
            Assert.AreSame(hiddenFace, locked.image.sprite);
            Assert.IsFalse(flying.GetComponent<UnityEngine.UI.Image>().raycastTarget);
            string before = controller.Session.Key(); controller.Tap(7); Assert.AreEqual(before, controller.Session.Key());
            yield return WaitForAnimation();
            Assert.IsFalse(controller.ui.categoryViews.ContainsKey(0));
            Assert.IsFalse(locked.LockIconRect.gameObject.activeSelf);
            Assert.AreSame(controller.Session.Stickers[7].placement.definition.ArtworkSprite, locked.image.sprite);
            Assert.AreEqual(Vector3.one * controller.Session.Stickers[7].placement.scale, locked.Rect.localScale);
            controller.LoadLevel(5);
            foreach (int index in new[] { 0, 1, 2 }) { controller.Tap(index); yield return WaitForAnimation(); }
            controller.Tap(3); yield return null; controller.LoadLevel(5); yield return null;
            Assert.AreEqual(0, controller.ui.effects.childCount);
            Assert.IsTrue(controller.ui.stickerViews[7].LockIconRect.gameObject.activeSelf);
            Assert.IsTrue(controller.CanTap);
        }
        [UnityTest] public IEnumerator MysteryFlipsHiddenFaceBeforeRevealingArtworkAndUndoRestoresIt()
        {
            LoadFlatLevel(0); var level = controller.levels[0];
            var mystery = level.stickers[4]; mystery.special.kind = SpecialKind.Mystery;
            mystery.position = level.stickers[0].position + new Vector2(-60, 0);
            mystery.sortingOrder = -100; controller.LoadLevel(0);
            var view = controller.ui.stickerViews[4]; Assert.AreEqual("?", view.title.text);
            var hiddenFace = view.image.sprite; Vector3 home = view.Rect.localScale;
            fast.specialRevealDuration = .4f; controller.Tap(0);
            Assert.IsFalse(controller.CanTap);
            float deadline = Time.realtimeSinceStartup + 3;
            while (view.Rect.localScale.x >= home.x * .9f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Less(view.Rect.localScale.x, home.x * .9f);
            Assert.AreEqual("?", view.title.text); Assert.AreSame(hiddenFace, view.image.sprite);
            yield return WaitForAnimation();
            Assert.AreSame(mystery.definition.ArtworkSprite, view.image.sprite);
            Assert.AreNotEqual("?", view.title.text); Assert.AreEqual(home, view.Rect.localScale);
            controller.Undo(); Assert.AreEqual("?", controller.ui.stickerViews[4].title.text);
            Assert.IsTrue(controller.ui.stickerViews[4].IsCoveredShadeVisible);
        }
        [UnityTest] public IEnumerator CoveredBlackVeilMatchesGeometryAndClearsImmediatelyWhenUncovered()
        {
            var before = new Dictionary<int, bool>();
            foreach (var pair in controller.ui.stickerViews)
            {
                bool covered = BoardGeometry.Covered(controller.Session, pair.Key); before[pair.Key] = covered;
                Assert.AreEqual(covered, pair.Value.IsCoveredShadeVisible);
                var shade = pair.Value.transform.Find("Covered black veil").GetComponent<UnityEngine.UI.Image>();
                Assert.AreSame(pair.Value.image.sprite, shade.sprite); Assert.AreEqual(pair.Value.image.preserveAspect, shade.preserveAspect);
                Assert.IsFalse(shade.raycastTarget); Assert.Greater(shade.color.a, 0); Assert.AreEqual(0, shade.color.r);
            }
            controller.Tap(0); bool revealed = false;
            foreach (var pair in controller.ui.stickerViews)
            {
                bool covered = BoardGeometry.Covered(controller.Session, pair.Key);
                Assert.AreEqual(covered, pair.Value.IsCoveredShadeVisible);
                revealed |= before[pair.Key] && !covered;
            }
            Assert.IsTrue(revealed); Assert.IsTrue(controller.CanTap);
            yield return WaitForAnimation(); controller.Undo();
            foreach (var pair in controller.ui.stickerViews) Assert.AreEqual(before[pair.Key], pair.Value.IsCoveredShadeVisible);
        }
        [UnityTest] public IEnumerator StickerPeelsAtSourceAndFlattensAtLiveTargetBeforeLanding()
        {
            LoadFlatLevel(0); fast.peelDuration = .25f; fast.travelDuration = .12f; fast.stickDuration = .3f;
            Vector3 start = controller.ui.stickerViews[0].Rect.position;
            var target = controller.ui.categoryViews[0].targets[0].rectTransform;
            controller.Tap(0); yield return null;
            var ghost = controller.ui.effects.Find("Flying sticker 0"); Assert.IsNotNull(ghost);
            var curl = ghost.GetComponent<StickerCurlMesh>(); Assert.IsNotNull(curl);
            float deadline = Time.realtimeSinceStartup + 4;
            while (curl.Amount < .1f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(curl.Amount, .1f); Assert.Less(curl.Amount, 1);
            Assert.Less(Vector3.Distance(start, ghost.position), .01f, "Peel must happen before the jump.");
            Assert.AreEqual(0, controller.ui.categoryViews[0].FilledCount); Assert.IsTrue(controller.CanTap);
            while ((Vector3.Distance(target.position, ghost.position) > .01f || curl.Amount >= .99f) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(curl.Amount, 0); Assert.Less(curl.Amount, .99f);
            Assert.AreEqual(0, controller.ui.categoryViews[0].FilledCount, "Do not commit the target while the sticker is still curved.");
            // Keep tracking the live slot even if the layout moves during the reverse fold.
            target.anchoredPosition += new Vector2(7, 0); yield return null;
            Assert.Less(Vector3.Distance(target.position, ghost.position), .01f);
            yield return WaitForAnimation(); Assert.AreEqual(1, controller.ui.categoryViews[0].FilledCount);
            Assert.IsNull(controller.ui.effects.Find("Flying sticker 0"));
            // Loading another level halfway through peeling must cancel and dispose the whole flight.
            controller.LoadLevel(0); controller.Tap(0); yield return null; controller.LoadLevel(0); yield return null;
            Assert.AreEqual(0, controller.ui.effects.childCount); Assert.IsFalse(controller.IsAnimating); Assert.IsTrue(controller.CanTap);
        }
        [UnityTest] public IEnumerator LandingSoundWaitsForArrival()
        {
            var feedback = new LandingFeedback { controller = controller }; controller.Feedback = feedback;
            fast.travelDuration = .15f; controller.Tap(0);
            Assert.AreEqual(0, feedback.landings); Assert.AreEqual(0, controller.ui.categoryViews[0].FilledCount);
            yield return WaitForAnimation(); Assert.AreEqual(1, feedback.landings);
        }
        [UnityTest] public IEnumerator StickerFliesToItsOwnTextureSlotWhenSelectedOutOfOrder()
        {
            LoadFlatLevel(0); var card = controller.ui.categoryViews[0]; var definition = controller.levels[0].stickers[2].definition;
            Assert.AreSame(definition.ArtworkSprite, card.targets[2].sprite); Assert.Less(card.targets[2].color.a, .5f);
            Assert.AreEqual(-1, card.stickers[2]);
            bool landed = false;
            controller.Feedback = new LandingFeedback { controller = controller, onLanding = () =>
            {
                var flying = controller.ui.effects.GetChild(0).GetComponent<UnityEngine.UI.Image>();
                Assert.Less(Vector3.Distance(flying.transform.position, card.targets[2].transform.position), .01f);
                Assert.Less(Vector2.Distance(flying.rectTransform.sizeDelta, card.targets[2].rectTransform.sizeDelta), .01f);
                Assert.AreSame(card.targets[2].sprite, flying.sprite); Assert.AreEqual(card.targets[2].preserveAspect, flying.preserveAspect); landed = true;
            } };
            controller.Tap(2); yield return WaitForAnimation(); Assert.IsTrue(landed);
            Assert.AreEqual(2, card.stickers[2]); Assert.AreEqual(-1, card.stickers[0]);
            Assert.AreEqual(definition.tint, card.targets[2].color); Assert.Less(card.targets[0].color.a, .5f);
        }
        sealed class BufferLandingFeedback : IGameFeedback
        {
            public GameController controller;
            public int arrivals;
            public void Play(PuzzleEventKind kind)
            {
                if (kind != PuzzleEventKind.Buffered) return;
                int index = 8 + arrivals++;
                var ghost = controller.ui.effects.Find("Flying sticker " + index).GetComponent<UnityEngine.UI.Image>();
                var waiting = controller.ui.waitingViews[index];
                Assert.Less(Vector3.Distance(ghost.transform.position, waiting.transform.position), .01f, "Flight must track a recentered waiting slot.");
                Assert.Less(Vector2.Distance(ghost.rectTransform.sizeDelta, waiting.rectTransform.sizeDelta), .01f);
            }
            public void Haptic() { }
        }
        [UnityTest] public IEnumerator ConcurrentBufferFlightsStopAtFullTrayWithoutAnOverflowSlot()
        {
            LoadFlatLevel(4, 2); fast.travelDuration = .15f;
            var feedback = new BufferLandingFeedback { controller = controller }; controller.Feedback = feedback;
            foreach (int index in new[] { 8, 9, 10, 11, 12 }) { Assert.IsFalse(controller.Session.HasTarget(index)); controller.Tap(index); }
            yield return WaitForAnimation(); Assert.AreEqual(4, feedback.arrivals); Assert.AreEqual(GameplayState.Fail, controller.Session.State);
            Assert.AreEqual(4, controller.ui.waitingViews.Count); Assert.AreEqual(StickerLocation.Board, controller.Session.Stickers[12].location);
        }
        [UnityTest] public IEnumerator MutingStopsAudioAndRestoresPreference()
        {
            const string key = "StickyPeerSort.SoundMuted";
            bool existed = PlayerPrefs.HasKey(key); int previous = PlayerPrefs.GetInt(key, 0);
            var audio = controller.GetComponent<GameAudioFeedback>(); Assert.IsNotNull(audio); Assert.IsNotNull(controller.ui.sound);
            try
            {
                audio.SetMuted(false); audio.Play(PuzzleEventKind.Win); yield return null;
                bool playing = false; foreach (var source in controller.GetComponents<AudioSource>()) playing |= source.isPlaying;
                Assert.IsTrue(playing, "Generated clip should actually play through an AudioSource.");
                audio.SetMuted(true); audio.Play(PuzzleEventKind.Win); yield return null;
                foreach (var source in controller.GetComponents<AudioSource>()) Assert.IsFalse(source.isPlaying);
                Assert.AreEqual(1, PlayerPrefs.GetInt(key));
            }
            finally
            {
                audio.SetMuted(previous != 0);
                if (!existed) PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
            }
        }
        [UnityTest] public IEnumerator RewardCelebrationDoesNotGrantReplayCoinsAndClearsOnRestart()
        {
            var level = controller.levels[0];
            foreach (string id in level.solutionTrace)
            { controller.Tap(level.stickers.FindIndex(p => p.instanceId == id)); yield return WaitForAnimation(); }
            Assert.AreEqual(GameplayState.Win, controller.Session.State); Assert.AreEqual(level.coinReward, controller.Save.Data.coins);
            var layer = controller.ui.effects.parent.Find("Celebration Layer"); Assert.IsNotNull(layer);
            Assert.IsNotNull(layer.Find("Coin reward"));
            foreach (var graphic in layer.GetComponentsInChildren<UnityEngine.UI.Graphic>()) Assert.IsFalse(graphic.raycastTarget);
            ScreenCapture.CaptureScreenshot("Temp/GameplayRewardPreview.png"); yield return null;
            controller.LoadLevel(0); yield return null; Assert.AreEqual(0, layer.childCount);
            foreach (string id in level.solutionTrace)
            { controller.Tap(level.stickers.FindIndex(p => p.instanceId == id)); yield return WaitForAnimation(); }
            Assert.AreEqual(level.coinReward, controller.Save.Data.coins); Assert.IsNull(layer.Find("Coin reward"));
        }
        Vector2 VisiblePoint(int index)
        {
            var s = controller.Session.Stickers[index];
            for (int y = -8; y <= 8; y++) for (int x = -8; x <= 8; x++)
            {
                var p = s.position + new Vector2(x * s.placement.size.x / 18, y * s.placement.size.y / 18);
                if (BoardGeometry.Hit(controller.Session, p) == index) return p;
            }
            Assert.Fail("Sticker has no visible test point: " + index); return Vector2.zero;
        }
        void LoadFlatLevel(int index, int activeCategoryCount = -1)
        {
            // These fixtures isolate transition/buffer rules from the separately tested pile layout.
            var fixture = Object.Instantiate(controller.levels[index]); fixtures.Add(fixture); fixture.piles.Clear();
            if (activeCategoryCount > 0) fixture.activeCategoryCount = activeCategoryCount;
            Vector2 spacing = new Vector2(210, 170);
            foreach (var placement in fixture.stickers)
            {
                spacing.x = Mathf.Max(spacing.x, placement.size.x * placement.scale + 24);
                spacing.y = Mathf.Max(spacing.y, placement.size.y * placement.scale + 24);
            }
            for (int i = 0; i < fixture.stickers.Count; i++)
            {
                var p = fixture.stickers[i]; p.position = new Vector2((i % 4 - 1.5f) * spacing.x, (i / 4 - 2) * spacing.y); p.rotation = 0;
            }
            controller.levels[index] = fixture; controller.LoadLevel(index);
        }
        [UnityTest] public IEnumerator FinalSlotRendersBeforeExitAndNewCardEntersBeforeBufferFlights()
        {
            LoadFlatLevel(2);
            controller.levels[2].bufferCapacity = 5; controller.LoadLevel(2); // Isolate transitions from the full-tray lose rule.
            foreach (int i in new[] { 8, 9, 10, 11, 0, 1, 2 }) { controller.Tap(i); yield return WaitForAnimation(); }
            Assert.AreEqual(4, controller.ui.waitingViews.Count);
            int oldLane = controller.ui.categoryViews[0].lane;
            Vector3 otherPosition = controller.ui.categoryViews[1].rect.position;
            bool finalSlotSeen = false, oldRemoved = false, newEntered = false, bufferCollected = false, cascadeRemoved = false;
            fast.categoryFilledHold = .08f; fast.categoryExitDuration = .08f; fast.categoryEnterDuration = .16f;
            System.Action<PuzzleEvent> observe = e =>
            {
                if (e.kind == PuzzleEventKind.Collected && e.category == 0 && e.slot == 3)
                {
                    var card = controller.ui.categoryViews[0];
                    Assert.AreEqual(4, card.FilledCount); Assert.AreEqual("4 / 4", card.counter.text);
                    Assert.AreEqual(3, card.stickers[3]); Assert.IsTrue(card.rect.gameObject.activeInHierarchy);
                    Assert.AreEqual(4, controller.ui.waitingViews.Count); finalSlotSeen = true;
                }
                if (e.kind == PuzzleEventKind.CategoryCompleted && e.category == 0)
                { Assert.IsTrue(finalSlotSeen); Assert.IsFalse(controller.ui.categoryViews.ContainsKey(0)); oldRemoved = true; }
                if (e.kind == PuzzleEventKind.CategoryActivated && e.category == 2)
                {
                    Assert.IsTrue(oldRemoved); var card = controller.ui.categoryViews[2];
                    Assert.AreEqual(oldLane, card.lane); Assert.AreEqual(1, card.group.alpha); Assert.AreEqual(Vector3.one, card.rect.localScale);
                    Assert.AreEqual(0, card.FilledCount); Assert.AreEqual(4, controller.ui.waitingViews.Count); newEntered = true;
                }
                if (e.kind == PuzzleEventKind.Collected && e.category == 2)
                { Assert.IsTrue(newEntered); Assert.IsFalse(controller.ui.waitingViews.ContainsKey(e.sticker)); bufferCollected = true; }
                if (e.kind == PuzzleEventKind.CategoryCompleted && e.category == 2)
                { Assert.IsTrue(bufferCollected); Assert.IsFalse(controller.ui.categoryViews.ContainsKey(2)); cascadeRemoved = true; }
            };
            controller.PresentationEventFinished += observe;
            controller.Tap(3);
            bool entranceFrameSeen = false; float deadline = Time.realtimeSinceStartup + 10;
            while (controller.IsAnimating && Time.realtimeSinceStartup < deadline)
            {
                if (controller.PresentationPhase == PuzzleEventKind.CategoryActivated && controller.ui.categoryViews.ContainsKey(2))
                {
                    var card = controller.ui.categoryViews[2];
                    if (card.group.alpha < 1)
                    {
                        Assert.IsFalse(controller.ui.categoryViews.ContainsKey(0)); Assert.AreEqual(4, controller.ui.waitingViews.Count);
                        Assert.AreEqual(0, controller.ui.effects.childCount); entranceFrameSeen = true;
                    }
                }
                yield return null;
            }
            controller.PresentationEventFinished -= observe;
            Assert.IsFalse(controller.IsAnimating); Assert.IsTrue(finalSlotSeen && oldRemoved && newEntered && bufferCollected && cascadeRemoved && entranceFrameSeen);
            Assert.AreEqual(otherPosition, controller.ui.categoryViews[1].rect.position);
            Assert.IsTrue(controller.ui.categoryViews.ContainsKey(3)); Assert.IsEmpty(controller.ui.waitingViews);
        }
        [UnityTest] public IEnumerator BoardShowsTraysAndOverlappingStickersWithCorrectTapPriority()
        {
            Assert.AreEqual(3, controller.Session.Level.piles.Count);
            int trayCount = 0; foreach (Transform child in controller.ui.board) if (child.name.StartsWith("Pile tray ")) trayCount++;
            Assert.AreEqual(3, trayCount);
            bool overlapFound = false;
            for (int y = -350; y <= 350 && !overlapFound; y += 10) for (int x = -430; x <= 430 && !overlapFound; x += 10)
            {
                Vector2 point = new Vector2(x, y); int count = 0;
                foreach (var sticker in controller.Session.Stickers) if (BoardGeometry.Contains(sticker, point)) count++;
                if (count < 2) continue;
                int top = BoardGeometry.Hit(controller.Session, point); var pointer = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, controller.ui.board.TransformPoint(point)) };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Assert.IsNotEmpty(hits); Assert.AreEqual(controller.ui.stickerViews[top].gameObject, hits[0].gameObject); overlapFound = true;
            }
            Assert.IsTrue(overlapFound, "Level 1 needs actual overlapping silhouettes, not a spaced grid."); yield return null;
        }
        [UnityTest] public IEnumerator AllTenLevelsPlayThroughWinNextAndRetry()
        {
            for (int l = 0; l < 10; l++)
            {
                controller.LoadLevel(l); var level = controller.levels[l];
                foreach (string id in level.solutionTrace)
                {
                    int i = level.stickers.FindIndex(p => p.instanceId == id); controller.Tap(i); yield return WaitForAnimation();
                }
                Assert.AreEqual(GameplayState.Win, controller.Session.State, "Level " + (l + 1)); Assert.IsTrue(controller.ui.modal.activeSelf);
                Assert.IsEmpty(controller.Session.Buffer); Assert.AreEqual(0, controller.ui.stickerViews.Count);
                controller.ui.primary.onClick.Invoke(); Assert.AreEqual(l < 9 ? l + 2 : 1, controller.Session.Level.levelNumber);
            }
            controller.LoadLevel(0); controller.ui.restart.onClick.Invoke(); Assert.AreEqual(GameplayState.Paused, controller.Session.State);
            controller.ui.primary.onClick.Invoke(); Assert.AreEqual(GameplayState.Playing, controller.Session.State); Assert.AreEqual(8, controller.ui.stickerViews.Count);
        }
        [UnityTest] public IEnumerator FullTrayShowsFailureUndoRecoversAndRetryResets()
        {
            LoadFlatLevel(4, 2);
            foreach (int i in new[] { 8, 9, 10, 11 }) { Assert.IsFalse(controller.Session.HasTarget(i)); controller.Tap(i); yield return WaitForAnimation(); }
            Assert.AreEqual(GameplayState.Fail, controller.Session.State); Assert.IsTrue(controller.ui.modal.activeSelf);
            controller.ui.secondary.onClick.Invoke(); Assert.AreEqual(GameplayState.Playing, controller.Session.State); Assert.AreEqual(3, controller.Session.Buffer.Count);
            controller.Tap(11); yield return WaitForAnimation(); Assert.AreEqual(GameplayState.Fail, controller.Session.State);
            controller.ui.primary.onClick.Invoke(); Assert.AreEqual(GameplayState.Playing, controller.Session.State); Assert.IsEmpty(controller.Session.Buffer);
        }
        [UnityTest] public IEnumerator ActualLevelThreeLosesAtFourOfFourAndCanRecover()
        {
            controller.LoadLevel(2); int coinsBefore = controller.Save.Data.coins; bool fullSeen = false; int last = -1;
            int[] path = { 1, 2, 4, 7, 5, 8, 10, 13, 12, 11 };
            for (int step = 0; step < path.Length; step++)
            {
                Assert.IsTrue(controller.Session.Legal(path[step]), "Actual overlapping-board move " + step);
                controller.Tap(path[step]);
                if (controller.Session.State == GameplayState.Fail)
                {
                    last = path[step];
                    Assert.AreEqual(GameplayState.Fail, controller.Session.State); Assert.IsFalse(controller.CanTap);
                    Assert.IsFalse(controller.ui.modal.activeSelf, "Lose modal waits for the sticker to arrive.");
                }
                yield return WaitForAnimation();
                if (controller.Session.Buffer.Count == 4)
                {
                    fullSeen = true; Assert.AreEqual(GameplayState.Fail, controller.Session.State); Assert.IsFalse(controller.CanTap);
                    StringAssert.Contains("TRAY FULL", controller.ui.capacity.text); Assert.IsTrue(controller.ui.modal.activeSelf);
                    ScreenCapture.CaptureScreenshot("Temp/TrayFullPreview.png"); yield return null;
                    break;
                }
            }
            Assert.IsTrue(fullSeen); Assert.AreEqual(4, controller.Session.Buffer.Count);
            StringAssert.Contains("FULL", controller.ui.modalTitle.text); StringAssert.Contains("4 / 4", controller.ui.modalBody.text);
            Assert.AreEqual(coinsBefore, controller.Save.Data.coins);
            ScreenCapture.CaptureScreenshot("Temp/LosePreview.png"); yield return null;
            controller.ui.secondary.onClick.Invoke(); Assert.AreEqual(GameplayState.Playing, controller.Session.State);
            Assert.AreEqual(3, controller.Session.Buffer.Count); Assert.IsFalse(controller.ui.modal.activeSelf);
            controller.Tap(last); yield return WaitForAnimation(); controller.ui.primary.onClick.Invoke();
            Assert.AreEqual(3, controller.Session.Level.levelNumber); Assert.IsEmpty(controller.Session.Buffer);
            Assert.IsFalse(controller.ui.modal.activeSelf); Assert.IsTrue(controller.CanTap);
        }
        [UnityTest] public IEnumerator CoveredStickerShakesWithoutChangingPuzzleState()
        {
            int covered = -1;
            for (int i = 0; i < controller.Session.Stickers.Count; i++)
                if (BoardGeometry.Covered(controller.Session, i) && BoardGeometry.Exposed(controller.Session, i)) { covered = i; break; }
            Assert.GreaterOrEqual(covered, 0); var view = controller.ui.stickerViews[covered];
            Vector2 home = view.Rect.anchoredPosition; string before = controller.Session.Key();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, controller.ui.board.TransformPoint(VisiblePoint(covered))) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits); Assert.AreEqual(view.gameObject, hits[0].gameObject);
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            Assert.IsTrue(view.IsShowingClickFail); Assert.AreEqual(before, controller.Session.Key()); Assert.IsFalse(controller.IsAnimating);
            yield return new WaitForSecondsRealtime(fast.clickFailDuration + .1f);
            Assert.IsFalse(view.IsShowingClickFail); Assert.AreEqual(home, view.Rect.anchoredPosition); Assert.AreEqual(before, controller.Session.Key());
        }
        [UnityTest] public IEnumerator RapidTapsStartConcurrentFlightsAndCommitBeforeCardCompletion()
        {
            LoadFlatLevel(0); fast.travelDuration = .15f;
            controller.Tap(0); controller.Tap(1);
            Assert.IsTrue(controller.IsAnimating); Assert.IsTrue(controller.CanTap); Assert.AreEqual(2, controller.Session.Categories[0].Count);
            Assert.IsFalse(controller.ui.stickerViews.ContainsKey(0)); Assert.IsFalse(controller.ui.stickerViews.ContainsKey(1));
            Assert.AreEqual(2, controller.ui.effects.childCount); Assert.AreEqual(0, controller.ui.categoryViews[0].FilledCount);
            controller.Tap(0); Assert.AreEqual(2, controller.Session.Categories[0].Count);
            controller.Tap(2); controller.Tap(3); Assert.IsFalse(controller.CanTap);
            var committed = new List<int>();
            System.Action<PuzzleEvent> observe = e =>
            {
                if (e.kind == PuzzleEventKind.Collected && e.category == 0) committed.Add(e.sticker);
                if (e.kind == PuzzleEventKind.CategoryCompleted && e.category == 0) CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, committed);
            };
            controller.PresentationEventFinished += observe; yield return WaitForAnimation(); controller.PresentationEventFinished -= observe;
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, committed); Assert.IsTrue(controller.ui.categoryViews.ContainsKey(1)); Assert.IsTrue(controller.CanTap);
        }
        [UnityTest] public IEnumerator LegacyInputModuleIsActiveAndOldInputApiWorks()
        {
            yield return null;
            Assert.IsInstanceOf<StandaloneInputModule>(EventSystem.current.currentInputModule);
            Assert.DoesNotThrow(() => { Input.GetMouseButton(0); Input.touchCount.ToString(); });
            // Changing activeInputHandler without restarting can leave the legacy backend inert:
            // Input calls return normally, while no physical mouse is registered.
            if (Application.platform == RuntimePlatform.WindowsEditor && !Application.isBatchMode)
                Assert.IsTrue(Input.mousePresent, "Legacy input has no mouse device. Restart Unity after changing Active Input Handling before validating real mouse input.");
        }
        [UnityTest] public IEnumerator IrregularTextureIsSharedByBoardFlightBufferAndCategoryWithoutStretching()
        {
            LoadFlatLevel(2); var level = controller.levels[2];
            var texture = new Texture2D(32, 64, TextureFormat.RGBA32, false); texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[32 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 32; x++) pixels[y * 32 + x] = x < 9 || y < 12 ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels); texture.Apply(); artworkObjects.Add(texture);
            var definition = Object.Instantiate(level.stickers[8].definition); artworkObjects.Add(definition);
            definition.texture = texture; definition.preserveAspect = true; definition.tint = Color.white; definition.showLabel = false;
            level.stickers[8].definition = definition; level.objectives[2].exactTargets[0] = definition; controller.LoadLevel(2);
            var image = controller.ui.stickerViews[8].image;
            Assert.AreSame(texture, image.sprite.texture); Assert.IsTrue(image.preserveAspect);
            controller.Tap(8); Assert.IsTrue(controller.IsAnimating);
            var flying = controller.ui.effects.GetChild(0).GetComponent<UnityEngine.UI.Image>();
            Assert.AreSame(texture, flying.sprite.texture); Assert.IsTrue(flying.preserveAspect);
            yield return WaitForAnimation(); var waiting = controller.ui.waitingViews[8];
            Assert.AreSame(texture, waiting.sprite.texture); Assert.IsTrue(waiting.preserveAspect);
            foreach (int i in new[] { 0, 1, 2, 3 }) { controller.Tap(i); yield return WaitForAnimation(); }
            var card = controller.ui.categoryViews[2]; Assert.AreEqual(8, card.stickers[0]);
            Assert.AreSame(texture, card.targets[0].sprite.texture); Assert.IsTrue(card.targets[0].preserveAspect);
            Assert.IsFalse(controller.ui.waitingViews.ContainsKey(8));
        }
    }
}
