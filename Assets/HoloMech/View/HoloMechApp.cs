using ARBeam;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    public enum AppState
    {
        /// <summary>Scene 2: section, boundary, length and material (Guided layout only).</summary>
        BeamInfo,

        /// <summary>Scene 3: point and uniform loads (Guided layout only).</summary>
        Loading,

        /// <summary>Scene 4: deflected beam.</summary>
        Deformation,

        /// <summary>Scene 5: diagrams and section cut.</summary>
        Stress,
    }

    public enum AppLayout
    {
        /// <summary>All inputs on screen; the beam updates while sliders move.</summary>
        Workbench,

        /// <summary>The paper's step-by-step flow: Scene 2 → 3 → 4 ⇄ 5 with Next / Back.</summary>
        Guided,
    }

    /// <summary>
    /// Entry point and state controller of the HoloMech scene. Everything lives in one Unity scene (the beam stays in
    /// the scene, so nothing is passed between scenes). Two layouts share the same inputs, beam and result menus:
    /// Workbench (default) and Guided (the paper's flow).
    /// </summary>
    public class HoloMechApp : MonoBehaviour
    {
        [SerializeField] UiTheme theme;
        [SerializeField] HoloMechBeamView beamView;
        [SerializeField] Camera viewCamera;
        [SerializeField] AppLayout layout = AppLayout.Workbench;

        [Tooltip("Mixed reality: panels on a world-space board in front of the user, XRI input, pin / follow (milestone 5).")]
        [SerializeField] bool worldSpace;

        [Tooltip("Optional world-space canvas prepared in the scene (Quest: with Meta ray and poke canvas interaction). " +
                 "Empty: the app creates its own canvas.")]
        [SerializeField] Canvas boardCanvas;

        [Tooltip("Optional: build the controls from Meta's UI Set prefabs (Quest scene).")]
        [SerializeField] MetaUiSkin uiSkin;

        [Tooltip("Optional (Quest): a grabbable window whose panels can each be moved; see XRWindowLayout.")]
        [SerializeField] XRWindowLayout windowLayout;

        [Tooltip("Guided layout only: where to start. Deformation starts from the inputs set on the beam view (demos).")]
        [SerializeField] AppState startState = AppState.BeamInfo;

        HoloMechInputs inputs;
        BeamInfoScreen info;
        DimensionsPopup dimensions;
        LoadingScreen loading;
        DeformationPanel deformation;
        StressPanel stress;
        StressProfileView stressView;
        WorkbenchLayout workbench;
        ErrorPopup error;
        Button layoutButton;
        XRPresentation xr;
        bool dirty, switching;

        public AppState State { get; private set; }
        public AppLayout Layout => layout;
        public HoloMechInputs Inputs => inputs;
        public DeformationPanel DeformationPanel => deformation;
        public StressPanel StressPanel => stress;
        public StressProfileView StressView => stressView;
        public WorkbenchLayout Workbench => workbench;

        void Start()
        {
            if (theme == null) { Debug.LogError("HoloMechApp needs a UiTheme."); return; }
            if (beamView == null) beamView = FindAnyObjectByType<HoloMechBeamView>();
            UiKit.EnsureEventSystem();

            bool fromView = layout == AppLayout.Guided && startState == AppState.Deformation;
            beamView.BuildOnStart = false;
            inputs = fromView ? beamView.Inputs.Clone() : new HoloMechInputs();

            stressView = beamView.gameObject.GetComponent<StressProfileView>() ?? beamView.gameObject.AddComponent<StressProfileView>();
            stressView.Initialise(beamView, theme);

            UiKit.Skin = uiSkin;
            var canvas = boardCanvas != null ? boardCanvas : UiKit.ScreenCanvas("HoloMech UI", transform);
            info = BeamInfoScreen.Create(theme, canvas.transform, inputs);
            loading = LoadingScreen.Create(theme, canvas.transform, inputs);
            workbench = WorkbenchLayout.Create(theme, canvas.transform, inputs, () => dirty = true);
            workbench.LayoutChanged += () => { PlaceResultMenus(layout == AppLayout.Guided); FrameCamera(); };
            deformation = DeformationPanel.Create(theme, canvas.transform, beamView);
            stress = StressPanel.Create(theme, canvas.transform, stressView, beamView);
            dimensions = DimensionsPopup.Create(theme, canvas.transform, inputs);
            error = ErrorPopup.Create(theme, canvas.transform);

            layoutButton = UiKit.Button(theme, canvas.transform, "Layout", "", theme.captionSize + 2);
            layoutButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(300, 48));
            layoutButton.onClick.AddListener(() => SetLayout(layout == AppLayout.Workbench ? AppLayout.Guided : AppLayout.Workbench));

            if (worldSpace)
            {
                xr = GetComponent<XRPresentation>();
                if (xr == null) xr = gameObject.AddComponent<XRPresentation>();
                xr.Setup(canvas, beamView);
                xr.PinnedChanged += UpdatePinLabels;
                if (windowLayout != null)
                    windowLayout.Attach(theme, workbench, (RectTransform)deformation.transform, (RectTransform)stress.transform,
                                        canvas, xr, beamView.transform);
            }

            deformation.pinButton.onClick.AddListener(TogglePin);
            stress.pinButton.onClick.AddListener(TogglePin);
            UpdatePinLabels();

            info.SectionChosen += _ => dimensions.Show();
            info.nextButton.onClick.AddListener(OnInfoNext);
            loading.backButton.onClick.AddListener(() => Go(AppState.BeamInfo));
            loading.nextButton.onClick.AddListener(OnLoadingNext);
            deformation.backButton.onClick.AddListener(Back);
            stress.backButton.onClick.AddListener(Back);
            deformation.stressTab.interactable = true;
            deformation.stressTab.onValueChanged.AddListener(on => { if (!switching) Go(on ? AppState.Stress : AppState.Deformation); });
            deformation.deformationTab.onValueChanged.AddListener(on => { if (!switching) Go(on ? AppState.Deformation : AppState.Stress); });
            stress.deformationTab.onValueChanged.AddListener(on => { if (!switching) Go(on ? AppState.Deformation : AppState.Stress); });
            stress.stressTab.onValueChanged.AddListener(on => { if (!switching) Go(on ? AppState.Stress : AppState.Deformation); });
            beamView.Solved += _ => { if (State == AppState.Stress) stressView.Rebuild(); };

            SetLayout(layout, fromView ? startState : (AppState?)null);
        }

        void OnDestroy()
        {
            if (UiKit.Skin == uiSkin) UiKit.Skin = null;
        }

        void Update()
        {
            if (!dirty || layout != AppLayout.Workbench) return;
            dirty = false;
            Solve(false);
        }

        /// <summary>Workbench: validate and re-solve; problems go to the banner and the last good beam stays.</summary>
        bool Solve(bool resetScale)
        {
            var problems = inputs.Validate();
            workbench.ShowProblems(problems);
            if (problems.Count > 0) return false;
            bool rebuiltBefore = beamView.Solution == null;
            bool ok = beamView.Apply(inputs, resetScale || beamView.AutoScale);
            if (ok && rebuiltBefore) FrameCamera();
            return ok;
        }

        public void SetLayout(AppLayout newLayout, AppState? state = null)
        {
            layout = newLayout;
            layoutButton.GetComponentInChildren<TMPro.TMP_Text>().text =
                layout == AppLayout.Workbench ? "Guided flow (paper) ›" : "Workbench (all panels) ›";
            if (layout == AppLayout.Workbench)
            {
                workbench.Sync();
                Solve(true);
                Go(State == AppState.Stress ? AppState.Stress : AppState.Deformation);
            }
            else
            {
                workbench.ShowProblems(null);
                Go(state ?? AppState.BeamInfo);
            }
        }

        /// <summary>Pin button: pinned = the board stays put; follow = it stays in front of the user (XR only).</summary>
        void TogglePin()
        {
            if (xr != null) xr.TogglePin(); // labels follow through PinnedChanged
        }

        void UpdatePinLabels()
        {
            string label = xr == null ? "Pin" : xr.Pinned ? "Pinned" : "Follow";
            foreach (var b in new[] { deformation.pinButton, stress.pinButton })
            {
                b.GetComponentInChildren<TMPro.TMP_Text>().text = label;
                b.interactable = xr != null;
            }
        }

        void OnInfoNext()
        {
            inputs.ClampLoadsToLength();
            var problems = inputs.Validate();
            problems.RemoveAll(p => p.StartsWith("Point load") || p.StartsWith("Uniform load"));
            if (problems.Count > 0) { error.Show(string.Join("\n\n", problems)); return; }
            Go(AppState.Loading);
        }

        void OnLoadingNext()
        {
            if (!beamView.Apply(inputs)) return; // SolveFailed shows the message
            Go(AppState.Deformation);
        }

        void Back()
        {
            if (layout == AppLayout.Guided) Go(AppState.Loading);
            else Go(AppState.Deformation);
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
            if (layout == AppLayout.Guided && error != null) error.Show(message);
            else if (workbench != null) workbench.ShowProblems(new[] { message });
        }

        public void Go(AppState state)
        {
            State = state;
            bool guided = layout == AppLayout.Guided;
            if (state == AppState.Loading) loading.Sync();
            if (state == AppState.BeamInfo) info.Sync();
            if ((state == AppState.Deformation || state == AppState.Stress) && beamView.Solution == null && guided && startState == AppState.Deformation)
                beamView.Apply(inputs);

            bool result = state == AppState.Deformation || state == AppState.Stress;
            info.gameObject.SetActive(guided && state == AppState.BeamInfo);
            loading.gameObject.SetActive(guided && state == AppState.Loading);
            workbench.gameObject.SetActive(!guided);
            deformation.gameObject.SetActive(state == AppState.Deformation);
            stress.gameObject.SetActive(state == AppState.Stress);
            beamView.gameObject.SetActive(result && beamView.Solution != null);
            if (Detached) windowLayout.SetVisible(!guided, result);

            // Tabs mirror the state; assigning isOn restyles the switches, the guard stops it re-entering Go.
            switching = true;
            deformation.deformationTab.isOn = true;
            deformation.stressTab.isOn = false;
            stress.deformationTab.isOn = false;
            stress.stressTab.isOn = true;
            switching = false;

            if (beamView.Solution != null)
            {
                stressView.Show(state == AppState.Stress);
                if (state == AppState.Stress) stress.Sync();
            }

            PlaceResultMenus(guided);
            FrameCamera();
        }

        /// <summary>
        /// True when the result menu sits bottom-right as in the paper: always in Guided, and in the Workbench once both
        /// side panels are collapsed. Otherwise it sits bottom-centre between the panels.
        /// </summary>
        bool MenuInCorner => layout == AppLayout.Guided || (workbench.LeftCollapsed && workbench.RightCollapsed);

        /// <summary>Quest window: the panels and the result menu sit on their own canvases and are moved by hand.</summary>
        bool Detached => windowLayout != null && windowLayout.Attached;

        void PlaceResultMenus(bool guided)
        {
            if (Detached) return;
            bool corner = MenuInCorner;
            foreach (var r in new[] { (RectTransform)deformation.transform, (RectTransform)stress.transform })
            {
                if (corner)
                {
                    r.anchorMin = r.anchorMax = new Vector2(1, 0);
                    r.pivot = new Vector2(1, 0);
                    r.anchoredPosition = new Vector2(-48, 64);
                    r.localScale = Vector3.one;
                }
                else
                {
                    // Between the panels; full size once one side panel is out of the way.
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 0);
                    r.pivot = new Vector2(0.5f, 0);
                    r.anchoredPosition = new Vector2(0, 64);
                    bool roomy = workbench.FreeRight - workbench.FreeLeft > 0.6f;
                    r.localScale = Vector3.one * (roomy ? 1f : 0.74f);
                }
            }
        }

        /// <summary>
        /// Fits the beam (and, in Scene 5, the diagrams under it) into the free part of the screen: the band above the
        /// result menu, and in the Workbench layout between the side panels. The camera looks straight along +Z and is
        /// offset sideways so the content lands in that band.
        /// </summary>
        public void FrameCamera()
        {
            if (worldSpace) return; // in XR the user's head is the camera
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null || beamView == null) return;
            bool guided = layout == AppLayout.Guided;
            bool stressMode = State == AppState.Stress;

            float D = beamView.DisplayLength;
            // Content box in the beam's local frame (x along the beam, y up).
            float xMin = -D * (stressMode ? 0.2f : 0.08f), xMax = D * (stressMode ? 1.2f : 1.08f);
            float yMax = D * 0.22f, yMin = stressMode ? -D * 0.68f : -D * 0.22f;

            // Free screen area as fractions of the screen. With the menu in the bottom-right corner the stress view uses
            // the whole height to its left (as in the paper); otherwise the content sits above the menu.
            float x0 = guided ? 0.03f : workbench.FreeLeft + 0.01f;
            float x1 = guided ? 0.97f : workbench.FreeRight - 0.01f;
            float menuScale = ((RectTransform)deformation.transform).localScale.y;
            float y0 = (64f + 580f * menuScale + 24f) / 1080f, y1 = 0.95f;
            if (stressMode && MenuInCorner)
            {
                x1 = 1f - (48f + 940f + 90f) / 1920f; // clear of the menu and the moment-axis title
                y0 = 0.06f;
            }

            float tanV = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * viewCamera.aspect;
            float w = xMax - xMin, h = yMax - yMin;
            // Distance at which the content fills the free area in both directions.
            float d = Mathf.Max(w / (2 * tanH * (x1 - x0)), h / (2 * tanV * (y1 - y0)));

            var centreLocal = new Vector3((xMin + xMax) / 2, (yMin + yMax) / 2, 0);
            var centre = beamView.transform.TransformPoint(centreLocal);
            // Screen offset of the free area's centre from the screen centre, converted to world units at distance d.
            float sx = ((x0 + x1) / 2 - 0.5f) * 2 * d * tanH;
            float sy = ((y0 + y1) / 2 - 0.5f) * 2 * d * tanV;
            viewCamera.transform.rotation = Quaternion.identity;
            viewCamera.transform.position = centre + new Vector3(-sx, -sy, -d);
        }
    }
}
