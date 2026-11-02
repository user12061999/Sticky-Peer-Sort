using NUnit.Framework;
using UnityEngine;

namespace StickyPeerSort.Tests
{
    public sealed class PeelDragProgressTests
    {
        [Test] public void PartialPeelReversesAndCommitsOnlyAtFullDistance()
        {
            var drag = new PeelDragProgress(new Vector2(20, 20), 100, .05f);
            Assert.AreEqual(0, drag.Update(new Vector2(18, 20)));
            Assert.AreEqual(.4f, drag.Update(new Vector2(-20, 20)), .0001f);
            Assert.AreEqual(.1f, drag.Update(new Vector2(10, 20)), .0001f);
            Assert.AreEqual(0, drag.Update(new Vector2(20, 20)));
            Assert.AreEqual(1, drag.Update(new Vector2(-100, 20)));
            Assert.AreEqual(Vector2.left, drag.Direction);
        }
        [Test] public void PerpendicularMotionDoesNotIncreasePeelOrSwitchDirection()
        {
            var drag = new PeelDragProgress(Vector2.zero, 100, .05f);
            Assert.AreEqual(.4f, drag.Update(new Vector2(0, 40)), .0001f);
            Assert.AreEqual(.4f, drag.Update(new Vector2(100, 40)), .0001f);
            Assert.AreEqual(0, drag.Update(new Vector2(100, -20)));
            Assert.AreEqual(Vector2.up, drag.Direction);
        }
        [Test] public void AnchoredPeelKeepsGrabDirectionAndCountsCurvedDrags()
        {
            var drag = new PeelDragProgress(Vector2.zero, 100, .1f, new Vector2(-1, -1));
            Assert.AreEqual(0, drag.Update(new Vector2(-5, -5)), "inside the dead zone");
            Assert.AreEqual(.46f, drag.Update(new Vector2(-55, 0)), .02f, "a curved, sideways-ish drag still peels");
            Assert.AreEqual(1, drag.Update(new Vector2(-80, -80)), .0001f);
            Assert.AreEqual(new Vector2(-1, -1).normalized, drag.Direction);
            float outward = drag.Update(new Vector2(60, 0)), inward = drag.Update(new Vector2(-60, 0));
            Assert.Less(outward, inward, "pulling away from the sticker peels slower than across it");
            Assert.AreEqual(0, drag.Update(Vector2.zero));
        }
    }
}
