using ARBeam;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoloMech
{
    /// <summary>Modal message (paper §3.1.2: an unstable beam shows an error and stays on the input screen).</summary>
    public class ErrorPopup : MonoBehaviour
    {
        TMP_Text message;

        public static ErrorPopup Create(UiTheme t, Transform canvas)
        {
            var root = UiKit.Panel(t, canvas, "Error Popup", new Color(0.03f, 0.05f, 0.1f, 0.78f));
            root.raycastTarget = true;
            root.sprite = null;
            root.type = Image.Type.Simple;
            root.rectTransform.Stretch();
            var p = root.gameObject.AddComponent<ErrorPopup>();

            var card = UiKit.Panel(t, root.transform, "Card", Color.Lerp(t.panel, t.backdrop, 0.5f));
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 420));

            var title = UiKit.Label(t, card.transform, "Title", "This beam cannot be analysed", t.bold, t.titleSize, t.error, TextAlignmentOptions.TopLeft);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -36), new Vector2(860, 44));
            p.message = UiKit.Label(t, card.transform, "Message", "", t.regular, t.labelSize, t.textPrimary, TextAlignmentOptions.TopLeft);
            p.message.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -100), new Vector2(872, 200));
            p.message.textWrappingMode = TextWrappingModes.Normal;

            var ok = UiKit.Button(t, card.transform, "OK", "OK", 28);
            ok.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(240, 76));
            ok.onClick.AddListener(() => p.gameObject.SetActive(false));

            root.gameObject.SetActive(false);
            return p;
        }

        public void Show(string text)
        {
            message.text = text;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }
    }
}
