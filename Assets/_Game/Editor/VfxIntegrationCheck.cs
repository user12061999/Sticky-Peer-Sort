using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StickyPeerSort.Editor
{
    [InitializeOnLoad]
    public static class VfxIntegrationCheck
    {
        const string Request = "Temp/CheckStickerVfx.request";
        const string Report = "Temp/StickerVfxValidation.txt";
        static VfxIntegrationCheck() { EditorApplication.delayCall += RunRequested; }
        static void RunRequested()
        {
            if (File.Exists(Request)) { File.Delete(Request); Validate(); }
            if (EditorApplication.isPlaying && File.Exists("Temp/PreviewStickerVfx.request"))
            { File.Delete("Temp/PreviewStickerVfx.request"); Preview(); }
        }
        static double next;
        static int stage;
        static string original;
        static readonly StringBuilder live = new StringBuilder();
        [MenuItem("Tools/Sticker Sort/Preview Imported VFX (Play Mode)")]
        public static void Preview()
        {
            if (!EditorApplication.isPlaying) { Debug.Log("Enter Play Mode to preview VFX."); return; }
            stage = 0; original = null; live.Clear(); next = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update -= PreviewStep; EditorApplication.update += PreviewStep;
        }
        static void PreviewStep()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= PreviewStep; return; }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                var controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
                if (controller == null || controller.Session == null) throw new InvalidOperationException("No running game");
                var player = controller.GetComponent<ImportedVfxPlayer>();
                Require(player != null, "Imported VFX player exists");
                var ui = controller.ui;
                var position = ui.effects.TransformPoint(new Vector3(0, 250));
                if (stage == 0) { original = controller.Session.Key(); Require(player.Land(position), "Landing plays"); ui.AudioFeedback.Play(PuzzleEventKind.Collected); }
                if (stage == 1) Require(player.Collection(position), "Collection plays");
                if (stage == 2)
                {
                    int count = 0;
                    foreach (var graphic in ui.GetComponentsInChildren<UIParticleGraphic>())
                    {
                        Require(!graphic.raycastTarget, "No VFX raycast interception");
                        var mesh = graphic.canvasRenderer.GetMesh();
                        if (mesh != null) count += mesh.vertexCount;
                    }
                    Require(count > 0, "Canvas renders VFX geometry");
                    live.AppendLine("PASS: live Canvas vertices=" + count);
                    ScreenCapture.CaptureScreenshot("Temp/StickerVfxPreview.png");
                }
                if (stage == 3) { Require(player.Reveal(position), "Reveal plays"); ui.AudioFeedback.Play(PuzzleEventKind.Revealed); }
                if (stage == 4) { Require(player.Win(position), "Win plays"); ui.AudioFeedback.Play(PuzzleEventKind.Win); }
                if (stage == 5)
                {
                    Require(ui.GetComponentsInChildren<UIParticleGraphic>().Length == 0, "Effects expire automatically");
                    for (int i = 0; i < 15; i++) player.Land(position);
                    var config = Resources.Load<GameVfxConfig>("StickerVfx");
                    Require(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length <= config.maxConcurrentEffects * config.landing.GetComponentsInChildren<ParticleSystem>().Length, "Concurrency cap respected");
                    player.Clear();
                }
                if (stage == 6)
                {
                    Require(ui.GetComponentsInChildren<UIParticleGraphic>().Length == 0, "Reset clears Canvas effects");
                    Require(original == controller.Session.Key(), "VFX does not change game state");
                    live.AppendLine("PASS: landing/collection/reveal/win, automatic expiry, concurrency cap, reset cleanup, unchanged game state.");
                    File.WriteAllText("Temp/StickerVfxLiveValidation.txt", live.ToString());
                    Debug.Log(live.ToString()); EditorApplication.update -= PreviewStep;
                }
                stage++; next = EditorApplication.timeSinceStartup + (stage == 2 ? .2 : stage == 5 ? 4.2 : .6);
            }
            catch (Exception e)
            {
                File.WriteAllText("Temp/StickerVfxLiveValidation.txt", live + "FAIL: " + e);
                Debug.LogException(e); EditorApplication.update -= PreviewStep;
            }
        }
        [MenuItem("Tools/Sticker Sort/Validate Imported VFX")]
        public static void Validate()
        {
            var report = new StringBuilder();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var config = Resources.Load<GameVfxConfig>("StickerVfx");
                Require(config != null, "VFX config loads from Resources");
                Require(config.alphaMaterial != null && config.additiveMaterial != null, "UI materials resolve");
                Require(!ShaderUtil.ShaderHasError(config.alphaMaterial.shader), "UI particle shader compiles");
                var cameraObject = new GameObject("VFX validation camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                var canvasObject = new GameObject("VFX validation canvas", typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                foreach (var prefab in new[] { config.landing, config.collection, config.reveal, config.win })
                {
                    Require(prefab != null, "Effect prefab resolves");
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    int vertices = 0;
                    foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var renderer = particle.GetComponent<ParticleSystemRenderer>();
                        Require(renderer != null && renderer.sharedMaterial != null, "Particle material resolves");
                        var main = particle.main; main.loop = false; main.stopAction = ParticleSystemStopAction.None;
                        particle.Simulate(.2f, false, true, true);
                        var rect = UIFactory.Rect("Particle graphic", canvasObject.transform, new Vector2(100, 100), Vector2.zero);
                        var graphic = rect.gameObject.AddComponent<UIParticleGraphic>();
                        graphic.Initialize(renderer, instance.transform, camera, 40, config);
                        Canvas.ForceUpdateCanvases();
                        var mesh = new Mesh(); renderer.BakeMesh(mesh, camera, false); vertices += mesh.vertexCount;
                        UnityEngine.Object.DestroyImmediate(mesh);
                        Require(!graphic.raycastTarget, "VFX does not intercept input");
                    }
                    Require(vertices > 0, prefab.name + " produces visible particle geometry");
                    report.AppendLine(prefab.name + ": PASS, baked vertices=" + vertices);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
                var audio = AssetDatabase.LoadAssetAtPath<GameAudioConfig>("Assets/_Game/Data/Audio.asset");
                foreach (var clip in new[] { audio.land, audio.buffer, audio.collection, audio.entrance, audio.special, audio.win, audio.mysteryReveal })
                    Require(clip != null && clip.length > 0, "Event audio resolves");
                report.AppendLine("PASS: VFX references, materials, shader, geometry, raycasts and 7 event audio references.");
            }
            catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                File.WriteAllText(Report, report.ToString());
            }
            Debug.Log(report.ToString());
        }
        static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    }
}
