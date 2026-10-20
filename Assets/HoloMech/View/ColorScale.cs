using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// Green → yellow → red ramp used for deflection (and, later, the section-cut quantity). By default a value is
    /// normalised to the current beam's own range, as in the original HoloMech; with a locked scale a fixed full
    /// scale is used so different loads can be compared.
    /// </summary>
    public static class ColorScale
    {
        static readonly Color Green = new Color(0.10f, 0.72f, 0.20f);
        static readonly Color Yellow = new Color(1f, 0.92f, 0.12f);
        static readonly Color Red = new Color(0.95f, 0.12f, 0.08f);

        /// <summary>t in [0, 1]: 0 = green, 0.5 = yellow, 1 = red.</summary>
        public static Color Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? Color.Lerp(Green, Yellow, t * 2f) : Color.Lerp(Yellow, Red, (t - 0.5f) * 2f);
        }
    }
}
