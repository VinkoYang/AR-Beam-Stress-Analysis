using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DiagramPopup : MonoBehaviour
{
    public Image diagramImage;
    public float displayTime = 3.5f;
    public float fadeDuration = 2f;

    Coroutine currentRoutine;

    public void ShowDiagram()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

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
        Color c = diagramImage.color;
        c.a = a;
        diagramImage.color = c;
    }
}