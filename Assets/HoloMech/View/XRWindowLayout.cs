using System.Collections.Generic;
using ARBeam;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Quest window (PI's request, 2026-10-21): the whole app sits in one window that can be grabbed and moved in 3D
    /// by the handle under it; the beam and the panels are children and move with it. The Beam panel, the Load panel
    /// and the result menu each live on their own world-space canvas with a handle on top, so each can be pulled
    /// anywhere (also outside the window) and keeps facing the user. Reset Layout on the window bar puts the panels and
    /// the beam back where they started and brings the window back in front of the user.
    ///
    /// The holders, handles and canvases (with Meta's ray / poke canvas and grab interactions) are prepared in the
    /// scene by <c>HoloMechQuestSceneBuilder</c>; at runtime <see cref="Attach"/> moves the panels into them.
    /// </summary>
    public class XRWindowLayout : MonoBehaviour
    {
        [SerializeField] Transform beamPanelHolder;
        [SerializeField] Transform loadPanelHolder;
        [SerializeField] Transform resultsHolder;
        [SerializeField] Canvas barCanvas;
        [SerializeField] FaceUserGrabTransformer windowGrab;

        XRPresentation presentation;
        readonly List<(Transform t, Vector3 position, Quaternion rotation)> defaults = new List<(Transform, Vector3, Quaternion)>();

        public bool Attached { get; private set; }

        public void SetUp(Transform beamPanel, Transform loadPanel, Transform results, Canvas bar, FaceUserGrabTransformer grab)
        {
            beamPanelHolder = beamPanel;
            loadPanelHolder = loadPanel;
            resultsHolder = results;
            barCanvas = bar;
            windowGrab = grab;
        }

        /// <summary>Moves the panels into their holders, builds the window bar, and records the start layout.</summary>
        public void Attach(UiTheme t, WorkbenchLayout workbench, RectTransform deformation, RectTransform stress,
                           Canvas mainCanvas, XRPresentation xr, Transform beam)
        {
            Fill(workbench.LeftPanel, beamPanelHolder);
            Fill(workbench.RightPanel, loadPanelHolder);
            workbench.HideCollapseButtons();
            foreach (var menu in new[] { deformation, stress })
            {
                menu.SetParent(Canvas(resultsHolder).transform, false);
                menu.anchorMin = menu.anchorMax = menu.pivot = new Vector2(0.5f, 0.5f);
                menu.anchoredPosition = Vector2.zero;
                menu.localScale = Vector3.one;
            }

            // Window backplate behind everything on the main canvas.
            var plate = UiKit.Panel(t, mainCanvas.transform, "Window Backplate", new Color(t.backdrop.r, t.backdrop.g, t.backdrop.b, 0.55f));
            plate.rectTransform.Stretch();
            plate.raycastTarget = false;
            plate.transform.SetAsFirstSibling();

            BuildBar(t);
            presentation = xr;
            if (windowGrab != null && xr != null) windowGrab.Began += () => xr.SetPinned(true);

            defaults.Clear();
            foreach (var h in new[] { beamPanelHolder, loadPanelHolder, resultsHolder, beam })
                if (h != null) defaults.Add((h, h.localPosition, h.localRotation));
            Attached = true;
        }

        /// <summary>
        /// Puts the panels and the beam back where they started in the window, and the window back in front of the
        /// user (so a window pushed away or placed before head tracking settled is easy to recover).
        /// </summary>
        public void ResetLayout()
        {
            foreach (var (t, position, rotation) in defaults)
            {
                t.localPosition = position;
                t.localRotation = rotation;
            }

            if (presentation != null) presentation.PlaceInFrontOfUser();
            else FaceUserGrabTransformer.FaceUser(transform);
        }

        /// <summary>Workbench side panels only in the Workbench layout; the result menu only on the result screens.</summary>
        public void SetVisible(bool workbenchPanels, bool results)
        {
            beamPanelHolder.gameObject.SetActive(workbenchPanels);
            loadPanelHolder.gameObject.SetActive(workbenchPanels);
            resultsHolder.gameObject.SetActive(results);
        }

        static Canvas Canvas(Transform holder) => holder.GetComponentInChildren<Canvas>(true);

        static void Fill(RectTransform panel, Transform holder)
        {
            panel.SetParent(Canvas(holder).transform, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.localScale = Vector3.one;
        }

        void BuildBar(UiTheme t)
        {
            if (barCanvas == null) return;
            var bar = UiKit.Panel(t, barCanvas.transform, "Window Bar", t.panel);
            bar.rectTransform.Stretch();
            var title = UiKit.Label(t, bar.transform, "Title", "HoloMech — Beam Stress Analysis", t.semiBold, t.labelSize, t.textPrimary, TextAlignmentOptions.MidlineLeft);
            title.rectTransform.Stretch(new Vector2(28, 0), new Vector2(-320, 0));
            var reset = UiKit.Button(t, bar.transform, "Reset Layout", "Reset Layout", t.captionSize + 4);
            reset.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(260, 60));
            reset.onClick.AddListener(ResetLayout);
        }
    }
}
