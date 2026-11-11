using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// Thumbstick control while something is grabbed (window, panel or on-site beam): forward / back pushes the object
    /// away from or pulls it toward the user, left / right turns it about the vertical axis. Used by
    /// <see cref="FaceUserGrabTransformer"/> and <see cref="LevelGrabTransformer"/> in their UpdateTransform.
    /// Both thumbsticks are read and the one pushed further wins, so it works with whichever hand holds the grab.
    /// Locomotion that also listens to the thumbsticks is switched off by <see cref="LocomotionOff"/>.
    /// </summary>
    public static class GrabStick
    {
        /// <summary>Distance speed at full deflection, m/s.</summary>
        public const float PushSpeed = 1.0f;

        /// <summary>Turn speed at full deflection, degrees/s.</summary>
        public const float TurnSpeed = 60f;

        public const float DeadZone = 0.15f;

        /// <summary>Closest and farthest distance from the head an object can be pushed or pulled to, metres.</summary>
        public const float MinDistance = 0.25f, MaxDistance = 10f;

        /// <summary>The more deflected of the two thumbsticks (x = right, y = forward), zero off the headset.</summary>
        public static Vector2 Read()
        {
            var l = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            var r = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            return r.sqrMagnitude >= l.sqrMagnitude ? r : l;
        }

        /// <summary>
        /// Shapes one axis: zero inside the dead zone, then quadratic up to 1, so small deflections give fine control
        /// (the beam is aligned to millimetres) and full deflection moves fast.
        /// </summary>
        public static float Shape(float v)
        {
            float a = Mathf.Abs(v);
            if (a <= DeadZone) return 0f;
            float t = Mathf.Clamp01((a - DeadZone) / (1f - DeadZone));
            return Mathf.Sign(v) * t * t;
        }

        /// <summary>Distance change (m) and yaw change (degrees) for this frame from the stick value.</summary>
        public static void Step(Vector2 stick, float dt, out float push, out float yaw)
        {
            push = Shape(stick.y) * PushSpeed * dt;
            yaw = Shape(stick.x) * TurnSpeed * dt;
        }

        /// <summary>
        /// Moves <paramref name="position"/> along the line from the head by <paramref name="push"/> metres, staying
        /// between <see cref="MinDistance"/> and <see cref="MaxDistance"/>. With <paramref name="level"/> only the
        /// horizontal distance changes, so a level beam keeps its height. <paramref name="applied"/> is the push after the
        /// limits; the caller stores it so that holding the stick against a limit does not wind up.
        /// </summary>
        public static Vector3 Push(Vector3 head, Vector3 position, float push, bool level, out float applied)
        {
            applied = 0f;
            var away = position - head;
            if (level) away.y = 0f;
            float d = away.magnitude;
            if (d < 1e-4f) return position;
            float lo = Mathf.Min(MinDistance, d), hi = Mathf.Max(MaxDistance, d); // never jump when already outside
            float wanted = Mathf.Clamp(d + push, lo, hi);
            applied = wanted - d;
            return position + away / d * applied;
        }

        /// <summary>The head position, or null when there is no main camera.</summary>
        public static Vector3? Head()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : (Vector3?)null;
        }
    }
}
