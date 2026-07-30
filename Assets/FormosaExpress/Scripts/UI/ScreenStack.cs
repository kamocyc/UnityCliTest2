using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FormosaExpress.Core;

namespace FormosaExpress.UI
{
    /// <summary>
    /// Every full-screen panel: title, shift briefing, pause, results and the garage. Driven by
    /// the same <see cref="InputRouter"/> as the game so a gamepad works everywhere.
    /// </summary>
    public sealed class ScreenStack : MonoBehaviour
    {
        public enum Screen
        {
            None,
            Title,
            Briefing,
            Paused,
            Results,
            Garage
        }

        public Screen Current { get; private set; } = Screen.None;

        public event Action<GameMode> ModeChosen;
        public event Action StartRequested;
        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action QuitToTitleRequested;
        public event Action ContinueRequested;
        public event Action<UpgradeKind> PurchaseRequested;

        Canvas _canvas;
        RectTransform _root;
        Image _backdrop;

        // Title
        RectTransform _title;
        Text _titleTag;
        Text _titleRecord;
        Text _titlePrompt;
        Text _titleControlsHint;
        Menu _modeMenu;
        readonly List<Image> _modePanels = new List<Image>(2);
        readonly List<GameMode> _modeValues = new List<GameMode>(2);
        readonly List<Text> _modeBlurbs = new List<Text>(2);
        readonly List<string> _modeBlurbKeys = new List<string>(2);

        // Briefing
        RectTransform _briefing;
        Text _briefingHeading;
        Text _briefingBody;
        Text _briefingPrompt;

        // Pause
        RectTransform _paused;
        Text _pauseHeading;
        Menu _pauseMenu;
        int _languageItemIndex;
        RectTransform _controls;
        bool _showingControls;
        Text _controlsHeading;
        Text _controlsBody;
        Text _controlsPrompt;

        // Results
        RectTransform _results;
        Text _resultsHeading;
        Text _resultsBody;
        Text _resultsPrompt;

        // Garage
        RectTransform _garage;
        Text _garageHeading;
        Text _garageMoney;
        Text _garagePrompt;
        readonly List<GarageRow> _garageRows = new List<GarageRow>(5);
        int _garageIndex;

        float _promptPhase;

        sealed class GarageRow
        {
            public UpgradeKind Kind;
            public Image Panel;
            public Text Name;
            public Text Detail;
            public Text Cost;
            public Image[] Pips;
        }

        sealed class Menu
        {
            public readonly List<Text> Items = new List<Text>();
            public readonly List<Action> Actions = new List<Action>();
            public readonly List<string> Keys = new List<string>();
            public int Index;

            public void Move(int delta)
            {
                if (Items.Count == 0) return;
                Index = (Index + delta + Items.Count) % Items.Count;
            }

            public void Refresh(Color selected, Color normal)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    bool active = i == Index;
                    Items[i].color = active ? selected : normal;
                    Items[i].text = (active ? "> " : "  ") + Items[i].text.TrimStart('>', ' ');
                }
            }

