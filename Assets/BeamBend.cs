using UnityEngine;

public class BeamBend : MonoBehaviour
{
    Mesh mesh;
    Vector3[] originalVertices;
    Vector3[] deformedVertices;
    Color[] colors;

    [Range(0f, 1f)]
    public float loadPosition = 0.5f;
    public float maxVisualStress = 0.035f; // reference scale
    public Transform loadArrow;

    [Range(0f, 0.05f)] // 🔥 smaller max deflection
    public float bendAmount = 0.01f;

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

    public float deformationScale = 0.0005f; // VERY small

    void Update()
    {
        Vector3 localDown = transform.InverseTransformDirection(Vector3.down);

        float maxStress = 0f;
        float[] stressValues = new float[originalVertices.Length];

        // 🔹 Calculate stress proxy
        for (int i = 0; i < originalVertices.Length; i++)
        {
            float xNorm = (originalVertices[i].x - minX) / length;

            float distance = Mathf.Abs(xNorm - loadPosition);
            float influence = Mathf.Exp(-distance * 10f); // sharper load effect
            float boundary = Mathf.Sin(Mathf.PI * xNorm);

            float bend = bendAmount * influence * boundary;

            stressValues[i] = bend;
            if (bend > maxStress) maxStress = bend;
        }

        // 🔹 Apply deformation + color
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];

            float bend = stressValues[i];

            // Minimal deformation
            // No deformation — stress only visualization
            v = originalVertices[i];
            deformedVertices[i] = v;

            float t = Mathf.Clamp01(bend / maxVisualStress);

            // 🎨 Engineering color gradient
            colors[i] = EvaluateStressColor(t);
        }

        mesh.vertices = deformedVertices;
        mesh.colors = colors;
        mesh.RecalculateNormals();

        // Position arrow along beam
        float xPos = Mathf.Lerp(minX, maxX, loadPosition);

        // Convert to world position
        Vector3 worldPos = transform.TransformPoint(new Vector3(xPos, 0f, 0f));

        // Place arrow slightly above beam
        loadArrow.position = worldPos + Vector3.up * 0.2f;
    }

    // 🌈 Custom Gradient Function
    Color EvaluateStressColor(float t)
    {
        if (t < 0.25f)
            return Color.Lerp(Color.blue, Color.green, t / 0.25f);
        else if (t < 0.5f)
            return Color.Lerp(Color.green, Color.yellow, (t - 0.25f) / 0.25f);
        else if (t < 0.75f)
            return new Color(1f, Mathf.Lerp(1f, 0.5f, (t - 0.5f) / 0.25f), 0f); // yellow→orange
        else
            return Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (t - 0.75f) / 0.25f);
    }

    public void SetBend(float value)
    {
        bendAmount = value;
    }

    public void SetLoadPosition(float value)
    {
        loadPosition = value;
    }
}