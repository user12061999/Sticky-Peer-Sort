using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort
{
    public sealed class GameplayUI : MonoBehaviour, IGameplayScreen
    {
        public RectTransform safeArea, categories, buffer, board, effects;
        public TextMeshProUGUI levelLabel, coins, instruction, progress, capacity;
        public Button undo, hint, extra, shuffle, restart, settings;
        public GameObject modal;
        public TextMeshProUGUI modalTitle, modalBody;
        public Button primary, secondary;
        public Button sound;
        public GameAudioFeedback AudioFeedback { get; set; }
        public GameCelebration Celebration { get; set; }
        public Sprite panelSprite;
        public GameplayUITheme theme;
        public TextMeshProUGUI undoCount, hintCount, extraCount, shuffleCount;
        public Image albumFill, modalIcon;
        public Image trayPanel;
        public StickerView stickerPrefab;
        public GameObject cardPrefab, slotPrefab;
        public StickerTrayView trayPrefab;
        public readonly Dictionary<int, StickerView> stickerViews = new Dictionary<int, StickerView>();
        public readonly Dictionary<int, CategoryCardPresentation> categoryViews = new Dictionary<int, CategoryCardPresentation>();
        public readonly Dictionary<int, Image> waitingViews = new Dictionary<int, Image>();
        readonly List<RectTransform> bufferSlots = new List<RectTransform>();
        readonly Queue<int> vacantLanes = new Queue<int>();
        int laneCount;
        float cardWidth;
        IGameplayReadModel context;
        IStickerInput input;
        IGameplayCommands commands;
        public bool HasModal => modal.activeSelf;
        public void Initialize(IGameplayReadModel context, IStickerInput input) { this.context = context; this.input = input; }
        public void BindCommands(IGameplayCommands value)
        {
            UnbindCommands(); commands = value;
            undo.onClick.AddListener(commands.Undo); hint.onClick.AddListener(commands.Hint);
            extra.onClick.AddListener(commands.Extra); shuffle.onClick.AddListener(commands.Shuffle);
            restart.onClick.AddListener(commands.ConfirmRestart); settings.onClick.AddListener(commands.Pause);
        }
        public void UnbindCommands()
        {
            if (commands == null) return;
            undo.onClick.RemoveListener(commands.Undo); hint.onClick.RemoveListener(commands.Hint);
            extra.onClick.RemoveListener(commands.Extra); shuffle.onClick.RemoveListener(commands.Shuffle);
            restart.onClick.RemoveListener(commands.ConfirmRestart); settings.onClick.RemoveListener(commands.Pause); commands = null;
        }
        public void Render() { RenderFrom(context); }
        public void HideModal() { modal.SetActive(false); }
        public void SetInstruction(string message) { instruction.text = message; }
        public void SynchronizeAfterMoves() { CompactBuffer(context.Session); UpdateHud(context); UpdateTrayInstruction(); RefreshCoveredStickers(); }
        void UpdateTrayInstruction()
        {
            var s = context.Session;
            instruction.text = s.Buffer.Count >= s.Capacity ? "Waiting tray full! Undo or retry with a different order."
                : s.Buffer.Count > 0 && s.Buffer.Count == s.Capacity - 1 ? "Only one waiting slot left. Plan your next sticker."
                : s.Level.tutorial;
            if (context.Balance.dragToPeel) instruction.text += "\nDrag to peel; release early to reattach.";
        }
        public void UpdateTrayStatus(int count, int limit)
        {
            capacity.text = (count >= limit ? "TRAY FULL   " : "WAITING TRAY   ") + count + " / " + limit;
            capacity.color = count >= limit ? new Color(.65f, .12f, .04f)
                : count > 0 && count == limit - 1 ? new Color(.59f, .28f, .02f) : theme != null ? theme.muted : UIFactory.Ink;
            if (trayPanel != null) trayPanel.color = count >= limit ? new Color(1, .69f, .42f) : new Color(1, .84f, .49f);
        }
        public void RefreshSticker(int index) { if (stickerViews.TryGetValue(index, out var view)) view.Bind(context, input, index); }
        void RefreshCoveredStickers()
        {
            foreach (var pair in stickerViews) pair.Value.SetCovered(BoardGeometry.Covered(context.Session, pair.Key));
        }
        public void RejectSticker(int index)
        {
            if (index < 0 || !stickerViews.TryGetValue(index, out var view)) return;
            view.ClickFail(context.Balance);
            AudioFeedback?.Reject();
            instruction.text = BoardGeometry.Covered(context.Session, index) ? "Remove the sticker on top first." : "Sticker blocked. Check its lock or linked partner.";
        }
        public void HighlightSticker(int index)
        {
            UpdateHud(context);
            foreach (var entry in stickerViews) entry.Value.Bind(context, input, entry.Key);
            if (!stickerViews.TryGetValue(index, out var view)) return;
            view.image.color = Color.white; instruction.text = "Try the highlighted " + context.Session.Stickers[index].placement.definition.displayName + " sticker.";
        }
        public void ShowModal(string title, string body, string a, Action actionA, string b, Action actionB)
        {
            StickerBackingPaper.Clear(effects);
            modal.SetActive(true); modalTitle.text = title; modalBody.text = body;
            if (theme != null && modalIcon != null)
            {
                modalIcon.sprite = title.Contains("COMPLETE") ? theme.starIcon : title.Contains("FULL") || title.Contains("OVERFLOW") || title.Contains("LEFT") ? theme.failIcon : theme.settingsIcon;
                modalIcon.preserveAspect = true;
            }
            primary.onClick.RemoveAllListeners(); secondary.onClick.RemoveAllListeners();
            primary.GetComponentInChildren<TextMeshProUGUI>().text = a; secondary.GetComponentInChildren<TextMeshProUGUI>().text = b;
            primary.onClick.AddListener(() => actionA()); secondary.onClick.AddListener(() => actionB());
        }
        public void ApplySafeArea()
        {
            if (Screen.width == 0 || Screen.height == 0) return;
            var area = Screen.safeArea; safeArea.anchorMin = area.min / new Vector2(Screen.width, Screen.height); safeArea.anchorMax = area.max / new Vector2(Screen.width, Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero; Canvas.ForceUpdateCanvases();
            if (safeArea.childCount > 0) safeArea.GetChild(0).localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(safeArea.rect.width / 1080f, safeArea.rect.height / 1920f));
        }
        public void UpdateHud(IGameplayReadModel owner)
        {
            var s = owner.Session;
            levelLabel.text = "Level " + s.Level.levelNumber; coins.text = theme != null ? owner.Progress.coins.ToString() : "COINS  " + owner.Progress.coins;
            int completed = s.Categories.FindAll(c => c.Complete).Count;
            progress.text = theme != null ? completed + " / " + s.Categories.Count : completed + " / " + s.Categories.Count + " COLLECTIONS  |  " + s.Level.difficulty.ToUpperInvariant();
            if (albumFill != null) albumFill.fillAmount = (float)completed / Mathf.Max(1, s.Categories.Count);
            UpdateTrayStatus(s.Buffer.Count, s.Capacity);
            SetBoosterCount(undo, undoCount, "UNDO", owner.Progress.undo);
            SetBoosterCount(hint, hintCount, "HINT", owner.Progress.hint);
            SetBoosterCount(extra, extraCount, "+ SLOT", owner.Progress.extraSlot);
            SetBoosterCount(shuffle, shuffleCount, "MIX", owner.Progress.shuffle);
            undo.interactable = owner.Balance.boostersEnabled && owner.Progress.undo > 0 && s.CanUndo;
            hint.interactable = owner.Balance.boostersEnabled && owner.Progress.hint > 0;
            extra.interactable = owner.Balance.boostersEnabled && owner.Progress.extraSlot > 0;
            shuffle.interactable = owner.Balance.boostersEnabled && owner.Progress.shuffle > 0;
            foreach (var button in new[] { undo, hint, extra, shuffle })
                if (button.TryGetComponent<CanvasGroup>(out var group)) group.alpha = button.interactable ? 1 : .72f;
        }
        static void SetBoosterCount(Button button, TextMeshProUGUI counter, string title, int value)
        { if (counter != null) counter.text = value.ToString(); else button.GetComponentInChildren<TextMeshProUGUI>().text = title + "  " + value; }
        void RenderFrom(IGameplayReadModel owner)
        {
            Celebration?.ResetEffects();
            var s = owner.Session; ApplySafeArea(); UpdateHud(owner); instruction.text = s.Level.tutorial;
            UpdateTrayInstruction();
            UIFactory.Clear(categories); UIFactory.Clear(buffer); UIFactory.Clear(board);
            stickerViews.Clear(); categoryViews.Clear(); waitingViews.Clear(); bufferSlots.Clear(); vacantLanes.Clear();
            laneCount = Mathf.Min(s.Level.activeCategoryCount, s.Categories.Count);
            cardWidth = theme != null && laneCount == 1 ? 620 : Mathf.Min(theme != null ? 310 : 300, 940f / Mathf.Max(1, laneCount));
            int lane = 0;
            for (int c = 0; c < s.Categories.Count; c++) if (s.Categories[c].active)
            {
                var view = CreateCategory(c, lane++, s);
                for (int t = 0; t < s.Categories[c].targets.Length; t++)
                {
                    int i = s.Categories[c].targets[t]; if (i >= 0) view.Fill(t, i, s.Stickers[i].placement.definition);
                }
            }
            while (lane < laneCount) vacantLanes.Enqueue(lane++);
            EnsureBufferSlots(Mathf.Max(s.Capacity, s.Buffer.Count));
            for (int b = 0; b < s.Buffer.Count; b++) FillBuffer(s.Buffer[b], b, s);
            DrawPiles(s.Level);
            var order = new List<int>(); for (int i = 0; i < s.Stickers.Count; i++) if (s.Stickers[i].location == StickerLocation.Board) order.Add(i);
            order.Sort((a, b) => { int v = s.Stickers[a].order.CompareTo(s.Stickers[b].order); return v != 0 ? v : a.CompareTo(b); });
            foreach (int i in order) { var view = Instantiate(stickerPrefab, board); view.name = "Sticker " + s.Stickers[i].placement.instanceId; view.Bind(owner, input, i); stickerViews[i] = view; }
        }
        void DrawPiles(LevelDefinition level)
        {
            int number = 0;
            foreach (var pile in level.piles)
            {
                if (trayPrefab != null)
                {
                    var compartment = Instantiate(trayPrefab, board);
                    compartment.Bind(pile); continue;
                }
                number++;
                var tray = UIFactory.Panel("Pile tray " + pile.id, board, pile.size, pile.position, theme != null ? theme.ink : pile.color, theme != null ? theme.trayFrame : panelSprite);
                tray.rectTransform.localRotation = Quaternion.Euler(0, 0, pile.rotation);
                var shadow = tray.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.23f, .1f, .02f, .35f); shadow.effectDistance = new Vector2(3, -9);
                var frame = theme != null ? theme.roundedPanel : panelSprite;
                UIFactory.Panel("Raised rim", tray.transform, pile.size - new Vector2(12, 12), new Vector2(0, 2), theme != null ? theme.trayRim : Color.Lerp(pile.color, Color.white, .7f), frame);
                UIFactory.Panel("Tray well", tray.transform, pile.size - new Vector2(38, 38), Vector2.zero, theme != null ? theme.trayWell : Color.Lerp(pile.color, Color.white, .45f), frame);
                if (theme != null)
                {
                    var tape = UIFactory.Panel("Paper tab", tray.transform, new Vector2(84, 24), new Vector2(-pile.size.x * .24f, pile.size.y * .5f - 1), new Color(1, .89f, .63f, .95f), frame);
                    tape.rectTransform.localRotation = Quaternion.Euler(0, 0, 5);
                }
                UIFactory.Text("Pile label TMP", tray.transform, theme != null ? "PILE " + number.ToString("00") : "STICKER PILE", 16, new Vector2(250, 24), new Vector2(0, -pile.size.y * .5f + 23), theme != null ? theme.muted : new Color(.4f, .34f, .29f, .7f));
            }
        }
        public CategoryCardPresentation CreateCategory(int index, int lane, GameSession s)
        {
            var cat = s.Categories[index];
            var categoryColor = theme != null ? theme.CategoryColor(cat.objective.category.color) : cat.objective.category.color;
            var card = Instantiate(cardPrefab, categories).GetComponent<RectTransform>();
            float height = theme != null ? 344 : 280;
            float headerY = theme != null ? height * .5f - 41 : 100;
            float counterY = theme != null ? -height * .5f + 32 : -110;
            card.name = "Category " + cat.objective.category.title; card.sizeDelta = new Vector2(cardWidth - 14, height);
            card.anchoredPosition = new Vector2((lane - (laneCount - 1) * .5f) * cardWidth, 0);
            card.GetComponent<Image>().color = theme != null ? categoryColor : Color.Lerp(cat.objective.category.color, Color.white, .65f);
            if (theme != null)
            {
                var shadow = card.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.25f, .1f, .01f, .35f); shadow.effectDistance = new Vector2(0, -6);
                UIFactory.Panel("Card paper", card, card.sizeDelta - new Vector2(8, 8), Vector2.zero, new Color(1f, 0.98f, 0.93f, 1f), theme.roundedPanel);
                UIFactory.Panel("Category ribbon", card, new Vector2(cardWidth - 24, 56), new Vector2(0, headerY), categoryColor, theme.roundedPanel);
                if (theme.referenceComposition)
                {
                    // A thin colored cover edge and cream paper: no heavy square target wells.
                    card.Find("Card paper").GetComponent<Image>().color = theme.paper;
                    var rim = UIFactory.Panel("Header highlight", card, new Vector2(cardWidth - 34, 3), new Vector2(0, headerY + 22), new Color(1, 1, 1, .5f), theme.roundedPanel);
                    rim.raycastTarget = false;
                }
            }
            var view = new CategoryCardPresentation { categoryIndex = index, lane = lane, rect = card, group = card.gameObject.AddComponent<CanvasGroup>(),
                targets = new Image[cat.targets.Length], stickers = new int[cat.targets.Length], progressDots = null,
                completedColor = categoryColor };
            var title = UIFactory.Text("Title", card, cat.objective.category.title, theme != null && laneCount == 1 ? 36 : 28, new Vector2(cardWidth - 36, 50), new Vector2(0, headerY));
            if (theme != null) { title.color = Color.white; theme.ApplyTextStyle(title); }
            if (theme != null && s.Stickers.Exists(x => x.placement.special.kind == SpecialKind.Locked && x.placement.special.unlockCondition == UnlockCondition.CategoryCompleted && x.placement.special.dependencyId == cat.objective.category.id))
            {
                var key = UIFactory.Panel("Unlock key", card, new Vector2(38, 38), new Vector2(cardWidth * .5f - 38, headerY), Color.white, theme.keyIcon);
                key.type = Image.Type.Simple; key.preserveAspect = true;
            }
            if (theme != null) UIFactory.Panel("Progress badge", card, new Vector2(106, 38), new Vector2(0, counterY), categoryColor, theme.roundedPanel);
            view.counter = UIFactory.Text("Count", card, "0 / " + cat.targets.Length, 25, new Vector2(110, 36), new Vector2(0, counterY));
            if (theme != null) { view.counter.color = Color.white; theme.ApplyTextStyle(view.counter); }
            int columns = theme != null && laneCount == 1 ? Mathf.Min(4, cat.targets.Length) : 2;
            int rows = Mathf.CeilToInt((float)cat.targets.Length / columns);
            float colWidth = (cardWidth - 36) / columns;
            float rowHeight = theme != null ? 224f / rows : Mathf.Min(86, 172f / rows);
            for (int t = 0; t < cat.targets.Length; t++)
            {
                view.stickers[t] = -1;
                Vector2 pos = new Vector2((t % columns - (columns - 1) * .5f) * colWidth, (rows - 1) * rowHeight * .5f - t / columns * rowHeight + (theme != null ? -8 : 2));
                Vector2 targetSize = new Vector2(colWidth - (theme != null ? 4 : 16), rowHeight - (theme != null ? 4 : 12));
                var def = cat.objective.exactTargets.Count > t ? cat.objective.exactTargets[t] : null;
                Transform parent = card;
                if (theme != null && !theme.referenceComposition)
                {
                    parent = UIFactory.Panel("Slot well " + t, card, new Vector2(colWidth - 2, rowHeight - 2), pos, new Color(0.94f, 0.89f, 0.80f, 1f), theme.slotFrame).transform;
                    pos = Vector2.zero;
                }
                view.targets[t] = UIFactory.Panel("Target " + t, parent, targetSize, pos, new Color(.88f, .74f, .56f, .45f), def != null ? def.ArtworkSprite : theme != null ? theme.collectionIcon : panelSprite);
                view.targets[t].preserveAspect = true;
                if (theme != null && def == null)
                {
                    view.targets[t].color = Color.clear;
                    UIFactory.Text("Empty slot mark", view.targets[t].transform, "+", 36, targetSize, Vector2.zero, new Color(.55f, .31f, .08f, .5f));
                }
                if (def != null)
                {
                    StickerVisuals.Apply(view.targets[t], def);
                    view.targets[t].color = new Color(0.88f, 0.74f, 0.56f, 0.45f);
                    if (theme != null && theme.referenceComposition && def.targetSilhouette != null)
                    { view.targets[t].sprite = def.targetSilhouette; view.targets[t].color = Color.white; }
                    if (def.showLabel) UIFactory.Text("Target label", view.targets[t].transform, def.displayName, 17, targetSize * .95f, Vector2.zero, theme != null ? theme.muted : UIFactory.Ink);
                }
            }
            categoryViews.Add(index, view); return view;
        }
        public void RemoveCategory(int index)
        {
            var view = categoryViews[index]; vacantLanes.Enqueue(view.lane); categoryViews.Remove(index);
            view.rect.gameObject.SetActive(false); Destroy(view.rect.gameObject);
        }
        public CategoryCardPresentation ActivateCategory(int index, GameSession session)
        {
            if (vacantLanes.Count == 0) throw new InvalidOperationException("Cannot show a new category until a completed card has left.");
            return CreateCategory(index, vacantLanes.Dequeue(), session);
        }
        void EnsureBufferSlots(int count)
        {
            while (bufferSlots.Count < count) bufferSlots.Add(Instantiate(slotPrefab, buffer).GetComponent<RectTransform>());
            float width = Mathf.Min(theme != null && theme.referenceComposition ? 176 : theme != null ? 154 : 140, 920f / count);
            for (int i = 0; i < bufferSlots.Count; i++)
            {
                bufferSlots[i].sizeDelta = new Vector2(width - 12, theme != null && theme.referenceComposition ? width - 12 : 124);
                bufferSlots[i].anchoredPosition = new Vector2((i - (count - 1) * .5f) * width, 0);
                if (theme != null)
                {
                    bool overflow = i >= context.Session.Capacity;
                    bufferSlots[i].GetComponent<Image>().color = overflow ? new Color(1, .23f, .12f) : theme.trayRim;
                    var well = bufferSlots[i].Find("Recessed well");
                    if (well != null) well.GetComponent<Image>().color = overflow ? new Color(.62f, .13f, .05f) : theme.trayWell;
                }
            }
        }
        void FillBuffer(int index, int slot, GameSession s)
        {
            var def = s.Stickers[index].placement.definition;
            var img = UIFactory.Panel("Waiting sticker " + index, bufferSlots[slot], bufferSlots[slot].sizeDelta - new Vector2(10, 19), Vector2.zero, def.tint, def.ArtworkSprite);
            StickerVisuals.Apply(img, def);
            if (def.showLabel) UIFactory.Text("Label", img.transform, def.displayName, 22, img.rectTransform.sizeDelta * .9f, Vector2.zero);
            waitingViews.Add(index, img);
        }
        public RectTransform Destination(PuzzleEvent e)
        {
            if (e.kind == PuzzleEventKind.Buffered) { EnsureBufferSlots(Mathf.Max(bufferSlots.Count, e.slot + 1)); return bufferSlots[e.slot]; }
            CategoryCardPresentation card;
            if (!categoryViews.TryGetValue(e.category, out card)) throw new InvalidOperationException("Sticker destination card has not appeared yet: " + e.category);
            return card.targets[e.slot].rectTransform;
        }
        public bool TakeSticker(int index, out Vector3 position, out Vector2 size)
        { float amount, direction; return TakeSticker(index, out position, out size, out amount, out direction); }
        public bool TakeSticker(int index, out Vector3 position, out Vector2 size, out float peeledAmount, out float peelDirection)
        {
            peeledAmount = peelDirection = 0;
            StickerView view; Image waiting;
            if (stickerViews.TryGetValue(index, out view))
            {
                position = view.transform.position; size = StickerPeelLayer.SizeInOverlay(view.Rect, effects);
                if (view.DragPeel != null) { peeledAmount = view.DragPeel.CompletedAmount; peelDirection = view.DragPeel.DirectionDegrees; }
                view.gameObject.SetActive(false); Destroy(view.gameObject); stickerViews.Remove(index); RefreshCoveredStickers(); return true;
            }
            if (waitingViews.TryGetValue(index, out waiting))
            {
                position = waiting.transform.position; size = waiting.rectTransform.sizeDelta;
                waiting.gameObject.SetActive(false); Destroy(waiting.gameObject); waitingViews.Remove(index); return true;
            }
            position = Vector3.zero; size = Vector2.zero; return false;
        }
        public void Land(PuzzleEvent e, GameSession s)
        {
            if (e.kind == PuzzleEventKind.Buffered) FillBuffer(e.sticker, e.slot, s);
            else categoryViews[e.category].Fill(e.slot, e.sticker, s.Stickers[e.sticker].placement.definition);
        }
        public void CompactBuffer(GameSession s)
        {
            UIFactory.Clear(buffer); waitingViews.Clear(); bufferSlots.Clear();
            EnsureBufferSlots(Mathf.Max(s.Capacity, s.Buffer.Count));
            for (int b = 0; b < s.Buffer.Count; b++) FillBuffer(s.Buffer[b], b, s);
        }
    }
}
