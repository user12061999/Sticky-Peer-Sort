using UnityEditor;
using UnityEngine;
namespace StickyPeerSort.Editor
{
    [CustomEditor(typeof(StickerDefinition)), CanEditMultipleObjects]
    public sealed class StickerDefinitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Assign a transparent PNG to Texture, or choose a Sprite. Alpha defines the shape for clicks and coverage. Texture takes priority. No predefined circle/square mesh is used.", MessageType.Info);
            EditorGUI.BeginChangeCheck(); DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            if (GUILayout.Button("Use production artwork colors / hide placeholder label"))
            {
                Undo.RecordObjects(targets, "Configure sticker artwork");
                foreach (StickerDefinition definition in targets) { definition.tint = Color.white; definition.showLabel = false; definition.preserveAspect = true; }
                changed = true;
            }
            if (GUILayout.Button("Bake / refresh alpha mask")) changed = true;
            if (changed)
            {
                foreach (StickerDefinition definition in targets) StickerArtworkBaker.Bake(definition);
                AssetDatabase.SaveAssets();
            }
            if (targets.Length == 1)
            {
                var definition = (StickerDefinition)target;
                EditorGUILayout.HelpBox(!definition.HasArtwork ? "Assign artwork to this sticker." : definition.HasAlphaMask ? "Alpha mask ready. Read/Write can remain disabled on the source texture." : "No baked mask yet. Click Bake / refresh alpha mask.", definition.HasAlphaMask ? MessageType.Info : MessageType.Warning);
                if (definition.HasArtwork)
                {
                    Rect preview = GUILayoutUtility.GetRect(180, 180, GUILayout.ExpandWidth(true));
                    var rect = definition.SourceRect; var texture = definition.SourceTexture;
                    float scale = Mathf.Min(preview.width / rect.width, preview.height / rect.height);
                    Rect image = new Rect(preview.center.x - rect.width * scale * .5f, preview.center.y - rect.height * scale * .5f, rect.width * scale, rect.height * scale);
                    GUI.DrawTextureWithTexCoords(image, texture, new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height), true);
                }
            }
        }
    }
}
