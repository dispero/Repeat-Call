using UnityEngine;
using UnityEngine.UI;

namespace Golovomka
{
    public partial class EchoMvp
    {
        RectTransform pistolHelp;
        bool rangeGuideShown;

        void CreatePistolHelp()
        {
            pistolHelp = new GameObject("PistolHelp", typeof(RectTransform)).GetComponent<RectTransform>();
            pistolHelp.SetParent(canvas.transform, false);
            pistolHelp.anchorMin = new Vector2(.11f, .09f);
            pistolHelp.anchorMax = new Vector2(.89f, .91f);
            pistolHelp.offsetMin = pistolHelp.offsetMax = Vector2.zero;

            var card = new GameObject("ControlsCard", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            card.transform.SetParent(pistolHelp, false);
            var image = card.GetComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("Menu/PistolHelp");
            image.raycastTarget = false;
            var fit = card.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)image.texture.width / image.texture.height;
            pistolHelp.gameObject.SetActive(false);
        }

        void ShowPistolHelp()
        {
            mode = "pistol-help";
            prompt.text = string.Empty;
            pistolHelp.transform.SetAsLastSibling();
            pistolHelp.gameObject.SetActive(true);
        }

        void HidePistolHelp()
        {
            pistolHelp.gameObject.SetActive(false);
            if (mode == "pistol-help") mode = "play";
        }
    }
}
