using System;
using ARBeam;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Scene 3 (paper Fig. 4): Point-Load and Uniform-Load columns, each optional, with position / magnitude sliders and
    /// an up / down direction choice; Back and Next. Upward is positive on screen, as in the paper.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public Button backButton, nextButton;

        UiTheme t;
        HoloMechInputs inputs;

        Toggle pointEnable, uniformEnable;
        Slider pointPos, pointForce, uniStart, uniEnd, uniForce;
        TMP_Text pointPosL, pointForceL, uniStartL, uniEndL, uniForceL;
        Toggle pointUp, pointDown, uniUp, uniDown;
        bool syncing;

        public static LoadingScreen Create(UiTheme t, Transform canvas, HoloMechInputs inputs)
        {
            var root = UiKit.Rect("Loading Screen", canvas);
            root.Stretch();
            var s = root.gameObject.AddComponent<LoadingScreen>();
            s.t = t;
            s.inputs = inputs;

            var title = UiKit.Label(t, root, "Title", "Loading Condition", t.bold, t.titleSize + 4, t.textPrimary, TextAlignmentOptions.TopLeft);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(-860, -80), new Vector2(900, 52));
            var hint = UiKit.Label(t, root, "Hint", "Select the loading condition\nInput the information\nUpward load is positive", t.regular, t.captionSize + 2, t.textSecondary, TextAlignmentOptions.TopLeft);
            hint.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(-860, -136), new Vector2(900, 90));

            var card = UiKit.Panel(t, root, "Card", t.panel);
            card.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(1760, 640));

            s.BuildPoint(card.rectTransform);
            s.BuildUniform(card.rectTransform);

            s.backButton = UiKit.Button(t, root, "Back", "‹  Back", 28);
            s.backButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-220, 50), new Vector2(300, 88));
            s.nextButton = UiKit.Button(t, root, "Next", "Next  ›", 28);
            s.nextButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(220, 50), new Vector2(300, 88));
            return s;
        }

        void BuildPoint(RectTransform card)
        {
            var col = Column(card, "Point", -440);
            pointEnable = UiKit.Checkbox(t, col, "Point-Load", "Point-Load", inputs.pointLoad.enabled);
            Row(pointEnable.GetComponent<RectTransform>(), 0, 76);
            pointEnable.onValueChanged.AddListener(on => { if (!syncing) { inputs.pointLoad.enabled = on; Interactable(); } });

            pointPos = UiKit.LabeledSlider(t, col, "Position", "Distance from left-end (cm)", 0, inputs.lengthCm, inputs.pointLoad.positionCm, 1f, "{0:0}",
                out pointPosL, v => inputs.pointLoad.positionCm = v);
            Row(pointPos.transform.parent as RectTransform, 104, 84);
            pointForce = UiKit.LabeledSlider(t, col, "Force", "Force Magnitude (N)", 0, HoloMechRanges.ForceMaxN, inputs.pointLoad.forceN, 10f, "{0:0}",
                out pointForceL, v => inputs.pointLoad.forceN = v);
            Row(pointForce.transform.parent as RectTransform, 216, 84);
            Direction(col, 340, inputs.pointLoad.direction, d => inputs.pointLoad.direction = d, out pointUp, out pointDown);
        }

        void BuildUniform(RectTransform card)
        {
            var col = Column(card, "Uniform", 440);
            uniformEnable = UiKit.Checkbox(t, col, "Uniform-Load", "Uniform-Load", inputs.uniformLoad.enabled);
            Row(uniformEnable.GetComponent<RectTransform>(), 0, 76);
            uniformEnable.onValueChanged.AddListener(on => { if (!syncing) { inputs.uniformLoad.enabled = on; Interactable(); } });

            uniStart = UiKit.LabeledSlider(t, col, "Start", "Start distance from left-end (cm)", 0, inputs.lengthCm, inputs.uniformLoad.startCm, 1f, "{0:0}",
                out uniStartL, v =>
                {
                    if (syncing) return;
                    inputs.uniformLoad.startCm = v;
                    if (inputs.uniformLoad.endCm <= v) inputs.uniformLoad.endCm = Mathf.Min(v + 1f, inputs.lengthCm);
                    SyncUniform();
                });
            Row(uniStart.transform.parent as RectTransform, 104, 84);
            uniEnd = UiKit.LabeledSlider(t, col, "End", "End distance from left-end (cm)", 0, inputs.lengthCm, inputs.uniformLoad.endCm, 1f, "{0:0}",
                out uniEndL, v =>
                {
                    if (syncing) return;
                    inputs.uniformLoad.endCm = v;
                    if (inputs.uniformLoad.startCm >= v) inputs.uniformLoad.startCm = Mathf.Max(v - 1f, 0f);
                    SyncUniform();
                });
            Row(uniEnd.transform.parent as RectTransform, 204, 84);
            uniForce = UiKit.LabeledSlider(t, col, "Force", "Force Magnitude (N/m)", 0, HoloMechRanges.UniformMaxNPerM, inputs.uniformLoad.intensityNPerM, 10f, "{0:0}",
                out uniForceL, v => inputs.uniformLoad.intensityNPerM = v);
            Row(uniForce.transform.parent as RectTransform, 304, 84);
            Direction(col, 420, inputs.uniformLoad.direction, d => inputs.uniformLoad.direction = d, out uniUp, out uniDown);
        }

        RectTransform Column(RectTransform card, string name, float centerX)
        {
            var col = UiKit.Panel(t, card, name + " Column", t.panelRaised * new Color(1, 1, 1, 0.35f));
            col.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(centerX, 0), new Vector2(800, 600));
            return col.rectTransform;
        }

        static void Row(RectTransform r, float top, float height)
        {
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.offsetMin = new Vector2(30, -30 - top - height);
            r.offsetMax = new Vector2(-30, -30 - top);
        }

        void Direction(RectTransform col, float top, LoadDirection current, Action<LoadDirection> set, out Toggle up, out Toggle down)
        {
            var caption = UiKit.Label(t, col, "Direction", "Force Direction", t.medium, t.labelSize - 2, t.textPrimary, TextAlignmentOptions.TopLeft);
            caption.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30 - top), new Vector2(400, 34));
            var group = col.gameObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
            up = UiKit.Checkbox(t, col, "Up", "Up  ↑", current == LoadDirection.Up, group, t.labelSize);
            up.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -78 - top), new Vector2(340, 72));
            down = UiKit.Checkbox(t, col, "Down", "Down  ↓", current == LoadDirection.Down, group, t.labelSize);
            down.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(390, -78 - top), new Vector2(340, 72));
            up.onValueChanged.AddListener(on => { if (on && !syncing) set(LoadDirection.Up); });
            down.onValueChanged.AddListener(on => { if (on && !syncing) set(LoadDirection.Down); });
        }

        /// <summary>Refreshes ranges and values from the inputs (the length may have changed since the last visit).</summary>
        public void Sync()
        {
            syncing = true;
            inputs.ClampLoadsToLength();
            pointPos.maxValue = inputs.lengthCm;
            uniStart.maxValue = inputs.lengthCm;
            uniEnd.maxValue = inputs.lengthCm;
            UiKit.SetSlider(pointPos, pointPosL, "{0:0}", inputs.pointLoad.positionCm);
            UiKit.SetSlider(pointForce, pointForceL, "{0:0}", inputs.pointLoad.forceN);
            UiKit.SetSlider(uniForce, uniForceL, "{0:0}", inputs.uniformLoad.intensityNPerM);
            SyncUniform();
            pointEnable.isOn = inputs.pointLoad.enabled;
            uniformEnable.isOn = inputs.uniformLoad.enabled;
            pointUp.isOn = inputs.pointLoad.direction == LoadDirection.Up;
            pointDown.isOn = inputs.pointLoad.direction == LoadDirection.Down;
            uniUp.isOn = inputs.uniformLoad.direction == LoadDirection.Up;
            uniDown.isOn = inputs.uniformLoad.direction == LoadDirection.Down;
            syncing = false;
            Interactable();
        }

        void SyncUniform()
        {
            bool was = syncing;
            syncing = true;
            UiKit.SetSlider(uniStart, uniStartL, "{0:0}", inputs.uniformLoad.startCm);
            UiKit.SetSlider(uniEnd, uniEndL, "{0:0}", inputs.uniformLoad.endCm);
            syncing = was;
        }

        void Interactable()
        {
            pointPos.interactable = pointForce.interactable = pointUp.interactable = pointDown.interactable = inputs.pointLoad.enabled;
            uniStart.interactable = uniEnd.interactable = uniForce.interactable = uniUp.interactable = uniDown.interactable = inputs.uniformLoad.enabled;
        }
    }
}
