using UnityEngine;
using UnityEngine.UI;
using FormosaExpress.Core;

namespace FormosaExpress.UI
{
    /// <summary>
    /// The only thing on screen before the city, palette and camera exist. Built and shown first
    /// (no camera required - the canvas is Screen Space Overlay), so the multi-second procedural
    /// generation in <see cref="GameBootstrap"/> doesn't read as a frozen black screen.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        Text _status;
        Image _spinner;
        float _spin;

        public void Build()
        {
            Canvas canvas = UiKit.CreateCanvas("LoadingCanvas", transform, 1000);
            RectTransform root = canvas.GetComponent<RectTransform>();

            Image backdrop = UiKit.CreateImage("Backdrop", root, TextureFactory.Plain(), new Color(0.03f, 0.03f, 0.05f, 1f));
            backdrop.rectTransform.Stretch();
            backdrop.preserveAspect = false;

            Image logo = UiKit.CreateImage("Logo", root, TextureFactory.ScooterGlyph(128), new Color(1f, 0.82f, 0.28f));
            logo.rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, 90f), new Vector2(110f, 110f));

            UiKit.CreateOutlinedLabel("Name", root, "FORMOSA EXPRESS", 64, new Color(1f, 0.82f, 0.28f))
                .rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, 10f), new Vector2(1000f, 76f));

            _spinner = UiKit.CreateImage("Spinner", root, TextureFactory.Circle(96, 0.7f), new Color(1f, 1f, 1f, 0.85f));
            _spinner.rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, -56f), new Vector2(36f, 36f));

            _status = UiKit.CreateOutlinedLabel("Status", root, Localization.T("LOADING"), 24,
                new Color(1f, 1f, 1f, 0.82f));
            _status.rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, -104f), new Vector2(900f, 32f));
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        void Update()
        {
            _spin -= 260f * Time.unscaledDeltaTime;
            if (_spinner != null) _spinner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _spin);
        }
    }
}
