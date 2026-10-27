using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    public enum StickerBoardState { Available, Blocked, Mystery, Locked }

    // Lock chains only. Ordinary stickers have no destination badges.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StickerBoardAdornment : MaskableGraphic
    {
        public void Configure()
        {
            raycastTarget = false;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Vector2 size = rectTransform.rect.size;
            Chain(mesh, new Vector2(-size.x, -size.y) * .35f, size * .35f, size);
            Chain(mesh, new Vector2(-size.x, size.y) * .35f, new Vector2(size.x, -size.y) * .35f, size);
        }
        static void Chain(VertexHelper mesh, Vector2 start, Vector2 end, Vector2 size)
        {
            Vector2 axis = (end - start).normalized;
            float unit = Mathf.Min(size.x, size.y), rx = (end - start).magnitude / 6 * .65f;
            for (int link = 0; link < 7; link++)
            {
                Vector2 center = Vector2.Lerp(start, end, link / 6f);
                float ry = unit * (link % 2 == 0 ? .047f : .031f), width = unit * .019f;
                Ring(mesh, center + new Vector2(2, -3), axis, rx + 2, ry + 2, width + 3, new Color(0, 0, 0, .55f));
                Ring(mesh, center, axis, rx + 1.4f, ry + 1.4f, width + 2.8f, new Color(.12f, .16f, .2f));
                Ring(mesh, center, axis, rx, ry, width, new Color(.68f, .76f, .83f));
                Ring(mesh, center + Vector2.up * .9f, axis, rx - .6f, ry - .6f, Mathf.Max(.7f, width * .3f), new Color(.97f, .99f, 1));
            }
        }
        static void Ring(VertexHelper mesh, Vector2 center, Vector2 axis, float rx, float ry, float width, Color tint)
        {
            Vector2 normal = new Vector2(-axis.y, axis.x); int first = mesh.currentVertCount;
            const int segments = 20;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(center + axis * Mathf.Cos(angle) * rx + normal * Mathf.Sin(angle) * ry, tint, Vector2.zero);
                mesh.AddVert(center + axis * Mathf.Cos(angle) * Mathf.Max(.1f, rx - width) + normal * Mathf.Sin(angle) * Mathf.Max(.1f, ry - width), tint, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = first + i * 2, b = first + (i + 1) % segments * 2;
                mesh.AddTriangle(a, b, a + 1); mesh.AddTriangle(a + 1, b, b + 1);
            }
        }
    }
}
