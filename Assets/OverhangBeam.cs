using UnityEngine;

public class OverhangBeam : MonoBehaviour
{
    Mesh mesh;
    Vector3[] originalVertices;
    Vector3[] deformedVertices;
    Color[] colors;

    [Header("Load Settings")]
    [Range(0f, 1f)]
    public float loadPos = 0.5f;

    [Range(0f, 0.05f)]
    public float loadMag = 0.01f;

    public float deformationScale = 0.001f;
    public float maxVisualStress = 0.01f;

    [Header("Arrow")]
    public Transform loadArrow;

    float minX, maxX, length;

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        mesh = Instantiate(mesh);
        GetComponent<MeshFilter>().mesh = mesh;

        originalVertices = mesh.vertices;
        deformedVertices = new Vector3[originalVertices.Length];
        colors = new Color[originalVertices.Length];

        minX = float.MaxValue;
        maxX = float.MinValue;

        foreach (var v in originalVertices)
        {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
        }

        length = maxX - minX;
    }

    void Update()
    {
        Vector3 localDown = transform.InverseTransformDirection(Vector3.down);

        // 🔹 Move arrow (NO scaling)
        if (loadArrow != null)
        {
            float xPos = Mathf.Lerp(minX, maxX, loadPos);
            Vector3 worldPos = transform.TransformPoint(new Vector3(xPos, 0f, 0f));
            loadArrow.position = worldPos + Vector3.up * 0.2f;
        }

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];

            float xNorm = (v.x - minX) / length;

            // 🔥 Double support boundary (zero at 1/3 and 2/3)
            float support1 = 1f / 3f;
            float support2 = 2f / 3f;

            float boundary = Mathf.Abs((xNorm - support1) * (xNorm - support2));

            // Load influence (localized)
            float distance = Mathf.Abs(xNorm - loadPos);
            float influence = Mathf.Exp(-distance * 8f);

            float bend = loadMag * boundary * influence;

            // Minimal deformation
            v += localDown * bend * deformationScale;
            deformedVertices[i] = v;

            // Stress color
            float t = Mathf.Clamp01(bend / maxVisualStress);
            colors[i] = EvaluateStressColor(t);
        }

        mesh.vertices = deformedVertices;
        mesh.colors = colors;
        mesh.RecalculateNormals();
    }

    // 🌈 Stress Gradient
    Color EvaluateStressColor(float t)
    {
        if (t < 0.25f)
            return Color.Lerp(Color.blue, Color.green, t / 0.25f);
        else if (t < 0.5f)
            return Color.Lerp(Color.green, Color.yellow, (t - 0.25f) / 0.25f);
        else if (t < 0.75f)
            return new Color(1f, Mathf.Lerp(1f, 0.5f, (t - 0.5f) / 0.25f), 0f);
        else
            return Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (t - 0.75f) / 0.25f);
    }

    // 🎮 UI Controls
    public void SetLoadMagnitude(float value)
    {
        loadMag = value;
    }

    public void SetLoadPosition(float value)
    {
        loadPos = value;
    }
}