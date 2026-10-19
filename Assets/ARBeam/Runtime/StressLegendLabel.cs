using UnityEngine;

namespace ARBeam
{
    /// <summary>Writes the beam's fixed colour-scale limits onto the stress legend.</summary>
    public class StressLegendLabel : MonoBehaviour
    {
        [SerializeField] BeamController beam;
        [SerializeField] TMPro.TMP_Text maxLabel;
        [SerializeField] TMPro.TMP_Text minLabel;

        void OnEnable()
        {
            if (beam == null) beam = FindAnyObjectByType<BeamController>();
            if (beam != null) beam.Solved += OnSolved;
            Refresh();
        }

        void OnDisable()
        {
            if (beam != null) beam.Solved -= OnSolved;
        }

        void OnSolved(BeamController _) => Refresh();

        void Refresh()
        {
            if (beam == null) return;
            if (maxLabel != null) maxLabel.text = $"{beam.StressScalePa / 1e6f:0} MPa";
            if (minLabel != null) minLabel.text = "0 MPa";
        }
    }
}
