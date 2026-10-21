using Oculus.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// The pill-shaped handle under the window and on top of each panel (as on Horizon OS windows): dim at rest,
    /// brighter and wider while a ray, hand or controller hovers it, solid while it is grabbed.
    /// </summary>
    public class GrabHandleVisual : MonoBehaviour
    {
        [SerializeField] Image pill;

        IInteractableView[] views;
        Vector3 restScale;

        public void SetPill(Image image) => pill = image;

        void Awake()
        {
            views = GetComponentsInChildren<IInteractableView>(true);
            if (pill != null) restScale = pill.rectTransform.localScale;
        }

        void OnEnable()
        {
            if (views == null) return;
            foreach (var v in views) v.WhenStateChanged += OnStateChanged;
            Show(InteractableState.Normal);
        }

        void OnDisable()
        {
            if (views == null) return;
            foreach (var v in views) v.WhenStateChanged -= OnStateChanged;
        }

        void OnStateChanged(InteractableStateChangeArgs _)
        {
            var state = InteractableState.Normal;
            foreach (var v in views)
            {
                if (v.State == InteractableState.Select) { state = InteractableState.Select; break; }
                if (v.State == InteractableState.Hover) state = InteractableState.Hover;
            }

            Show(state);
        }

        void Show(InteractableState state)
        {
            if (pill == null) return;
            pill.color = new Color(1f, 1f, 1f, state == InteractableState.Select ? 1f : state == InteractableState.Hover ? 0.85f : 0.45f);
            pill.rectTransform.localScale = state == InteractableState.Normal ? restScale : Vector3.Scale(restScale, new Vector3(1.15f, 1.2f, 1f));
        }
    }
}
