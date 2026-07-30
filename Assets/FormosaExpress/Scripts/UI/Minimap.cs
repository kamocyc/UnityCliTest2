using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FormosaExpress.Core;
using FormosaExpress.City;
using FormosaExpress.Gameplay;

namespace FormosaExpress.UI
{
    /// <summary>
    /// The corner map. The street layout is baked once into a texture and shown through a
    /// scrolling UV window; the route, the targets and the rider are UI elements on top, so
    /// nothing has to be re-rasterised per frame.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        const int TextureSize = 1024;
        const float ViewMetres = 240f;
        const int RouteDots = 34;
        const int MaxPins = 8;

        RawImage _map;
        RectTransform _overlay;
        RectTransform _playerMarker;
        Image _rivalMarker;
        readonly List<Image> _routeDots = new List<Image>(RouteDots);
        readonly List<Image> _pins = new List<Image>(MaxPins);

        Texture2D _texture;
        Bounds _worldBounds;
        float _size;

        public void Build(RectTransform parent, CityModel city, CityBuilder layout)
        {
            _worldBounds = city.WorldBounds;
            _texture = Bake(city, layout, _worldBounds);

            RectTransform root = UiKit.CreateRect("MinimapContent", parent);
            root.Stretch(4f);
            root.gameObject.AddComponent<RectMask2D>();

            var mapGo = UiKit.CreateRect("Map", root);
            mapGo.Stretch();
            _map = mapGo.gameObject.AddComponent<RawImage>();
            _map.texture = _texture;
            _map.raycastTarget = false;

            _overlay = UiKit.CreateRect("Overlay", root);
            _overlay.Stretch();

            // Route breadcrumbs, from nearest to furthest.
            for (int i = 0; i < RouteDots; i++)
            {
                Image dot = UiKit.CreateImage($"RouteDot{i}", _overlay, TextureFactory.Circle(24), Art.HudGreen);
                dot.rectTransform.sizeDelta = new Vector2(7f, 7f);
                dot.enabled = false;
                _routeDots.Add(dot);
            }

            for (int i = 0; i < MaxPins; i++)
            {
                Image pin = UiKit.CreateImage($"Pin{i}", _overlay, TextureFactory.Pin(48), Art.BeaconGreen);
                pin.rectTransform.sizeDelta = new Vector2(24f, 24f);
                pin.enabled = false;
                _pins.Add(pin);
            }

            _rivalMarker = UiKit.CreateImage("Rival", _overlay, TextureFactory.Triangle(48, false), Art.RivalTint);
            _rivalMarker.rectTransform.sizeDelta = new Vector2(18f, 18f);
            _rivalMarker.enabled = false;

            Image marker = UiKit.CreateImage("Player", _overlay, TextureFactory.Triangle(48, false), Color.white);
            marker.rectTransform.sizeDelta = new Vector2(20f, 20f);
            _playerMarker = marker.rectTransform;
            _playerMarker.anchoredPosition = Vector2.zero;

            _size = _worldBounds.size.x;
        }

        // ------------------------------------------------------------------ baking

