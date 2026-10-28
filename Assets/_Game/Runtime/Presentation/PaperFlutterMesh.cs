using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Small continuous waves keep the released liner soft instead of rotating a rigid rectangle.
    public sealed class PaperFlutterMesh : BaseMeshEffect
    {
        readonly List<UIVertex> source = new List<UIVertex>();
        float phase, bend;
        public bool PaperFace { get; set; }
        public void SetFlutter(float phase, float bend) { this.phase = phase; this.bend = bend; graphic.SetVerticesDirty(); }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            if (bend <= 0)
            {
                if (PaperFace) for (int i = 0; i < mesh.currentVertCount; i++)
                { var v = new UIVertex(); mesh.PopulateUIVertex(ref v, i); v.uv1 = new Vector4(1, 0, 0, 0); mesh.SetUIVertex(v, i); }
                return;
            }
            source.Clear(); mesh.GetUIVertexStream(source); if (source.Count < 6) return;
            var first = source[0]; Vector3 min = first.position, max = min;
            Vector2 uvMin = first.uv0, uvMax = uvMin;
            foreach (var v in source) { min = Vector3.Min(min, v.position); max = Vector3.Max(max, v.position); uvMin = Vector2.Min(uvMin, v.uv0); uvMax = Vector2.Max(uvMax, v.uv0); }
            mesh.Clear(); const int columns = 12, rows = 8;
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                float u = (float)x / columns, v = (float)y / rows;
                float wave = Mathf.Sin(phase + u * Mathf.PI * 2) * bend;
                var vertex = first;
                vertex.position = new Vector3(Mathf.Lerp(min.x, max.x, u) + (v - .5f) * wave * (max.x - min.x) * .25f,
                    Mathf.Lerp(min.y, max.y, v) + wave * (max.y - min.y) * (.35f + .65f * v), min.z);
                vertex.uv0 = new Vector4(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v), 0, 0);
                if (PaperFace) vertex.uv1 = new Vector4(1, 0, 0, 0);
                var tint = first.color; float shade = 1 - .12f * Mathf.Abs(Mathf.Cos(phase + u * Mathf.PI * 2)) * Mathf.Clamp01(bend * 12);
                tint.r = (byte)(tint.r * shade); tint.g = (byte)(tint.g * shade); tint.b = (byte)(tint.b * shade); vertex.color = tint;
                mesh.AddVert(vertex);
            }
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            { int a = y * (columns + 1) + x, b = a + 1, c = a + columns + 1; mesh.AddTriangle(a, c, b); mesh.AddTriangle(b, c, c + 1); }
        }
    }
}
