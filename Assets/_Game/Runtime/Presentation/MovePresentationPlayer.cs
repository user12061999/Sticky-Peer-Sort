using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace StickyPeerSort
{
    // Unity animation adapter. Logical gameplay only sees IMovePresenter.
    public sealed class MovePresentationPlayer : IMovePresenter
    {
        readonly GameplayUI ui;
        readonly IGameplayReadModel context;
        readonly ICoroutineRunner runner;
        int activePresentations, issuedMove, nextCommit;
        bool transitionPending;
        public bool IsBusy { get; private set; }
        public bool BlocksInput => transitionPending;
        public PuzzleEventKind? Phase { get; private set; }
        public IGameFeedback Feedback { get; set; } = new SilentFeedback();
        public event Action<PuzzleEvent> EventFinished;
        public event Action Drained;
        public MovePresentationPlayer(GameplayUI ui, IGameplayReadModel context, ICoroutineRunner runner)
        { this.ui = ui; this.context = context; this.runner = runner; }
        public void Reset()
        {
            runner.CancelAll(); activePresentations = issuedMove = nextCommit = 0;
            IsBusy = transitionPending = false; Phase = null; if (ui != null && ui.effects != null) UIFactory.Clear(ui.effects);
            if (ui != null) ui.Celebration?.ResetEffects();
        }
        public void Enqueue(MovePresentation move)
        {
            transitionPending |= move.BlocksInput; int ticket = issuedMove++;
            activePresentations++; IsBusy = true; runner.Run(Present(move.Events, move.Snapshot, ticket));
        }
        IEnumerator Present(IReadOnlyList<PuzzleEvent> events, GameSession resolved, int ticket)
        {
            IsBusy = true;
            var specials = new SpecialStickerPresentation(ui, context);
            var animatedUnlocks = new HashSet<int>();
            foreach (var e in events)
            {
                Phase = e.kind;
                bool landing = e.kind == PuzzleEventKind.Collected || e.kind == PuzzleEventKind.Buffered;
                if (!landing && !(e.kind == PuzzleEventKind.Unlocked && animatedUnlocks.Contains(e.sticker))) Feedback.Play(e.kind);
                if (context.Balance.haptics && (e.kind == PuzzleEventKind.Peel || e.kind == PuzzleEventKind.CategoryCompleted)) Feedback.Haptic();
                StickerView view;
                // Each flight owns its peel/stick sequence; other ordinary taps can start concurrently.
                if (e.kind == PuzzleEventKind.Collected || e.kind == PuzzleEventKind.Buffered)
                {
                    // Use the visible card and actual target slot, never the final logical category list.
                    var target = ui.Destination(e);
                    float rotation = ui.stickerViews.TryGetValue(e.sticker, out view) ? view.Rect.localEulerAngles.z : 0;
                    if (rotation > 180) rotation -= 360;
                    Vector3 start; Vector2 size;
                    float peeledAmount, peelDirection;
                    if (!ui.TakeSticker(e.sticker, out start, out size, out peeledAmount, out peelDirection))
                        throw new System.InvalidOperationException("Missing visible source for sticker " + e.sticker);
                    var d = resolved.Stickers[e.sticker].placement.definition;
                    var ghost = UIFactory.Panel("Flying sticker " + e.sticker, ui.effects, size, Vector2.zero, d.tint, d.ArtworkSprite);
                    StickerVisuals.Apply(ghost, d);
                    ghost.transform.position = start;
                    ghost.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
                    var text = d.showLabel ? UIFactory.Text("Label", ghost.transform, d.displayName, 25, size * .9f, Vector2.zero) : null;
                    var page = new StickerPeelPresentation(ghost, text, context.Balance);
                    page.DirectionDegrees = peelDirection;
                    // Curl the original sprite mesh before adding the cosmetic shadow mesh.
                    UnityEngine.UI.Shadow flightShadow = null;
                    if (ui.theme != null && ui.theme.referenceComposition)
                    {
                        flightShadow = ghost.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                        flightShadow.effectColor = new Color(.2f, .1f, .03f, .25f);
                        flightShadow.effectDistance = new Vector2(4, -7);
                    }
                    if (peeledAmount < .999f) yield return page.Animate(peeledAmount, 1, context.Balance.peelDuration);
                    else page.Set(1); // Continue the user's completed peel; never restart it at zero.
                    Vector2 targetSize = target.sizeDelta;
                    if (e.kind == PuzzleEventKind.Buffered) targetSize -= new Vector2(10, 19);
                    Vector3 end = target.position; float t = 0;
                    while (t < context.Balance.travelDuration)
                    {
                        end = target.position;
                        targetSize = target.sizeDelta - (e.kind == PuzzleEventKind.Buffered ? new Vector2(10, 19) : Vector2.zero);
                        t += Time.unscaledDeltaTime; float f = context.Balance.easing.Evaluate(Mathf.Clamp01(t / Mathf.Max(.001f, context.Balance.travelDuration)));
                        ghost.transform.position = Vector3.Lerp(start, end, f) + ui.effects.up * Mathf.Sin(f * Mathf.PI) * context.Balance.flightArc;
                        ghost.rectTransform.sizeDelta = Vector2.Lerp(size, targetSize, f);
                        float press = Mathf.Clamp01(t / Mathf.Max(.001f, Mathf.Min(context.Balance.pressDuration, context.Balance.travelDuration * .3f)));
                        float peel = Mathf.Clamp01(t / Mathf.Max(.001f, context.Balance.pressDuration + context.Balance.peelDuration));
                        ghost.transform.localScale = Vector3.one * (1 - Mathf.Sin(press * Mathf.PI) * .06f + Mathf.Sin(peel * Mathf.PI) * context.Balance.flightPop);
                        ghost.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(rotation, 0, f));
                        if (flightShadow != null) flightShadow.effectDistance = new Vector2(4, -7 - Mathf.Sin(f * Mathf.PI) * 10);
                        if (text != null) text.rectTransform.sizeDelta = ghost.rectTransform.sizeDelta * .9f;
                        yield return null;
                    }
                    ghost.transform.position = target.position;
                    ghost.transform.localScale = Vector3.one;
                    ghost.rectTransform.sizeDelta = targetSize;
                    ghost.rectTransform.localRotation = Quaternion.identity;
                    // Flights may overlap, but commits and card transitions keep the logical move order.
                    while (ticket != nextCommit)
                    {
                        ghost.transform.position = target.position;
                        ghost.rectTransform.sizeDelta = target.sizeDelta - (e.kind == PuzzleEventKind.Buffered ? new Vector2(10, 19) : Vector2.zero);
                        yield return null;
                    }
                    ghost.transform.position = target.position;
                    ghost.rectTransform.sizeDelta = target.sizeDelta - (e.kind == PuzzleEventKind.Buffered ? new Vector2(10, 19) : Vector2.zero);
                    // Reverse the same fold onto the live destination; commit/count/SFX follow the final flattening.
                    yield return page.Animate(1, 0, context.Balance.stickDuration, () =>
                    {
                        ghost.transform.position = target.position;
                        ghost.rectTransform.sizeDelta = target.sizeDelta - (e.kind == PuzzleEventKind.Buffered ? new Vector2(10, 19) : Vector2.zero);
                        if (flightShadow != null) flightShadow.effectDistance = new Vector2(4, -7);
                        if (text != null) text.rectTransform.sizeDelta = ghost.rectTransform.sizeDelta * .9f;
                    });
                    ui.Land(e, resolved);
                    Feedback.Play(e.kind);
                    if (context.Balance.celebrations) ui.Celebration?.Land(target);
                    ghost.gameObject.SetActive(false); UnityEngine.Object.Destroy(ghost.gameObject);
                    ui.UpdateTrayStatus(ui.waitingViews.Count, resolved.Capacity);
                    yield return null; // Render the landed sticker for at least one frame.
                }
                if (e.kind == PuzzleEventKind.CategoryCompleted)
                {
                    var card = ui.categoryViews[e.category];
                    ui.instruction.text = resolved.Categories[e.category].objective.category.title + " collection complete!";
                    float hold = 0;
                    while (hold < context.Balance.categoryFilledHold) { hold += Time.unscaledDeltaTime; yield return null; }
                    if (context.Balance.celebrations) ui.Celebration?.Collection(card.rect);
                    yield return Scale(card.rect, 1.06f, context.Balance.categoryDuration * .5f);
                    yield return Scale(card.rect, 1, context.Balance.categoryDuration * .5f);
                    // A key must leave the filled card before that card exits or its replacement enters.
                    foreach (var unlock in events)
                    {
                        if (unlock.kind != PuzzleEventKind.Unlocked || animatedUnlocks.Contains(unlock.sticker)) continue;
                        var config = resolved.Stickers[unlock.sticker].placement.special;
                        if (config.unlockCondition != UnlockCondition.CategoryCompleted || config.dependencyId != resolved.Categories[e.category].objective.category.id) continue;
                        Feedback.Play(PuzzleEventKind.Unlocked);
                        yield return specials.Unlock(unlock.sticker, card.rect.Find("Unlock key") as RectTransform);
                        animatedUnlocks.Add(unlock.sticker);
                    }
                    yield return TransitionCard(card, false);
                    ui.RemoveCategory(e.category);
                }
                if (e.kind == PuzzleEventKind.CategoryActivated)
                {
                    var card = ui.ActivateCategory(e.category, resolved);
                    yield return TransitionCard(card, true);
                }
                if (e.kind == PuzzleEventKind.IceBroken) ui.RefreshSticker(e.sticker);
                if (e.kind == PuzzleEventKind.Unlocked && !animatedUnlocks.Contains(e.sticker)) yield return specials.Unlock(e.sticker, null);
                if (e.kind == PuzzleEventKind.Revealed) yield return specials.Reveal(e.sticker);
                if (e.kind == PuzzleEventKind.Win && context.Balance.celebrations)
                    ui.Celebration?.Win(context.Progress.highestCompleted < resolved.Level.levelNumber ? resolved.Level.coinReward : 0);
                if (e.kind == PuzzleEventKind.Fail)
                {
                    ui.instruction.text = "Waiting tray full! Undo or retry with a different order.";
                    if (context.Balance.celebrations) ui.Celebration?.Land(ui.buffer);
                    yield return new WaitForSecondsRealtime(.24f); // Let the final occupied slot show before the lose modal.
                }
                EventFinished?.Invoke(e);
            }
            while (ticket != nextCommit) yield return null;
            nextCommit++; activePresentations--;
            if (activePresentations > 0) yield break;
            Phase = null; IsBusy = false; transitionPending = false;
            Drained?.Invoke();
        }
        IEnumerator TransitionCard(CategoryCardPresentation card, bool entering)
        {
            Vector2 home = card.rect.anchoredPosition;
            Vector2 from = entering ? home + new Vector2(0, -90) : home;
            Vector2 to = entering ? home : home + new Vector2(0, 150);
            float duration = entering ? context.Balance.categoryEnterDuration : context.Balance.categoryExitDuration;
            float t = 0;
            card.rect.anchoredPosition = from; card.rect.localScale = Vector3.one * (entering ? .8f : 1);
            card.group.alpha = entering ? 0 : 1;
            do
            {
                t += Time.unscaledDeltaTime; float f = context.Balance.easing.Evaluate(Mathf.Clamp01(t / Mathf.Max(.001f, duration)));
                card.rect.anchoredPosition = Vector2.Lerp(from, to, f);
                card.rect.localScale = Vector3.one * Mathf.Lerp(entering ? .8f : 1, entering ? 1 : .85f, f);
                card.group.alpha = entering ? f : 1 - f;
                yield return null;
            } while (t < duration);
            card.rect.anchoredPosition = to; card.rect.localScale = Vector3.one * (entering ? 1 : .85f); card.group.alpha = entering ? 1 : 0;
        }
        IEnumerator Scale(Transform target, float factor, float duration)
        {
            Vector3 start = target.localScale, end = Vector3.one * factor; float t = 0;
            while (t < duration) { t += Time.unscaledDeltaTime; target.localScale = Vector3.Lerp(start, end, Mathf.Clamp01(t / Mathf.Max(.001f, duration))); yield return null; }
            target.localScale = end;
        }
    }
}
