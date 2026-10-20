using System;
using ARBeam;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Scenes 2.a–c: dimension sliders for the chosen section with a live preview of its cross-section and Apply.
    /// Sliders are in cm; dependent limits are enforced (pipe wall ≤ D/2, flanges thinner than h/2, web ≤ flange).
    /// </summary>
    public class DimensionsPopup : MonoBehaviour
    {
        public event Action Applied;

        UiTheme t;
        HoloMechInputs inputs;
        RectTransform sliderHost;
        SectionPreviewGraphic preview;
        TMP_Text subtitle, summary;
        Slider[] sliders = new Slider[4];
        TMP_Text[] labels = new TMP_Text[4];
        bool syncing;

        public static DimensionsPopup Create(UiTheme t, Transform canvas, HoloMechInputs inputs)
        {
            var root = UiKit.Panel(t, canvas, "Dimensions Popup", new Color(0.03f, 0.05f, 0.1f, 0.72f));
            root.raycastTarget = true; // blocks clicks on the screen behind
            root.sprite = null;
            root.type = Image.Type.Simple;
            root.rectTransform.Stretch();
            var p = root.gameObject.AddComponent<DimensionsPopup>();
            p.t = t;
            p.inputs = inputs;

            var card = UiKit.Panel(t, root.transform, "Card", Color.Lerp(t.panel, t.backdrop, 0.5f));
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 700));

            var title = UiKit.Label(t, card.transform, "Title", "Dimension", t.bold, t.titleSize, t.textPrimary, TextAlignmentOptions.TopLeft);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -30), new Vector2(600, 44));
            p.subtitle = UiKit.Label(t, card.transform, "Subtitle", "", t.regular, t.captionSize + 2, t.textSecondary, TextAlignmentOptions.TopLeft);
            p.subtitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -78), new Vector2(700, 32));

            p.sliderHost = UiKit.Rect("Sliders", card.transform);
            p.sliderHost.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -130), new Vector2(640, 380));

            var pv = UiKit.Panel(t, card.transform, "Preview Frame", t.panel);
            pv.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -130), new Vector2(400, 320));
            var gfx = UiKit.Rect("Section", pv.transform);
            gfx.Stretch(new Vector2(20, 20), new Vector2(-20, -20));
            p.preview = gfx.gameObject.AddComponent<SectionPreviewGraphic>();
            p.preview.color = new Color(0.8f, 0.84f, 0.92f, 1f);
            p.preview.raycastTarget = false;
            p.summary = UiKit.Label(t, card.transform, "Summary", "", t.medium, t.captionSize + 2, t.textSecondary, TextAlignmentOptions.TopLeft);
            p.summary.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -462), new Vector2(400, 60));
            p.summary.textWrappingMode = TextWrappingModes.Normal;

            var apply = UiKit.Button(t, card.transform, "Apply", "Apply", 28);
            apply.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(300, 84));
            apply.onClick.AddListener(() =>
            {
                p.gameObject.SetActive(false);
                p.Applied?.Invoke();
            });

            root.gameObject.SetActive(false);
            return p;
        }

        /// <summary>Opens the popup for the section currently selected in the inputs.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            for (int i = sliderHost.childCount - 1; i >= 0; i--) Destroy(sliderHost.GetChild(i).gameObject);
            sliders = new Slider[4];
            labels = new TMP_Text[4];

            switch (inputs.section)
            {
                case SectionType.Pipe:
                    subtitle.text = "Pipe-Beam: outer diameter and wall thickness";
                    Add(0, "Outer diameter D (cm)", HoloMechRanges.PipeDiameterMinCm, HoloMechRanges.PipeDiameterMaxCm, inputs.pipe.outerDiameterCm, v => inputs.pipe.outerDiameterCm = v);
                    Add(1, "Wall thickness t (cm)", HoloMechRanges.PipeWallMinCm, HoloMechRanges.PipeWallMaxCm, inputs.pipe.wallCm, v => inputs.pipe.wallCm = v);
                    break;
                case SectionType.Box:
                    subtitle.text = "Box-Beam: width, height and wall thickness (0 = solid)";
                    Add(0, "Width b (cm)", HoloMechRanges.BoxWidthMinCm, HoloMechRanges.BoxWidthMaxCm, inputs.box.widthCm, v => inputs.box.widthCm = v);
                    Add(1, "Height h (cm)", HoloMechRanges.BoxHeightMinCm, HoloMechRanges.BoxHeightMaxCm, inputs.box.heightCm, v => inputs.box.heightCm = v);
                    Add(2, "Wall thickness t (cm)", HoloMechRanges.BoxWallMinCm, HoloMechRanges.BoxWallMaxCm, inputs.box.wallCm, v => inputs.box.wallCm = v);
                    break;
                default:
                    subtitle.text = "I-Beam: height, flange width, flange and web thickness";
                    Add(0, "Height h (cm)", HoloMechRanges.IHeightMinCm, HoloMechRanges.IHeightMaxCm, inputs.iBeam.heightCm, v => inputs.iBeam.heightCm = v);
                    Add(1, "Flange width bf (cm)", HoloMechRanges.IFlangeWidthMinCm, HoloMechRanges.IFlangeWidthMaxCm, inputs.iBeam.flangeWidthCm, v => inputs.iBeam.flangeWidthCm = v);
                    Add(2, "Flange thickness tf (cm)", HoloMechRanges.IFlangeThicknessMinCm, HoloMechRanges.IFlangeThicknessMaxCm, inputs.iBeam.flangeThicknessCm, v => inputs.iBeam.flangeThicknessCm = v);
                    Add(3, "Web thickness tw (cm)", HoloMechRanges.IWebThicknessMinCm, HoloMechRanges.IWebThicknessMaxCm, inputs.iBeam.webThicknessCm, v => inputs.iBeam.webThicknessCm = v);
                    break;
            }

            Refresh();
        }

        void Add(int row, string caption, float min, float max, float value, Action<float> set)
        {
            var s = UiKit.LabeledSlider(t, sliderHost, caption, caption, min, max, value, 0.1f, "{0:0.0}", out labels[row], v =>
            {
                if (syncing) return;
                set(v);
                Refresh();
            });
            var rt = (RectTransform)s.transform.parent;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -row * 96 - 84);
            rt.offsetMax = new Vector2(0, -row * 96);
            sliders[row] = s;
        }

        /// <summary>Enforces dependent limits, mirrors them into the sliders and redraws the preview.</summary>
        void Refresh()
        {
            syncing = true;
            inputs.ClampSectionDimensions();
            switch (inputs.section)
            {
                case SectionType.Pipe:
                    Mirror(1, inputs.pipe.wallCm);
                    break;
                case SectionType.IBeam:
                    Mirror(2, inputs.iBeam.flangeThicknessCm);
                    Mirror(3, inputs.iBeam.webThicknessCm);
                    break;
            }

            syncing = false;
            var profile = SectionProfile.For(inputs);
            preview.SetProfile(profile);
            var sec = inputs.SectionProperties();
            summary.text = $"A = {sec.Area * 1e4:0.##} cm²\nI = {sec.I * 1e8:0.##} cm⁴";
        }

        void Mirror(int row, float value)
        {
            if (sliders[row] != null) UiKit.SetSlider(sliders[row], labels[row], "{0:0.0}", value);
        }
    }
}
