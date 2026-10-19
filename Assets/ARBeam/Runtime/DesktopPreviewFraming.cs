using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace ARBeam
{
    /// <summary>
    /// On a desktop (Editor Play mode with XR Simulation, or a PC build) moves the XR Origin so the whole beam is in
    /// view, seen from the side, and re-frames when the window's aspect ratio changes. Does nothing on phones and
    /// headsets, where the user's own head or device position decides the view.
    /// </summary>
    public class DesktopPreviewFraming : MonoBehaviour
    {
        [SerializeField] BeamController beam;
        [SerializeField] XROrigin origin;

        [Tooltip("Extra room around the beam, as a fraction of its length.")]
        [SerializeField, Range(1f, 2f)] float margin = 1.5f;

        [Tooltip("Shift the beam up from the screen centre by this fraction of the visible height, so the bottom control panel does not cover it.")]
        [SerializeField, Range(0f, 0.3f)] float raise = 0.08f;

        [Tooltip("Shift the beam left from the screen centre by this fraction of the visible width, clear of the stress legend on the right.")]
        [SerializeField, Range(0f, 0.3f)] float shiftLeft = 0.06f;

        float framedAspect;

        void Start()
        {
            if (!IsDesktop() || beam == null)
            {
                enabled = false;
                return;
            }

            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            StartCoroutine(FrameWhenCameraSettles());
        }

        void LateUpdate()
        {
            var cam = Camera();
            if (cam != null && framedAspect > 0 && Mathf.Abs(cam.aspect - framedAspect) > 0.01f) Frame();
        }

        IEnumerator FrameWhenCameraSettles()
        {
            // XR Simulation sets the starting camera pose during the first frames.
            for (int i = 0; i < 10; i++) yield return null;
            Frame();
        }

        /// <summary>Places the camera in front of the beam so its full length fits the view.</summary>
        public void Frame()
        {
            var cam = Camera();
            var renderer = beam.GetComponent<Renderer>();
            if (cam == null || renderer == null) return;

            Vector3 left = beam.BeamXToWorld(0), right = beam.BeamXToWorld(beam.LengthM);
            Vector3 centre = 0.5f * (left + right);
            float length = Vector3.Distance(left, right) * margin;

            float halfV = 0.5f * cam.fieldOfView * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * cam.aspect);
            float distance = 0.5f * length / Mathf.Tan(halfH);

            // Look across the beam, perpendicular to its axis, from the side the camera is already on.
            Vector3 axis = (right - left).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, axis).normalized;
            if (Vector3.Dot(across, cam.transform.position - centre) < 0) across = -across;
            Vector3 forward = -across;

            if (origin != null)
            {
                // Turn to face the beam, keep whatever pitch the camera has, and back off along the actual view
                // direction so the beam sits on the view axis (raised a little above the screen centre).
                origin.MatchOriginUpCameraForward(Vector3.up, forward);
                Transform view = cam.transform;
                float visibleHeight = 2f * distance * Mathf.Tan(halfV);
                float visibleWidth = visibleHeight * cam.aspect;
                Vector3 target = centre - view.up * (raise * visibleHeight) + view.right * (shiftLeft * visibleWidth);
                origin.MoveCameraToWorldLocation(target - view.forward * distance);
            }

            framedAspect = cam.aspect;
        }

        Camera Camera() => origin != null && origin.Camera != null ? origin.Camera : UnityEngine.Camera.main;

        static bool IsDesktop()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.LinuxPlayer:
                    return true;
                default:
                    return false;
            }
        }
    }
}
