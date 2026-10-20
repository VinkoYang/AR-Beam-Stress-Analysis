using System.Globalization;
using ARBeam;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Scene 5 menu (paper Fig. 7): Deformation / Stress tabs, the section-cut slider, a vertical green → red colour bar
    /// with a pointer and the value at the cut, and five switches: the two diagrams, and which quantity the cut shows
    /// (Shear Force / Bending Moment / Normal Stress, one at a time). Pin and back buttons on the right.
    /// </summary>
    public class StressPanel : MonoBehaviour
    {
        public Toggle deformationTab, stressTab;
        public Toggle shearDiagram, momentDiagram, shearCut, momentCut, stressCut;
        public Slider cutSlider;
        public TMP_Text cutValue;
        public Button pinButton, backButton;

        StressProfileView view;
        HoloMechBeamView beam;
        RectTransform barRect, pointer;
        TMP_Text pointerText, barMax, barMin;
        bool syncing;

        public static StressPanel Create(UiTheme t, Transform canvas, StressProfileView view, HoloMechBeamView beam)
        {
            var root = UiKit.Panel(t, canvas, "Stress Menu", Color.clear);
            var rt = root.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-48, 48), new Vector2(920, 560));
            var p = root.gameObject.AddComponent<StressPanel>();
            p.view = view;
            p.beam = beam;

            var tabs = UiKit.Panel(t, rt, "Tabs", t.panel);
            tabs.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(780, 96));
            p.deformationTab = UiKit.Switch(t, tabs.transform, "Deformation Profile", "Deformation Profile", false);
            Fit(p.deformationTab, new Vector2(0, 0), new Vector2(0.5f, 1), 10, 10, 5, 0);
            p.stressTab = UiKit.Switch(t, tabs.transform, "Stress Profile", "Stress Profile", true);
            Fit(p.stressTab, new Vector2(0.5f, 0), new Vector2(1, 1), 5, 10, 10, 0);

            var body = UiKit.Panel(t, rt, "Body", t.panel);
            body.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(780, 444));

            // Colour bar with pointer on the left.
            var bar = new GameObject("Colour bar", typeof(RectTransform), typeof(RawImage));
            p.barRect = (RectTransform)bar.transform;
            p.barRect.SetParent(body.transform, false);
            p.barRect.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(34, 340));
            bar.GetComponent<RawImage>().texture = GradientTexture();
            p.barMax = UiKit.Label(t, body.transform, "Bar max", "", t.medium, 18, t.textSecondary, TextAlignmentOptions.Left);
            p.barMax.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(190, 160), new Vector2(120, 24));
            p.barMin = UiKit.Label(t, body.transform, "Bar min", "", t.medium, 18, t.textSecondary, TextAlignmentOptions.Left);
            p.barMin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(190, -160), new Vector2(120, 24));

            p.pointer = UiKit.Rect("Pointer", p.barRect);
            p.pointer.Place(new Vector2(0, 0), new Vector2(1, 0.5f), Vector2.zero, new Vector2(140, 4));
            var line = UiKit.Panel(t, p.pointer, "Line", Color.white);
            line.sprite = null;
            line.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(40, 4));
            var tip = UiKit.Panel(t, p.pointer, "Tip", Color.white);
            tip.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-40, 0), new Vector2(14, 14));
            tip.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            p.pointerText = UiKit.Label(t, p.pointer, "Value", "", t.semiBold, 22, t.textPrimary, TextAlignmentOptions.Right);
            p.pointerText.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-50, 0), new Vector2(160, 60));
            p.pointerText.textWrappingMode = TextWrappingModes.Normal;

            // Controls on the right.
            var controls = UiKit.Rect("Controls", body.transform);
            controls.anchorMin = new Vector2(0, 0);
            controls.anchorMax = new Vector2(1, 1);
            controls.offsetMin = new Vector2(250, 20);
            controls.offsetMax = new Vector2(-20, -20);

            p.cutSlider = UiKit.LabeledSlider(t, controls, "Cut", "Section cut at (cm)", 0, 1, 0.5f, 0f, "{0:0}", out p.cutValue, v =>
            {
                if (p.syncing || p.beam.Solution == null) return;
                p.view.SetCut(v / 100f);
            });
            var cr = (RectTransform)p.cutSlider.transform.parent;
            cr.anchorMin = new Vector2(0, 1);
            cr.anchorMax = new Vector2(1, 1);
            cr.pivot = new Vector2(0.5f, 1);
            cr.offsetMin = new Vector2(0, -84);
            cr.offsetMax = Vector2.zero;

            p.shearDiagram = UiKit.Switch(t, controls, "Shear Force Diagram", "Shear Force Diagram", true);
            Fit(p.shearDiagram, new Vector2(0, 1), new Vector2(0.5f, 1), 0, -184, 6, 84);
            p.momentDiagram = UiKit.Switch(t, controls, "Bending Moment Diagram", "Bending Moment Diagram", true);
            Fit(p.momentDiagram, new Vector2(0.5f, 1), new Vector2(1, 1), 6, -184, 0, 84);
            p.shearCut = UiKit.Switch(t, controls, "Shear Force", "Shear Force", true);
            Fit(p.shearCut, new Vector2(0, 1), new Vector2(0.5f, 1), 0, -280, 6, 84);
            p.momentCut = UiKit.Switch(t, controls, "Bending Moment", "Bending Moment", false);
            Fit(p.momentCut, new Vector2(0.5f, 1), new Vector2(1, 1), 6, -280, 0, 84);
            p.stressCut = UiKit.Switch(t, controls, "Normal Stress", "Normal Stress", false);
            Fit(p.stressCut, new Vector2(0, 1), new Vector2(0.5f, 1), 0, -376, 6, 84);
            var brand = UiKit.Label(t, controls, "Brand", "<i>HoloMech</i> <size=60%>reproduction</size>", t.bold, 30, t.textPrimary, TextAlignmentOptions.MidlineRight);
            brand.rectTransform.anchorMin = new Vector2(0.5f, 1);
            brand.rectTransform.anchorMax = new Vector2(1, 1);
            brand.rectTransform.offsetMin = new Vector2(6, -376);
            brand.rectTransform.offsetMax = new Vector2(-10, -292);

            p.pinButton = UiKit.Button(t, rt, "Pin", "Pin", t.captionSize + 4);
            p.pinButton.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(120, 120));
            p.backButton = UiKit.Button(t, rt, "Back", "‹", 48);
            p.backButton.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -270), new Vector2(120, 120));

            p.Bind();
            return p;
        }

        /// <summary>Row between two anchors; y is the top edge (negative, from the parent's top) when height &gt; 0.</summary>
        static void Fit(Toggle tg, Vector2 min, Vector2 max, float left, float y, float right, float height)
        {
            var r = tg.GetComponent<RectTransform>();
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = new Vector2(0.5f, 0.5f);
            if (height > 0)
            {
                r.offsetMin = new Vector2(left, y);
                r.offsetMax = new Vector2(-right, y + height);
            }
            else
            {
                r.offsetMin = new Vector2(left, 10);
                r.offsetMax = new Vector2(-right, -10);
            }
        }

        static Texture2D GradientTexture()
        {
            var tex = new Texture2D(1, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 128; i++) tex.SetPixel(0, i, ColorScale.Evaluate(i / 127f));
            tex.Apply();
            return tex;
        }

        void Bind()
        {
            shearDiagram.onValueChanged.AddListener(_ => view.SetShowDiagrams(shearDiagram.isOn, momentDiagram.isOn));
            momentDiagram.onValueChanged.AddListener(_ => view.SetShowDiagrams(shearDiagram.isOn, momentDiagram.isOn));
            shearCut.onValueChanged.AddListener(on => Choose(on, CutQuantity.ShearForce));
            momentCut.onValueChanged.AddListener(on => Choose(on, CutQuantity.BendingMoment));
            stressCut.onValueChanged.AddListener(on => Choose(on, CutQuantity.NormalStress));
            view.Changed += Refresh;
        }

        /// <summary>The three cut quantities behave as radio buttons; switching the current one off is ignored.</summary>
        void Choose(bool on, CutQuantity q)
        {
            if (syncing) return;
            if (!on && view.Quantity == q)
            {
                Sync();
                return;
            }

            if (on) view.SetQuantity(q);
            Sync();
        }

        /// <summary>Called when the panel opens: slider range follows the beam length.</summary>
        public void Sync()
        {
            syncing = true;
            if (beam.Solution != null)
            {
                cutSlider.maxValue = (float)beam.Solution.Length * 100f;
                UiKit.SetSlider(cutSlider, cutValue, "{0:0}", view.CutMetres * 100f);
            }

            shearCut.isOn = view.Quantity == CutQuantity.ShearForce;
            momentCut.isOn = view.Quantity == CutQuantity.BendingMoment;
            stressCut.isOn = view.Quantity == CutQuantity.NormalStress;
            syncing = false;
            Refresh();
        }

        void Refresh()
        {
            var (min, max) = view.Range();
            double value = view.ValueAtCut();
            double t = max - min > 1e-12 ? (value - min) / (max - min) : 0.5;
            pointer.anchorMin = new Vector2(0, (float)t);
            pointer.anchorMax = new Vector2(0, (float)t);
            pointer.anchoredPosition = Vector2.zero;
            pointerText.text = $"{Name()}\n{value.ToString(Format(value), CultureInfo.InvariantCulture)}";
            barMax.text = max.ToString(Format(max), CultureInfo.InvariantCulture);
            barMin.text = min.ToString(Format(min), CultureInfo.InvariantCulture);
        }

        string Name()
        {
            switch (view.Quantity)
            {
                case CutQuantity.ShearForce: return "V (N)";
                case CutQuantity.BendingMoment: return "M (N·m)";
                default: return "σmax (MPa)";
            }
        }

        static string Format(double v) => System.Math.Abs(v) >= 100 ? "0.#" : "0.###";
    }
}
