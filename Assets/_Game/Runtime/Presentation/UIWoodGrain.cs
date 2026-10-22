using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort
{
    // Decorative vector grain, rebuilt only when the Canvas geometry changes.
    // No texture, animation, input or puzzle dependency.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UIWoodGrain : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = rectTransform.rect;
            int lines = Mathf.CeilToInt(r.height / 9);
            for (int line = 0; line < lines; line++)
            {
                float y = r.yMin + line * 9;
                float thickness = line % 7 == 0 ? 2.4f : .9f;
                Color tint = line % 3 == 0 ? new Color(1, .9f, .58f, .13f) : new Color(.38f, .16f, .025f, .075f);
                for (int part = 0; part < 16; part++)
                {
                    float x1 = r.xMin + r.width * part / 16;
                    float x2 = r.xMin + r.width * (part + 1) / 16;
                    float y1 = y + Mathf.Sin(part * .65f + line * .41f) * 3;
                    float y2 = y + Mathf.Sin((part + 1) * .65f + line * .41f) * 3;
                    int start = mesh.currentVertCount;
                    mesh.AddVert(new Vector3(x1, y1), tint, Vector2.zero);
                    mesh.AddVert(new Vector3(x2, y2), tint, Vector2.zero);
                    mesh.AddVert(new Vector3(x2, y2 + thickness), tint, Vector2.zero);
                    mesh.AddVert(new Vector3(x1, y1 + thickness), tint, Vector2.zero);
                    mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
                }
            }
        }
    }
}
