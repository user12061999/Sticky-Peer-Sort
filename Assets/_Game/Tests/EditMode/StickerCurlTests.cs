using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StickyPeerSort.Tests
{
    public sealed class StickerCurlTests
    {
        static VertexHelper Quad()
        {
            var mesh = new VertexHelper();
            mesh.AddVert(new Vector3(-100, -60), Color.white, new Vector2(.2f, .3f));
            mesh.AddVert(new Vector3(-100, 60), Color.white, new Vector2(.2f, .8f));
            mesh.AddVert(new Vector3(100, 60), Color.white, new Vector2(.7f, .8f));
            mesh.AddVert(new Vector3(100, -60), Color.white, new Vector2(.7f, .3f));
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0); return mesh;
        }
        [Test] public void PageFoldAddsCurvedUndersideAndPreservesSpriteUVRange()
        {
            var obj = new GameObject("Curl test", typeof(RectTransform), typeof(Image), typeof(StickerCurlMesh));
            try
            {
                var curl = obj.GetComponent<StickerCurlMesh>(); curl.Amount = .7f;
                using (var mesh = Quad())
                {
                    curl.ModifyMesh(mesh); Assert.Greater(mesh.currentVertCount, 4);
                    bool underside = false, front = false, bent = false;
                    for (int i = 0; i < mesh.currentVertCount; i++)
                    {
                        var vertex = new UIVertex(); mesh.PopulateUIVertex(ref vertex, i);
                        Assert.That(vertex.uv0.x, Is.InRange(.2f, .70001f)); Assert.That(vertex.uv0.y, Is.InRange(.3f, .80001f));
                        underside |= vertex.uv1.x > .9f; front |= vertex.uv1.x < .1f;
                        bent |= vertex.position.y > 61;
                        Assert.IsFalse(float.IsNaN(vertex.position.x));
                    }
                    Assert.IsTrue(underside); Assert.IsTrue(front); Assert.IsTrue(bent);
                }
                curl.Amount = 0;
                using (var flat = Quad())
                {
                    curl.ModifyMesh(flat); Assert.AreEqual(4, flat.currentVertCount);
                    var vertex = new UIVertex(); flat.PopulateUIVertex(ref vertex, 2); Assert.AreEqual(new Vector3(100, 60), vertex.position);
                }
            }
            finally { Object.DestroyImmediate(obj); }
        }
        [TestCase(0)] [TestCase(45)] [TestCase(90)] [TestCase(180)]
        public void FlexibleFoldKeepsUVsAndDrawsLiftedTrianglesAfterTheFront(float direction)
        {
            var obj = new GameObject("Flexible curl test", typeof(RectTransform), typeof(Image), typeof(StickerCurlMesh));
            var result = new Mesh();
            try
            {
                var curl = obj.GetComponent<StickerCurlMesh>(); curl.Amount = .65f; curl.DirectionDegrees = direction;
                curl.ArtworkBounds = new Rect(.1f, .1f, .8f, .8f);
                using (var helper = Quad()) { curl.ModifyMesh(helper); helper.FillMesh(result); }
                var curlData = new System.Collections.Generic.List<Vector4>(); result.GetUVs(1, curlData);
                bool transition = false;
                foreach (var data in curlData) { transition |= data.x > 0 && data.x < 1; Assert.IsFalse(float.IsNaN(data.z)); }
                Assert.IsTrue(transition, "The front/back transition should be continuous, not a binary white fragment.");
                var uv = result.uv;
                foreach (var point in uv) { Assert.That(point.x, Is.InRange(.2f, .70001f)); Assert.That(point.y, Is.InRange(.3f, .80001f)); }
                var indices = result.triangles; float previous = -1;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    float depth = curlData[indices[i]].z + curlData[indices[i + 1]].z + curlData[indices[i + 2]].z;
                    Assert.GreaterOrEqual(depth + .0001f, previous, "Lifted parts must render after the flat front."); previous = depth;
                }
            }
            finally { Object.DestroyImmediate(result); Object.DestroyImmediate(obj); }
        }
        [Test] public void BothGameBalancesReferenceTheIncludedCurlShader()
        {
            foreach (var path in new[] { "Assets/_Game/Data/Balance.asset", "Assets/_Game/ReferenceStyle/Data/ReferenceBalance.asset" })
            {
                var config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(path);
                Assert.IsTrue(config.pagePeel); Assert.IsNotNull(config.pagePeelMaterial);
                Assert.AreEqual("StickyPeerSort/UI/StickerCurl", config.pagePeelMaterial.shader.name);
                Assert.IsTrue(config.pagePeelMaterial.shader.isSupported);
            }
        }
    }
}
