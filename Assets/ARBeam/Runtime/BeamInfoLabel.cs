using ARBeam.Mechanics;
using UnityEngine;

namespace ARBeam
{
    /// <summary>Title and one-line description of the beam on screen, read from the BeamController so it stays true.</summary>
    public class BeamInfoLabel : MonoBehaviour
    {
        [SerializeField] BeamController beam;
        [SerializeField] TMPro.TMP_Text title;
        [SerializeField] TMPro.TMP_Text details;

        void OnEnable()
        {
            if (beam == null) beam = FindAnyObjectByType<BeamController>();
            Refresh();
        }

        public void Refresh()
        {
            if (beam == null) return;
            if (title != null) title.text = Title(beam.Supports);
            if (details != null)
                details.text = $"L = {beam.LengthM * 1000:0} mm   ·   {beam.Material.Name}   ·   " +
                               $"{beam.WidthMm:0.#} × {beam.ThicknessMm:0.##} mm bar";
        }

        static string Title(BeamSupports s)
        {
            switch (s.Type)
            {
                case SupportType.Cantilever: return "Cantilever beam";
                case SupportType.SimplySupported: return "Simply supported beam";
                case SupportType.Overhang: return $"Overhanging beam  (supports at {s.A * 1000:0} and {s.B * 1000:0} mm)";
                case SupportType.FixedFixed: return "Beam fixed at both ends";
                default: return $"Beam ({s})";
            }
        }
    }
}
