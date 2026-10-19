using TMPro;
using UnityEngine;

namespace ARBeam
{
    /// <summary>
    /// Shared look for every screen in the project (rig scenes and HoloMech): Inter type scale, navy translucent
    /// panels, white text, blue accent. Created and filled by the editor tool ARBeam ▸ UI ▸ Build Theme Assets.
    /// Sizes are for a 1920×1080 reference canvas.
    /// </summary>
    [CreateAssetMenu(menuName = "ARBeam/UI Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        [Header("Type (Inter)")]
        public TMP_FontAsset regular;
        public TMP_FontAsset medium;
        public TMP_FontAsset semiBold;
        public TMP_FontAsset bold;

        [Header("Type scale, px at 1920×1080")]
        public float displaySize = 56f;
        public float titleSize = 32f;
        public float labelSize = 24f;
        public float valueSize = 24f;
        public float buttonSize = 24f;
        public float captionSize = 18f;

        [Header("Shapes")]
        [Tooltip("White rounded rectangle, 9-sliced. Tint with the colours below.")]
        public Sprite roundedRect;
        [Tooltip("White circle, used for slider handles.")]
        public Sprite circle;

        [Header("Colours")]
        public Color panel = new Color32(0x10, 0x1A, 0x3A, 0xDB);
        public Color panelRaised = new Color32(0x1E, 0x2C, 0x5A, 0xFF);
        public Color panelHover = new Color32(0x29, 0x40, 0x7A, 0xFF);
        public Color accent = new Color32(0x3B, 0x82, 0xF6, 0xFF);
        public Color accentPressed = new Color32(0x25, 0x63, 0xEB, 0xFF);
        public Color textPrimary = Color.white;
        public Color textSecondary = new Color32(0xB8, 0xC4, 0xE0, 0xFF);
        public Color track = new Color(1f, 1f, 1f, 0.18f);
        public Color backdrop = new Color32(0x0B, 0x12, 0x26, 0xFF);
        public Color error = new Color32(0xF8, 0x71, 0x71, 0xFF);

        [Header("Diagram colours (HoloMech: cyan shear, yellow moment)")]
        public Color shearLine = new Color32(0x5E, 0xEA, 0xD4, 0xFF);
        public Color shearFill = new Color32(0x2D, 0xD4, 0xBF, 0x59);
        public Color momentLine = new Color32(0xFC, 0xD3, 0x4D, 0xFF);
        public Color momentPositiveFill = new Color32(0xFB, 0xBF, 0x24, 0x59);
        public Color momentNegativeFill = new Color32(0x60, 0xA5, 0xFA, 0x59);
        public Color diagramAxis = new Color(1f, 1f, 1f, 0.35f);
    }
}
