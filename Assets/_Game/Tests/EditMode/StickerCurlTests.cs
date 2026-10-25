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
