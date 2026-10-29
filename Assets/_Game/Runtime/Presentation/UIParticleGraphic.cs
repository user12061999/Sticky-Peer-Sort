using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Render the imported particle simulation inside the existing overlay Canvas.
    // No world camera, input raycasts or extra render texture is needed.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIParticleGraphic : MaskableGraphic
    {
        ParticleSystemRenderer source;
        Transform simulationRoot;
        Camera bakeCamera;
        Mesh baked;
        float units;
        Texture texture;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<int> indices = new List<int>();
        public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;

        public void Initialize(ParticleSystemRenderer renderer, Transform root, Camera camera, float scale, GameVfxConfig config)
        {
            source = renderer; simulationRoot = root; bakeCamera = camera; units = scale;
            baked = new Mesh { name = "UI particle mesh" };
            var original = renderer.sharedMaterial;
            texture = original != null ? original.mainTexture : null;
            bool additive = original != null && original.HasProperty("_DstBlend") && original.GetFloat("_DstBlend") == 1;
            material = additive ? config.additiveMaterial : config.alphaMaterial;
            raycastTarget = false; maskable = false;
            renderer.enabled = false;
        }
        void LateUpdate() { if (source != null) SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (source == null || baked == null || bakeCamera == null) return;
            source.BakeMesh(baked, bakeCamera, false);
            baked.GetVertices(vertices); baked.GetUVs(0, uvs); baked.GetColors(colors); baked.GetTriangles(indices, 0);
            var matrix = simulationRoot.worldToLocalMatrix * source.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Count; i++)
            {
                var point = matrix.MultiplyPoint3x4(vertices[i]) * units; point.z = 0;
                helper.AddVert(point, i < colors.Count ? colors[i] : new Color32(255, 255, 255, 255), i < uvs.Count ? uvs[i] : Vector2.zero);
            }
            for (int i = 0; i + 2 < indices.Count; i += 3) helper.AddTriangle(indices[i], indices[i + 1], indices[i + 2]);
        }
        protected override void OnDestroy()
        {
            if (baked != null)
            {
                if (Application.isPlaying) Destroy(baked);
                else DestroyImmediate(baked);
            }
            base.OnDestroy();
        }
    }
}
