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
    }
}
