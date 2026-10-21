using System;
using Oculus.Interaction;
using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// Interaction SDK transformer for the Quest window and its panels: the grabbed object follows the grab point
    /// (hand, controller or ray) freely in 3D and keeps facing the user's head, so a panel never ends up edge-on.
    /// Set as the one- and two-grab transformer of a <see cref="Grabbable"/> whose target is the window or a panel.
    /// </summary>
    public class FaceUserGrabTransformer : MonoBehaviour, ITransformer
    {
        IGrabbable grabbable;
        Vector3 offset;

        /// <summary>Raised when a grab starts (the window pins itself so Follow does not fight the hand).</summary>
        public event Action Began;

        public void Initialize(IGrabbable owner) => grabbable = owner;

        public void BeginTransform()
        {
            offset = grabbable.Transform.position - grabbable.GrabPoints[0].position;
            Began?.Invoke();
        }

        public void UpdateTransform()
        {
            var target = grabbable.Transform;
            target.position = grabbable.GrabPoints[0].position + offset;
            FaceUser(target);
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
    }
}
