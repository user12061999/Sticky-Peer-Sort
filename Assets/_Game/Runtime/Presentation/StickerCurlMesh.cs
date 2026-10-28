using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // A flexible sheet with smoothly distributed curvature. Front/back share one alpha/UV surface.
    public sealed class StickerCurlMesh : BaseMeshEffect
    {
        const int Columns = 64, Rows = 16;
        readonly List<UIVertex> source = new List<UIVertex>(6);
        readonly List<FoldTriangle> triangles = new List<FoldTriangle>(Columns * Rows * 2);
        readonly float[] lifts = new float[(Columns + 1) * (Rows + 1)];
        struct FoldTriangle { public int a, b, c; public float depth; }
        float amount;
        public Rect ArtworkBounds { get; set; } = new Rect(0, 0, 1, 1);
        float directionDegrees;
        public float DirectionDegrees
        {
            get => directionDegrees;
            set { directionDegrees = value; if (graphic != null) graphic.SetVerticesDirty(); }
        }
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
            float originalWidth = max.x - min.x; if (originalWidth < .001f) return;
            float height = max.y - min.y, angleDegrees = directionDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleDegrees), sin = Mathf.Sin(angleDegrees);
            float width = Mathf.Abs(cos) * originalWidth + Mathf.Abs(sin) * height;
            Vector2 center = (min + max) * .5f;
            float contentMin = 1, contentMax = 0;
            foreach (float x in new[] { ArtworkBounds.xMin, ArtworkBounds.xMax }) foreach (float y in new[] { ArtworkBounds.yMin, ArtworkBounds.yMax })
            {
                Vector2 p = new Vector2(min.x + x * originalWidth, min.y + y * height) - center;
                float along = (cos * p.x + sin * p.y) / width + .5f;
                contentMin = Mathf.Min(contentMin, along); contentMax = Mathf.Max(contentMax, along);
            }
            float contentWidth = Mathf.Max(.01f, contentMax - contentMin);
            mesh.Clear();
            float flex = Mathf.Sin(amount * Mathf.PI);
            float fold = Mathf.Lerp(contentMax + .015f * contentWidth, contentMin - .065f * contentWidth, amount);
            float radius = (.045f + .02f * flex) * contentWidth;
            float maxAngle = 3.0f;
            for (int y = 0; y <= Rows; y++) for (int x = 0; x <= Columns; x++)
            {
                float u = (float)x / Columns, v = (float)y / Rows;
                Vector2 original = new Vector2(Mathf.Lerp(min.x, max.x, u), Mathf.Lerp(min.y, max.y, v));
                Vector2 offset = original - center;
                Vector2 local = new Vector2(cos * offset.x + sin * offset.y, -sin * offset.x + cos * offset.y);
                float along = local.x / width + .5f;
                float crease = fold + .095f * contentWidth * flex * (4 * (v - .5f) * (v - .5f) - .33f);
                float distance = Mathf.Max(0, along - crease);
                float projected = along, lift = 0, back = 0, shade = 0, creaseShade = 0;
                if (distance > 0)
                {
                    // Integrate a tangent that bends gradually, instead of a cylinder followed by a rigid tail.
                    float length = distance / 12, advance = 0;
                    for (int sample = 0; sample < 12; sample++)
                    {
                        float s = (sample + .5f) * length;
                        float tangent = maxAngle * (1 - Mathf.Exp(-s / (radius * maxAngle)));
                        advance += Mathf.Cos(tangent) * length;
                        lift += Mathf.Sin(tangent) * length;
                    }
                    float angle = maxAngle * (1 - Mathf.Exp(-distance / (radius * maxAngle)));
                    projected = crease + advance;
                    back = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.16f, -.16f, Mathf.Cos(angle)));
                    shade = Mathf.Sin(angle);
                    creaseShade = Mathf.Exp(-distance / (radius * 1.2f)) * shade;
                }
                var vertex = first;
                // Slight diagonal elevation makes the curled white underside visible without a 3D camera.
                local.x = (projected + lift * (.14f + .055f * Mathf.Sin((v - .5f) * Mathf.PI)) - .5f) * width;
                local.y += lift * width * (.27f + .045f * (v - .5f));
                vertex.position = new Vector3(center.x + cos * local.x - sin * local.y,
                    center.y + sin * local.x + cos * local.y, min.z);
                vertex.uv0 = new Vector4(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v), 0, 0);
                vertex.uv1 = new Vector4(back, shade, lift, creaseShade);
                lifts[y * (Columns + 1) + x] = lift;
                mesh.AddVert(vertex);
            }
            triangles.Clear();
            for (int y = 0; y < Rows; y++) for (int x = 0; x < Columns; x++)
            {
                int a = y * (Columns + 1) + x, b = a + 1, c = a + Columns + 1, d = c + 1;
                triangles.Add(new FoldTriangle { a = a, b = c, c = b, depth = lifts[a] + lifts[c] + lifts[b] });
                triangles.Add(new FoldTriangle { a = b, b = c, c = d, depth = lifts[b] + lifts[c] + lifts[d] });
            }
            triangles.Sort((a, b) => a.depth.CompareTo(b.depth));
            foreach (var t in triangles) mesh.AddTriangle(t.a, t.b, t.c);
        }
    }
}
