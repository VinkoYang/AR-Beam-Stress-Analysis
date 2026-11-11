using System;
using Oculus.Interaction;
using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// Grab transformer for the on-site beam: the beam follows the hand and turns with the hand about the vertical
    /// axis only, so it stays level (real beams on shop-floor equipment are horizontal). Pitch and roll of the hand are
    /// ignored; fine alignment is done with the nudge buttons. While grabbed, the thumbstick pushes the beam away /
    /// pulls it closer horizontally (forward / back, height unchanged) and turns it about the vertical through the grab
    /// point (left / right), see <see cref="GrabStick"/>.
    /// </summary>
    public class LevelGrabTransformer : MonoBehaviour, ITransformer
    {
        IGrabbable grabbable;
        Vector3 offset;
        float startHandYaw;
        Quaternion startRotation;
        float push;
        float stickYaw;

        /// <summary>Raised when a grab starts and ends (the placement card shows the new pose).</summary>
        public event Action Began, Ended;

        public void Initialize(IGrabbable owner) => grabbable = owner;

        static float Yaw(Quaternion q)
        {
            var f = Vector3.ProjectOnPlane(q * Vector3.forward, Vector3.up);
            return f.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        public void BeginTransform()
        {
            var target = grabbable.Transform;
            var grip = grabbable.GrabPoints[0];
            offset = target.position - grip.position;
            startHandYaw = Yaw(grip.rotation);
            startRotation = target.rotation;
            push = 0f;
            stickYaw = 0f;
            Began?.Invoke();
        }

        public void UpdateTransform()
        {
            var target = grabbable.Transform;
            var grip = grabbable.GrabPoints[0];

            GrabStick.Step(GrabStick.Read(), Time.deltaTime, out float dPush, out float dYaw);
            stickYaw += dYaw;

            var turn = Quaternion.Euler(0f, Mathf.DeltaAngle(startHandYaw, Yaw(grip.rotation)) + stickYaw, 0f);
            target.rotation = turn * startRotation;
            var position = grip.position + turn * offset;

            // Push along the horizontal line from the head through the grab point, so the beam turns about the same
            // point it is held by and keeps its height.
            var head = GrabStick.Head();
            if (head.HasValue)
            {
                var held = grip.position;
                var pushed = GrabStick.Push(head.Value, held, push + dPush, true, out float applied);
                push = applied;
                position += pushed - held;
            }

            target.position = position;
        }

        public void EndTransform() => Ended?.Invoke();
    }
}
