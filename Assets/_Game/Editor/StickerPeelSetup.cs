using System;
using UnityEditor;
using UnityEngine;

namespace StickyPeerSort.Editor
{
    public static class StickerPeelSetup
    {
        [MenuItem("Tools/Sticker Category Game/Enable Sticker Page Peel")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before changing animation assets.");
            const string path = "Assets/_Game/UI/StickerPagePeel.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/UI/Shaders/StickerCurl.shader");
                if (shader == null) throw new InvalidOperationException("Sticker curl shader has not imported.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            foreach (string balancePath in new[] { "Assets/_Game/Data/Balance.asset", ReferenceStyleBuilder.Root + "/Data/ReferenceBalance.asset" })
            {
                var balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(balancePath); if (balance == null) continue;
                balance.pagePeel = true; balance.pagePeelMaterial = material;
                balance.peelDuration = .16f; balance.stickDuration = .14f;
                EditorUtility.SetDirty(balance);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Sticker page peel enabled: 0.16s peel, existing travel, 0.14s reverse stick. Both main and reference balance assets configured.");
        }
    }
}
