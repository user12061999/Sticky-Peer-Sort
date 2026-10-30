using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort
{
    // Fullscreen dark overlay with circular spotlight cutouts.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialSpotlightGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        Vector2 center1;
        float radius1;
        Vector2 center2;
        float radius2;
        float softness = 14f;
        Material spotlightMat;

        public void SetCircle1(Vector2 center, float radius)
        {
            center1 = center;
            radius1 = radius;
            SetMaterialDirty();
        }

        public void SetCircle2(Vector2 center, float radius)
        {
            center2 = center;
            radius2 = radius;
            SetMaterialDirty();
        }

        public void ClearCircle2()
        {
            radius2 = 0f;
            SetMaterialDirty();
        }

        public void SetTarget(RectTransform target, RectTransform relativeTo = null, float padding = 26f)
        {
            if (target == null) return;
            if (relativeTo == null) relativeTo = rectTransform;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 c0 = relativeTo.InverseTransformPoint(corners[0]);
            Vector2 c1 = relativeTo.InverseTransformPoint(corners[1]);
            Vector2 c2 = relativeTo.InverseTransformPoint(corners[2]);
            Vector2 c3 = relativeTo.InverseTransformPoint(corners[3]);
            Vector2 localCenter = (c0 + c2) * 0.5f;
            float w = Vector2.Distance(c1, c2);
            float h = Vector2.Distance(c0, c1);
            float r = Mathf.Max(w, h) * 0.54f + padding;
            SetCircle1(localCenter, r);
        }

        public void SetSecondaryTarget(RectTransform secondary, RectTransform relativeTo = null, float padding = 18f)
        {
            if (secondary == null)
            {
                ClearCircle2();
                return;
            }
            if (relativeTo == null) relativeTo = rectTransform;
            var corners = new Vector3[4];
            secondary.GetWorldCorners(corners);
            Vector2 c0 = relativeTo.InverseTransformPoint(corners[0]);
            Vector2 c1 = relativeTo.InverseTransformPoint(corners[1]);
            Vector2 c2 = relativeTo.InverseTransformPoint(corners[2]);
            Vector2 c3 = relativeTo.InverseTransformPoint(corners[3]);
            Vector2 localCenter = (c0 + c2) * 0.5f;
            float w = Vector2.Distance(c1, c2);
            float h = Vector2.Distance(c0, c1);
            float r = Mathf.Max(w, h) * 0.54f + padding;
            SetCircle2(localCenter, r);
        }

        public override Material materialForRendering
        {
            get
            {
                if (spotlightMat == null)
                {
                    var s = Shader.Find("UI/TutorialCircularSpotlight");
                    if (s != null) spotlightMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
                }
                if (spotlightMat != null)
                {
                    spotlightMat.SetVector("_Center1", new Vector4(center1.x, center1.y, 0, 0));
                    spotlightMat.SetFloat("_Radius1", radius1);
                    spotlightMat.SetVector("_Center2", new Vector4(center2.x, center2.y, 0, 0));
                    spotlightMat.SetFloat("_Radius2", radius2);
                    spotlightMat.SetFloat("_Softness", softness);
                    spotlightMat.SetColor("_Color", color);
                    return spotlightMat;
                }
                return base.materialForRendering;
            }
        }

        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out Vector2 local))
                return false;

            // Inside circular spotlight 1: raycast falls through to the sticker underneath
            if (radius1 > 0f && Vector2.Distance(local, center1) <= radius1)
                return false;

            // Inside circular spotlight 2: raycast falls through to destination
            if (radius2 > 0f && Vector2.Distance(local, center2) <= radius2)
                return false;

            // Outside circular spotlights: dark veil absorbs taps
            return true;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            Color c = color;
            vh.AddVert(new Vector3(r.xMin, r.yMin, 0), c, new Vector2(0, 0));
            vh.AddVert(new Vector3(r.xMin, r.yMax, 0), c, new Vector2(0, 1));
            vh.AddVert(new Vector3(r.xMax, r.yMax, 0), c, new Vector2(1, 1));
            vh.AddVert(new Vector3(r.xMax, r.yMin, 0), c, new Vector2(1, 0));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (spotlightMat != null)
            {
                if (Application.isPlaying) Destroy(spotlightMat);
                else DestroyImmediate(spotlightMat);
                spotlightMat = null;
            }
        }
    }
}
