using System.IO;
using UnityEditor;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    public static class WarmTextMaterials
    {
        const string Folder = "Assets/_Game/UI/TextMaterials";
        public static void Ensure(GameplayUITheme theme)
        {
            if (theme.font == null) return;
            if (!AssetDatabase.IsValidFolder(Folder))
            { Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); }
            if (theme.headingTextMaterial == null)
                theme.headingTextMaterial = Preset(theme, "Sen Warm Heading", new Color(.21f, .085f, .025f), .20f, .8f, -.5f, .08f);
            if (theme.lightTextMaterial == null)
                theme.lightTextMaterial = Preset(theme, "Sen Warm Light", new Color(.24f, .10f, .03f), .14f, .65f, -.35f, .08f);
            if (theme.darkTextMaterial == null)
                theme.darkTextMaterial = Preset(theme, "Sen Warm Dark", theme.paper, .085f, .25f, -.25f, .18f);
            EditorUtility.SetDirty(theme);
        }
        static Material Preset(GameplayUITheme theme, string name, Color outline, float width, float shadowAlpha, float offsetY, float softness)
        {
            string path = Folder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(theme.font.material) { name = name };
            material.SetColor("_FaceColor", Color.white);
            material.SetFloat("_FaceDilate", 0);
            material.SetColor("_OutlineColor", outline);
            material.SetFloat("_OutlineWidth", width);
            material.SetFloat("_OutlineSoftness", .015f);
            material.EnableKeyword("UNDERLAY_ON"); material.DisableKeyword("UNDERLAY_INNER");
            material.SetColor("_UnderlayColor", new Color(.20f, .075f, .015f, shadowAlpha));
            material.SetFloat("_UnderlayOffsetX", .12f); material.SetFloat("_UnderlayOffsetY", offsetY);
            material.SetFloat("_UnderlayDilate", .08f); material.SetFloat("_UnderlaySoftness", softness);
            AssetDatabase.CreateAsset(material, path); return material;
        }
    }
}
