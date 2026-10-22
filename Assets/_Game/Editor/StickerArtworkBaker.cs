using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    public static class StickerArtworkBaker
    {
        public static void Bake(StickerDefinition definition)
        {
            if (definition == null || !definition.HasArtwork) return;
            Texture2D source = definition.SourceTexture, readable = source;
            Rect region = definition.SourceRect; RenderTexture temporary = null;
            var previous = RenderTexture.active;
            try
            {
                if (!source.isReadable)
                {
                    // Editor-only readback keeps production textures non-readable on mobile.
                    temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                    readable = new Texture2D(Mathf.RoundToInt(region.width), Mathf.RoundToInt(region.height), TextureFormat.RGBA32, false, true);
                    readable.ReadPixels(region, 0, 0); readable.Apply(false, false);
                    region = new Rect(0, 0, readable.width, readable.height);
                }
                float scale = Mathf.Min(1, Mathf.Clamp(definition.alphaMaskResolution, 32, 512) / Mathf.Max(region.width, region.height));
                int width = Mathf.Max(1, Mathf.RoundToInt(region.width * scale)), height = Mathf.Max(1, Mathf.RoundToInt(region.height * scale));
                var mask = new byte[width * height];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    var uv = new Vector2((x + .5f) / width, (y + .5f) / height);
                    mask[y * width + x] = (byte)Mathf.RoundToInt(StickerAlphaSampling.Sample(readable, region, uv) * 255);
                }
                definition.SetAlphaMask(mask, width, height); EditorUtility.SetDirty(definition);
            }
            finally
            {
                RenderTexture.active = previous;
                if (readable != source) UnityEngine.Object.DestroyImmediate(readable);
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
            }
        }
        [MenuItem("Tools/Sticker Category Game/Bake All Sticker Artwork")]
        public static void BakeAll()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:StickerDefinition", new[] { PrototypeBootstrapTool.Root }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<StickerDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition.HasArtwork) { Bake(definition); count++; }
            }
            AssetDatabase.SaveAssets(); Debug.Log("Baked alpha masks for " + count + " sticker definitions.");
        }
    }
    public sealed class StickerArtworkPostprocessor : AssetPostprocessor
    {
        static readonly HashSet<string> pending = new HashSet<string>();
        static bool scheduled;
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported) if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D)) pending.Add(path);
            if (pending.Count == 0 || scheduled) return;
            scheduled = true; EditorApplication.delayCall += RefreshMasks;
        }
        static void RefreshMasks()
        {
            scheduled = false; var paths = new HashSet<string>(pending); pending.Clear(); bool changed = false;
            foreach (string guid in AssetDatabase.FindAssets("t:StickerDefinition", new[] { PrototypeBootstrapTool.Root }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<StickerDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition.HasArtwork && paths.Contains(AssetDatabase.GetAssetPath(definition.SourceTexture)))
                { StickerArtworkBaker.Bake(definition); changed = true; }
            }
            if (changed) AssetDatabase.SaveAssets();
        }
    }
}
