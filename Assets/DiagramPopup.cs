using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Shows the shear-force / bending-moment panel. With a CanvasGroup assigned (the live solver diagrams), the
/// button toggles the panel and it stays up while the load changes. Without one, it falls back to the original
/// behaviour: show the static image, wait, fade out.
/// </summary>
public class DiagramPopup : MonoBehaviour
{
    [Tooltip("Live diagram panel. When set, ShowDiagram toggles it instead of flashing the static image.")]
    public CanvasGroup panel;

    [Tooltip("Hide the live panel again after displayTime, like the original popup.")]
    public bool autoHide;

    [Tooltip("Original static diagram image (legacy fallback).")]
    public Image diagramImage;
    public float displayTime = 3.5f;
    public float fadeDuration = 2f;

    Coroutine currentRoutine;
    bool shown;

    void Start()
    {
        if (panel != null) SetAlpha(0f);
    }

    public void ShowDiagram()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        if (panel != null && !autoHide)
        {
            shown = !shown;
            SetAlpha(shown ? 1f : 0f);
            return;
        }

        currentRoutine = StartCoroutine(ShowAndFade());
    }

    IEnumerator ShowAndFade()
    {
        // Show instantly
        SetAlpha(1f);

        // Wait
        yield return new WaitForSeconds(displayTime);

        // Fade out
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            SetAlpha(alpha);
            yield return null;
        }

        SetAlpha(0f);
    }

    void SetAlpha(float a)
    {
        if (panel != null)
        {
            panel.alpha = a;
            return;
        }

        if (diagramImage == null) return;
        Color c = diagramImage.color;
        c.a = a;
        diagramImage.color = c;
    }
}
