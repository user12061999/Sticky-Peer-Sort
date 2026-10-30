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

        public void SetTarget(RectTransform target, RectTransform relativeTo, float padding = 16f)
        {
            if (target == null || relativeTo == null) return;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 centerWorld = (corners[0] + corners[2]) * 0.5f;
            Vector2 localCenter = relativeTo.InverseTransformPoint(centerWorld);
            float w = Vector3.Distance(corners[1], corners[2]);
            float h = Vector3.Distance(corners[0], corners[1]);
            float r = Mathf.Max(w, h) * 0.52f + padding;
            SetCircle1(localCenter, r);
        }

        public void SetSecondaryTarget(RectTransform secondary, RectTransform relativeTo, float padding = 12f)
        {
            if (secondary == null || relativeTo == null)
            {
                ClearCircle2();
                return;
            }
            var corners = new Vector3[4];
            secondary.GetWorldCorners(corners);
            Vector3 centerWorld = (corners[0] + corners[2]) * 0.5f;
            Vector2 localCenter = relativeTo.InverseTransformPoint(centerWorld);
            float w = Vector3.Distance(corners[1], corners[2]);
            float h = Vector3.Distance(corners[0], corners[1]);
            float r = Mathf.Max(w, h) * 0.52f + padding;
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
