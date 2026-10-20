using UnityEngine;

namespace HoloMech
{
    /// <summary>
    /// Meta Horizon OS UI Set prefabs (Interaction SDK, Runtime/Sample/Objects/UISet) used for the Quest build. When
    /// <see cref="UiKit.Skin"/> is set, UiKit builds buttons, switches, check boxes, segmented choices and sliders from
    /// these prefabs instead of drawing its own, so the panels keep their logic and layout but look and respond like
    /// Quest system UI (poke and ray, hover and press states). Filled by HoloMech ▸ Build Quest Scene.
    /// </summary>
    [CreateAssetMenu(menuName = "HoloMech/Meta UI Skin", fileName = "MetaUiSkin")]
    public class MetaUiSkin : ScriptableObject
    {
        [Tooltip("White call-to-action button on Unity UI Button (PrimaryButton_IconAndLabel_UnityUIButton); fallback when there is no secondary button.")]
        public GameObject primaryButton;

        [Tooltip("Grey button on Unity UI Button (SecondaryButton_IconAndLabel_UnityUIButton), used for the panel buttons.")]
        public GameObject secondaryButton;

        [Tooltip("Toggle-based tile (TextTileButton_IconAndLabel_Toggle, shows its on state), used for each option of a segmented choice.")]
        public GameObject choiceButton;

        [Tooltip("ToggleButton_Switch.")]
        public GameObject switchToggle;

        [Tooltip("ToggleButton_Checkbox.")]
        public GameObject checkbox;

        [Tooltip("MediumSlider.")]
        public GameObject slider;

        [Tooltip("Extra scale for switches and check boxes placed in caption rows (1 = the default size).")]
        public float controlScale = 1f;
    }
}
