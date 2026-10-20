using ARBeam;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Scene 4 menu (paper Fig. 5): Deformation / Stress tabs, Deflection Scale slider (log, 1–100000),
    /// Deformation and Loading switches, "Max. Deflection" in cm, pin and back buttons.
    /// </summary>
    public class DeformationPanel : MonoBehaviour
    {
        public Toggle deformationTab, stressTab, deformationSwitch, loadingSwitch;
        public Slider scaleSlider;
        public TMP_Text scaleValue, maxDeflection;
        public Button pinButton, backButton;

        HoloMechBeamView view;
        bool syncing;

        public static DeformationPanel Create(UiTheme t, Transform canvas, HoloMechBeamView view)
        {
            var root = UiKit.Panel(t, canvas, "Deformation Menu", Color.clear);
            var rt = root.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-48, 48), new Vector2(920, 520));
            var panel = root.gameObject.AddComponent<DeformationPanel>();
            panel.view = view;

            // Tabs.
            var tabs = UiKit.Panel(t, rt, "Tabs", t.panel);
            tabs.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(780, 96));
            panel.deformationTab = UiKit.Switch(t, tabs.transform, "Deformation Profile", "Deformation Profile", true);
            Fit(panel.deformationTab.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.5f, 1), 10, 10, 5);
            panel.stressTab = UiKit.Switch(t, tabs.transform, "Stress Profile", "Stress Profile", false);
            Fit(panel.stressTab.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(1, 1), 5, 10, 10);
            panel.stressTab.interactable = false; // Scene 5 arrives with milestone 4

            // Controls.
            var body = UiKit.Panel(t, rt, "Body", t.panel);
            body.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(780, 404));

            var caption = UiKit.Label(t, body.transform, "Scale caption", "Deflection Scale", t.medium, t.labelSize, t.textPrimary, TextAlignmentOptions.MidlineLeft);
            caption.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(300, 40));
            panel.scaleValue = UiKit.Label(t, body.transform, "Scale value", "1", t.semiBold, t.valueSize, t.textPrimary, TextAlignmentOptions.MidlineRight);
            panel.scaleValue.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -28), new Vector2(300, 40));

            panel.scaleSlider = UiKit.Slider(t, body.transform, "Scale slider", 0f, 1f, 0f);
            var sr = (RectTransform)panel.scaleSlider.transform;
            sr.anchorMin = new Vector2(0, 1);
            sr.anchorMax = new Vector2(1, 1);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.offsetMin = new Vector2(28, -112);
            sr.offsetMax = new Vector2(-28, -80);

            panel.deformationSwitch = UiKit.Switch(t, body.transform, "Deformation", "Deformation", true);
            Fit(panel.deformationSwitch.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.5f, 0), 24, 110, 8, 80);
            panel.loadingSwitch = UiKit.Switch(t, body.transform, "Loading", "Loading", true);
            Fit(panel.loadingSwitch.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(1, 0), 8, 110, 24, 80);

            panel.maxDeflection = UiKit.Label(t, body.transform, "Max deflection", "", t.semiBold, t.valueSize + 2, t.textPrimary, TextAlignmentOptions.MidlineLeft);
            panel.maxDeflection.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 24), new Vector2(700, 56));

            // Pin and back buttons on the right.
            panel.pinButton = UiKit.Button(t, rt, "Pin", "Pin", t.captionSize + 4);
            panel.pinButton.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(120, 120));
            panel.backButton = UiKit.Button(t, rt, "Back", "‹", 48);
            panel.backButton.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -270), new Vector2(120, 120));

            panel.Bind();
            return panel;
        }

        /// <summary>Stretches a row between two anchors with pixel margins (left, top, right, bottom).</summary>
        static void Fit(RectTransform r, Vector2 min, Vector2 max, float left, float bottomOrTop, float right, float height = 0)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = new Vector2(0.5f, 0.5f);
            if (height > 0)
            {
                r.offsetMin = new Vector2(left, bottomOrTop);
                r.offsetMax = new Vector2(-right, bottomOrTop + height);
            }
            else
            {
                r.offsetMin = new Vector2(left, bottomOrTop);
                r.offsetMax = new Vector2(-right, -bottomOrTop);
            }
        }

        void Bind()
        {
            scaleSlider.onValueChanged.AddListener(v =>
            {
                if (syncing || view == null) return;
                view.AutoScale = false;
                view.SetDeflectionScale(UiKit.LogToValue(v, HoloMechRanges.ScaleMin, HoloMechRanges.ScaleMax));
                UpdateTexts();
            });
            deformationSwitch.onValueChanged.AddListener(v => { if (view != null) view.ShowDeformation = v; });
            loadingSwitch.onValueChanged.AddListener(v => { if (view != null) view.ShowLoading = v; });
            view.Solved += _ => SyncFromView();
            if (view.Solution != null) SyncFromView();
        }

        void SyncFromView()
        {
            syncing = true;
            scaleSlider.SetValueWithoutNotify(UiKit.ValueToLog(view.DeflectionScale, HoloMechRanges.ScaleMin, HoloMechRanges.ScaleMax));
            deformationSwitch.SetIsOnWithoutNotify(view.ShowDeformation);
            loadingSwitch.SetIsOnWithoutNotify(view.ShowLoading);
            syncing = false;
            UpdateTexts();
        }

        void UpdateTexts()
        {
            scaleValue.text = view.DeflectionScale.ToString("0");
            // Real maximum deflection (not multiplied by the scale), in cm, as in the paper.
            maxDeflection.text = $"<color=#{ColorUtility.ToHtmlStringRGB(new Color(0.72f, 0.77f, 0.88f))}>Max. Deflection:</color>  " +
                                 $"{view.MaxDeflectionMetres * 100:0.#####} cm";
        }
    }
}
