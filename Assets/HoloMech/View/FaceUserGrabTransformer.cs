using System;
using Oculus.Interaction;
using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// Interaction SDK transformer for the Quest window and its panels: the grabbed object follows the grab point
    /// (hand, controller or ray) freely in 3D and keeps facing the user's head, so a panel never ends up edge-on.
    /// While grabbed, the thumbstick pushes it away / pulls it closer (forward / back) and turns it about the vertical
    /// (left / right) on top of facing the user; the turn is kept after release (<see cref="GrabStick"/>).
    /// Set as the one- and two-grab transformer of a <see cref="Grabbable"/> whose target is the window or a panel.
    /// </summary>
    public class FaceUserGrabTransformer : MonoBehaviour, ITransformer
    {
        IGrabbable grabbable;
        Vector3 offset;
        float push;
        float yaw;

        /// <summary>Raised when a grab starts (the window pins itself so Follow does not fight the hand).</summary>
        public event Action Began;

        public void Initialize(IGrabbable owner) => grabbable = owner;

        public void BeginTransform()
        {
            var target = grabbable.Transform;
            offset = target.position - grabbable.GrabPoints[0].position;
            push = 0f;
            yaw = YawFromFacing(target);
            Began?.Invoke();
        }

        public void UpdateTransform()
        {
            var target = grabbable.Transform;
            var position = grabbable.GrabPoints[0].position + offset;

            GrabStick.Step(GrabStick.Read(), Time.deltaTime, out float dPush, out float dYaw);
            yaw = Mathf.Repeat(yaw + dYaw + 180f, 360f) - 180f;
            var head = GrabStick.Head();
            if (head.HasValue)
            {
                position = GrabStick.Push(head.Value, position, push + dPush, false, out float applied);
                push = applied;
            }

            target.position = position;
            FaceUser(target);
            target.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * target.rotation;
        }

        public void EndTransform() { }

        /// <summary>Turns a board or panel so its front faces the head (canvas +Z points away from the user).</summary>
        public static void FaceUser(Transform target)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var away = target.position - cam.transform.position;
            if (away.sqrMagnitude < 1e-4f) return;
            target.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        /// <summary>How far (degrees, about the vertical) the object is turned from facing the head; 0 without a camera.</summary>
        static float YawFromFacing(Transform target)
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            var away = Vector3.ProjectOnPlane(target.position - cam.transform.position, Vector3.up);
            var forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (away.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(away, forward, Vector3.up);
        }
    }
}
