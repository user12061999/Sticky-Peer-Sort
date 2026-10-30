using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace StickyPeerSort.Tests
{
    public sealed class ArtworkAndArchitectureTests
    {
        readonly List<Object> objects = new List<Object>();
        T Make<T>() where T : ScriptableObject { var item = ScriptableObject.CreateInstance<T>(); objects.Add(item); return item; }
        Texture2D Shape(int width, int height, Func<int, int, bool> solid)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Point;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = solid(x, y) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels); texture.Apply(); objects.Add(texture); return texture;
        }
        [TearDown] public void Cleanup() { for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]); objects.Clear(); }
        [Test] public void ArbitraryTextureSilhouetteAndTransparentHoleDriveGeometry()
        {
            var definition = Make<StickerDefinition>(); definition.texture = Shape(32, 32, (x, y) => x < 8 || y < 8); definition.preserveAspect = true;
            var placement = new StickerPlacement { definition = definition, position = Vector2.zero, size = new Vector2(320, 320) };
            var sticker = new StickerRuntime(placement);
            Assert.IsTrue(BoardGeometry.Contains(sticker, new Vector2(-120, 100)));
            Assert.IsTrue(BoardGeometry.Contains(sticker, new Vector2(100, -120)));
            Assert.IsFalse(BoardGeometry.Contains(sticker, new Vector2(70, 70)), "Transparent L-shape cutout must not become a square hitbox.");
        }
        [Test] public void TextureOverrideWinsOverSpriteAndIsSharedByRenderingAndSampling()
        {
            var definition = Make<StickerDefinition>(); definition.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Circle.png");
            definition.texture = Shape(32, 16, (x, y) => x < 8);
            Assert.AreSame(definition.texture, definition.ArtworkSprite.texture); Assert.AreEqual(new Rect(0, 0, 32, 16), definition.ArtworkSprite.rect);
            Assert.IsTrue(definition.ContainsUV(new Vector2(.1f, .5f))); Assert.IsFalse(definition.ContainsUV(new Vector2(.9f, .5f)));
            Assert.AreEqual(new Vector2(200, 100), definition.FitSize(new Vector2(200, 200)));
        }
        [Test] public void LetterboxMarginIsNotClickableAndRotationUsesTheSameAspect()
        {
            var definition = Make<StickerDefinition>(); definition.texture = Shape(32, 16, (x, y) => true);
            var sticker = new StickerRuntime(new StickerPlacement { definition = definition, size = new Vector2(200, 200), rotation = 90 });
            Assert.IsTrue(BoardGeometry.Contains(sticker, new Vector2(40, 80)));
            Assert.IsFalse(BoardGeometry.Contains(sticker, new Vector2(80, 40)));
        }
        [Test] public void NonReadableTextureWorksAfterBakingAndMaskInvalidatesOnReassignment()
        {
            var definition = Make<StickerDefinition>(); definition.texture = Shape(32, 32, (x, y) => x < 8 || y < 8);
            // GPU bake must work without enabling Read/Write on the production source.
            definition.texture.Apply(false, true); Assert.IsFalse(definition.texture.isReadable);
            StickyPeerSort.Editor.StickerArtworkBaker.Bake(definition);
            Assert.IsTrue(definition.HasAlphaMask); Assert.IsTrue(definition.ContainsUV(new Vector2(.1f, .7f))); Assert.IsFalse(definition.ContainsUV(new Vector2(.7f, .7f)));
            definition.texture = Shape(16, 16, (x, y) => true); Assert.IsFalse(definition.HasAlphaMask);
        }
        [Test] public void ComplementaryIrregularImagesDoNotBlockEachOtherJustBecauseBoundsOverlap()
        {
            var a = Make<StickerDefinition>(); var b = Make<StickerDefinition>();
            a.texture = Shape(32, 32, (x, y) => x < 7); b.texture = Shape(32, 32, (x, y) => x > 24);
            var sa = new StickerRuntime(new StickerPlacement { definition = a, size = new Vector2(160, 160) });
            var sb = new StickerRuntime(new StickerPlacement { definition = b, size = new Vector2(160, 160) });
            Assert.IsFalse(BoardGeometry.Overlaps(sa, sb));
        }
        [Test] public void GameplayAssembliesHaveNoPresentationDependency()
        {
            foreach (var type in new[] { typeof(GameSession), typeof(GameplayCoordinator), typeof(GameplayBoosters) })
            {
                Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(type));
                foreach (var assembly in type.Assembly.GetReferencedAssemblies())
                    Assert.IsFalse(assembly.Name == "Unity.TextMeshPro" || assembly.Name == "UnityEngine.UI" || assembly.Name == "Unity.ugui" || assembly.Name == "StickyPeerSort.Runtime", type.Name + " depends on presentation assembly " + assembly.Name);
            }
        }
        [Test] public void CoordinatorRunsWithoutCanvasAndWaitsForPresentationBeforeShowingWin()
        {
            var level = Make<LevelDefinition>(); level.activeCategoryCount = 1;
            var category = Make<CategoryDefinition>(); category.id = "test";
            var definition = Make<StickerDefinition>(); definition.category = category; definition.texture = Shape(8, 8, (x, y) => true);
            level.objectives.Add(new CategoryObjective { category = category, targetCount = 2 });
            for (int i = 0; i < 2; i++) level.stickers.Add(new StickerPlacement { instanceId = "test" + i, definition = definition, position = new Vector2(i * 220, 0) });
            var progress = new MemoryProgress(); var balance = Make<GameBalanceConfig>(); var context = new GameplayContext(progress.Data, balance);
            var presenter = new RecordingPresenter(); var screen = new RecordingScreen();
            using (var flow = new GameplayCoordinator(context, new LevelRepository(new[] { level }), new GameSessionFactory(), progress,
                new GameplayBoosters(progress, new BoundedPuzzleSolver()), presenter, screen))
            {
                flow.LoadLevel(0); flow.Tap(0); Assert.IsTrue(flow.CanTap); Assert.AreEqual(1, presenter.moves.Count);
                flow.Tap(1); Assert.AreEqual(GameplayState.Win, context.Session.State); Assert.IsFalse(flow.CanTap); Assert.IsFalse(screen.HasModal);
                Assert.AreEqual(0, progress.Data.highestCompleted); presenter.Drain();
                Assert.IsTrue(screen.HasModal); Assert.AreEqual(1, progress.Data.highestCompleted); Assert.AreEqual(2, presenter.moves[1].Snapshot.Categories[0].Count);
                Assert.AreEqual(1, presenter.moves[0].Snapshot.Categories[0].Count, "Each animation batch retains its own resolved snapshot.");
            }
        }
        sealed class MemoryProgress : IProgressStore
        {
            public SaveData Data { get; } = new SaveData();
            public bool Complete(LevelDefinition level) { Data.highestCompleted = level.levelNumber; return true; }
            public void Flush() { } public void ResetTutorial(TutorialLesson lesson) { Data.completedTutorials &= ~(int)lesson; }
        }
        sealed class RecordingPresenter : IMovePresenter
        {
            public readonly List<MovePresentation> moves = new List<MovePresentation>();
            public bool IsBusy { get; private set; }
            public bool BlocksInput { get; private set; }
            public PuzzleEventKind? Phase => null;
            public event Action<PuzzleEvent> EventFinished;
            public event Action Drained;
            public void Enqueue(MovePresentation move) { moves.Add(move); IsBusy = true; BlocksInput |= move.BlocksInput; }
            public void Reset() { IsBusy = BlocksInput = false; moves.Clear(); }
            public void Drain() { IsBusy = BlocksInput = false; if (moves.Count > 0) foreach (var e in moves[moves.Count - 1].Events) EventFinished?.Invoke(e); Drained?.Invoke(); }
        }
        sealed class RecordingScreen : IGameplayScreen
        {
            public bool HasModal { get; private set; }
            public void Render() { } public void SynchronizeAfterMoves() { } public void HideModal() { HasModal = false; }
            public void RejectSticker(int index) { } public void HighlightSticker(int index) { } public void SetInstruction(string message) { }
            public void ShowModal(string title, string body, string primary, Action primaryAction, string secondary, Action secondaryAction) { HasModal = true; }
        }
    }
}

