using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FormosaExpress.Core;
using FormosaExpress.City;
using FormosaExpress.Gameplay;

namespace FormosaExpress.UI
{
    /// <summary>
    /// The riding HUD: shift clock, score, money, live order cards, minimap, speed, combo,
    /// boost and cargo readouts, plus the world-tracked destination marker and score popups.
    /// </summary>
    public sealed class HudRoot : MonoBehaviour
    {
        const int PopupPool = 12;
        const int OrderCards = 5;

        Canvas _canvas;
        RectTransform _root;

        // Top bar
        Text _clock;
        Text _score;
        Text _scoreCaption;
        Text _level;
        Text _levelCaption;
        Image _clockIcon;
        Text _money;
        Image _quotaFill;
        Text _quotaLabel;

        // Destination marker
        RectTransform _marker;
        Text _markerTitle;
        Text _markerCaption;
        Text _markerDistance;
        Image _markerIcon;
        Image _markerArrow;

        // Order cards
        readonly List<OrderCard> _cards = new List<OrderCard>(OrderCards);

        // Bottom cluster
        Text _speedValue;
        Text _speedUnit;
        Text _boostCaption;
        Text _combo;
        RectTransform _comboRect;
        Image _comboWindow;
        Image _boostFill;
        Image _cargoFill;
        Text _cargoLabel;
        Text _hints;

        // Race
        RectTransform _versus;
        Text _versusCaption;
        Text _versusYouLabel;
        Text _versusPlayer;
        Text _versusRival;
        Text _versusRivalName;
        Image _versusPlayerBar;
        Image _versusRivalBar;
        Image _rivalArrow;

        // Feedback
        Image _damageFlash;
        Image _boostVignette;
        Text _toast;
        RectTransform _toastRect;
        float _toastTimer;
        readonly List<Popup> _popups = new List<Popup>(PopupPool);

        Minimap _minimap;
        float _comboPulse;
        float _markerPulse;
        float _flash;
        float _shownScore;
        float _shownMoney;

        sealed class OrderCard
        {
            public RectTransform Root;
            public Image Panel;
            public Image Stripe;
            public Text Title;
            public Text Detail;
            public Image TimeFill;
            public Image ConditionDot;
        }

        sealed class Popup
        {
            public RectTransform Rect;
            public Text Text;
            public float Life;
            public Vector3 World;
            public Vector2 Drift;
        }

        // ------------------------------------------------------------------ construction

        public void Build(CityModel city, CityBuilder layout)
        {
            _canvas = UiKit.CreateCanvas("HUD", transform, 100);
            _root = _canvas.GetComponent<RectTransform>();

            BuildTopBar();
            BuildQuotaBar();
            BuildOrderCards();
            BuildMinimap(city, layout);
            BuildBottomCluster();
            BuildDestinationMarker();
            BuildVersusPanel();
            BuildFeedback();

            SetVisible(false);

            Localization.Changed += ApplyLanguage;
            ApplyLanguage();
        }

        /// <summary>Re-stamps the HUD's static captions after a language toggle. Everything else
        /// (order cards, toasts, quota, cargo) already rebuilds from Localization.T every frame.</summary>
        void ApplyLanguage()
        {
            _scoreCaption.text = Localization.T("SCORE");
            _levelCaption.text = Localization.T("SHIFT");
            _boostCaption.text = Localization.T("BOOST");
            _markerCaption.text = Localization.T("DESTINATION");
            _hints.text = Localization.T("W/S throttle & brake   A/D steer   SPACE drift   SHIFT boost   TAB switch job   E horn   C camera");
            _versusCaption.text = Localization.T("RACE  ·  FIRST TO 5");
            _versusYouLabel.text = Localization.T("YOU");
        }

        /// <summary>The race scoreboard. Hidden entirely in career mode.</summary>
        void BuildVersusPanel()
        {
            Image panel = UiKit.CreatePanel("Versus", _root, Art.HudPanelSolid, 16);
            _versus = panel.rectTransform;
            _versus.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -22f), new Vector2(430f, 96f));

