using ARBeam;
using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// Entry point of the HoloMech scene. Milestone 2: the beam is shown from the inputs set in the Inspector
    /// (the paper's Fig. 5 case by default) with the Scene 4 menu. The input panels (Scene 2 / 3) and the stress
    /// view (Scene 5) are added by later milestones.
    /// </summary>
    public class HoloMechApp : MonoBehaviour
    {
        [SerializeField] UiTheme theme;
        [SerializeField] HoloMechBeamView beamView;
        [SerializeField] Camera viewCamera;

        public DeformationPanel DeformationPanel { get; private set; }

        void Start()
        {
            if (theme == null) { Debug.LogError("HoloMechApp needs a UiTheme."); return; }
            if (beamView == null) beamView = FindAnyObjectByType<HoloMechBeamView>();
            UiKit.EnsureEventSystem();

            var canvas = UiKit.ScreenCanvas("HoloMech UI", transform);
            DeformationPanel = DeformationPanel.Create(theme, canvas.transform, beamView);

            FrameCamera();
        }

        /// <summary>Puts the camera in front of the beam so the whole length is visible above the menu.</summary>
        void FrameCamera()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;
            float length = beamView.DisplayLength;
            float halfV = viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * viewCamera.aspect);
            float distance = length * 0.78f / Mathf.Tan(Mathf.Min(halfH, halfV * 1.6f));
            var centre = beamView.transform.TransformPoint(new Vector3(length * 0.5f, 0, 0));
            viewCamera.transform.position = centre + new Vector3(0, -length * 0.02f, -distance) + Vector3.up * length * 0.12f;
            viewCamera.transform.rotation = Quaternion.LookRotation(centre - viewCamera.transform.position + Vector3.down * length * 0.2f, Vector3.up);
        }
    }
}
