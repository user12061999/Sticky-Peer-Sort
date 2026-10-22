using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort
{
    // Small, weighted copies of the sprite alpha soften the black veil at its edges.
    // Uses the existing UI material, including sprite atlas UVs, without per-sticker materials.
    public sealed class SoftCoveredShade : BaseMeshEffect
    {
        public float softness = 1.5f;
        readonly List<UIVertex> source = new List<UIVertex>();
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            source.Clear(); mesh.GetUIVertexStream(source); mesh.Clear();
            int vertex = 0;
            for (int pass = 0; pass < 9; pass++)
            {
                float angle = pass * Mathf.PI * .25f;
                Vector3 offset = pass == 8 ? Vector3.zero : new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * softness;
                for (int i = 0; i < source.Count; i++)
                {
                    var v = source[i]; v.position += offset;
                    if (pass != 8) { var color = v.color; color.a = (byte)(color.a * .08f); v.color = color; }
                    mesh.AddVert(v);
                    if (i % 3 == 2) mesh.AddTriangle(vertex - 2, vertex - 1, vertex);
                    vertex++;
                }
            }
        }
    }
}
