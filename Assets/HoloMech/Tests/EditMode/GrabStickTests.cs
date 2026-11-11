using NUnit.Framework;
using UnityEngine;

namespace HoloMech.Tests
{
    /// <summary>Thumbstick shaping and the push / pull of a grabbed object (<see cref="GrabStick"/>).</summary>
    public class GrabStickTests
    {
        [Test]
        public void DeadZoneThenQuadratic()
        {
            Assert.That(GrabStick.Shape(0f), Is.EqualTo(0f));
            Assert.That(GrabStick.Shape(GrabStick.DeadZone), Is.EqualTo(0f));
            Assert.That(GrabStick.Shape(-0.1f), Is.EqualTo(0f));
            Assert.That(GrabStick.Shape(1f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(GrabStick.Shape(-1f), Is.EqualTo(-1f).Within(1e-6f));
            float half = (0.5f - GrabStick.DeadZone) / (1f - GrabStick.DeadZone);
            Assert.That(GrabStick.Shape(0.5f), Is.EqualTo(half * half).Within(1e-6f));
        }

        [Test]
        public void FullStickGivesTheRatesPerSecond()
        {
            GrabStick.Step(new Vector2(1f, -1f), 0.5f, out float push, out float yaw);
            Assert.That(push, Is.EqualTo(-GrabStick.PushSpeed * 0.5f).Within(1e-6f), "back pulls closer");
            Assert.That(yaw, Is.EqualTo(GrabStick.TurnSpeed * 0.5f).Within(1e-4f), "right turns clockwise seen from above");
        }

        [Test]
        public void PushMovesAlongTheLineFromTheHead()
        {
            var head = new Vector3(0f, 1.6f, 0f);
            var p = GrabStick.Push(head, new Vector3(0f, 1.6f, 2f), 0.5f, false, out float applied);
            Assert.That(applied, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(Vector3.Distance(p, new Vector3(0f, 1.6f, 2.5f)), Is.LessThan(1e-5f));
        }

        [Test]
        public void LevelPushKeepsTheHeight()
        {
            var head = new Vector3(0f, 1.6f, 0f);
            var beam = new Vector3(3f, 2.43f, 4f); // a girder overhead, 5 m away horizontally
            var p = GrabStick.Push(head, beam, -1f, true, out float applied);
            Assert.That(p.y, Is.EqualTo(2.43f).Within(1e-5f));
            Assert.That(applied, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(new Vector2(p.x, p.z).magnitude, Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void PushStopsAtTheLimitsWithoutWindingUp()
        {
            var head = Vector3.zero;
            var p = GrabStick.Push(head, new Vector3(0f, 0f, 0.5f), -1f, false, out float applied);
            Assert.That(p.z, Is.EqualTo(GrabStick.MinDistance).Within(1e-5f));
            Assert.That(applied, Is.EqualTo(GrabStick.MinDistance - 0.5f).Within(1e-5f), "only what was applied is kept");

            GrabStick.Push(head, new Vector3(0f, 0f, 9.8f), 1f, false, out applied);
            Assert.That(applied, Is.EqualTo(GrabStick.MaxDistance - 9.8f).Within(1e-4f));

            // Already closer than the limit (a hand grab at the body): pulling does nothing, pushing works.
            GrabStick.Push(head, new Vector3(0f, 0f, 0.1f), -0.05f, false, out applied);
            Assert.That(applied, Is.EqualTo(0f).Within(1e-6f));
            GrabStick.Push(head, new Vector3(0f, 0f, 0.1f), 0.05f, false, out applied);
            Assert.That(applied, Is.EqualTo(0.05f).Within(1e-6f));
        }
    }
}
