using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>
    /// Draws the cross-section of the selected beam (from <see cref="SectionProfile"/>) inside its rect, scaled to fit,
    /// with the neutral axis as a dashed line. Stands in for the paper's small 3D section preview.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SectionPreviewGraphic : MaskableGraphic
    {
        SectionProfile profile;
        public Color axisColor = new Color(1f, 1f, 1f, 0.45f);

        public void SetProfile(SectionProfile p)
        {
            profile = p;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (profile == null || profile.CapU.Count == 0) return;
            Rect r = GetPixelAdjustedRect();
            float scale = 0.8f * Mathf.Min(r.width / (float)profile.Width, r.height / (float)profile.Height);
            Vector2 c = r.center;

            for (int i = 0; i < profile.CapU.Count; i += 3)
            {
                int k = vh.currentVertCount;
                for (int j = 0; j < 3; j++)
                {
                    var v = UIVertex.simpleVert;
                    v.color = color;
                    v.position = c + new Vector2((float)profile.CapU[i + j], (float)profile.CapV[i + j]) * scale;
                    vh.AddVert(v);
                }

                vh.AddTriangle(k, k + 1, k + 2);
                vh.AddTriangle(k + 2, k + 1, k); // visible whichever way the triangle winds
            }

            // Neutral axis: short dashes across the full preview width.
            float y = c.y;
            for (float x = r.xMin + 6; x < r.xMax - 12; x += 22)
            {
                int k = vh.currentVertCount;
                var v = UIVertex.simpleVert;
                v.color = axisColor;
                v.position = new Vector2(x, y - 1.5f); vh.AddVert(v);
                v.position = new Vector2(x + 12, y - 1.5f); vh.AddVert(v);
                v.position = new Vector2(x + 12, y + 1.5f); vh.AddVert(v);
                v.position = new Vector2(x, y + 1.5f); vh.AddVert(v);
                vh.AddTriangle(k, k + 1, k + 2);
                vh.AddTriangle(k, k + 2, k + 3);
            }
        }
    }
}
