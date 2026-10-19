using System;
using System.Collections.Generic;
using ARBeam.Mechanics;
using UnityEngine;

namespace ARBeam
{
    /// <summary>Fixed list of hanging masses typed into the Inspector. For testing and demo setups.</summary>
    public class InspectorLoadSource : BeamLoadSource
    {
        [Serializable]
        public struct Mass
        {
            [Min(0f)] public float positionMm;
            [Min(0f)] public float grams;
        }

        [SerializeField] List<Mass> masses = new List<Mass>();

        public override void CollectLoads(BeamController beam, List<PointLoad> loads)
        {
            foreach (var m in masses)
                if (m.grams > 0f) loads.Add(PointLoad.FromMass(m.positionMm / 1000.0, m.grams / 1000.0));
        }
    }
}