            public void Activate()
            {
                if (Index >= 0 && Index < Actions.Count) Actions[Index]?.Invoke();
            }
        }

        // ------------------------------------------------------------------ construction

        public void Build()
        {
            _canvas = UiKit.CreateCanvas("Screens", transform, 200);
            _root = _canvas.GetComponent<RectTransform>();

            _backdrop = UiKit.CreateImage("Backdrop", _root, TextureFactory.Plain(), new Color(0.03f, 0.03f, 0.05f, 0.72f));
            _backdrop.rectTransform.Stretch();
            _backdrop.preserveAspect = false;

            BuildTitle();
            BuildBriefing();
            BuildPause();
            BuildControls();
            BuildResults();
            BuildGarage();

            Show(Screen.None);

            Localization.Changed += ApplyLanguage;
            ApplyLanguage();
        }

        /// <summary>Re-stamps every static caption in every screen after a language toggle. The
        /// dynamic ones (results, garage rows, briefing body) already rebuild from Localization.T
        /// each time they're populated, so they don't need a hook here.</summary>
        void ApplyLanguage()
        {
            _titleTag.text = Localization.T("NIGHT MARKET DELIVERY");
            _titlePrompt.text = Localization.T("W / S  choose        ENTER  go");
            _titleControlsHint.text = Localization.T(
                "W / S  throttle & brake        A / D  steer        SPACE  drift        SHIFT  boost\n"
                + "TAB  switch job        E  horn        C  camera        ESC  pause");
            for (int i = 0; i < _modeBlurbs.Count; i++) _modeBlurbs[i].text = Localization.T(_modeBlurbKeys[i]);
            if (Services.Save != null) SetTitleRecord(Services.Save);

            _briefingPrompt.text = Localization.T("ENTER  to start riding");

            _pauseHeading.text = Localization.T("PAUSED");
            ApplyPauseLanguage();

            _controlsHeading.text = Localization.T("CONTROLS");
            _controlsBody.text =
                Localization.T("W / S               throttle & brake") + "\n"
                + Localization.T("A / D               steer") + "\n"
                + Localization.T("SPACE               drift") + "\n"
                + Localization.T("LEFT SHIFT          boost") + "\n"
                + Localization.T("TAB                 switch delivery job") + "\n"
                + Localization.T("E                   horn") + "\n"
                + Localization.T("Q                   look back") + "\n"
                + Localization.T("C                   change camera") + "\n"
                + Localization.T("R                   restart shift") + "\n"
                + Localization.T("ESC                 pause");
            _controlsPrompt.text = Localization.T("ENTER / ESC  back");

            _garageHeading.text = Localization.T("GARAGE");
            _garagePrompt.text = Localization.T("W / S  choose        ENTER  buy        TAB  head out for the next shift");
            if (Current == Screen.Garage) RefreshGarage();
        }

        string LanguageMenuLabel() => Localization.Current switch
        {
            Language.Japanese => Localization.T("LANGUAGE: 日本語"),
            Language.SimplifiedChinese => Localization.T("LANGUAGE: 简体中文"),
            Language.TraditionalChinese => Localization.T("LANGUAGE: 繁體中文"),
            _ => Localization.T("LANGUAGE: ENGLISH")
        };

        void ApplyPauseLanguage()
        {
            for (int i = 0; i < _pauseMenu.Items.Count; i++)
                _pauseMenu.Items[i].text = i == _languageItemIndex ? LanguageMenuLabel() : Localization.T(_pauseMenu.Keys[i]);
            if (Current == Screen.Paused) _pauseMenu.Refresh(Art.HudGold, Art.HudDim);
        }

        void ToggleLanguage() => Localization.Toggle();

        void BuildTitle()
        {
            _title = UiKit.CreateRect("Title", _root);
            _title.Stretch();

            Image logo = UiKit.CreateImage("Logo", _title, TextureFactory.ScooterGlyph(128), Art.HudGold);
            logo.rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, 210f), new Vector2(150f, 150f));

            UiKit.CreateOutlinedLabel("Name", _title, "FORMOSA EXPRESS", 92, Art.HudGold)
                .rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, 86f), new Vector2(1200f, 100f));

            _titleTag = UiKit.CreateOutlinedLabel("Tag", _title, "NIGHT MARKET DELIVERY", 30, Art.HudCyan);
            _titleTag.rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, 26f), new Vector2(1200f, 40f));

            _titleRecord = UiKit.CreateOutlinedLabel("Record", _title, "", 22, Art.HudDim);
            _titleRecord.rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, -26f), new Vector2(1200f, 30f));

            // Mode select.
            _modeMenu = new Menu();
            AddModeCard(0, "CLOCK ON",
                "Work the shifts. Beat the cash quota before the clock runs out.", GameMode.Career);
            AddModeCard(1, "RIVAL RACE",
                "Head to head with another courier for the same orders. First to five wins.",
                GameMode.RivalRace);

            _titlePrompt = UiKit.CreateOutlinedLabel("Prompt", _title, "W / S  choose        ENTER  go", 26,
                Art.HudText);
            _titlePrompt.rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, -262f), new Vector2(1200f, 34f));

            _titleControlsHint = UiKit.CreateOutlinedLabel("Controls", _title,
                    "W / S  throttle & brake        A / D  steer        SPACE  drift        SHIFT  boost\n"
                    + "TAB  switch job        E  horn        C  camera        ESC  pause",
                    19, new Color(1f, 1f, 1f, 0.55f));
            _titleControlsHint.rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, -322f), new Vector2(1200f, 76f));
        }

        void AddModeCard(int index, string title, string blurb, GameMode mode)
        {
            Image panel = UiKit.CreatePanel($"Mode{index}", _title, new Color(0f, 0f, 0f, 0.42f), 16);
            panel.rectTransform.Place(UiKit.Centre, UiKit.Centre, new Vector2(0f, -84f - index * 84f),
                new Vector2(720f, 74f));

            Text heading = UiKit.CreateOutlinedLabel($"Mode{index}Title", panel.transform, title, 32, Art.HudDim);
            heading.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(26f, -8f), new Vector2(400f, 36f));
            heading.alignment = TextAnchor.MiddleLeft;

            Text detail = UiKit.CreateLabel($"Mode{index}Blurb", panel.transform, Localization.T(blurb), 17, Art.HudDim);
            detail.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(26f, -42f), new Vector2(670f, 24f));

            _modePanels.Add(panel);
            _modeMenu.Items.Add(heading);
            _modeMenu.Actions.Add(() => { });
            _modeValues.Add(mode);
            _modeBlurbs.Add(detail);
            _modeBlurbKeys.Add(blurb);
        }

        void BuildBriefing()
        {
            _briefing = UiKit.CreateRect("Briefing", _root);
            _briefing.Stretch();

            Image panel = UiKit.CreatePanel("Panel", _briefing, Art.HudPanelSolid, 22);
            panel.rectTransform.Place(UiKit.Centre, UiKit.Centre, Vector2.zero, new Vector2(760f, 440f));

            _briefingHeading = UiKit.CreateOutlinedLabel("Heading", panel.transform, "SHIFT 1", 62, Art.HudGold);
            _briefingHeading.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -54f),
                new Vector2(700f, 70f));

            _briefingBody = UiKit.CreateLabel("Body", panel.transform, "", 26, Art.HudText, TextAnchor.UpperCenter);
            _briefingBody.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -130f),
                new Vector2(660f, 240f));

            _briefingPrompt = UiKit.CreateOutlinedLabel("Prompt", panel.transform, "ENTER  to start riding", 28, Art.HudCyan);
            _briefingPrompt.rectTransform.Place(UiKit.BottomCentre, UiKit.BottomCentre, new Vector2(0f, 34f),
                    new Vector2(660f, 36f));
        }

        void BuildPause()
        {
            _paused = UiKit.CreateRect("Paused", _root);
            _paused.Stretch();

            Image panel = UiKit.CreatePanel("Panel", _paused, Art.HudPanelSolid, 22);
            panel.rectTransform.Place(UiKit.Centre, UiKit.Centre, Vector2.zero, new Vector2(520f, 456f));

            _pauseHeading = UiKit.CreateOutlinedLabel("Heading", panel.transform, "PAUSED", 56, Art.HudGold);
            _pauseHeading.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -48f), new Vector2(460f, 64f));

            _pauseMenu = new Menu();
            AddMenuItem(_pauseMenu, panel.transform, "RESUME", -140f, () => ResumeRequested?.Invoke());
            AddMenuItem(_pauseMenu, panel.transform, "CONTROLS", -196f, ShowControls);
            _languageItemIndex = _pauseMenu.Items.Count;
            AddMenuItem(_pauseMenu, panel.transform, "LANGUAGE: ENGLISH", -252f, ToggleLanguage);
            AddMenuItem(_pauseMenu, panel.transform, "RESTART SHIFT", -308f, () => RestartRequested?.Invoke());
            AddMenuItem(_pauseMenu, panel.transform, "QUIT TO TITLE", -364f, () => QuitToTitleRequested?.Invoke());
        }

        void ShowControls()
        {
            _showingControls = true;
            _paused.gameObject.SetActive(false);
            _controls.gameObject.SetActive(true);
        }

        void HideControls()
        {
            _showingControls = false;
            _controls.gameObject.SetActive(false);
            _paused.gameObject.SetActive(true);
        }

        void BuildControls()
        {
            _controls = UiKit.CreateRect("Controls", _root);
            _controls.Stretch();

            Image panel = UiKit.CreatePanel("Panel", _controls, Art.HudPanelSolid, 22);
            panel.rectTransform.Place(UiKit.Centre, UiKit.Centre, Vector2.zero, new Vector2(620f, 560f));

            _controlsHeading = UiKit.CreateOutlinedLabel("Heading", panel.transform, "CONTROLS", 56, Art.HudGold);
            _controlsHeading.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -48f), new Vector2(560f, 64f));

            _controlsBody = UiKit.CreateLabel("Body", panel.transform,
                "W / S               throttle & brake\n"
                + "A / D               steer\n"
                + "SPACE               drift\n"
                + "LEFT SHIFT          boost\n"
                + "TAB                 switch delivery job\n"
                + "E                   horn\n"
                + "Q                   look back\n"
                + "C                   change camera\n"
                + "R                   restart shift\n"
                + "ESC                 pause",
                26, Art.HudText, TextAnchor.UpperLeft);
            _controlsBody.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -130f), new Vector2(480f, 360f));

            _controlsPrompt = UiKit.CreateOutlinedLabel("Prompt", panel.transform, "ENTER / ESC  back", 26, Art.HudCyan);
            _controlsPrompt.rectTransform.Place(UiKit.BottomCentre, UiKit.BottomCentre, new Vector2(0f, 30f), new Vector2(560f, 34f));
        }

        void AddMenuItem(Menu menu, Transform parent, string label, float y, Action action)
        {
            Text text = UiKit.CreateOutlinedLabel("Item" + menu.Items.Count, parent, Localization.T(label), 32, Art.HudDim);
            text.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, y), new Vector2(440f, 42f));
            menu.Items.Add(text);
            menu.Actions.Add(action);
            menu.Keys.Add(label);
        }

        void BuildResults()
        {
            _results = UiKit.CreateRect("Results", _root);
            _results.Stretch();

            Image panel = UiKit.CreatePanel("Panel", _results, Art.HudPanelSolid, 22);
            panel.rectTransform.Place(UiKit.Centre, UiKit.Centre, Vector2.zero, new Vector2(820f, 620f));

            _resultsHeading = UiKit.CreateOutlinedLabel("Heading", panel.transform, "", 58, Art.HudGold);
            _resultsHeading.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -52f),
                new Vector2(740f, 66f));

            _resultsBody = UiKit.CreateLabel("Body", panel.transform, "", 25, Art.HudText, TextAnchor.UpperLeft);
            _resultsBody.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -120f),
                new Vector2(600f, 420f));

            _resultsPrompt = UiKit.CreateOutlinedLabel("Prompt", panel.transform, "ENTER  to visit the garage", 28,
                Art.HudCyan);
            _resultsPrompt.rectTransform.Place(UiKit.BottomCentre, UiKit.BottomCentre, new Vector2(0f, 34f),
                new Vector2(700f, 36f));
        }

        void BuildGarage()
        {
            _garage = UiKit.CreateRect("Garage", _root);
            _garage.Stretch();

            Image panel = UiKit.CreatePanel("Panel", _garage, Art.HudPanelSolid, 22);
            panel.rectTransform.Place(UiKit.Centre, UiKit.Centre, Vector2.zero, new Vector2(900f, 620f));

            _garageHeading = UiKit.CreateOutlinedLabel("Heading", panel.transform, "GARAGE", 56, Art.HudGold);
            _garageHeading.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -46f), new Vector2(840f, 62f));

            _garageMoney = UiKit.CreateOutlinedLabel("Money", panel.transform, "", 30, Art.HudGold);
            _garageMoney.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -104f),
                new Vector2(840f, 36f));

            var kinds = (UpgradeKind[])Enum.GetValues(typeof(UpgradeKind));
            for (int i = 0; i < kinds.Length; i++)
            {
                var row = new GarageRow { Kind = kinds[i] };

                row.Panel = UiKit.CreatePanel($"Row{i}", panel.transform, new Color(0f, 0f, 0f, 0.35f), 12);
                row.Panel.rectTransform.Place(UiKit.TopCentre, UiKit.TopCentre, new Vector2(0f, -156f - i * 78f),
                    new Vector2(800f, 70f));

                row.Name = UiKit.CreateLabel("Name", row.Panel.transform, UpgradeName(kinds[i]), 26, Art.HudText);
                row.Name.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(20f, -8f), new Vector2(280f, 28f));

                row.Detail = UiKit.CreateLabel("Detail", row.Panel.transform, UpgradeBlurb(kinds[i]), 17, Art.HudDim);
                row.Detail.rectTransform.Place(UiKit.TopLeft, UiKit.TopLeft, new Vector2(20f, -36f),
                    new Vector2(420f, 24f));

                row.Pips = new Image[Tuning.UpgradeMaxLevel];
                for (int p = 0; p < Tuning.UpgradeMaxLevel; p++)
                {
                    Image pip = UiKit.CreateImage($"Pip{p}", row.Panel.transform, TextureFactory.RoundedRect(4),
                        Art.HudDim);
                    pip.type = Image.Type.Sliced;
                    pip.preserveAspect = false;
                    pip.rectTransform.Place(UiKit.TopRight, UiKit.TopRight,
                        new Vector2(-306f + p * 34f, -22f), new Vector2(26f, 14f));
                    row.Pips[p] = pip;
                }

                row.Cost = UiKit.CreateLabel("Cost", row.Panel.transform, "", 26, Art.HudGold, TextAnchor.MiddleRight);
                row.Cost.rectTransform.Place(UiKit.TopRight, UiKit.TopRight, new Vector2(-24f, -20f),
                    new Vector2(130f, 30f));

                _garageRows.Add(row);
            }

            _garagePrompt = UiKit.CreateOutlinedLabel("Prompt", panel.transform,
                    "W / S  choose        ENTER  buy        TAB  head out for the next shift", 24, Art.HudCyan);
            _garagePrompt.rectTransform.Place(UiKit.BottomCentre, UiKit.BottomCentre, new Vector2(0f, 28f),
                    new Vector2(840f, 34f));
        }

        static string UpgradeName(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Engine: return Localization.T("ENGINE");
                case UpgradeKind.Tyres: return Localization.T("TYRES");
                case UpgradeKind.Suspension: return Localization.T("SUSPENSION");
                case UpgradeKind.Bag: return Localization.T("DELIVERY BAG");
                default: return Localization.T("ADRENALINE TANK");
            }
        }

        static string UpgradeBlurb(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Engine: return Localization.T("Higher top speed and quicker pull away from the lights.");
                case UpgradeKind.Tyres: return Localization.T("More grip through corners, stronger brakes.");
                case UpgradeKind.Suspension: return Localization.T("Softer landings; kerbs and knocks cost you less.");
                case UpgradeKind.Bag: return Localization.T("Carry more orders at once and keep the food intact.");
                default: return Localization.T("A bigger tank, so boost lasts longer.");
            }
        }

        // ------------------------------------------------------------------ presentation

        public void Show(Screen screen)
        {
            Current = screen;

            bool any = screen != Screen.None;
            _canvas.enabled = any;
            _backdrop.enabled = any;

            _title.gameObject.SetActive(screen == Screen.Title);
            _briefing.gameObject.SetActive(screen == Screen.Briefing);
            _paused.gameObject.SetActive(screen == Screen.Paused);
            _results.gameObject.SetActive(screen == Screen.Results);
            _garage.gameObject.SetActive(screen == Screen.Garage);

            // The controls panel is a sub-screen of Paused, not a Screen of its own; always
            // start fresh on the pause menu rather than wherever it was left last time.
            _showingControls = false;
            _controls.gameObject.SetActive(false);

            if (screen == Screen.Title) RefreshModeSelect();

            if (screen == Screen.Paused)
            {
                _pauseMenu.Index = 0;
                _pauseMenu.Refresh(Art.HudGold, Art.HudDim);
            }

            if (screen == Screen.Garage)
            {
                _garageIndex = 0;
                RefreshGarage();
            }

            // A lighter backdrop over gameplay, heavier over the title.
            _backdrop.color = screen == Screen.Title
                ? new Color(0.02f, 0.02f, 0.04f, 0.55f)
                : new Color(0.03f, 0.03f, 0.05f, 0.74f);
        }

        public void SetTitleRecord(SaveData save)
        {
            if (save.bestScore <= 0)
            {
                _titleRecord.text = Localization.T("FIRST DAY ON THE JOB");
                return;
            }

            string races = save.racesWon + save.racesLost > 0
                ? string.Format(Localization.T("     RACES {0}-{1}"), save.racesWon, save.racesLost)
                : string.Empty;

            _titleRecord.text = string.Format(Localization.T("BEST SCORE {0}     DELIVERIES {1}     CASH {2}{3}"),
                save.bestScore.ToString("N0"), save.totalDeliveries, MathX.FormatMoney(save.money), races);
        }

        void RefreshModeSelect()
        {
            for (int i = 0; i < _modePanels.Count; i++)
            {
                bool selected = i == _modeMenu.Index;
                _modePanels[i].color = selected
                    ? new Color(0.16f, 0.18f, 0.24f, 0.94f)
                    : new Color(0f, 0f, 0f, 0.42f);

                Text heading = _modeMenu.Items[i];
                heading.text = (selected ? "> " : "  ") + heading.text.TrimStart('>', ' ');
                heading.color = selected
                    ? (_modeValues[i] == GameMode.RivalRace ? Art.RivalTint : Art.HudGold)
                    : Art.HudDim;
            }
        }

        public void SetRaceBriefing(int level, int target, float duration, string rivalName)
        {
            _briefingHeading.text = "RIVAL RACE";
            _briefingHeading.color = Art.RivalTint;
            _briefingBody.text =
                string.Format(Localization.T("{0} is working the same streets tonight."), Localization.T(rivalName)) + "\n\n"
                + Localization.T("You are both bidding for the same orders, and only one of you\n"
                    + "can take each one. Whoever reaches the shop first gets it.") + "\n\n"
                + string.Format(Localization.T("FIRST TO            {0} deliveries"), target) + "\n"
                + string.Format(Localization.T("TIME LIMIT          {0}"), MathX.FormatClock(duration)) + "\n"
                + string.Format(Localization.T("RIVAL SKILL         shift {0} pace"), level);
        }

        public void SetBriefing(int level, int quota, float duration, int capacity, string flavour)
        {
            _briefingHeading.text = $"{Localization.T("SHIFT")} {level}";
            _briefingHeading.color = Art.HudGold;
            _briefingBody.text =
                string.Format(Localization.T("Earn {0} before the clock runs out."), MathX.FormatMoney(quota)) + "\n\n"
                + $"{Localization.T("TIME ON SHIFT")}      {MathX.FormatClock(duration)}\n"
                + $"{Localization.T("BAG CAPACITY")}       {capacity} order{(capacity == 1 ? "" : "s")}\n\n"
                + flavour;
        }

        public void SetResults(ShiftReport report, bool levelledUp, int totalMoney)
        {
            if (report.Mode == GameMode.RivalRace)
            {
                SetRaceResults(report, totalMoney);
                return;
            }

            _resultsHeading.text = Localization.T(report.QuotaMet ? "SHIFT COMPLETE" : "SHIFT OVER");
            _resultsHeading.color = report.QuotaMet ? Art.HudGold : Art.HudRed;

            string verdict = report.QuotaMet
                ? levelledUp ? string.Format(Localization.T("\nQuota met. Shift {0} unlocked."), report.Level + 1)
                    : Localization.T("\nQuota met.")
                : Localization.T("\nQuota missed. The dispatcher is not impressed - try again.");

            _resultsBody.text =
                $"{Localization.T("EARNINGS")}            {MathX.FormatMoney(report.Earnings)}  /  {MathX.FormatMoney(report.Quota)}\n"
                + $"{Localization.T("SCORE")}               {report.Score:N0}\n"
                + $"{Localization.T("DELIVERED")}           {report.Delivered}\n"
                + $"{Localization.T("PERFECT")}             {report.PerfectDeliveries}\n"
                + $"{Localization.T("EXPIRED")}             {report.Expired}\n"
                + $"{Localization.T("BEST COMBO")}          x{Tuning.ComboMultipliers[Mathf.Clamp(report.BestCombo, 0, Tuning.ComboMultipliers.Length - 1)]:0.##}\n"
                + $"{Localization.T("NEAR MISSES")}         {report.NearMisses}\n"
                + $"{Localization.T("TOP SPEED")}           {Mathf.RoundToInt(report.TopSpeedKmh)} km/h\n"
                + $"\n{Localization.T("WALLET")}              {MathX.FormatMoney(totalMoney)}"
                + verdict;

            _resultsPrompt.text = Localization.T("ENTER  to visit the garage");
        }

        void SetRaceResults(ShiftReport report, int totalMoney)
        {
            _resultsHeading.text = Localization.T(report.RaceWon ? "YOU WIN" : "YOU LOSE");
            _resultsHeading.color = report.RaceWon ? Art.HudGold : Art.HudRed;

            string margin;
            if (report.Delivered == report.RivalDelivered)
            {
                margin = Localization.T("Settled on earnings.");
            }
            else
            {
                int diff = Mathf.Abs(report.Delivered - report.RivalDelivered);
                margin = Localization.Current switch
                {
                    Language.Japanese => string.Format(report.RaceWon ? "{0}件差で勝利。" : "{0}件差で敗北。", diff),
                    Language.SimplifiedChinese => string.Format(report.RaceWon ? "以{0}单之差获胜。" : "以{0}单之差落败。", diff),
                    Language.TraditionalChinese => string.Format(report.RaceWon ? "以{0}單之差獲勝。" : "以{0}單之差落敗。", diff),
                    _ => report.RaceWon
                        ? $"Won by {diff} delivery" + (diff == 1 ? "." : "ies.")
                        : $"Lost by {diff} delivery" + (diff == 1 ? "." : "ies.")
                };
            }

            _resultsBody.text =
                $"                    {Localization.T("YOU")}        {Localization.T(report.RivalName)}\n"
                + $"{Localization.T("DELIVERIES")}          {report.Delivered,-11}{report.RivalDelivered}\n"
                + $"{Localization.T("EARNINGS")}            {MathX.FormatMoney(report.Earnings),-11}{MathX.FormatMoney(report.RivalEarnings)}\n"
                + $"\n{Localization.T("SCORE")}               {report.Score:N0}\n"
                + $"{Localization.T("BEST COMBO")}          x{Tuning.ComboMultipliers[Mathf.Clamp(report.BestCombo, 0, Tuning.ComboMultipliers.Length - 1)]:0.##}\n"
                + $"{Localization.T("NEAR MISSES")}         {report.NearMisses}\n"
                + $"{Localization.T("TOP SPEED")}           {Mathf.RoundToInt(report.TopSpeedKmh)} km/h\n"
                + $"\n{Localization.T("WALLET")}              {MathX.FormatMoney(totalMoney)}\n"
                + $"\n{margin}";

            _resultsPrompt.text = Localization.T("ENTER  to visit the garage");
        }

        void RefreshGarage()
        {
            SaveData save = Services.Save;
            if (save == null) return;

            _garageMoney.text = Localization.T("WALLET") + "   " + MathX.FormatMoney(save.money);

            for (int i = 0; i < _garageRows.Count; i++)
            {
                GarageRow row = _garageRows[i];
                int level = save.GetUpgrade(row.Kind);
                bool selected = i == _garageIndex;
                bool maxed = level >= Tuning.UpgradeMaxLevel;
                int cost = Tuning.UpgradeCost(level);
                bool affordable = !maxed && save.money >= cost;

                row.Panel.color = selected ? new Color(0.16f, 0.18f, 0.22f, 0.95f) : new Color(0f, 0f, 0f, 0.35f);
                row.Name.text = (selected ? "> " : "  ") + UpgradeName(row.Kind);
                row.Name.color = selected ? Art.HudGold : Art.HudText;

                for (int p = 0; p < row.Pips.Length; p++)
                    row.Pips[p].color = p < level ? Art.HudGreen : new Color(1f, 1f, 1f, 0.16f);

                if (maxed)
                {
                    row.Cost.text = Localization.T("MAX");
                    row.Cost.color = Art.HudGreen;
                }
                else
                {
                    row.Cost.text = MathX.FormatMoney(cost);
                    row.Cost.color = affordable ? Art.HudGold : Art.HudRed;
                }
            }
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (Current == Screen.None) return;

            InputRouter input = Services.Input;
            if (input == null) return;

            float dt = Time.unscaledDeltaTime;
            _promptPhase += dt;
            float pulse = 0.6f + 0.4f * Mathf.Sin(_promptPhase * 3.4f);

            switch (Current)
            {
                case Screen.Title:
                    SetAlpha(_titlePrompt, pulse);

                    if (input.MenuVertical != 0)
                    {
                        _modeMenu.Move(-input.MenuVertical);
                        RefreshModeSelect();
                        Services.Audio?.PlayUiMove();
                    }

                    if (input.ConfirmPressed)
                    {
                        Services.Audio?.PlayUiConfirm();
                        ModeChosen?.Invoke(_modeValues[_modeMenu.Index]);
                        StartRequested?.Invoke();
                    }

                    break;

                case Screen.Briefing:
                    if (input.ConfirmPressed)
                    {
                        Services.Audio?.PlayUiConfirm();
                        StartRequested?.Invoke();
                    }

                    break;

                case Screen.Paused:
                    if (input.PausePressed) Debug.Log($"[Diag] Paused case sees PausePressed=true, showingControls={_showingControls}, confirm={input.ConfirmPressed}");
                    if (_showingControls)
                    {
                        if (input.ConfirmPressed || input.PausePressed || input.CancelPressed)
                        {
                            Services.Audio?.PlayUiBack();
                            HideControls();
                        }

                        break;
                    }

                    if (input.MenuVertical != 0)
                    {
                        _pauseMenu.Move(-input.MenuVertical);
                        _pauseMenu.Refresh(Art.HudGold, Art.HudDim);
                        Services.Audio?.PlayUiMove();
                    }

                    if (input.ConfirmPressed)
                    {
                        Services.Audio?.PlayUiConfirm();
                        _pauseMenu.Activate();
                    }
                    else if (input.PausePressed || input.CancelPressed)
                    {
                        Debug.Log($"[Diag] Resume branch hit. PausePressed={input.PausePressed} CancelPressed={input.CancelPressed}");
                        Services.Audio?.PlayUiBack();
                        ResumeRequested?.Invoke();
                    }

                    break;

                case Screen.Results:
                    SetAlpha(_resultsPrompt, pulse);
                    if (input.ConfirmPressed)
                    {
                        Services.Audio?.PlayUiConfirm();
                        ContinueRequested?.Invoke();
                    }

                    break;

                case Screen.Garage:
                    if (input.MenuVertical != 0)
                    {
                        _garageIndex = (_garageIndex - input.MenuVertical + _garageRows.Count) % _garageRows.Count;
                        RefreshGarage();
                        Services.Audio?.PlayUiMove();
                    }

                    if (input.ConfirmPressed)
                    {
                        PurchaseRequested?.Invoke(_garageRows[_garageIndex].Kind);
                        RefreshGarage();
                    }
                    else if (input.CycleTargetPressed || input.PausePressed)
                    {
                        Services.Audio?.PlayUiConfirm();
                        StartRequested?.Invoke();
                    }

                    break;
            }
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }
    }
}
