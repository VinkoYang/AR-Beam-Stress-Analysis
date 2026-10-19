using System;
using System.Collections.Generic;
using ARBeam.Mechanics;
using UnityEngine;

namespace ARBeam
{
    /// <summary>
    /// Anything that puts loads on a <see cref="BeamController"/>: UI sliders today; tracked weight markers
    /// (rig option B) or hand interaction (headset option A) later. The solver never sees where a load came from.
    /// </summary>
    public abstract class BeamLoadSource : MonoBehaviour
    {
        /// <summary>Raised when the loads this source reports have changed.</summary>
        public event Action<BeamLoadSource> Changed;

        /// <summary>
        /// Append this source's loads in beam coordinates: position in metres from the beam's left end,
        /// force in N (positive downward).
        /// </summary>
        public abstract void CollectLoads(BeamController beam, List<PointLoad> loads);

        protected void NotifyChanged() => Changed?.Invoke(this);

        protected virtual void OnEnable() => NotifyChanged();
        protected virtual void OnDisable() => NotifyChanged();
        protected virtual void OnValidate() => NotifyChanged();
    }
}
