using System;
using System.Collections;
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
        public PeelFeedback PeelFeel { get; set; }
        public Sprite panelSprite;
        public GameplayUITheme theme;
        public TextMeshProUGUI undoCount, hintCount, extraCount, shuffleCount;
        public Image albumFill, modalIcon;
        public Image modalRibbon;   // GUI Pro title ribbon behind the popup title; swapped to the red ribbon for failures
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
        bool Scrap => theme != null && theme.Scrapbook;
        Color SlotColor => Scrap ? Color.white : theme != null ? theme.trayRim : Color.white;
        Color OverflowColor => Scrap ? new Color(1, .55f, .5f) : new Color(1, .23f, .12f);
        public void Initialize(IGameplayReadModel context, IStickerInput input) { this.context = context; this.input = input; ApplyReadability(); }
        // Game feel: the 24 px booster captions and 17 px RESTART were unreadable on phones; enlarge them in place.
        void ApplyReadability()
        {
            if (Scrap) return;   // the scrapbook layout sets mockup text sizes in the scene
            foreach (var button in new[] { undo, hint, extra, shuffle })
            {
                if (button == null) continue;
                foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (label.name != "Label") continue;
                    label.enableAutoSizing = true; label.fontSizeMin = 26; label.fontSizeMax = 34; label.fontStyle |= FontStyles.Bold;
                    label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, Mathf.Max(label.rectTransform.sizeDelta.y, 50));
                }
            }
            if (restart != null)   // the RESTART pill sits between the booster captions, so only its text grows, not the pill
                foreach (var label in restart.GetComponentsInChildren<TextMeshProUGUI>(true))
                { label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 24; label.fontStyle |= FontStyles.Bold; }
        }
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
        public void SetInstruction(string message) { if (instruction != null) instruction.text = message; }
        public void Notify(string message) { SetInstruction(message); PeelSignals.Say(message); }
        public void SynchronizeAfterMoves() { CompactBuffer(context.Session); UpdateHud(context); UpdateTrayInstruction(); RefreshCoveredStickers(); }
        void UpdateTrayInstruction()
        {
            if (instruction == null) return;
            var s = context.Session;
            instruction.text = s.Buffer.Count >= s.Capacity ? "Waiting tray full! Undo or retry with a different order."
                : s.Buffer.Count > 0 && s.Buffer.Count == s.Capacity - 1 ? "Only one waiting slot left. Plan your next sticker."
                : s.Level.tutorial;
            if (context.Balance.dragToPeel) instruction.text += "\nDrag to peel; release early to reattach.";
        }
        public void UpdateTrayStatus(int count, int limit)
        {
            if (capacity != null)
            {
                capacity.text = (count >= limit ? "TRAY FULL   " : "WAITING TRAY   ") + count + " / " + limit;
                capacity.color = count >= limit ? new Color(.65f, .12f, .04f)
                    : count > 0 && count == limit - 1 ? new Color(.59f, .28f, .02f) : theme != null ? theme.muted : UIFactory.Ink;
            }
            if (trayPanel != null) trayPanel.color = count >= limit ? new Color(1, .69f, .42f) : new Color(1, .84f, .49f);
            WarnLastSlot(count, limit);
        }
        // Game feel: one free holding slot left → the free slot pulses red, a soft warning sound and a double tap of haptics, once per new count.
        int warnedCount = -1; Coroutine warning;
        void WarnLastSlot(int count, int limit)
        {
            bool danger = count > 0 && count == limit - 1;
            if (!danger) { warnedCount = -1; return; }
            if (warnedCount == count || !isActiveAndEnabled) return;
            warnedCount = count;
            AudioFeedback?.Reject();
            if (context != null && context.Balance.haptics) { GameHaptics.Pulse(14, 150, true); StartCoroutine(DelayedPulse()); }
            if (warning == null) warning = StartCoroutine(PulseLastSlot());
        }
        IEnumerator DelayedPulse() { yield return new WaitForSecondsRealtime(.09f); GameHaptics.Pulse(14, 150, true); }
        IEnumerator PulseLastSlot()
        {
            float t = 0;
            while (context != null && context.Session != null && context.Session.Buffer.Count == context.Session.Capacity - 1 && context.Session.Buffer.Count > 0)
            {
                t += Time.unscaledDeltaTime; int free = context.Session.Buffer.Count;
                if (free < bufferSlots.Count && bufferSlots[free] != null && bufferSlots[free].TryGetComponent<Image>(out var slot))
                    slot.color = Color.Lerp(SlotColor, new Color(1, .25f, .15f), .5f + .5f * Mathf.Sin(t * 9));
                yield return null;
            }
            for (int i = 0; i < bufferSlots.Count; i++)
                if (bufferSlots[i] != null && bufferSlots[i].TryGetComponent<Image>(out var slot) && context != null && context.Session != null)
                    slot.color = i >= context.Session.Capacity ? OverflowColor : theme != null ? SlotColor : slot.color;
            warning = null;
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
            SetInstruction(BoardGeometry.Covered(context.Session, index) ? "Remove the sticker on top first." : "Sticker blocked. Check its lock or linked partner.");
        }
        public void HighlightSticker(int index)
        {
            UpdateHud(context);
            foreach (var entry in stickerViews) entry.Value.Bind(context, input, entry.Key);
            if (!stickerViews.TryGetValue(index, out var view)) return;
            view.image.color = Color.white; SetInstruction("Try the highlighted " + context.Session.Stickers[index].placement.definition.displayName + " sticker.");
            PeelSignals.Hint(index);
        }
        // The dim layer lives inside the 1080-wide portrait layout; stretch it over the whole canvas (tall screens, notches) so no band stays undimmed.
        static void CoverWholeScreen(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>(); var parent = rect.parent as RectTransform;
            if (canvas == null || parent == null) return;
            var root = (RectTransform)canvas.rootCanvas.transform; var corners = new Vector3[4]; root.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]), max = parent.InverseTransformPoint(corners[2]);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var centre = (min + max) * .5f - parent.rect.center;
            rect.sizeDelta = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y)) + Vector2.one * 4;
            rect.anchoredPosition = centre;
        }
        public void ShowModal(string title, string body, string a, Action actionA, string b, Action actionB)
        {
            StickerBackingPaper.Clear(effects);
            modal.SetActive(true); CoverWholeScreen((RectTransform)modal.transform); modalTitle.text = title; modalBody.text = body;
            if (theme != null && modalIcon != null)
            {
                modalIcon.sprite = title.Contains("COMPLETE") ? theme.starIcon : title.Contains("FULL") || title.Contains("OVERFLOW") || title.Contains("LEFT") ? theme.failIcon : theme.settingsIcon;
                modalIcon.preserveAspect = true;
            }
            if (theme != null && modalRibbon != null && theme.titleRibbon != null)
            {
                bool failure = title.Contains("FULL") || title.Contains("OVERFLOW") || title.Contains("LEFT");
                modalRibbon.sprite = failure && theme.failRibbon != null ? theme.failRibbon : theme.titleRibbon;
            }
            primary.onClick.RemoveAllListeners(); secondary.onClick.RemoveAllListeners();
            primary.GetComponentInChildren<TextMeshProUGUI>().text = a; secondary.GetComponentInChildren<TextMeshProUGUI>().text = b;
            primary.onClick.AddListener(() => actionA()); secondary.onClick.AddListener(() => actionB());
            if (Scrap) SkinScrapbookModal(title, a, b);
        }
        // Scrapbook GUI v01 has no popup art, so popups are built from the same parts as the board: a cream collection sheet
        // with the tan rim, a tinted header band (mint = win, coral = fail, blue = pause/restart), ink text and pill buttons.
        // TRAY FULL shows the actual full waiting row in mini holding slots.
        void SkinScrapbookModal(string title, string a, string b)
        {
            bool win = title.Contains("COMPLETE"), fail = title.Contains("FULL") || title.Contains("OVERFLOW") || title.Contains("LEFT");
            Color mint = new Color32(0x4F, 0xC7, 0x9A, 255), coral = new Color32(0xF9, 0x7F, 0x6B, 255), blue = new Color32(0x5B, 0x9B, 0xE6, 255);
            var dim = modal.GetComponent<Image>(); if (dim != null) dim.color = new Color(.08f, .12f, .24f, .66f);
            var frame = modalTitle.transform.parent as RectTransform; const float W = 840, H = 820;
            var frameImage = frame.GetComponent<Image>(); frame.sizeDelta = new Vector2(W, H);
            if (frameImage != null) { Sliced(frameImage, theme.sheetBase, theme.scrapbookRim, 1f); SoftShadow(frameImage, 16, .22f); }
            var paper = frame.Find("Modal paper") as RectTransform;
            if (paper != null) { paper.sizeDelta = new Vector2(W - 28, H - 28); paper.anchoredPosition = Vector2.zero; var sheet = paper.GetComponent<Image>(); sheet.enabled = true; Sliced(sheet, theme.sheetBase, Color.white, 1.05f); }
            var halo = frame.Find("Badge halo"); if (halo != null) halo.gameObject.SetActive(false);
            if (modalRibbon != null)
            {
                Sliced(modalRibbon, theme.headerBase, win ? mint : fail ? coral : blue, 1.05f);
                modalRibbon.rectTransform.sizeDelta = new Vector2(680, 128); modalRibbon.rectTransform.anchoredPosition = new Vector2(0, H * .5f - 6);
                SoftShadow(modalRibbon, 8, .24f);
            }
            ScrapText(modalTitle, Color.white, 60, new Vector2(620, 104), new Vector2(0, H * .5f - 6)); modalTitle.enableWordWrapping = false;
            ScrapText(modalBody, theme.scrapbookInk, 40, new Vector2(720, 150), new Vector2(0, 40));
            var row = frame.Find("Scrapbook full tray") as RectTransform;
            if (row == null) { row = new GameObject("Scrapbook full tray", typeof(RectTransform)).GetComponent<RectTransform>(); row.SetParent(frame, false); }
            UIFactory.Clear(row); row.anchoredPosition = new Vector2(0, 222); row.sizeDelta = new Vector2(W - 80, 170);
            var s = context != null ? context.Session : null;
            bool showRow = title.Contains("FULL") && s != null && s.Buffer.Count > 0;
            row.gameObject.SetActive(showRow);
            if (showRow)
            {
                int count = Mathf.Max(s.Capacity, s.Buffer.Count); float pitch = Mathf.Min(170, (W - 80) / count), side = pitch - 14;
                for (int i = 0; i < count; i++)
                {
                    var slot = UIFactory.Panel("Mini slot " + i, row, new Vector2(side, side * 205f / 211f), new Vector2((i - (count - 1) * .5f) * pitch, 0), Color.white, theme.holdingSlot);
                    Sliced(slot, theme.holdingSlot, Color.white, 1.82f * 211f / side); SoftShadow(slot, 5, .14f);
                    if (i >= s.Buffer.Count) continue;
                    var def = s.Stickers[s.Buffer[i]].placement.definition;
                    var art = UIFactory.Panel("Sticker", slot.transform, slot.rectTransform.sizeDelta * .82f, Vector2.zero, def.tint, def.ArtworkSprite);
                    StickerVisuals.Apply(art, def);
                }
            }
            if (modalIcon != null)
            {
                modalIcon.gameObject.SetActive(!showRow);
                modalIcon.sprite = win ? theme.popupCoinIcon : title.Contains("UNDO") ? theme.popupUndoIcon : title.Contains("SLOT") ? theme.popupSlotIcon : theme.popupSettingsIcon;
                if (modalIcon.sprite == null) modalIcon.sprite = theme.settingsIcon;
                modalIcon.color = Color.white; modalIcon.preserveAspect = true;
                modalIcon.rectTransform.sizeDelta = Vector2.one * 190; modalIcon.rectTransform.anchoredPosition = new Vector2(0, 222);
            }
            ScrapPill(primary, a == "UNDO" ? blue : a == "RESTART" ? coral : mint, Color.white, new Vector2(560, 124), new Vector2(0, -158), 48);
            bool boosterB = b == "UNDO" || b == "+ SLOT";
            ScrapPill(secondary, b == "UNDO" ? blue : b == "+ SLOT" ? mint : theme.scrapbookPill, boosterB ? Color.white : theme.scrapbookInk, new Vector2(500, 106), new Vector2(0, -296), 42);
            if (sound != null) sound.gameObject.SetActive(title == "PAUSED");   // sound switch belongs to the pause menu only
            if (sound != null && title == "PAUSED") ScrapPill(sound, theme.scrapbookPill, theme.scrapbookInk, new Vector2(210, 74), new Vector2(W * .5f - 145, H * .5f - 120), 30);
        }
        void ScrapText(TMP_Text text, Color color, float size, Vector2 box, Vector2 position)
        {
            if (text == null) return;
            if (theme.font != null) { text.font = theme.font; text.fontSharedMaterial = theme.font.material; }
            text.color = color; text.fontStyle = FontStyles.Bold; text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = size * .55f;
            text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = box; text.rectTransform.anchoredPosition = position;
        }
        void ScrapPill(Button button, Color fill, Color ink, Vector2 size, Vector2 position, float fontSize)
        {
            var rect = (RectTransform)button.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
            var image = button.GetComponent<Image>();
            if (image != null) { Sliced(image, theme.pillBase, fill, 2.6f * 66f / size.y); SoftShadow(image, 7, .2f); }
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) ScrapText(text, ink, fontSize, size - new Vector2(40, 16), Vector2.zero);
        }
        public void ApplySafeArea()
        {
            if (Screen.width == 0 || Screen.height == 0) return;
            var area = Screen.safeArea; safeArea.anchorMin = area.min / new Vector2(Screen.width, Screen.height); safeArea.anchorMax = area.max / new Vector2(Screen.width, Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero; Canvas.ForceUpdateCanvases();
            if (safeArea.childCount > 0) safeArea.GetChild(0).localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(safeArea.rect.width / 1080f, safeArea.rect.height / 1920f));
            // Scrapbook layout: on screens taller than 9:16 the layout grows to the full height, so the top-anchored HUD/cards/slots
            // hug the top and the bottom-anchored boosters hug the bottom (as in the mockup) instead of floating with empty bands.
            if (Scrap && safeArea.childCount > 0 && safeArea.GetChild(0) is RectTransform layout)
                layout.sizeDelta = new Vector2(1080, Mathf.Max(1920, safeArea.rect.height / Mathf.Max(.01f, layout.localScale.y)));
        }
        public void UpdateHud(IGameplayReadModel owner)
        {
            var s = owner.Session;
            levelLabel.text = "Level " + (owner is GameplayContext live && live.DisplayLevel > 0 ? live.DisplayLevel : s.Level.levelNumber); coins.text = theme != null ? owner.Progress.coins.ToString() : "COINS  " + owner.Progress.coins;
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
            var s = owner.Session; ApplySafeArea(); UpdateHud(owner); SetInstruction(s.Level.tutorial);
            UpdateTrayInstruction();
            UIFactory.Clear(categories); UIFactory.Clear(buffer); UIFactory.Clear(board);
            stickerViews.Clear(); categoryViews.Clear(); waitingViews.Clear(); bufferSlots.Clear(); vacantLanes.Clear();
            laneCount = Mathf.Min(s.Level.activeCategoryCount, s.Categories.Count);
            cardWidth = Scrap ? Mathf.Min(434, 1040f / Mathf.Max(1, laneCount)) : theme != null && laneCount == 1 ? 620 : Mathf.Min(theme != null ? 310 : 300, 940f / Mathf.Max(1, laneCount));
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
            if (Scrap) FitScrapbookBoard(level);
            int number = 0, pileIndex = -1;
            foreach (var pile in level.piles)
            {
                pileIndex++;
                if (trayPrefab != null)
                {
                    var compartment = Instantiate(trayPrefab, board);
                    compartment.Bind(pile); if (Scrap) SkinScrapbookTray(compartment, level, pileIndex); continue;
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
        // Scrapbook GUI v01: the paper strip replaces the wooden tray layers (children hidden, prefab untouched).
        // Scrapbook GUI v01 (owner 11/11: keep the authored arrangement, spread over the board, big stickers): the whole board is
        // scaled uniformly to fill the space between the holding slots and the boosters, so overlaps (and the cover rule) stay exact.
        float scrapbookBoardScale = .8f;
        void FitScrapbookBoard(LevelDefinition level)
        {
            var layout = safeArea != null && safeArea.childCount > 0 ? safeArea.GetChild(0) as RectTransform : null;
            float half = layout != null ? Mathf.Max(960, layout.sizeDelta.y * .5f) : 960;
            float top = half - (960 - 148) - 105 - 12, bottom = -half + (960 - 786) + 94 + 12;   // slot row bottom, booster top
            var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min;
            foreach (var pile in level.piles) { min = Vector2.Min(min, pile.position - pile.size * .5f); max = Vector2.Max(max, pile.position + pile.size * .5f); }
            foreach (var p in level.stickers)
            {
                var extent = p.size * Mathf.Max(p.scale, level.boardStickerSizeMultiplier) * .45f;   // art sits inside a padded rect
                min = Vector2.Min(min, p.position - extent); max = Vector2.Max(max, p.position + extent);
            }
            var size = max - min; if (size.x <= 0 || size.y <= 0) return;
            float s = Mathf.Min((top - bottom) / size.y, 1060f / size.x, 1.3f);
            scrapbookBoardScale = s; board.localScale = Vector3.one * s;
            var centre = (min + max) * .5f;
            board.anchoredPosition = new Vector2(-centre.x * s, (top + bottom) * .5f - centre.y * s);
        }
        // Papers reach the board width (1060 px on screen) on each side with no neighbouring pile; only the paper grows.
        float ScrapbookPaperEdge => 530f / Mathf.Max(.01f, scrapbookBoardScale);
        void SkinScrapbookTray(StickerTrayView tray, LevelDefinition level, int index)
        {
            if (tray.TryGetComponent<Image>(out var paper))
            { paper.sprite = theme.stickerSurface; paper.color = Color.white; paper.type = Image.Type.Sliced; paper.fillCenter = true; paper.pixelsPerUnitMultiplier = 1; SoftShadow(paper, 9, .14f); }
            foreach (Transform layer in tray.transform) layer.gameObject.SetActive(false);
            var pile = level.piles[index]; if (Mathf.Abs(pile.rotation) > .01f) return;
            bool freeLeft = true, freeRight = true;
            for (int i = 0; i < level.piles.Count; i++)
            {
                var other = level.piles[i];
                if (i == index || Mathf.Abs(other.position.y - pile.position.y) >= (other.size.y + pile.size.y) * .5f) continue;
                if (other.position.x < pile.position.x) freeLeft = false; else if (other.position.x > pile.position.x) freeRight = false;
            }
            float left = pile.position.x - pile.size.x * .5f, right = pile.position.x + pile.size.x * .5f;
            if (freeLeft) left = Mathf.Min(left, -ScrapbookPaperEdge);
            if (freeRight) right = Mathf.Max(right, ScrapbookPaperEdge);
            var rect = (RectTransform)tray.transform;
            rect.sizeDelta = new Vector2(right - left, pile.size.y); rect.anchoredPosition = new Vector2((left + right) * .5f, pile.position.y);
        }
        // Two stacked uGUI shadows read as one soft drop shadow (no blur shader needed). Idempotent.
        static void SoftShadow(Graphic graphic, float distance, float alpha)
        {
            foreach (var old in graphic.GetComponents<Shadow>()) if (old.GetType() == typeof(Shadow)) DestroyImmediate(old);
            var near = graphic.gameObject.AddComponent<Shadow>(); near.effectColor = new Color(.24f, .32f, .42f, alpha); near.effectDistance = new Vector2(0, -distance * .45f);
            var far = graphic.gameObject.AddComponent<Shadow>(); far.effectColor = new Color(.24f, .32f, .42f, alpha * .55f); far.effectDistance = new Vector2(0, -distance);
        }
        static Image Sliced(Image image, Sprite sprite, Color color, float multiplier)
        {
            image.sprite = sprite; image.color = color; image.type = Image.Type.Sliced; image.fillCenter = true;
            image.pixelsPerUnitMultiplier = multiplier; return image;
        }
        // Scrapbook GUI v01 collection sheet, laid out at the mockup's 415x497 and scaled down when 3+ cards share the row:
        // sheet base, tinted header, grey alpha silhouettes, count pill with dark text.
        CategoryCardPresentation CreateScrapbookCategory(int index, int lane, GameSession s)
        {
            var cat = s.Categories[index]; var category = cat.objective.category;
            float k = Mathf.Min(1, (cardWidth - 19) / 415f);
            var tint = theme.HeaderTint(category.title, category.color);
            var card = Instantiate(cardPrefab, categories).GetComponent<RectTransform>();
            card.name = "Category " + category.title; card.sizeDelta = new Vector2(415, 497) * k;
            card.anchoredPosition = new Vector2((lane - (laneCount - 1) * .5f) * cardWidth, 0);
            // Mockup look: tan outer rim, cream sheet inset 6 px, soft drop shadow; header and count pill cast a shadow on the sheet.
            var rim = Sliced(card.GetComponent<Image>(), theme.sheetBase, theme.scrapbookRim, 1.234f / k);
            SoftShadow(rim, 10 * k, .16f);
            Sliced(UIFactory.Panel("Card sheet", card, (new Vector2(415, 497) - new Vector2(12, 12)) * k, Vector2.zero, Color.white, theme.sheetBase), theme.sheetBase, Color.white, 1.3f / k).raycastTarget = false;
            var header = Sliced(UIFactory.Panel("Category header", card, new Vector2(385, 95) * k, new Vector2(0, 186) * k, tint, theme.headerBase), theme.headerBase, tint, 1.33f / k);
            header.raycastTarget = false; SoftShadow(header, 6 * k, .22f);
            var view = new CategoryCardPresentation { categoryIndex = index, lane = lane, rect = card, group = card.gameObject.AddComponent<CanvasGroup>(),
                targets = new Image[cat.targets.Length], stickers = new int[cat.targets.Length], progressDots = null, completedColor = tint };
            var title = UIFactory.Text("Title", card, category.title, 56 * k, new Vector2(350, 80) * k, new Vector2(0, 188) * k, Color.white);
            title.enableAutoSizing = true; title.fontSizeMin = 24 * k; title.fontSizeMax = 56 * k; title.fontStyle = TMPro.FontStyles.Bold;
            if (title.font != null) title.fontSharedMaterial = title.font.material;   // flat white like the mockup, no outline preset
            if (s.Stickers.Exists(x => x.placement.special.kind == SpecialKind.Locked && x.placement.special.unlockCondition == UnlockCondition.CategoryCompleted && x.placement.special.dependencyId == category.id))
            {
                var key = UIFactory.Panel("Unlock key", card, new Vector2(52, 52) * k, new Vector2(415 * .5f - 30, 230) * k, Color.white, theme.keyIcon);
                key.type = Image.Type.Simple; key.preserveAspect = true;
            }
            var pill = Sliced(UIFactory.Panel("Count pill", card, new Vector2(215, 66) * k, new Vector2(0, -175) * k, theme.scrapbookPill, theme.pillBase), theme.pillBase, theme.scrapbookPill, 2.6f / k);
            pill.raycastTarget = false; SoftShadow(pill, 5 * k, .2f);
            view.counter = UIFactory.Text("Count", card, "0 / " + cat.targets.Length, 48 * k, new Vector2(200, 62) * k, new Vector2(0, -175) * k, theme.scrapbookInk);
            view.counter.fontStyle = TMPro.FontStyles.Bold; if (view.counter.font != null) view.counter.fontSharedMaterial = view.counter.font.material;
            int n = cat.targets.Length, columns = n <= 1 ? 1 : 2, rows = Mathf.CeilToInt((float)n / columns);
            float rowPitch = Mathf.Min(123, 270f / rows), side = Mathf.Min(137, rowPitch + 14);
            for (int t = 0; t < n; t++)
            {
                view.stickers[t] = -1;
                int row = t / columns, inRow = Mathf.Min(columns, n - row * columns);
                var pos = new Vector2((t % columns - (inRow - 1) * .5f) * 178, -12.5f + ((rows - 1) * .5f - row) * rowPitch) * k;
                var size = new Vector2(side, side) * k;
                var def = cat.objective.exactTargets.Count > t ? cat.objective.exactTargets[t] : null;
                var target = UIFactory.Panel("Target " + t, card, size, pos, theme.silhouetteColor, def != null ? def.ArtworkSprite : theme.collectionIcon);
                view.targets[t] = target;
                if (def == null)
                {
                    target.color = Color.clear;
                    UIFactory.Text("Empty slot mark", target.transform, "+", 48 * k, size, Vector2.zero, theme.silhouetteColor);
                    continue;
                }
                StickerVisuals.Apply(target, def);
                if (def.targetSilhouette != null) target.sprite = def.targetSilhouette;
                target.material = theme.silhouetteMaterial; target.color = theme.silhouetteColor;
                if (def.showLabel) UIFactory.Text("Target label", target.transform, def.displayName, 17 * k, size * .95f, Vector2.zero, theme.scrapbookInk);
            }
            categoryViews.Add(index, view); return view;
        }
        public CategoryCardPresentation CreateCategory(int index, int lane, GameSession s)
        {
            if (Scrap) return CreateScrapbookCategory(index, lane, s);
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
            if (theme != null && theme.HasSkin) SkinCard(card, categoryColor);
            categoryViews.Add(index, view); return view;
        }
        // GUI Pro-SuperCasual look: black-outlined category frame, cream paper inset, outlined header tab and progress pill.
        void SkinCard(RectTransform card, Color color)
        {
            UISkin.Apply(card.GetComponent<Image>(), theme.outlinePanel, color);
            // The colored frame itself is the header band; the paper sits under it, its corners concentric with the frame.
            const float inset = 11, header = 66;
            var paper = card.Find("Card paper") as RectTransform;
            if (paper != null)
            {
                paper.sizeDelta = new Vector2(card.sizeDelta.x - inset * 2, card.sizeDelta.y - header - inset);
                paper.anchoredPosition = new Vector2(0, -(header - inset) * .5f);
                var paperImage = UISkin.Apply(paper.GetComponent<Image>(), theme.innerPanel != null ? theme.innerPanel : theme.roundedPanel, theme.paper);
                UISkin.Concentric(card.GetComponent<Image>(), paperImage, inset);
            }
            var ribbon = card.Find("Category ribbon"); if (ribbon != null) ribbon.GetComponent<Image>().enabled = false;
            var badge = card.Find("Progress badge"); if (badge != null) UISkin.Apply(badge.GetComponent<Image>(), theme.outlinePanel, color);
            var glare = card.Find("Header highlight"); if (glare != null) glare.gameObject.SetActive(false);
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
            if (Scrap) { LayoutScrapbookSlots(count); return; }
            float width = Mathf.Min(theme != null && theme.referenceComposition ? 176 : theme != null ? 154 : 140, 920f / count);
            for (int i = 0; i < bufferSlots.Count; i++)
            {
                float gap = theme != null && theme.referenceComposition ? 24 : 12;
                float side = theme != null && theme.HasSkin ? Mathf.Min(width - gap, 132) : width - gap;   // GUI Pro skin: slots sit inside the 164 px tray with a margin
                bufferSlots[i].sizeDelta = new Vector2(side, theme != null && theme.referenceComposition ? side : 124);
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
        // Scrapbook GUI v01: shallow paper slots, 211x205 at a 238 px pitch in the mockup; narrower when 5+ slots must fit 1040 px.
        void LayoutScrapbookSlots(int count)
        {
            float pitch = Mathf.Min(238, 1040f / Mathf.Max(1, count)), w = Mathf.Min(211, pitch - 16), h = w * 205f / 211f;
            for (int i = 0; i < bufferSlots.Count; i++)
            {
                var slot = bufferSlots[i];
                slot.sizeDelta = new Vector2(w, h); slot.anchoredPosition = new Vector2((i - (count - 1) * .5f) * pitch, 0);
                var well = Sliced(slot.GetComponent<Image>(), theme.holdingSlot, i >= context.Session.Capacity ? OverflowColor : Color.white, 1.82f * 211f / w);
                if (well.GetComponents<Shadow>().Length == 0) SoftShadow(well, 7, .12f);
                foreach (var layer in new[] { "Inset edge", "Recessed well", "Top bevel" })
                { var child = slot.Find(layer); if (child != null) child.gameObject.SetActive(false); }
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