        static Texture2D Bake(CityModel city, CityBuilder layout, Bounds bounds)
        {
            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false)
            {
                name = "FE_Minimap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[TextureSize * TextureSize];
            Color32 background = new Color(0.12f, 0.13f, 0.15f, 1f);
            Color32 blockColour = new Color(0.22f, 0.23f, 0.26f, 1f);
            Color32 roadColour = new Color(0.58f, 0.60f, 0.64f, 1f);
            Color32 avenueColour = new Color(0.80f, 0.82f, 0.86f, 1f);
            Color32 alleyColour = new Color(0.40f, 0.42f, 0.46f, 1f);

            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

            float scale = TextureSize / bounds.size.x;

            Vector2Int ToPixel(Vector3 world)
            {
                float u = (world.x - bounds.min.x) / bounds.size.x;
                float v = (world.z - bounds.min.z) / bounds.size.z;
                return new Vector2Int(
                    Mathf.Clamp(Mathf.RoundToInt(u * (TextureSize - 1)), 0, TextureSize - 1),
                    Mathf.Clamp(Mathf.RoundToInt(v * (TextureSize - 1)), 0, TextureSize - 1));
            }

            void FillRect(Vector3 centre, float sizeX, float sizeZ, Color32 colour)
            {
                Vector2Int a = ToPixel(centre - new Vector3(sizeX * 0.5f, 0f, sizeZ * 0.5f));
                Vector2Int b = ToPixel(centre + new Vector3(sizeX * 0.5f, 0f, sizeZ * 0.5f));
                for (int y = a.y; y <= b.y; y++)
                for (int x = a.x; x <= b.x; x++)
                    pixels[y * TextureSize + x] = colour;
            }

            void DrawLine(Vector3 from, Vector3 to, float widthMetres, Color32 colour)
            {
                Vector2Int p0 = ToPixel(from);
                Vector2Int p1 = ToPixel(to);
                int halfWidth = Mathf.Max(1, Mathf.RoundToInt(widthMetres * scale * 0.5f));

                int steps = Mathf.Max(Mathf.Abs(p1.x - p0.x), Mathf.Abs(p1.y - p0.y)) + 1;
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    int cx = Mathf.RoundToInt(Mathf.Lerp(p0.x, p1.x, t));
                    int cy = Mathf.RoundToInt(Mathf.Lerp(p0.y, p1.y, t));

                    for (int dy = -halfWidth; dy <= halfWidth; dy++)
                    for (int dx = -halfWidth; dx <= halfWidth; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= TextureSize || y >= TextureSize) continue;
                        pixels[y * TextureSize + x] = colour;
                    }
                }
            }

            foreach (CityBlock block in city.Blocks)
                FillRect(block.Centre, block.Size.x, block.Size.y, blockColour);

            foreach (RoadEdge edge in city.Edges)
            {
                Vector3 a = city.Nodes[edge.A].Position;
                Vector3 b = city.Nodes[edge.B].Position;

                if (edge.IsAlley) DrawLine(a, b, Tuning.AlleyHalfWidth * 2f, alleyColour);
                else DrawLine(a, b, Tuning.RoadHalfWidth * 2f, edge.IsAvenue ? avenueColour : roadColour);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        // ------------------------------------------------------------------ tick

        void Update()
        {
            if (_map == null) return;

            Vector3 player = Services.PlayerPosition;

            // Scroll the UV window so the rider sits in the middle.
            float window = ViewMetres / Mathf.Max(1f, _worldBounds.size.x);
            float u = (player.x - _worldBounds.min.x) / _worldBounds.size.x - window * 0.5f;
            float v = (player.z - _worldBounds.min.z) / _worldBounds.size.z - window * 0.5f;
            _map.uvRect = new Rect(u, v, window, window);

            Rect rect = _overlay.rect;
            float pixelsPerMetre = rect.width / ViewMetres;

            Vector2 ToLocal(Vector3 world)
            {
                return new Vector2((world.x - player.x) * pixelsPerMetre, (world.z - player.z) * pixelsPerMetre);
            }

            // Player arrow points along the heading. The triangle glyph faces the opposite way
            // from what this rotation math assumes, so flip it 180 degrees.
            if (Services.Player != null)
                _playerMarker.localRotation = Quaternion.Euler(0f, 0f, -Services.Player.transform.eulerAngles.y + 180f);

            // Rival blip, clamped to the map edge so you can always see which way they went.
            RivalCourier rival = Services.Rival;
            if (rival != null && rival.Active)
            {
                Transform rivalTransform = rival.Scooter.transform;
                Vector2 local = ToLocal(rivalTransform.position);
                float halfW = rect.width * 0.5f - 10f;
                float halfH = rect.height * 0.5f - 10f;

                _rivalMarker.enabled = true;
                _rivalMarker.rectTransform.anchoredPosition =
                    new Vector2(Mathf.Clamp(local.x, -halfW, halfW), Mathf.Clamp(local.y, -halfH, halfH));
                _rivalMarker.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, -rivalTransform.eulerAngles.y);
            }
            else
            {
                _rivalMarker.enabled = false;
            }

            // Route breadcrumbs: resampled at a fixed spacing so they read as a dotted line.
            int dotIndex = 0;
            RouteService routes = Services.Routes;
            if (routes != null && routes.HasRoute && routes.Points.Count > 1)
            {
                const float spacing = 11f;
                float carry = 0f;

                for (int i = 1; i < routes.Points.Count && dotIndex < _routeDots.Count; i++)
                {
                    Vector3 a = routes.Points[i - 1];
                    Vector3 b = routes.Points[i];
                    float segment = Vector3.Distance(a, b);
                    float travelled = carry;

                    while (travelled < segment && dotIndex < _routeDots.Count)
                    {
                        Vector3 world = Vector3.Lerp(a, b, travelled / Mathf.Max(0.01f, segment));
                        Vector2 local = ToLocal(world);

                        Image dot = _routeDots[dotIndex];
                        if (Mathf.Abs(local.x) < rect.width * 0.5f && Mathf.Abs(local.y) < rect.height * 0.5f)
                        {
                            dot.enabled = true;
                            dot.rectTransform.anchoredPosition = local;
                            float fade = 1f - dotIndex / (float)_routeDots.Count;
                            dot.color = new Color(Art.HudGreen.r, Art.HudGreen.g, Art.HudGreen.b, 0.35f + fade * 0.6f);
                            dotIndex++;
                        }

                        travelled += spacing;
                    }

                    carry = travelled - segment;
                }
            }

            for (int i = dotIndex; i < _routeDots.Count; i++) _routeDots[i].enabled = false;

            // Destination pins for every live order, clamped to the map edge.
            int pinIndex = 0;
            OrderManager orders = Services.Orders;
            if (orders != null)
            {
                foreach (Order order in orders.ActiveOrders)
                {
                    if (pinIndex >= _pins.Count) break;

                    Vector2 local = ToLocal(order.ActiveTarget);
                    float halfW = rect.width * 0.5f - 12f;
                    float halfH = rect.height * 0.5f - 12f;
                    local.x = Mathf.Clamp(local.x, -halfW, halfW);
                    local.y = Mathf.Clamp(local.y, -halfH, halfH);

                    Image pin = _pins[pinIndex++];
                    pin.enabled = true;
                    pin.rectTransform.anchoredPosition = local;

                    bool focused = orders.Focus == order;
                    Color tint = order.PickedUp ? Art.BeaconGreen : Art.BeaconAmber;
                    pin.color = focused ? tint : new Color(tint.r, tint.g, tint.b, 0.55f);
                    pin.rectTransform.sizeDelta = focused ? new Vector2(28f, 28f) : new Vector2(20f, 20f);
                }
            }

            for (int i = pinIndex; i < _pins.Count; i++) _pins[i].enabled = false;
        }
    }
}