            _versusCaption = UiKit.CreateLabel("VersusCaption", _versus, "RACE  ·  FIRST TO 5", 15, Art.HudDim,
                    TextAnchor.MiddleCenter);
            _versusCaption.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -6f), new Vector2(400f, 18f));

            _versusYouLabel = UiKit.CreateLabel("YouLabel", _versus, "YOU", 18, Art.HudCyan);
            _versusYouLabel.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(18f, -26f), new Vector2(80f, 22f));

            _versusPlayer = UiKit.CreateLabel("YouCount", _versus, "0", 30, Art.HudCyan, TextAnchor.MiddleRight);
            _versusPlayer.rectTransform.Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-18f, -24f),
                new Vector2(60f, 26f));

            _versusPlayerBar = UiKit.CreateBar("YouBar", _versus, new Color(0f, 0f, 0f, 0.5f), Art.HudCyan,
                out Image playerBg, 4);
            playerBg.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -50f),
                new Vector2(394f, 7f));

            _versusRivalName = UiKit.CreateLabel("RivalLabel", _versus, "RIVAL", 18, Art.RivalTint);
            _versusRivalName.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(18f, -60f),
                new Vector2(280f, 22f));

            _versusRival = UiKit.CreateLabel("RivalCount", _versus, "0", 30, Art.RivalTint, TextAnchor.MiddleRight);
            _versusRival.rectTransform.Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-18f, -58f),
                new Vector2(60f, 26f));

            _versusRivalBar = UiKit.CreateBar("RivalBar", _versus, new Color(0f, 0f, 0f, 0.5f), Art.RivalTint,
                out Image rivalBg, 4);
            rivalBg.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -84f),
                new Vector2(394f, 7f));

            // Edge arrow pointing at the rival, so you always know where the threat is.
            _rivalArrow = UiKit.CreateImage("RivalArrow", _root, TextureFactory.Triangle(64, false), Art.RivalTint);
            _rivalArrow.rectTransform.sizeDelta = new Vector2(44f, 44f);
            _rivalArrow.enabled = false;

            _versus.gameObject.SetActive(false);
        }

        void BuildTopBar()
        {
            Image panel = UiKit.CreatePanel("TopLeft", _root, Art.HudPanel, 18);
            panel.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(26f, -22f), new Vector2(430f, 56f));

            _clockIcon = UiKit.CreateImage("ClockIcon", panel.transform, TextureFactory.Circle(48, 0.62f), Art.HudGold);
            _clockIcon.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(16f, -14f), new Vector2(28f, 28f));

            _clock = UiKit.CreateLabel("Clock", panel.transform, "00:00", 30, Art.HudText);
            _clock.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(52f, -13f), new Vector2(110f, 32f));

            UiKit.CreateLabel("Sep1", panel.transform, "|", 26, Art.HudDim)
                .rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(158f, -14f), new Vector2(12f, 30f));

            _scoreCaption = UiKit.CreateLabel("ScoreCaption", panel.transform, "SCORE", 15, Art.HudDim);
            _scoreCaption.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(176f, -8f), new Vector2(70f, 18f));

            _score = UiKit.CreateLabel("Score", panel.transform, "0", 26, Art.HudGold);
            _score.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(176f, -26f), new Vector2(130f, 28f));

            UiKit.CreateLabel("Sep2", panel.transform, "|", 26, Art.HudDim)
                .rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(316f, -14f), new Vector2(12f, 30f));

            _levelCaption = UiKit.CreateLabel("LevelCaption", panel.transform, "SHIFT", 15, Art.HudDim);
            _levelCaption.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(334f, -8f), new Vector2(70f, 18f));

            _level = UiKit.CreateLabel("Level", panel.transform, "1", 26, Art.HudText);
            _level.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(334f, -26f), new Vector2(80f, 28f));

            // Money, top right.
            Image moneyPanel = UiKit.CreatePanel("MoneyPanel", _root, Art.HudPanel, 18);
            moneyPanel.rectTransform.Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-26f, -22f), new Vector2(180f, 46f));

            Image coin = UiKit.CreateImage("Coin", moneyPanel.transform, TextureFactory.Circle(48, 0.55f), Art.HudGold);
            coin.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(12f, -9f), new Vector2(28f, 28f));

            _money = UiKit.CreateLabel("Money", moneyPanel.transform, "$0", 26, Art.HudGold, TextAnchor.MiddleRight);
            _money.rectTransform.Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-14f, -9f), new Vector2(120f, 28f));
        }

        void BuildQuotaBar()
        {
            Image panel = UiKit.CreatePanel("QuotaPanel", _root, Art.HudPanel, 12);
            panel.rectTransform.Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-26f, -76f), new Vector2(180f, 34f));

            _quotaLabel = UiKit.CreateLabel("QuotaLabel", panel.transform, "QUOTA 0 / 0", 14, Art.HudDim,
                TextAnchor.MiddleCenter);
            _quotaLabel.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -4f), new Vector2(170f, 16f));

            _quotaFill = UiKit.CreateBar("QuotaBar", panel.transform, new Color(0f, 0f, 0f, 0.55f), Art.HudGreen,
                out Image background, 6);
            background.rectTransform.Place(UiKit.BottomCentre, UiKit.BottomCentre, new Vector2(0f, 6f),
                new Vector2(158f, 9f));
        }

        void BuildOrderCards()
        {
            for (int i = 0; i < OrderCards; i++)
            {
                var card = new OrderCard();

                card.Panel = UiKit.CreatePanel($"OrderCard{i}", _root, Art.HudPanel, 14);
                card.Root = card.Panel.rectTransform;
                card.Root.Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-26f, -122f - i * 78f),
                    new Vector2(330f, 70f));

                card.Stripe = UiKit.CreatePanel("Stripe", card.Root, Art.BeaconAmber, 4);
                card.Stripe.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(6f, -6f), new Vector2(6f, 58f));

                card.Title = UiKit.CreateLabel("Title", card.Root, "", 19, Art.HudText);
                card.Title.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(20f, -8f), new Vector2(300f, 24f));

                card.Detail = UiKit.CreateLabel("Detail", card.Root, "", 15, Art.HudDim);
                card.Detail.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(20f, -30f), new Vector2(300f, 20f));

                card.TimeFill = UiKit.CreateBar("TimeBar", card.Root, new Color(0f, 0f, 0f, 0.5f), Art.HudGreen,
                    out Image background, 4);
                background.rectTransform.Place(UiKit.BottomLeft, UiKit.BottomLeft, new Vector2(20f, 8f),
                    new Vector2(258f, 8f));

                card.ConditionDot = UiKit.CreateImage("Condition", card.Root, TextureFactory.Circle(24), Art.HudGreen);
                card.ConditionDot.rectTransform.Place(UiKit.BottomRight, UiKit.BottomRight, new Vector2(-16f, 8f),
                    new Vector2(12f, 12f));

                card.Root.gameObject.SetActive(false);
                _cards.Add(card);
            }
        }

        void BuildMinimap(CityModel city, CityBuilder layout)
        {
            // Brand badge above the map, as in the reference layout.
            Image badge = UiKit.CreatePanel("Badge", _root, Art.HudPanelSolid, 14);
            badge.rectTransform.Place(UiKit.BottomLeft, UiKit.BottomLeft, new Vector2(26f, 246f), new Vector2(268f, 52f));

            Image logo = UiKit.CreateImage("Logo", badge.transform, TextureFactory.ScooterGlyph(64), Art.HudGold);
            logo.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(10f, -8f), new Vector2(36f, 36f));

            UiKit.CreateLabel("Brand", badge.transform, "FORMOSA", 20, Art.HudGold)
                .rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(54f, -6f), new Vector2(200f, 22f));
            UiKit.CreateLabel("Brand2", badge.transform, "EXPRESS", 20, Art.HudText, TextAnchor.MiddleLeft,
                    FontStyle.BoldAndItalic)
                .rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(54f, -26f), new Vector2(200f, 22f));

            Image frame = UiKit.CreatePanel("MinimapFrame", _root, Art.HudPanelSolid, 14);
            frame.rectTransform.Place(UiKit.BottomLeft, UiKit.BottomLeft, new Vector2(26f, 26f), new Vector2(268f, 212f));

            _minimap = frame.gameObject.AddComponent<Minimap>();
            _minimap.Build(frame.rectTransform, city, layout);
        }

        void BuildBottomCluster()
        {
            // Speedometer, bottom right.
            Image speedPanel = UiKit.CreatePanel("SpeedPanel", _root, Art.HudPanel, 18);
            speedPanel.rectTransform.Place(UiKit.BottomRight, UiKit.BottomRight, new Vector2(-26f, 26f),
                new Vector2(210f, 108f));

            _speedValue = UiKit.CreateLabel("Speed", speedPanel.transform, "0", 60, Art.HudText, TextAnchor.MiddleRight);
            _speedValue.rectTransform.Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-70f, -8f),
                new Vector2(150f, 60f));

            _speedUnit = UiKit.CreateLabel("SpeedUnit", speedPanel.transform, "KM / H", 17, Art.HudDim,
                TextAnchor.LowerRight);
            _speedUnit.rectTransform.Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-16f, -44f),
                new Vector2(54f, 24f));

            // Boost meter.
            _boostCaption = UiKit.CreateLabel("BoostCaption", speedPanel.transform, "BOOST", 13, Art.HudDim);
            _boostCaption.rectTransform.Place(UiKit.BottomLeft, UiKit.BottomLeft, new Vector2(16f, 32f), new Vector2(60f, 16f));

            _boostFill = UiKit.CreateBar("BoostBar", speedPanel.transform, new Color(0f, 0f, 0f, 0.55f), Art.HudCyan,
                out Image boostBg, 6);
            boostBg.rectTransform.Place(UiKit.BottomLeft, UiKit.BottomLeft, new Vector2(16f, 18f),
                new Vector2(178f, 12f));

            // Cargo meter.
            Image cargoPanel = UiKit.CreatePanel("CargoPanel", _root, Art.HudPanel, 14);
            cargoPanel.rectTransform.Place(UiKit.BottomRight, UiKit.BottomRight, new Vector2(-246f, 26f),
                new Vector2(190f, 62f));

            _cargoLabel = UiKit.CreateLabel("CargoCaption", cargoPanel.transform, "BAG EMPTY", 14, Art.HudDim);
            _cargoLabel.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(14f, -8f), new Vector2(170f, 18f));

            _cargoFill = UiKit.CreateBar("CargoBar", cargoPanel.transform, new Color(0f, 0f, 0f, 0.55f), Art.HudGreen,
                out Image cargoBg, 6);
            cargoBg.rectTransform.Place(UiKit.BottomLeft, UiKit.BottomLeft, new Vector2(14f, 14f),
                new Vector2(162f, 12f));

            // Combo, bottom centre.
            Image comboPanel = UiKit.CreatePanel("ComboPanel", _root, new Color(0f, 0f, 0f, 0f), 14);
            _comboRect = comboPanel.rectTransform;
            _comboRect.Place(UiKit.BottomCentre, UiKit.BottomCentre, new Vector2(0f, 34f), new Vector2(280f, 84f));

            _combo = UiKit.CreateOutlinedLabel("Combo", _comboRect, "", 48, Art.HudGold);
            _combo.rectTransform.Stretch();

            _comboWindow = UiKit.CreateBar("ComboWindow", _comboRect, new Color(0f, 0f, 0f, 0.5f), Art.HudGold,
                out Image comboBg, 4);
            comboBg.rectTransform.Place(UiKit.BottomCentre, UiKit.BottomCentre, new Vector2(0f, 0f),
                new Vector2(150f, 7f));
            comboBg.gameObject.SetActive(false);

            _hints = UiKit.CreateLabel("Hints", _root,
                Localization.T("W/S throttle & brake   A/D steer   SPACE drift   SHIFT boost   TAB switch job   E horn   C camera"),
                15, new Color(1f, 1f, 1f, 0.42f), TextAnchor.MiddleCenter);
            _hints.rectTransform.Place(UiKit.BottomCentre, UiKit.BottomCentre, new Vector2(0f, 12f),
                new Vector2(1100f, 20f));
        }

        void BuildDestinationMarker()
        {
            _marker = UiKit.CreateRect("Marker", _root);
            _marker.sizeDelta = new Vector2(360f, 150f);
            _marker.pivot = new Vector2(0.5f, 0f);

            _markerTitle = UiKit.CreateOutlinedLabel("MarkerTitle", _marker, "", 30, Art.HudGreen);
            _markerTitle.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -32f),
                new Vector2(360f, 34f));

            _markerCaption = UiKit.CreateOutlinedLabel("MarkerCaption", _marker, "DESTINATION", 16, Art.HudText);
            _markerCaption.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -10f), new Vector2(360f, 20f));

            _markerDistance = UiKit.CreateOutlinedLabel("MarkerDistance", _marker, "", 24, Art.HudText);
            _markerDistance.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -62f),
                new Vector2(360f, 28f));

            _markerIcon = UiKit.CreateImage("MarkerIcon", _marker, TextureFactory.GiftGlyph(64), Art.HudGreen);
            _markerIcon.rectTransform.Place(UiKit.BottomCentre, UiKit.BottomCentre, new Vector2(0f, 10f),
                new Vector2(46f, 46f));

            // Off-screen indicator: an arrow pinned to the screen edge pointing at the target.
            _markerArrow = UiKit.CreateImage("MarkerArrow", _root, TextureFactory.Triangle(64, false), Art.HudGreen);
            _markerArrow.rectTransform.sizeDelta = new Vector2(58f, 58f);
            _markerArrow.enabled = false;

            _marker.gameObject.SetActive(false);
        }

        void BuildFeedback()
        {
            _damageFlash = UiKit.CreateImage("DamageFlash", _root, TextureFactory.Plain(), new Color(1f, 0.1f, 0.1f, 0f));
            _damageFlash.rectTransform.Stretch();
            _damageFlash.preserveAspect = false;

            _boostVignette = UiKit.CreateImage("BoostVignette", _root, TextureFactory.SoftGlow(256, 1.2f),
                new Color(0.4f, 0.8f, 1f, 0f));
            _boostVignette.rectTransform.Stretch(-260f);
            _boostVignette.preserveAspect = false;

            Image toastPanel = UiKit.CreatePanel("ToastPanel", _root, new Color(0f, 0f, 0f, 0.6f), 12);
            _toastRect = toastPanel.rectTransform;
            _toastRect.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -110f), new Vector2(560f, 52f));

            _toast = UiKit.CreateOutlinedLabel("Toast", _toastRect, "", 28, Art.HudGold);
            _toast.rectTransform.Stretch();
            _toastRect.gameObject.SetActive(false);

            for (int i = 0; i < PopupPool; i++)
            {
                var popup = new Popup();
                popup.Text = UiKit.CreateOutlinedLabel($"Popup{i}", _root, "", 26, Art.HudGold);
                popup.Rect = popup.Text.rectTransform;
                popup.Rect.sizeDelta = new Vector2(420f, 40f);
                popup.Rect.gameObject.SetActive(false);
                _popups.Add(popup);
            }
        }

        // ------------------------------------------------------------------ public API

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
        }

        public void ShowToast(string message, Color colour, float duration = 1.8f)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toast.color = colour;
            _toastTimer = duration;
            _toastRect.gameObject.SetActive(true);
            _toastRect.localScale = Vector3.one * 0.85f;
        }

        public void ShowPopup(string text, Color colour, Vector3 worldPosition)
        {
            Popup free = null;
            foreach (Popup popup in _popups)
            {
                if (popup.Life > 0f) continue;
                free = popup;
                break;
            }

            // All busy: steal the oldest.
            if (free == null)
            {
                float least = float.MaxValue;
                foreach (Popup popup in _popups)
                    if (popup.Life < least) { least = popup.Life; free = popup; }
            }

            if (free == null) return;

            free.Life = 1.15f;
            free.World = worldPosition + Vector3.up * 1.6f;
            free.Drift = new Vector2(Random.Range(-40f, 40f), Random.Range(70f, 110f));
            free.Text.text = text;
            free.Text.color = colour;
            free.Rect.gameObject.SetActive(true);
            free.Rect.localScale = Vector3.one * 0.7f;
        }

        public void FlashDamage(float severity)
        {
            _flash = Mathf.Min(1f, _flash + Mathf.Lerp(0.15f, 0.7f, severity));
        }

        // ------------------------------------------------------------------ tick

        void LateUpdate()
        {
            if (_canvas == null || !_canvas.enabled) return;

            float dt = Time.unscaledDeltaTime;

            UpdateTopBar(dt);
            UpdateOrderCards();
            UpdateBottomCluster(dt);
            UpdateMarker(dt);
            UpdateVersus();
            UpdateFeedback(dt);
        }

        void UpdateVersus()
        {
            GameDirector director = Services.Director;
            RivalCourier rival = Services.Rival;
            bool racing = director != null && director.IsRace && rival != null && rival.Active;

            if (_versus.gameObject.activeSelf != racing) _versus.gameObject.SetActive(racing);

            if (!racing)
            {
                _rivalArrow.enabled = false;
                return;
            }

            int target = Mathf.Max(1, director.RaceTarget);
            int mine = Services.Combo != null ? Services.Combo.Deliveries : 0;

            _versusPlayer.text = mine.ToString();
            _versusRival.text = rival.Delivered.ToString();
            _versusRivalName.text = Localization.T(rival.RivalName);
            _versusPlayerBar.fillAmount = Mathf.Clamp01(mine / (float)target);
            _versusRivalBar.fillAmount = Mathf.Clamp01(rival.Delivered / (float)target);

            // Highlight whoever is ahead.
            bool leading = mine >= rival.Delivered;
            _versusPlayer.color = leading ? Art.HudGold : Art.HudCyan;
            _versusRival.color = leading ? Art.RivalTint : Art.HudGold;

            // Rival edge arrow, only while they are off screen.
            Camera camera = Services.Camera != null ? Services.Camera.Camera : null;
            if (camera == null) return;

            Vector3 rivalPos = rival.Scooter.transform.position + Vector3.up * 1.2f;
            Vector3 screen = camera.WorldToScreenPoint(rivalPos);
            bool onScreen = screen.z > 1f && screen.x > 0f && screen.x < Screen.width
                            && screen.y > 0f && screen.y < Screen.height;

            if (onScreen)
            {
                _rivalArrow.enabled = false;
                return;
            }

            _rivalArrow.enabled = true;

            Vector3 flat = rival.Scooter.transform.position - Services.PlayerPosition;
            flat.y = 0f;
            Vector3 local = camera.transform.InverseTransformDirection(flat.normalized);
            var dir = new Vector2(local.x, local.z);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();

            Vector2 canvasSize = _root.rect.size;
            var radius = new Vector2(canvasSize.x * 0.40f, canvasSize.y * 0.34f);
            _rivalArrow.rectTransform.anchoredPosition = new Vector2(dir.x * radius.x, dir.y * radius.y);
            _rivalArrow.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
        }

        void UpdateTopBar(float dt)
        {
            GameDirector director = Services.Director;
            ComboSystem combo = Services.Combo;
            if (director == null) return;

            _clock.text = MathX.FormatClock(director.ShiftTimeRemaining);

            // The clock turns red and ticks visibly in the last half minute.
            bool critical = director.ShiftTimeRemaining < 30f;
            _clock.color = critical
                ? Color.Lerp(Art.HudText, Art.HudRed, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f))
                : Art.HudText;
            _clockIcon.color = critical ? Art.HudRed : Art.HudGold;

            // Score and money roll up rather than snapping, which reads as reward.
            int targetScore = combo != null ? combo.Score : 0;
            _shownScore = Mathf.MoveTowards(_shownScore, targetScore, Mathf.Max(180f, Mathf.Abs(targetScore - _shownScore) * 6f) * dt);
            _score.text = Mathf.RoundToInt(_shownScore).ToString("N0");

            _shownMoney = Mathf.MoveTowards(_shownMoney, director.ShiftEarnings,
                Mathf.Max(90f, Mathf.Abs(director.ShiftEarnings - _shownMoney) * 5f) * dt);
            _money.text = MathX.FormatMoney(Mathf.RoundToInt(_shownMoney));

            _level.text = director.Level.ToString();

            // Races have no cash quota; the versus panel replaces it.
            int quota = director.Quota;
            bool showQuota = !director.IsRace && quota > 0;
            Transform quotaPanel = _quotaLabel.transform.parent;
            if (quotaPanel.gameObject.activeSelf != showQuota) quotaPanel.gameObject.SetActive(showQuota);

            if (showQuota)
            {
                _quotaLabel.text = $"{Localization.T("QUOTA")}  {director.ShiftEarnings} / {quota}";
                float progress = Mathf.Clamp01(director.ShiftEarnings / (float)quota);
                _quotaFill.fillAmount = progress;
                _quotaFill.color = progress >= 1f ? Art.HudGold : Art.HudGreen;
            }
        }

        void UpdateOrderCards()
        {
            OrderManager orders = Services.Orders;
            if (orders == null) return;

            int index = 0;
            foreach (Order order in orders.ActiveOrders)
            {
                if (index >= _cards.Count) break;

                OrderCard card = _cards[index++];
                if (!card.Root.gameObject.activeSelf) card.Root.gameObject.SetActive(true);

                bool takenByRival = order.CarriedBy == Courier.Rival;
                bool focused = orders.Focus == order;
                bool carrying = order.CarriedBy == Courier.Player;

                // The focused card needs to read at a glance, mid-ride: a warm tint, a thicker
                // accent stripe and a leading "> " marker, mirroring the menu-selection convention
                // used elsewhere - not just a subtle brightness shift that's easy to miss.
                card.Panel.color = focused
                    ? new Color(0.24f, 0.19f, 0.07f, 0.96f)
                    : new Color(Art.HudPanel.r, Art.HudPanel.g, Art.HudPanel.b, Art.HudPanel.a * 0.7f);

                Color accent = takenByRival ? Art.RivalTint
                    : carrying ? Art.BeaconGreen
                    : Art.BeaconAmber;
                card.Stripe.color = focused ? accent : new Color(accent.r, accent.g, accent.b, 0.5f);
                card.Stripe.rectTransform.sizeDelta = focused ? new Vector2(9f, 58f) : new Vector2(4f, 58f);

                string label = takenByRival ? Localization.T("TAKEN") + "  " + Localization.T(order.ShopName)
                    : carrying ? Localization.T("TO") + "  " + Localization.T(order.CustomerName)
                    : Localization.T("PICK UP") + "  " + Localization.T(order.ShopName);
                card.Title.text = (focused ? "> " : "   ") + label;
                card.Title.color = takenByRival ? Art.RivalTint
                    : focused ? Art.HudGold
                    : new Color(0.85f, 0.85f, 0.85f, 0.8f);

                // The focused job quotes its road distance, matching the world marker; the rest
                // quote straight-line distance, which is cheap and good enough for triage.
                float distance = focused && Services.Routes != null && Services.Routes.HasRoute
                    ? Services.Routes.DistanceRemaining
                    : Vector3.Distance(Services.PlayerPosition, order.ActiveTarget);
                card.Detail.text = $"{Localization.T(order.DishName)}  ·  {MathX.FormatDistance(distance)}  ·  ${order.BaseFare}";

                float remaining = order.TimeLimit > 0f ? Mathf.Clamp01(order.TimeRemaining / order.TimeLimit) : 0f;
                card.TimeFill.fillAmount = remaining;
                card.TimeFill.color = remaining > 0.5f ? Art.HudGreen
                    : remaining > 0.22f ? Art.HudGold
                    : Art.HudRed;

                card.ConditionDot.enabled = carrying;
                if (carrying)
                {
                    card.ConditionDot.color = order.Condition > Tuning.CargoPerfectThreshold ? Art.HudGreen
                        : order.Condition > Tuning.CargoGoodThreshold ? Art.HudGold
                        : Art.HudRed;
                }
            }

            for (int i = index; i < _cards.Count; i++)
                if (_cards[i].Root.gameObject.activeSelf) _cards[i].Root.gameObject.SetActive(false);
        }

        void UpdateBottomCluster(float dt)
        {
            var player = Services.Player;
            ComboSystem combo = Services.Combo;
            OrderManager orders = Services.Orders;

            if (player != null)
            {
                _speedValue.text = Mathf.RoundToInt(player.SpeedKmh).ToString();
                _speedValue.color = player.IsBoosting ? Art.HudCyan : Art.HudText;

                float boost01 = player.Adrenaline01;
                _boostFill.fillAmount = boost01;
                _boostFill.color = player.IsBoosting
                    ? Color.Lerp(Art.HudCyan, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f))
                    : boost01 >= Tuning.AdrenalineMinToStart / Tuning.AdrenalineMax ? Art.HudCyan
                    : new Color(0.35f, 0.55f, 0.65f);
            }

            if (orders != null)
            {
                bool carrying = orders.CarriedCount > 0;
                _cargoLabel.text = carrying
                    ? $"{Localization.T("BAG")}  {orders.CarriedCount}/{orders.Capacity}   {Mathf.RoundToInt(orders.WorstCondition)}%"
                    : $"{Localization.T("BAG EMPTY")}  0/{orders.Capacity}";
                _cargoLabel.color = carrying ? Art.HudText : Art.HudDim;

                float condition = carrying ? orders.WorstCondition / Tuning.CargoMax : 1f;
                _cargoFill.fillAmount = condition;
                _cargoFill.color = !carrying ? new Color(0.3f, 0.3f, 0.34f)
                    : condition > 0.68f ? Art.HudGreen
                    : condition > 0.32f ? Art.HudGold
                    : Art.HudRed;
            }

            if (combo != null)
            {
                bool active = combo.Step > 0;
                _comboWindow.transform.parent.gameObject.SetActive(active);

                if (active)
                {
                    _combo.text = $"x{combo.Multiplier:0.##}";
                    _comboWindow.fillAmount = combo.Window01;

                    Color hot = Color.Lerp(Art.HudGold, Art.HudRed, combo.Step / (float)Tuning.ComboMaxStep);
                    _combo.color = hot;
                    _comboWindow.color = hot;

                    _comboPulse = MathX.ExpSmooth(_comboPulse, 1f, 10f, dt);
                    float pulse = 1f + 0.10f * Mathf.Sin(Time.unscaledTime * 7f) + combo.Step * 0.035f;
                    _comboRect.localScale = Vector3.one * pulse * _comboPulse;
                }
                else
                {
                    _combo.text = "";
                    _comboPulse = 0.6f;
                }
            }

            // The control hints fade out once the rider is clearly under way.
            if (player != null && _hints != null)
            {
                float target = player.DistanceTravelled > 220f ? 0f : 0.42f;
                Color c = _hints.color;
                c.a = MathX.ExpSmooth(c.a, target, 1.2f, dt);
                _hints.color = c;
            }
        }

        void UpdateMarker(float dt)
        {
            OrderManager orders = Services.Orders;
            RouteService routes = Services.Routes;
            Camera camera = Services.Camera != null ? Services.Camera.Camera : null;

            if (orders?.Focus == null || camera == null)
            {
                if (_marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);
                _markerArrow.enabled = false;
                return;
            }

            Order order = orders.Focus;
            Vector3 world = order.ActiveTarget + Vector3.up * 7.4f;
            Vector3 screen = camera.WorldToScreenPoint(world);
            bool onScreen = screen.z > 1f && screen.x > 0f && screen.x < Screen.width
                            && screen.y > 60f && screen.y < Screen.height;

            Color accent = order.PickedUp ? Art.BeaconGreen : Art.BeaconAmber;
            float distance = routes != null && routes.HasRoute
                ? routes.DistanceRemaining
                : Vector3.Distance(Services.PlayerPosition, order.ActiveTarget);

            _markerPulse += dt;

            if (onScreen)
            {
                if (!_marker.gameObject.activeSelf) _marker.gameObject.SetActive(true);
                _markerArrow.enabled = false;

                // Convert to canvas space so the label tracks the beacon exactly.
                Vector2 canvasPoint = ScreenToCanvas(screen);
                _marker.anchoredPosition = canvasPoint;

                _markerTitle.text = order.PickedUp ? Localization.T(order.CustomerName).ToUpperInvariant()
                    : Localization.T(order.ShopName).ToUpperInvariant();
                _markerTitle.color = accent;
                _markerDistance.text = MathX.FormatDistance(distance);
                _markerIcon.color = accent;

                float bob = 1f + 0.06f * Mathf.Sin(_markerPulse * 3.4f);
                _markerIcon.rectTransform.localScale = Vector3.one * bob;

                // Fade the whole marker down when the target is very close, so it stops
                // covering the thing you are aiming at.
                float alpha = Mathf.Clamp01(MathX.Remap(distance, 8f, 26f, 0.15f, 1f));
                SetAlpha(_markerTitle, alpha);
                SetAlpha(_markerDistance, alpha);
                _markerIcon.color = new Color(accent.r, accent.g, accent.b, alpha);
            }
            else
            {
                if (_marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);

                // Edge arrow.
                _markerArrow.enabled = true;
                _markerArrow.color = accent;

                Vector3 flat = order.ActiveTarget - Services.PlayerPosition;
                flat.y = 0f;
                Vector3 local = camera.transform.InverseTransformDirection(flat.normalized);
                var dir = new Vector2(local.x, local.z);
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
                dir.Normalize();

                // Push the arrow right out to the screen edge, on an ellipse rather than a
                // circle, so it hugs the border of a wide viewport properly.
                Vector2 canvasSize = _root.rect.size;
                var radius = new Vector2(canvasSize.x * 0.44f, canvasSize.y * 0.40f);
                _markerArrow.rectTransform.anchoredPosition = new Vector2(dir.x * radius.x, dir.y * radius.y);
                _markerArrow.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            }
        }

        Vector2 ScreenToCanvas(Vector3 screenPoint)
        {
            Vector2 canvasSize = _root.rect.size;
            return new Vector2(
                (screenPoint.x / Screen.width - 0.5f) * canvasSize.x,
                (screenPoint.y / Screen.height - 0.5f) * canvasSize.y);
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }

        void UpdateFeedback(float dt)
        {
            // Damage flash.
            _flash = Mathf.Max(0f, _flash - dt * 2.6f);
            _damageFlash.color = new Color(1f, 0.12f, 0.10f, _flash * 0.30f);

            // Boost vignette.
            var player = Services.Player;
            float boostTarget = player != null && player.IsBoosting ? 0.30f : 0f;
            Color vignette = _boostVignette.color;
            vignette.a = MathX.ExpSmooth(vignette.a, boostTarget, 7f, dt);
            _boostVignette.color = vignette;

            // Toast.
            if (_toastTimer > 0f)
            {
                _toastTimer -= dt;
                _toastRect.localScale = Vector3.one * Mathf.Lerp(_toastRect.localScale.x, 1f, 1f - Mathf.Exp(-16f * dt));
                float alpha = Mathf.Clamp01(_toastTimer / 0.35f);
                SetAlpha(_toast, alpha);
                Image panel = _toastRect.GetComponent<Image>();
                if (panel != null) panel.color = new Color(0f, 0f, 0f, 0.6f * alpha);
                if (_toastTimer <= 0f) _toastRect.gameObject.SetActive(false);
            }

            // Score popups drift up and fade.
            Camera camera = Services.Camera != null ? Services.Camera.Camera : null;
            foreach (Popup popup in _popups)
            {
                if (popup.Life <= 0f) continue;

                popup.Life -= dt;
                if (popup.Life <= 0f)
                {
                    popup.Rect.gameObject.SetActive(false);
                    continue;
                }

                float age = 1f - popup.Life / 1.15f;

                if (camera != null)
                {
                    Vector3 screen = camera.WorldToScreenPoint(popup.World);
                    if (screen.z > 1f)
                        popup.Rect.anchoredPosition = ScreenToCanvas(screen) + popup.Drift * age;
                }

                popup.Rect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.05f, MathX.EaseOutBack(Mathf.Min(1f, age * 4f)));
                SetAlpha(popup.Text, Mathf.Clamp01(1f - age * age));
            }
        }
    }
}
