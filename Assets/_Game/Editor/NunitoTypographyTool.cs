using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort.EditorTools
{
    // Typography & Header Tint v01 (partner spec 2026-11-13): Nunito 900/800 SDF assets, material presets, theme wiring,
    // every scene TMP text moved off the old fonts, Canvas Scaler 1080x1920 Match Width. Idempotent.
    public static class NunitoTypographyTool
    {
        const string Dir = "Assets/_Game/Art/Fonts/Nunito/";
        const string UnderlayPath = Dir + "Nunito-ExtraBold SDF - Category Underlay.mat";

        [MenuItem("Tools/Sticker Category Game/Apply Typography v01 (Nunito)")]
        public static void Apply()
        {
            var black = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir + "Nunito-Black SDF.asset");
            var extra = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir + "Nunito-ExtraBold SDF.asset");
            if (black == null || extra == null) { Debug.LogError("Nunito SDF font assets missing in " + Dir); return; }
            BaseMaterial(black); BaseMaterial(extra);
            var underlay = Underlay(extra);
            foreach (var guid in AssetDatabase.FindAssets("t:GameplayUITheme"))
            {
                var theme = AssetDatabase.LoadAssetAtPath<GameplayUITheme>(AssetDatabase.GUIDToAssetPath(guid));
                theme.font = black; theme.fontBlack = black; theme.fontExtraBold = extra; theme.underlayMaterial = underlay;
                theme.headerPalette = new Color[]
                {
                    new Color32(0xC7, 0x62, 0x59, 255), new Color32(0x39, 0x8C, 0x92, 255), new Color32(0x73, 0x7D, 0xB8, 255),
                    new Color32(0xAE, 0x64, 0x88, 255), new Color32(0x4E, 0x7D, 0xA1, 255), new Color32(0x63, 0x83, 0x6D, 255),
                    new Color32(0xA7, 0x6F, 0x49, 255), new Color32(0x89, 0x70, 0x9E, 255), new Color32(0x81, 0x81, 0x4C, 255),
                    new Color32(0x91, 0x6E, 0x64, 255),
                };
                EditorUtility.SetDirty(theme);
            }
            AssetDatabase.SaveAssets();

            var ui = Object.FindFirstObjectByType<GameplayUI>(FindObjectsInactive.Include);
            if (ui == null) { Debug.LogWarning("No GameplayUI in the open scene; assets and themes updated only."); return; }
            int moved = 0;
            foreach (var text in ui.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == black || text.font == extra) continue;
                if (ui.theme != null) ui.theme.Face(text, true); moved++;
            }
            var scaler = ui.GetComponentInParent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0; scaler.referencePixelsPerUnit = 100;
                EditorUtility.SetDirty(scaler);
            }
            EditorApplication.ExecuteMenuItem("Tools/Sticker Category Game/Apply GUI v01 Scrapbook Skin");   // re-applies spec sizes on HUD + boosters
            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            EditorSceneManager.SaveScene(ui.gameObject.scene);
            Debug.Log("Typography v01 applied: " + moved + " scene texts moved to Nunito.");
        }

        // Face white, no dilate/softness/outline/glow/bevel, shader TextMeshPro/Distance Field.
        static void BaseMaterial(TMP_FontAsset font)
        {
            var m = font.material; var shader = Shader.Find("TextMeshPro/Distance Field");
            if (shader != null && m.shader != shader) m.shader = shader;
            Clean(m);
            EditorUtility.SetDirty(m); EditorUtility.SetDirty(font);
        }
        static void Clean(Material m)
        {
            m.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, 0); m.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0); m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0);
            m.DisableKeyword(ShaderUtilities.Keyword_Glow); m.DisableKeyword(ShaderUtilities.Keyword_Bevel); m.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            m.DisableKeyword("UNDERLAY_INNER");
        }
        // Category names: Underlay Normal #15243B80, offset +0.10/-0.30, dilate +0.06, softness 0.25 (material-relative).
        static Material Underlay(TMP_FontAsset font)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(UnderlayPath);
            if (m == null) { m = new Material(font.material); AssetDatabase.CreateAsset(m, UnderlayPath); }
            else { m.shader = font.material.shader; m.CopyPropertiesFromMaterial(font.material); }
            m.name = "Nunito-ExtraBold SDF - Category Underlay";
            Clean(m);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color32(0x15, 0x24, 0x3B, 0x80));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .10f); m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.30f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, .06f); m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .25f);
            EditorUtility.SetDirty(m); return m;
        }
    }
}
