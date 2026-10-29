using System;
using Oculus.Interaction;
using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// Grab transformer for the on-site beam: the beam follows the hand and turns with the hand about the vertical
    /// axis only, so it stays level (real beams on shop-floor equipment are horizontal). Pitch and roll of the hand are
    /// ignored; fine alignment is done with the nudge buttons.
    /// </summary>
    public class LevelGrabTransformer : MonoBehaviour, ITransformer
    {
        IGrabbable grabbable;
        Vector3 offset;
        float startHandYaw;
        Quaternion startRotation;

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
            Began?.Invoke();
        }

        public void UpdateTransform()
        {
            var target = grabbable.Transform;
            var grip = grabbable.GrabPoints[0];
            var turn = Quaternion.Euler(0f, Mathf.DeltaAngle(startHandYaw, Yaw(grip.rotation)), 0f);
            target.rotation = turn * startRotation;
            target.position = grip.position + turn * offset;
        }

        public void EndTransform() => Ended?.Invoke();
    }
}
