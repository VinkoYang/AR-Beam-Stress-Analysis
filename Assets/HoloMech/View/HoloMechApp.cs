using ARBeam;
using UnityEngine;

namespace HoloMech
{
    public enum AppState
    {
        /// <summary>Scene 2: section, boundary, length and material.</summary>
        BeamInfo,

        /// <summary>Scene 3: point and uniform loads.</summary>
        Loading,

        /// <summary>Scene 4: deflected beam.</summary>
        Deformation,
    }

    /// <summary>
    /// Entry point and state controller of the HoloMech scene. All screens live in one Unity scene (the beam stays in the
    /// scene, so nothing is passed between scenes): Scene 2 → (2.a–c dimensions popup) → Scene 3 → Scene 4, with Back
    /// available throughout. Scene 5 (stress profile) is added by milestone 4.
    /// </summary>
    public class HoloMechApp : MonoBehaviour
    {
        [SerializeField] UiTheme theme;
        [SerializeField] HoloMechBeamView beamView;
        [SerializeField] Camera viewCamera;

        [Tooltip("Where to start. Deformation starts from the inputs set on the beam view (for demos and screenshots).")]
        [SerializeField] AppState startState = AppState.BeamInfo;

        HoloMechInputs inputs;
        BeamInfoScreen info;
        DimensionsPopup dimensions;
        LoadingScreen loading;
        DeformationPanel deformation;
        ErrorPopup error;

        public AppState State { get; private set; }
        public HoloMechInputs Inputs => inputs;
        public DeformationPanel DeformationPanel => deformation;

        void Start()
        {
            if (theme == null) { Debug.LogError("HoloMechApp needs a UiTheme."); return; }
            if (beamView == null) beamView = FindAnyObjectByType<HoloMechBeamView>();
            UiKit.EnsureEventSystem();

            beamView.BuildOnStart = startState == AppState.Deformation;
            inputs = startState == AppState.Deformation ? beamView.Inputs.Clone() : new HoloMechInputs();

            var canvas = UiKit.ScreenCanvas("HoloMech UI", transform);
            info = BeamInfoScreen.Create(theme, canvas.transform, inputs);
            loading = LoadingScreen.Create(theme, canvas.transform, inputs);
            deformation = DeformationPanel.Create(theme, canvas.transform, beamView);
            dimensions = DimensionsPopup.Create(theme, canvas.transform, inputs);
            error = ErrorPopup.Create(theme, canvas.transform);

            info.SectionChosen += _ => dimensions.Show();
            dimensions.Applied += () => { };
            info.nextButton.onClick.AddListener(OnInfoNext);
            loading.backButton.onClick.AddListener(() => Go(AppState.BeamInfo));
            loading.nextButton.onClick.AddListener(OnLoadingNext);
            deformation.backButton.onClick.AddListener(() => Go(AppState.Loading));

            FrameCamera();
            Go(startState);
        }

        void OnInfoNext()
        {
            inputs.ClampLoadsToLength();
            // Only the beam itself is checked here; loads are entered on the next screen.
            var problems = inputs.Validate();
            problems.RemoveAll(p => p.StartsWith("Point load") || p.StartsWith("Uniform load"));
            if (problems.Count > 0) { error.Show(string.Join("\n\n", problems)); return; }
            Go(AppState.Loading);
        }

        void OnLoadingNext()
        {
            if (!beamView.Apply(inputs)) return; // SolveFailed already showed the message
            Go(AppState.Deformation);
        }

        void OnEnable()
        {
            if (beamView != null) beamView.SolveFailed += OnSolveFailed;
        }

        void OnDisable()
        {
            if (beamView != null) beamView.SolveFailed -= OnSolveFailed;
        }

        void OnSolveFailed(string message)
        {
            if (error != null) error.Show(message);
        }

        public void Go(AppState state)
        {
            State = state;
            if (state == AppState.Loading) loading.Sync();
            if (state == AppState.BeamInfo) info.Sync();
            if (state == AppState.Deformation && startState == AppState.Deformation && beamView.Solution == null) beamView.Rebuild(true);

            info.gameObject.SetActive(state == AppState.BeamInfo);
            loading.gameObject.SetActive(state == AppState.Loading);
            deformation.gameObject.SetActive(state == AppState.Deformation);
            beamView.gameObject.SetActive(state == AppState.Deformation);
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
