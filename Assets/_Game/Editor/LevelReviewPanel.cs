using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    // Window presentation and scheduling are kept out of the analysis/replay services.
    public sealed class LevelReviewPanel : IDisposable
    {
        readonly Action repaint;
        GameController controller;
        LevelDefinition level;
        string fingerprint;
        LevelAnalysisReport report;
        LevelSolutionReplay replay;
        bool playing, searchInBatch;
        float interval = .7f;
        double nextStep, nextChangeCheck;
        int budget = 12000;
        LevelDefinition[] batch;
        int batchIndex;
        readonly List<AuditRow> rows = new List<AuditRow>();
        bool running;
        string batchStatus;
        sealed class AuditRow { public LevelDefinition level; public LevelAnalysisReport report; public string fingerprint; }

        public LevelReviewPanel(Action repaint) { this.repaint = repaint; LevelPreviewWindow.PreviewRefreshed += OnPreview; }
        void OnPreview(LevelDefinition changed) { if (changed == level) ResetReplay(); }
        static string Fingerprint(LevelDefinition value) { return value == null ? "missing" : EditorJsonUtility.ToJson(value); }
        void ResetReplay()
        {
            playing = false;
            // Render a source-backed model before destroying a temporary replay level.
            if (replay != null && controller != null && level != null && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (LevelAnalysis.Analyze(level).verification != LevelVerification.Invalid)
                    LevelPreviewWindow.DisplaySession(controller, new GameSession(level));
                else LevelPreviewWindow.ClearPreview(controller, "Invalid level — fix its data and preview again.");
            }
            replay?.Dispose(); replay = null;
        }
        void Analyze(bool search)
        {
            ResetReplay();
            report = LevelAnalysis.Analyze(level, search, budget); fingerprint = Fingerprint(level);
            repaint();
        }
        public void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            { playing = false; if (running) { running = false; batchStatus = "Batch paused: run again in Edit Mode."; } return; }
            double now = EditorApplication.timeSinceStartup;
            if (level != null && now >= nextChangeCheck)
            {
                nextChangeCheck = now + .5;
                if (Fingerprint(level) != fingerprint) Analyze(false);
            }
            if (playing && replay != null && now >= nextStep)
            {
                replay.Seek(replay.Step + 1); ShowStep(); nextStep = now + interval;
                if (replay.Step == replay.Count) playing = false;
                repaint();
            }
            // One level per editor update: progress and cancel remain available between checks.
            if (running && batchIndex < batch.Length)
            {
                var item = batch[batchIndex++];
                rows.Add(new AuditRow { level = item, report = LevelAnalysis.Analyze(item, searchInBatch, budget), fingerprint = Fingerprint(item) });
                batchStatus = "Checked " + batchIndex + " / " + batch.Length;
                if (batchIndex == batch.Length) { running = false; batchStatus += " — complete"; }
                repaint();
            }
        }
        void ShowStep()
        {
            if (replay == null || controller == null) return;
            LevelPreviewWindow.DisplaySession(controller, replay.Session);
            if (replay.NextSticker >= 0 && controller.ui.stickerViews.TryGetValue(replay.NextSticker, out var view))
                view.image.color = new Color(1, .85f, .4f);
            controller.ui.SetInstruction("SOLUTION " + replay.Step + "/" + replay.Count + " | " + StepSummary());
        }
        string StepSummary()
        {
            if (replay == null || replay.Step == 0) return "Start — gold tint marks the next sticker.";
            return string.Join(" · ", replay.Events.Select(e =>
            {
                string sticker = e.sticker >= 0 ? replay.Session.Stickers[e.sticker].placement.instanceId : "";
                string category = e.category >= 0 ? replay.Session.Categories[e.category].objective.category.title : "";
                switch (e.kind)
                {
                    case PuzzleEventKind.Peel: return "Pick " + sticker;
                    case PuzzleEventKind.Buffered: return "Buffer + " + sticker;
                    case PuzzleEventKind.Collected: return "Place → " + category + " #" + (e.slot + 1);
                    case PuzzleEventKind.CategoryCompleted: return "Complete " + category;
                    case PuzzleEventKind.CategoryActivated: return "Open " + category;
                    case PuzzleEventKind.Unlocked: return "Unlock " + sticker;
                    case PuzzleEventKind.Revealed: return "Reveal " + sticker;
                    case PuzzleEventKind.IceBroken: return "Break ice " + sticker;
                    default: return e.kind.ToString();
                }
            }));
        }
        public void Draw(GameController owner, LevelDefinition selected)
        {
            if (level != selected || controller != owner)
            {
                ResetReplay(); level = selected; controller = owner; Analyze(false);
                if (report.verification != LevelVerification.Invalid && !EditorApplication.isPlayingOrWillChangePlaymode)
                    LevelPreviewWindow.DisplaySession(controller, new GameSession(level));
            }
            else if (!EditorApplication.isPlayingOrWillChangePlaymode && Fingerprint(level) != fingerprint) Analyze(false);
            EditorGUILayout.LabelField("Level Analysis & Solution", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || running))
            {
                budget = Mathf.Clamp(EditorGUILayout.IntField("Solver node budget", budget), 1, 100000);
                if (GUILayout.Button("Analyze & Find Winning Path")) Analyze(true);
            }
            if (report != null)
            {
                EditorGUILayout.LabelField("Verification", report.verification.ToString());
                EditorGUILayout.LabelField("Initially clickable / covered", report.initiallyClickable + " / " + report.initiallyCovered + " (of " + report.stickers + ")");
                EditorGUILayout.LabelField("Maximum overlap chain", report.maxOverlapDepth + " layers");
                EditorGUILayout.LabelField("Locked / Mystery", report.locked + " / " + report.mystery);
                if (report.Verified)
                {
                    EditorGUILayout.LabelField("Winning actions", report.solution.Count + " — " + report.solutionSource);
                    EditorGUILayout.LabelField("Peak resolved buffer", report.peakResolvedBuffer + " / " + level.bufferCapacity + (report.peakResolvedBuffer > 0 ? " (first at step " + report.firstPeakStep + ")" : " — not used"));
                    EditorGUILayout.LabelField("Transient buffer peak", report.peakTransientBuffer.ToString());
                    EditorGUILayout.HelpBox("Buffer peaks belong to this winning path, not a proven minimum. Full-buffer loss is checked after automatic transfers. Overlap depth is the longest cover chain, not the number of images at one point.", MessageType.Info);
                }
                foreach (string message in report.messages) EditorGUILayout.HelpBox(message, report.verification == LevelVerification.Invalid || report.verification == LevelVerification.NoSolution ? MessageType.Error : MessageType.Warning);
                if (report.verification == LevelVerification.Unverified) EditorGUILayout.HelpBox("No verified path yet. Analyze & Find Winning Path can search within the node budget.", MessageType.Info);
            }
            using (new EditorGUI.DisabledScope(report == null || !report.Verified || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Load Solution Preview")) { ResetReplay(); replay = new LevelSolutionReplay(level, report.solution); ShowStep(); }
                if (replay != null)
                {
                    interval = EditorGUILayout.Slider("Seconds per action", interval, .15f, 2);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Reset")) { playing = false; replay.Seek(0); ShowStep(); }
                    if (GUILayout.Button("Previous")) { playing = false; replay.Seek(replay.Step - 1); ShowStep(); }
                    if (GUILayout.Button(playing ? "Pause" : "Play Solution"))
                    { if (replay.Step == replay.Count) replay.Seek(0); playing = !playing; nextStep = EditorApplication.timeSinceStartup + interval; ShowStep(); }
                    if (GUILayout.Button("Next")) { playing = false; replay.Seek(replay.Step + 1); ShowStep(); }
                    EditorGUILayout.EndHorizontal();
                    int step = EditorGUILayout.IntSlider("Action", replay.Step, 0, replay.Count);
                    if (step != replay.Step) { playing = false; replay.Seek(step); ShowStep(); }
                    EditorGUILayout.LabelField("Buffer / State", replay.Session.Buffer.Count + " / " + replay.Session.Capacity + " — " + replay.Session.State);
                    EditorGUILayout.HelpBox(StepSummary(), MessageType.None);
                }
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Batch check — current scene", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(running || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                searchInBatch = EditorGUILayout.Toggle("Search if trace is invalid", searchInBatch);
                if (GUILayout.Button("Check All Scene Levels"))
                {
                    playing = false; batch = owner.levels.ToArray(); batchIndex = 0; rows.Clear(); running = batch.Length > 0; batchStatus = "Checking…";
                }
            }
            if (running && GUILayout.Button("Cancel Batch")) { running = false; batchStatus += " — cancelled; remaining levels unchecked"; }
            if (!string.IsNullOrEmpty(batchStatus)) EditorGUILayout.LabelField(batchStatus, EditorStyles.wordWrappedLabel);
            foreach (var row in rows)
            {
                bool stale = Fingerprint(row.level) != row.fingerprint;
                string title = row.level == null ? "Missing asset" : "Level " + row.level.levelNumber;
                if (GUILayout.Button(title + " | " + row.report.verification + (stale ? " | CHANGED — recheck" : "") + (row.report.Verified ? " | buffer peak " + row.report.peakResolvedBuffer : "")))
                { Selection.activeObject = row.level; if (row.level != null) EditorGUIUtility.PingObject(row.level); }
                if (!row.report.Verified) foreach (string message in row.report.messages) EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
            }
        }
        public void Dispose() { LevelPreviewWindow.PreviewRefreshed -= OnPreview; running = false; ResetReplay(); }
    }
}
