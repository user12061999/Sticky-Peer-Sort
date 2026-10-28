using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StickyPeerSort.Editor
{
    // Editor-only repeatable review. Sends real pointer events through the production drag handler.
    public sealed class PeelVisualReviewRecorder
    {
        static PeelVisualReviewRecorder current;
        readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        int lastFrame = -1;
        RecorderController recorder;
        RecorderControllerSettings controllerSettings;
        MovieRecorderSettings movie;
        GameController controller;
        Image touch;
        Texture2D touchTexture;
        Sprite touchSprite;
        StickerView view;
        PointerEventData pointer;
        Vector2 origin;
        float distance;
        int index;
        public static string OutputPath => Path.GetFullPath("Docs/Review/StickerPeelReview_2026-10-28.mp4");

        [MenuItem("Tools/Sticker Category Game/Record Peel Visual Review (Play Mode)")]
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode in SampleStickerLevel first.");
            if (current != null) return;
            current = new PeelVisualReviewRecorder(); current.routines.Push(current.Record());
            EditorApplication.update += current.Tick;
            AssemblyReloadEvents.beforeAssemblyReload += current.Cleanup;
        }
        void Tick()
        {
            if (!Application.isPlaying) { Cleanup(); return; }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                while (routines.Count > 0)
                {
                    var routine = routines.Peek();
                    if (!routine.MoveNext()) { routines.Pop(); continue; }
                    if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                    return;
                }
                Cleanup();
            }
            catch (Exception e) { SessionState.SetString("StickyPeerSort.PeelReview.Status", "Failed: " + e.Message); Cleanup(); Debug.LogException(e); }
        }
        IEnumerator Record()
        {
            SessionState.SetString("StickyPeerSort.PeelReview.Status", "Preparing");
            controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (controller == null || controller.levels.Length != 4) throw new InvalidOperationException("Open SampleStickerLevel for the review recording.");
            controller.LoadLevel(2); yield return null;
            index = FindCat();
            if (index < 0) throw new InvalidOperationException("Level19 needs an exposed cat with a matching active sheet.");
            var t = UIFactory.Rect("Review touch indicator", controller.ui.effects, Vector2.one * 48, Vector2.zero);
            touchTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                float alpha = radius < 23 ? .16f : radius < 28 ? .92f : Mathf.Clamp01(29 - radius) * .92f;
                pixels[y * 64 + x] = new Color(.65f, 1, 1, alpha);
            }
            touchTexture.SetPixels(pixels); touchTexture.Apply();
            touchSprite = Sprite.Create(touchTexture, new Rect(0, 0, 64, 64), Vector2.one * .5f);
            touch = t.gameObject.AddComponent<Image>(); touch.sprite = touchSprite; touch.raycastTarget = false;
            var overlay = t.gameObject.AddComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 100;
            touch.gameObject.SetActive(false);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            movie = ScriptableObject.CreateInstance<MovieRecorderSettings>(); movie.name = "Sticker peel visual review"; movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings { EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High, Codec = CoreEncoderSettings.OutputCodec.MP4 };
            movie.CaptureAudio = false; movie.CaptureAlpha = false;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1080, OutputHeight = 1920 };
            movie.OutputFile = Path.ChangeExtension(OutputPath, null);
            controllerSettings.AddRecorderSettings(movie); controllerSettings.SetRecordModeToManual(); controllerSettings.FrameRate = 30; controllerSettings.CapFrameRate = true;
            recorder = new RecorderController(controllerSettings); recorder.PrepareRecording(); recorder.StartRecording();
            SessionState.SetString("StickyPeerSort.PeelReview.Status", "Recording");
            Caption("1. PARTIAL PEEL / DRAG BACK", "Cyan ring = pointer input replay"); yield return Hold(.8f);
            Press(); yield return Drag(0, .65f, 1.5f); yield return Hold(.7f);
            yield return Drag(.65f, .08f, 1.2f); Release(); yield return Hold(1);
            Caption("2. RELEASE / REATTACH", "The sticker remains above the lower layers");
            Press(); yield return Drag(0, .72f, 1.5f); yield return Hold(.7f); Release(); yield return Hold(1.2f);
            Caption("3. FULL PEEL / COLLECTION", "Subtle outline previews the exact target");
            Press(); yield return Drag(0, .8f, 1.6f); yield return Hold(.7f); yield return Drag(.8f, 1.04f, .65f);
            touch.gameObject.SetActive(false);
            while (controller.IsAnimating) yield return null;
            yield return Hold(1.8f);
            recorder.StopRecording();
            SessionState.SetString("StickyPeerSort.PeelReview.Status", "Complete: " + OutputPath);
            yield return null;
        }
        int FindCat()
        {
            for (int i = 0; i < controller.Session.Stickers.Count; i++)
                if (controller.Session.Stickers[i].placement.definition.id == "animal_01" && controller.Session.Legal(i) && controller.Session.HasTarget(i)) return i;
            return -1;
        }
        void Caption(string title, string detail) { controller.ui.SetInstruction(title + "\n" + detail); }
        void Press()
        {
            view = controller.ui.stickerViews[index]; var s = controller.Session.Stickers[index]; origin = s.position;
            bool found = false;
            for (int y = -3; y <= 3 && !found; y++) for (int x = -3; x <= 3 && !found; x++)
            {
                var point = s.position + new Vector2(x * s.placement.size.x / 12, y * s.placement.size.y / 12);
                if (BoardGeometry.Hit(controller.Session, point) == index) { origin = point; found = true; }
            }
            Vector2 fit = s.placement.definition.FitSize(s.placement.size) * s.placement.scale;
            distance = Mathf.Min(fit.x, fit.y) * controller.balance.dragPeelDistanceRatio;
            pointer = new PointerEventData(EventSystem.current) { pointerId = -12, button = PointerEventData.InputButton.Left };
            SetPointer(0); ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            touch.gameObject.SetActive(true);
        }
        void SetPointer(float amount)
        {
            Vector3 point = controller.ui.board.TransformPoint(origin + Vector2.left * distance * amount);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, point); touch.transform.position = point;
        }
        IEnumerator Drag(float from, float to, float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetPointer(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / seconds)));
                if (view != null && view.gameObject.activeSelf) ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.dragHandler);
                yield return null;
            }
        }
        void Release()
        {
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerUpHandler); touch.gameObject.SetActive(false);
        }
        IEnumerator Hold(float seconds) { float t = 0; while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; } }
        void Cleanup()
        {
            EditorApplication.update -= Tick; AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            if (recorder != null && recorder.IsRecording()) recorder.StopRecording();
            if (view != null && view.DragPeel != null && view.DragPeel.IsPeeling) view.DragPeel.CancelImmediately();
            if (touch != null) UnityEngine.Object.DestroyImmediate(touch.gameObject);
            if (touchSprite != null) UnityEngine.Object.DestroyImmediate(touchSprite);
            if (touchTexture != null) UnityEngine.Object.DestroyImmediate(touchTexture);
            if (controllerSettings != null) UnityEngine.Object.DestroyImmediate(controllerSettings);
            if (movie != null) UnityEngine.Object.DestroyImmediate(movie);
            current = null;
        }
    }
}
