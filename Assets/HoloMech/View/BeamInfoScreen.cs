using System;
using ARBeam;
using ARBeam.Mechanics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Scene 2 (paper Fig. 3): Section Type, Boundary Selection and Beam Information panels side by side, and Next.
    /// </summary>
    public class BeamInfoScreen : MonoBehaviour
    {
        public Button nextButton;
        public Button editDimensionsButton;

        HoloMechInputs inputs;
        UiTheme t;
        Toggle pipe, box, iBeam;
        Toggle[] left = new Toggle[3], right = new Toggle[3];
        Slider lengthSlider, modulusSlider;
        TMP_Text lengthLabel, modulusLabel;
        readonly Button[] materialButtons = new Button[4];
        bool syncing;

        public event Action<SectionType> SectionChosen;

        static readonly EndCondition[] Ends = { EndCondition.Free, EndCondition.Pinned, EndCondition.Fixed };

        public static BeamInfoScreen Create(UiTheme t, Transform canvas, HoloMechInputs inputs)
        {
            var root = UiKit.Rect("Beam Info Screen", canvas);
            root.Stretch();
            var s = root.gameObject.AddComponent<BeamInfoScreen>();
            s.t = t;
            s.inputs = inputs;

            var section = s.Column(root, "Section Type", "Select the type of beam\nand edit the dimensions", -670, 380);
            var boundary = s.Column(root, "Boundary Selection", "Select the boundary type for each end of the beam", -110, 660);
            var info = s.Column(root, "Beam Information", "Use the sliders to select the beam length and Elastic Module of the material", 560, 600);

            s.BuildSection(section);
            s.BuildBoundary(boundary);
            s.BuildInfo(info);

            s.editDimensionsButton = UiKit.Button(t, section, "Edit dimensions", "Edit dimensions", t.captionSize + 4);
            s.editDimensionsButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -436), new Vector2(340, 64));
            s.editDimensionsButton.onClick.AddListener(() => s.SectionChosen?.Invoke(inputs.section));

            s.nextButton = UiKit.Button(t, root, "Next", "Next  ›", 30);
            s.nextButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(320, 92));

            s.Sync();
            return s;
        }

        /// <summary>A titled column: heading and hint above a panel; returns the panel's content rect.</summary>
        RectTransform Column(Transform parent, string title, string hint, float centerX, float width)
        {
            var col = UiKit.Rect(title + " Column", parent);
            col.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(centerX, -150), new Vector2(width, 640));

            var h = UiKit.Label(t, col, "Title", title, t.bold, t.titleSize, t.textPrimary, TextAlignmentOptions.TopLeft);
            h.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, 0), new Vector2(width, 44));
            var sub = UiKit.Label(t, col, "Hint", hint, t.regular, t.captionSize + 2, t.textSecondary, TextAlignmentOptions.TopLeft);
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -48), new Vector2(width - 16, 56));
            sub.textWrappingMode = TextWrappingModes.Normal;

            var panel = UiKit.Panel(t, col, "Panel", t.panel);
            panel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -124), new Vector2(width, 420));
            return panel.rectTransform;
        }

        void BuildSection(RectTransform panel)
        {
            var group = panel.gameObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
            var types = new[] { SectionType.Pipe, SectionType.Box, SectionType.IBeam };
            var names = new[] { "Pipe-Beam", "Box-Beam", "I-Beam" };
            var toggles = new Toggle[3];
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var tg = UiKit.Checkbox(t, panel, names[i], names[i], inputs.section == types[i], group);
                tg.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90 - i * 104), new Vector2(340, 88));
                tg.onValueChanged.AddListener(on =>
                {
                    if (!on || syncing) return;
                    inputs.section = types[k];
                    SectionChosen?.Invoke(types[k]);
                });
                toggles[i] = tg;
            }

            pipe = toggles[0];
            box = toggles[1];
            iBeam = toggles[2];
            var head = UiKit.Label(t, panel, "Heading", "Beam Type", t.semiBold, t.labelSize + 2, t.textSecondary, TextAlignmentOptions.Center);
            head.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(340, 40));
        }

        void BuildBoundary(RectTransform panel)
        {
            string[] captions = { "Free", "Pinned", "Fixed" };
            for (int side = 0; side < 2; side++)
            {
                bool isLeft = side == 0;
                var heading = UiKit.Label(t, panel, isLeft ? "Left End" : "Right End", isLeft ? "Left End" : "Right End", t.semiBold,
                    t.labelSize + 2, t.textSecondary, TextAlignmentOptions.Center);
                float cx = isLeft ? -158 : 158;
                heading.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(cx, -22), new Vector2(300, 40));

                var host = UiKit.Rect(isLeft ? "Left Options" : "Right Options", panel);
                host.Stretch();
                var group = host.gameObject.AddComponent<ToggleGroup>();
                group.allowSwitchOff = false;
                var arr = isLeft ? left : right;
                for (int i = 0; i < 3; i++)
                {
                    int k = i;
                    var cur = isLeft ? inputs.leftEnd : inputs.rightEnd;
                    var tg = UiKit.Checkbox(t, host, (isLeft ? "L " : "R ") + captions[i], captions[i], cur == Ends[i], group);
                    tg.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(cx, -90 - i * 104), new Vector2(300, 88));
                    tg.onValueChanged.AddListener(on =>
                    {
                        if (!on || syncing) return;
                        if (isLeft) inputs.leftEnd = Ends[k]; else inputs.rightEnd = Ends[k];
                    });
                    arr[i] = tg;
                }
            }
        }

        void BuildInfo(RectTransform panel)
        {
            lengthSlider = UiKit.LabeledSlider(t, panel, "Length", "Beam Length (cm)", HoloMechRanges.LengthMinCm, HoloMechRanges.LengthMaxCm,
                inputs.lengthCm, 1f, "{0:0}", out lengthLabel, v => inputs.lengthCm = v);
            Fit(lengthSlider.transform.parent, -28, 84);
            modulusSlider = UiKit.LabeledSlider(t, panel, "Modulus", "Elastic Module (GPa)", HoloMechRanges.ModulusMinGPa, HoloMechRanges.ModulusMaxGPa,
                inputs.elasticModulusGPa, 1f, "{0:0}", out modulusLabel, v =>
                {
                    if (syncing) return;
                    inputs.elasticModulusGPa = v;
                    inputs.material = MaterialPreset.Custom;
                    HighlightMaterial();
                });
            Fit(modulusSlider.transform.parent, -148, 84);

            var presets = new[] { MaterialPreset.Steel, MaterialPreset.Concrete, MaterialPreset.Wood, MaterialPreset.Custom };
            for (int i = 0; i < 4; i++)
            {
                var preset = presets[i];
                var b = UiKit.Button(t, panel, preset.ToString(), preset.ToString(), t.captionSize + 4);
                var rt = b.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(i / 4f, 0);
                rt.anchorMax = new Vector2((i + 1) / 4f, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.offsetMin = new Vector2(i == 0 ? 20 : 6, 24);
                rt.offsetMax = new Vector2(i == 3 ? -20 : -6, 100);
                b.onClick.AddListener(() =>
                {
                    inputs.ApplyMaterial(preset);
                    Sync();
                });
                materialButtons[i] = b;
            }
        }

        static void Fit(Transform row, float y, float height)
        {
            var r = (RectTransform)row;
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.offsetMin = new Vector2(28, y - height);
            r.offsetMax = new Vector2(-28, y);
        }

        void HighlightMaterial()
        {
            var presets = new[] { MaterialPreset.Steel, MaterialPreset.Concrete, MaterialPreset.Wood, MaterialPreset.Custom };
            for (int i = 0; i < 4; i++)
            {
                var img = materialButtons[i].GetComponent<Image>();
                bool on = inputs.material == presets[i];
                var c = materialButtons[i].colors;
                c.normalColor = on ? t.accent : t.panelRaised;
                c.selectedColor = c.normalColor;
                materialButtons[i].colors = c;
                img.color = Color.white;
            }
        }

        /// <summary>Refreshes every control from the inputs (after Back, or after a preset).</summary>
        public void Sync()
        {
            syncing = true;
            pipe.isOn = inputs.section == SectionType.Pipe;
            box.isOn = inputs.section == SectionType.Box;
            iBeam.isOn = inputs.section == SectionType.IBeam;
            for (int i = 0; i < 3; i++)
            {
                left[i].isOn = inputs.leftEnd == Ends[i];
                right[i].isOn = inputs.rightEnd == Ends[i];
            }

            UiKit.SetSlider(lengthSlider, lengthLabel, "{0:0}", inputs.lengthCm);
            UiKit.SetSlider(modulusSlider, modulusLabel, "{0:0}", inputs.elasticModulusGPa);
            HighlightMaterial();
            syncing = false;
        }
    }
}
