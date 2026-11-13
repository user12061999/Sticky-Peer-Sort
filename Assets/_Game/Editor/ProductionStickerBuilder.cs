using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    public static class ProductionStickerBuilder
    {
        public const string Root = "Assets/_Game/ProductionStickers";
        static readonly string[] Categories = { "food", "animal", "plant", "vehicle" };
        [Serializable] public sealed class Manifest { public Entry[] entries; }
        [Serializable] public sealed class Entry { public string id, category, displayName, filename; }
        public static List<StickerDefinition> PoolForLevel(int index, IEnumerable<StickerDefinition> legacy)
        {
            return File.Exists(Root + "/manifest.json") ? ProductionCategories.Pool(index) : legacy.ToList();
        }
        [MenuItem("Tools/Sticker Category Game/Production Stickers/Import and Apply 64 Individual Stickers")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/manifest.json"));
            if (manifest.entries.Length != 64 || manifest.entries.Select(e => e.id).Distinct().Count() != 64) throw new InvalidDataException("Expected 64 unique individual stickers.");
            Directory.CreateDirectory(Root + "/Data"); Directory.CreateDirectory(Root + "/Silhouettes"); AssetDatabase.Refresh();
            foreach (var entry in manifest.entries) Import(entry);
            var legacy = new List<StickerDefinition>();
            foreach (var category in Categories) for (int n = 1; n <= 4; n++)
            {
                string old = ReferenceStyleBuilder.Root + "/Data/Stickers/" + category + "_" + n.ToString("00") + ".asset";
                string renamed = ReferenceStyleBuilder.Root + "/Data/Stickers/ref_" + category + "_" + n.ToString("00") + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<StickerDefinition>(renamed);
                if (definition == null)
                {
                    string error = AssetDatabase.MoveAsset(old, renamed);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    definition = AssetDatabase.LoadAssetAtPath<StickerDefinition>(renamed);
                }
                definition.id = "ref_" + category + "_" + n.ToString("00"); definition.name = definition.id;
                EditorUtility.SetDirty(definition); legacy.Add(definition);
            }
            var staged = new List<LevelDefinition>();
            var sources = new List<LevelDefinition>();
            var report = new StringBuilder("# Production artwork integration\n\n64 independently generated PNGs, 512x512 RGBA. Four rotating artwork banks. Existing level counts, special ratios, difficulty settings and five-slot loss rule retained. All layouts rebuilt because alpha silhouettes affect occlusion.\n\n|Level|Stickers|Mystery|Locked|Winning route|Buffer peak|\n|---|---|---|---|---|---|\n");
            try
            {
                for (int i = 0; i < 15; i++)
                {
                    var source = AssetDatabase.LoadAssetAtPath<LevelDefinition>(CampaignBalanceBuilder.Root + "/Campaign_" + (i+1).ToString("00") + ".asset");
                    var copy = UnityEngine.Object.Instantiate(source); copy.name = source.name; staged.Add(copy); sources.Add(source);
                    copy.generation.stickerPool = PoolForLevel(i, legacy);
                    string message;
                    if (!ConfiguredLevelGenerator.Rebuild(copy, out message)) throw new InvalidOperationException(copy.name + ": " + message);
                    var analysis = LevelAnalysis.Analyze(copy);
                    if (!analysis.Verified || analysis.peakTransientBuffer >= copy.bufferCapacity) throw new InvalidOperationException(copy.name + ": route failed.");
                    report.AppendLine("|"+(i+1)+"|"+copy.stickers.Count+"|"+analysis.mystery+"|"+analysis.locked+"|PASS|"+analysis.peakTransientBuffer+"|");
                }
                for (int i=0;i<staged.Count;i++)
                {
                    Undo.RecordObject(sources[i], "Apply production stickers");
                    EditorUtility.CopySerialized(staged[i], sources[i]); sources[i].name=staged[i].name; EditorUtility.SetDirty(sources[i]);
                }
                AssetDatabase.SaveAssets(); ValidateCatalog();
                File.WriteAllText(Root + "/Validation/Integration.md",report.ToString());
                File.WriteAllText("Assets/_Game/Validation/CampaignSolutions.txt",string.Join("\n",sources.Select(l=>l.name+": "+string.Join(",",l.solutionTrace)))+"\n");
                File.WriteAllText("Temp/ProductionStickerImport.txt","PASS: 64 artwork imports, 15/15 verified campaign routes.\n"+report);
                AssetDatabase.Refresh(); LevelPreviewWindow.Preview(0, false);
                Debug.Log("64 production stickers applied to the campaign; all 15 winning routes verified.");
            }
            finally { foreach (var copy in staged) UnityEngine.Object.DestroyImmediate(copy); }
        }
        /// <summary>13/11 library expansion: imports the 56 new stickers (14 categories x 4) whose PNGs are already in Art/
        /// as sps_&lt;domain&gt;_&lt;design&gt;.png (512x512 RGBA), same pipeline as the first 64 (sprite, alpha mask, silhouette),
        /// then assigns categories. Levels are rebuilt separately with Build Balanced Campaign.</summary>
        [MenuItem("Tools/Sticker Category Game/Production Stickers/Import 56 Expansion Stickers")]
        public static void ImportExpansion()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var entries = ProductionCategories.ExpansionStickers().Select(kv => new Entry { id = kv.Key, displayName = kv.Value, filename = kv.Key + ".png" }).ToList();
            if (entries.Count != 56 || entries.Select(e => e.id).Distinct().Count() != 56) throw new InvalidDataException("Expected 56 unique expansion stickers.");
            var missing = entries.Where(e => !File.Exists(Root + "/Art/" + e.filename)).Select(e => e.filename).ToList();
            if (missing.Count > 0) throw new FileNotFoundException("Missing art: " + string.Join(", ", missing));
            AssetDatabase.Refresh();
            foreach (var entry in entries) Import(entry);
            AssetDatabase.SaveAssets();
            ProductionCategories.Apply();
            ValidateCatalog();
            Debug.Log("56 expansion stickers imported (30 categories, 120 stickers).");
        }
        static void Import(Entry e)
        {
            string path=Root+"/Art/"+e.filename;
            var sprite=ImportSprite(path);
            var pixelsTexture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!pixelsTexture.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(path);
                if(pixelsTexture.width!=512 || pixelsTexture.height!=512) throw new InvalidDataException("Expected 512x512: "+path);
                string dataPath=Root+"/Data/"+e.id+".asset";
                var d=AssetDatabase.LoadAssetAtPath<StickerDefinition>(dataPath);
                if(d==null) {d=ScriptableObject.CreateInstance<StickerDefinition>(); AssetDatabase.CreateAsset(d,dataPath);}
                d.name=d.id=e.id; d.displayName=e.displayName;
                d.category=ProductionCategories.For(e.id);
                d.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path); d.sprite=sprite; d.showLabel=false; d.tint=Color.white; d.preserveAspect=true;
                d.alphaThreshold=.15f; d.alphaMaskResolution=256;
                var alpha=new byte[256*256];
                for(int y=0;y<256;y++) for(int x=0;x<256;x++) alpha[y*256+x]=(byte)Mathf.RoundToInt(pixelsTexture.GetPixelBilinear((x+.5f)/256,(y+.5f)/256).a*255);
                d.SetAlphaMask(alpha,256,256);
                var colors=pixelsTexture.GetPixels32();
                for(int i=0;i<colors.Length;i++) {byte a=colors[i].a; colors[i]=new Color32(241,188,112,a);}
                pixelsTexture.SetPixels32(colors); pixelsTexture.Apply();
                string silhouettePath=Root+"/Silhouettes/"+e.id+"_silhouette.png";
                File.WriteAllBytes(silhouettePath,pixelsTexture.EncodeToPNG());
                d.targetSilhouette=ImportSprite(silhouettePath); EditorUtility.SetDirty(d);
            }
            finally {UnityEngine.Object.DestroyImmediate(pixelsTexture);}
        }
        static Sprite ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType=TextureImporterType.Sprite; imp.spriteImportMode=SpriteImportMode.Single;
            imp.alphaIsTransparency=true; imp.mipmapEnabled=false; imp.isReadable=false;
            imp.wrapMode=TextureWrapMode.Clamp; imp.filterMode=FilterMode.Bilinear;
            imp.maxTextureSize=512; imp.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings(); imp.ReadTextureSettings(settings); settings.spriteMeshType=SpriteMeshType.FullRect; imp.SetTextureSettings(settings);
            imp.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        [MenuItem("Tools/Sticker Category Game/Production Stickers/Validate Unique Catalog")]
        public static void ValidateCatalog()
        {
            var all=AssetDatabase.FindAssets("t:StickerDefinition",new[]{"Assets/_Game"}).Select(g=>AssetDatabase.LoadAssetAtPath<StickerDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var duplicate=all.GroupBy(d=>d.id).Where(g=>g.Count()>1).ToArray();
            if(duplicate.Length>0) throw new InvalidOperationException("Duplicate sticker IDs: "+string.Join(",",duplicate.Select(g=>g.Key)));
            if(all.Select(d=>Path.GetFileName(AssetDatabase.GetAssetPath(d))).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=all.Length) throw new InvalidOperationException("Duplicate sticker definition filenames.");
            var csv=new StringBuilder("id,display_name,category,definition,texture\n");
            foreach(var d in all.OrderBy(d=>d.id)) csv.AppendLine(d.id+","+d.displayName+","+d.category.id+","+AssetDatabase.GetAssetPath(d)+","+AssetDatabase.GetAssetPath(d.SourceTexture));
            File.WriteAllText(Root+"/Validation/StickerCatalog.csv",csv.ToString());
        }
    }
}

