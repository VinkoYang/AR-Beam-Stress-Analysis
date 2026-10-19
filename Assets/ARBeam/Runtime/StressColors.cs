using UnityEngine;

namespace ARBeam
{
    /// <summary>
    /// The blue → green → yellow → orange → red ramp from the original scene legend (stress_gradient.png).
    /// Input is a fraction of the fixed full-scale stress.
    /// </summary>
    public static class StressColors
    {
        static readonly Color Orange = new Color(1f, 0.5f, 0f);

        public static Color Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.25f) return Color.Lerp(Color.blue, Color.green, t / 0.25f);
            if (t < 0.5f) return Color.Lerp(Color.green, Color.yellow, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Color.Lerp(Color.yellow, Orange, (t - 0.5f) / 0.25f);
            return Color.Lerp(Orange, Color.red, (t - 0.75f) / 0.25f);
        }
    }
}
