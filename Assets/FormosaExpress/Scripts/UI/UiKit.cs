using UnityEngine;
using UnityEngine.UI;
using FormosaExpress.Core;

namespace FormosaExpress.UI
{
    /// <summary>
    /// Small builder helpers for the runtime UI. Every panel, bar and icon in the game is
    /// assembled from these, using procedurally generated sprites and the built-in font.
    /// </summary>
    public static class UiKit
    {
        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, Transform parent, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>A rounded, tinted panel. Returns the Image so callers can retint it.</summary>
        public static Image CreatePanel(string name, Transform parent, Color colour, int cornerRadius = 16)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = TextureFactory.RoundedRect(cornerRadius);
            image.type = Image.Type.Sliced;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color colour)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        public static Text CreateText(string name, Transform parent, string content, int size, Color colour,
            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Bold)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        /// <summary>Text with a hard drop shadow, which is what keeps a HUD legible over a busy street.</summary>
        public static Text CreateLabel(string name, Transform parent, string content, int size, Color colour,
            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Bold)
        {
            Text text = CreateText(name, parent, content, size, colour, anchor, style);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        public static Text CreateOutlinedLabel(string name, Transform parent, string content, int size, Color colour,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Text text = CreateText(name, parent, content, size, colour, anchor);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            return text;
        }

        /// <summary>A horizontal fill bar. The returned Image is the fill; set fillAmount on it.</summary>
        public static Image CreateBar(string name, Transform parent, Color backColour, Color fillColour,
            out Image background, int cornerRadius = 8)
        {
            background = CreatePanel(name, parent, backColour, cornerRadius);

            Image fill = CreatePanel("Fill", background.transform, fillColour, cornerRadius);
            RectTransform rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            return fill;
        }

        // ------------------------------------------------------------------ anchoring

        public static RectTransform Anchor(this RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        /// <summary>Places a fixed-size rect relative to a corner. Offsets are in reference pixels.</summary>
        public static RectTransform Place(this RectTransform rect, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(this RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            return rect;
        }

        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        public static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
        public static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
    }
}
