using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // A cylindrical page fold, projected into the overlay Canvas. Alpha and UVs stay with the art.
    public sealed class StickerCurlMesh : BaseMeshEffect
    {
        const int Columns = 32, Rows = 4;
        readonly List<UIVertex> source = new List<UIVertex>(6);
        float amount;
        public float Amount
        {
            get => amount;
            set { amount = Mathf.Clamp01(value); if (graphic != null) graphic.SetVerticesDirty(); }
        }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || amount <= .0001f) return;
            source.Clear(); mesh.GetUIVertexStream(source); if (source.Count < 6) return;
            var first = source[0];
            Vector3 min = first.position, max = min;
            Vector2 uvMin = first.uv0, uvMax = uvMin;
            foreach (var v in source)
            {
                min = Vector3.Min(min, v.position); max = Vector3.Max(max, v.position);
                uvMin = Vector2.Min(uvMin, v.uv0); uvMax = Vector2.Max(uvMax, v.uv0);
            }
            float width = max.x - min.x; if (width < .001f) return;
            mesh.Clear();
            float fold = 1 - amount * .82f, radius = .16f;
            for (int y = 0; y <= Rows; y++) for (int x = 0; x <= Columns; x++)
            {
                float u = (float)x / Columns, v = (float)y / Rows;
                float distance = Mathf.Max(0, u - fold), angle = Mathf.Min(Mathf.PI, distance / radius);
                float projected = u, lift = 0, back = 0, shade = 0;
                if (distance > 0)
                {
                    projected = fold + radius * Mathf.Sin(angle) - Mathf.Max(0, distance - Mathf.PI * radius);
                    lift = radius * (1 - Mathf.Cos(angle));
                    back = Mathf.Cos(angle) < 0 ? 1 : 0;
                    shade = Mathf.Sin(angle) * .28f;
                }
                var vertex = first;
                // Slight diagonal elevation makes the curled white underside visible without a 3D camera.
                vertex.position = new Vector3(min.x + (projected + lift * .14f) * width,
                    Mathf.Lerp(min.y, max.y, v) + lift * width * (.32f + v * .1f), min.z);
                vertex.uv0 = new Vector4(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v), 0, 0);
                vertex.uv1 = new Vector4(back, shade, 0, 0);
                mesh.AddVert(vertex);
            }
            for (int y = 0; y < Rows; y++) for (int x = 0; x < Columns; x++)
            {
                int a = y * (Columns + 1) + x, b = a + 1, c = a + Columns + 1, d = c + 1;
                mesh.AddTriangle(a, c, b); mesh.AddTriangle(b, c, d);
            }
        }
    }
}
