using UnityEngine;
using FormosaExpress.Core;
using FormosaExpress.Fx;
using FormosaExpress.Traffic;
using FormosaExpress.UI;
using FormosaExpress.Vehicle;

namespace FormosaExpress.Gameplay
{
    /// <summary>
    /// The state machine that ties everything together: shift lifecycle, money and score,
    /// progression between levels, and every cross-system reaction (a crash spoiling the food,
    /// a delivery paying out, the city getting darker as the shifts go on).
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        public GamePhase Phase { get; private set; } = GamePhase.Boot;
        public GameMode Mode { get; private set; } = GameMode.Career;
        public int Level { get; private set; } = 1;
        public int Quota { get; private set; }

        /// <summary>Race mode: deliveries needed to win outright.</summary>
        public int RaceTarget => Tuning.RaceTargetDeliveries;

        public bool IsRace => Mode == GameMode.RivalRace;
        public int ShiftEarnings { get; private set; }
        public float ShiftTimeRemaining { get; private set; }
        public float ShiftDuration { get; private set; }

        /// <summary>0..1 "how dark is it" reading for headlights/fill lights - clamped from the
        /// signed day/dusk/night value the environment actually renders.</summary>
        public float NightFactor { get; private set; }

        EnvironmentDirector _environment;
        Vector3 _shiftStartPosition;
        float _shiftStartYaw;

        int _delivered;
        int _expired;
        int _perfect;
        bool _levelledUp;
        bool _raceWon;
        bool _raceResolved;
        float _cargoSettleTimer;

        static readonly string[] BriefingFlavour =
        {
            "Rush hour. Traffic will not wait for you.",
            "The night market is filling up. Watch the pavements.",
            "Rain earlier means slick asphalt. Brake sooner.",
            "Dispatch is stacking orders tonight. Plan your route.",
            "Every regular is ordering at once. Go.",
            "Take the alleys. The main road is jammed.",
            "The lanterns are up. So are expectations.",
            "Late shift. Half the city is hungry."
        };

        public void Initialise(EnvironmentDirector environment, Vector3 startPosition, float startYaw)
        {
            _environment = environment;
            _shiftStartPosition = startPosition;
            _shiftStartYaw = startYaw;

            HookEvents();
            EnterTitle();
        }

        void HookEvents()
        {
            if (Services.Player != null)
            {
                Services.Player.Impact += OnImpact;
                Services.Player.Respawned += OnRespawned;
            }

            if (Services.Orders != null)
            {
                Services.Orders.Delivered += OnDelivered;
                Services.Orders.Expired += OnExpired;
                Services.Orders.PickedUp += OnPickedUp;
                Services.Orders.Offered += OnOffered;
            }

            if (Services.Combo != null) Services.Combo.Popup += OnPopup;

            ScreenStack screens = Services.Screens;
            if (screens != null)
            {
                screens.ModeChosen += OnModeChosen;
                screens.StartRequested += OnStartRequested;
                screens.ResumeRequested += Resume;
                screens.RestartRequested += RestartShift;
                screens.QuitToTitleRequested += EnterTitle;
                screens.ContinueRequested += EnterGarage;
                screens.PurchaseRequested += TryPurchase;
            }
        }

        // ------------------------------------------------------------------ phases

        public void EnterTitle()
        {
            Phase = GamePhase.Title;
            Time.timeScale = 1f;

            Services.Orders?.EndShift();
            Services.Routes?.ClearTarget();
            Services.Rival?.EndRace();
            Services.Traffic?.SetDensity(Tuning.TrafficCount(1));
            Services.Pedestrians?.SetDensity(Tuning.PedestrianCount(1));

            Level = Mathf.Max(1, Services.Save != null ? Services.Save.highestLevelUnlocked : 1);
            float timeOfDay = Tuning.NightFactor(Level);
            NightFactor = Mathf.Clamp01(timeOfDay);
            _environment?.SetNightFactor(timeOfDay);

            ResetPlayerToStart();
            if (Services.Player != null) Services.Player.ControlEnabled = false;

            Services.Camera?.BeginOrbit(_shiftStartPosition + Vector3.up * 0.6f);
            Services.Hud?.SetVisible(false);
            Services.Audio?.SetRidingMix(false);

            Services.Screens?.SetTitleRecord(Services.Save);
            Services.Screens?.Show(ScreenStack.Screen.Title);
        }

        void EnterBriefing()
        {
            Phase = GamePhase.Briefing;
            Time.timeScale = 1f;

            Quota = IsRace ? 0 : Tuning.ShiftQuota(Level);
            ShiftDuration = IsRace ? Tuning.RaceDuration : Tuning.ShiftDuration(Level);
            float timeOfDay = Tuning.NightFactor(Level);
            NightFactor = Mathf.Clamp01(timeOfDay);
            _environment?.SetNightFactor(timeOfDay);

            int capacity = Services.Player != null ? Services.Player.Stats.BagCapacity : 2;

            if (IsRace)
            {
                Services.Hud?.SetVisible(false);
                Services.Screens?.SetRaceBriefing(Level, RaceTarget, ShiftDuration,
                    Services.Rival != null ? Services.Rival.RivalName : "A RIVAL");
                Services.Screens?.Show(ScreenStack.Screen.Briefing);
                return;
            }

            string flavour = BriefingFlavour[(Level - 1) % BriefingFlavour.Length];
            Services.Hud?.SetVisible(false);
            Services.Screens?.SetBriefing(Level, Quota, ShiftDuration, capacity, flavour);
            Services.Screens?.Show(ScreenStack.Screen.Briefing);
        }

        void BeginShift()
        {
            Phase = GamePhase.Riding;
            Time.timeScale = 1f;

            Quota = IsRace ? 0 : Tuning.ShiftQuota(Level);
            ShiftDuration = IsRace ? Tuning.RaceDuration : Tuning.ShiftDuration(Level);
            ShiftTimeRemaining = ShiftDuration;
            ShiftEarnings = 0;
            _delivered = 0;
            _expired = 0;
            _perfect = 0;
            _levelledUp = false;
            _raceWon = false;
            _raceResolved = false;

            float timeOfDay = Tuning.NightFactor(Level);
            NightFactor = Mathf.Clamp01(timeOfDay);
            _environment?.SetNightFactor(timeOfDay);

            ApplyUpgradesToPlayer();
            ResetPlayerToStart();

            Services.Combo?.ResetRun();
            Services.Player?.ResetRun();
            if (Services.Player != null) Services.Player.ControlEnabled = true;
            Services.Fx?.ClearSkids();

            Services.Traffic?.SetDensity(Tuning.TrafficCount(Level));
            Services.Pedestrians?.SetDensity(Tuning.PedestrianCount(Level));
            Services.Traffic?.Populate(_shiftStartPosition);
            Services.Pedestrians?.Populate(_shiftStartPosition);

            int capacity = Services.Player != null ? Services.Player.Stats.BagCapacity : 2;
            Services.Orders?.BeginShift(Level, capacity);

            if (IsRace && Services.Rival != null)
            {
                var rng = new Rng(Level * 7919 + Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f));
                Services.Rival.BeginRace(Level, _shiftStartPosition, _shiftStartYaw, ref rng);
            }
            else
            {
                Services.Rival?.EndRace();
            }

            Services.Camera?.EndOrbit();
            Services.Screens?.Show(ScreenStack.Screen.None);
            Services.Hud?.SetVisible(true);
            Services.Audio?.SetRidingMix(true);
            Services.Routes?.SetLineVisible(true);

            Services.Hud?.ShowToast(
                IsRace
                    ? $"RACE  ·  FIRST TO {RaceTarget} DELIVERIES"
                    : $"SHIFT {Level} - GO",
                IsRace ? Art.RivalTint : Art.HudGold, 2.4f);
        }

        void Pause()
        {
            if (Phase != GamePhase.Riding) return;

            Phase = GamePhase.Paused;
            Time.timeScale = 0f;
            Services.Input?.SuppressDriving();
            Services.Screens?.Show(ScreenStack.Screen.Paused);
        }

        void Resume()
        {
            if (Phase != GamePhase.Paused) return;

            Phase = GamePhase.Riding;
            Time.timeScale = 1f;
            Services.Screens?.Show(ScreenStack.Screen.None);
        }

        void RestartShift()
        {
            Time.timeScale = 1f;
            BeginShift();
        }

        void EndShift()
        {
            Phase = GamePhase.ShiftResult;
            Time.timeScale = 1f;

            if (Services.Player != null) Services.Player.ControlEnabled = false;
            Services.Orders?.EndShift();
            Services.Routes?.ClearTarget();

            RivalCourier rival = Services.Rival;
            int rivalDelivered = rival != null ? rival.Delivered : 0;
            int rivalEarnings = rival != null ? rival.Earnings : 0;

            // A race that runs out of time is settled on deliveries, then on earnings.
            if (IsRace && !_raceResolved)
            {
                _raceWon = _delivered > rivalDelivered
                           || (_delivered == rivalDelivered && ShiftEarnings >= rivalEarnings);
                _raceResolved = true;
            }

            ComboSystem combo = Services.Combo;
            var report = new ShiftReport
            {
                Mode = Mode,
                Level = Level,
                Earnings = ShiftEarnings,
                Quota = Quota,
                Score = combo != null ? combo.Score : 0,
                Delivered = _delivered,
                Expired = _expired,
                PerfectDeliveries = _perfect,
                BestCombo = combo != null ? combo.BestStep : 0,
                TopSpeedKmh = combo != null ? combo.TopSpeedKmh : 0f,
                NearMisses = combo != null ? combo.NearMissCount : 0,
                QuotaMet = IsRace ? _raceWon : ShiftEarnings >= Quota,
                RivalDelivered = rivalDelivered,
                RivalEarnings = rivalEarnings,
                RaceTarget = RaceTarget,
                RaceWon = _raceWon,
                RivalName = rival != null ? rival.RivalName : "RIVAL"
            };

            Services.Rival?.EndRace();

            SaveData save = Services.Save;
            if (save != null)
            {
                save.money += ShiftEarnings;
                save.bestScore = Mathf.Max(save.bestScore, report.Score);
                save.totalDeliveries += _delivered;
                save.perfectDeliveries += _perfect;
                save.bestShiftEarnings = Mathf.Max(save.bestShiftEarnings, ShiftEarnings);

                if (IsRace)
                {
                    if (_raceWon) save.racesWon++;
                    else save.racesLost++;
                }
                else if (report.QuotaMet && Level >= save.highestLevelUnlocked)
                {
                    save.highestLevelUnlocked = Level + 1;
                    _levelledUp = true;
                }

                SaveSystem.Save(save);
            }

            Services.Hud?.SetVisible(false);
            Services.Audio?.SetRidingMix(false);
            if (report.QuotaMet) Services.Audio?.PlayLevelUp();
            else Services.Audio?.PlayExpire();

            Services.Screens?.SetResults(report, _levelledUp, save != null ? save.money : 0);
            Services.Screens?.Show(ScreenStack.Screen.Results);
            Services.Camera?.BeginOrbit(Services.PlayerPosition + Vector3.up * 0.6f);
        }

        void EnterGarage()
        {
            Phase = GamePhase.Garage;
            Time.timeScale = 1f;

            // Advance to the next shift once the quota has been met. Races do not advance the
            // career; they are a side challenge at whatever level you have reached.
            if (_levelledUp && !IsRace) Level++;

            Services.Screens?.Show(ScreenStack.Screen.Garage);
        }

        void OnModeChosen(GameMode mode)
        {
            Mode = mode;

            // Races always use the highest shift reached, so the rival scales with the player.
            if (mode == GameMode.RivalRace && Services.Save != null)
                Level = Mathf.Max(1, Services.Save.highestLevelUnlocked);
        }

        void OnStartRequested()
        {
            switch (Phase)
            {
                case GamePhase.Title:
                    EnterBriefing();
                    break;
                case GamePhase.Briefing:
                    BeginShift();
                    break;
                case GamePhase.Garage:
                    EnterBriefing();
                    break;
            }
        }

        // ------------------------------------------------------------------ upgrades

        void TryPurchase(UpgradeKind kind)
        {
            SaveData save = Services.Save;
            if (save == null) return;

            int level = save.GetUpgrade(kind);
            if (level >= Tuning.UpgradeMaxLevel)
            {
                Services.Audio?.PlayUiBack();
                return;
            }

            int cost = Tuning.UpgradeCost(level);
            if (save.money < cost)
            {
                Services.Audio?.PlayUiBack();
                return;
            }

            save.money -= cost;
            save.SetUpgrade(kind, level + 1);
            SaveSystem.Save(save);

            ApplyUpgradesToPlayer();
            Services.Audio?.PlayCoin();
        }

        void ApplyUpgradesToPlayer()
        {
            if (Services.Player == null || Services.Save == null) return;
            Services.Player.ApplyStats(VehicleStats.From(Services.Save));
        }

        void ResetPlayerToStart()
        {
            Services.Player?.Teleport(_shiftStartPosition, _shiftStartYaw);
            Services.Camera?.SnapBehindTarget();
        }

        // ------------------------------------------------------------------ tick

        void Update()
        {
            InputRouter input = Services.Input;

            if (Phase == GamePhase.Riding)
            {
                float dt = Time.deltaTime;
                ShiftTimeRemaining -= dt;

                if (input != null && input.PausePressed) Pause();

                TickCargoSettling(dt);
                if (IsRace) TickRace();

                if (ShiftTimeRemaining <= 0f)
                {
                    ShiftTimeRemaining = 0f;
                    EndShift();
                }
                else if (ShiftTimeRemaining < 31f && ShiftTimeRemaining + dt >= 31f)
                {
                    Services.Hud?.ShowToast("30 SECONDS", Art.HudRed, 2f);
                }
            }
        }

        /// <summary>Ends the race the moment either courier reaches the delivery target.</summary>
        void TickRace()
        {
            if (_raceResolved) return;

            int rivalDelivered = Services.Rival != null ? Services.Rival.Delivered : 0;

            if (_delivered >= RaceTarget)
            {
                _raceWon = true;
                _raceResolved = true;
                Services.Hud?.ShowToast("YOU WIN!", Art.HudGold, 3f);
                EndShift();
                return;
            }

            if (rivalDelivered >= RaceTarget)
            {
                _raceWon = false;
                _raceResolved = true;
                Services.Hud?.ShowToast("BEATEN TO IT", Art.HudRed, 3f);
                EndShift();
            }
        }

        void TickCargoSettling(float dt)
        {
            OrderManager orders = Services.Orders;
            var player = Services.Player;
            if (orders == null || player == null) return;

            // Hard leaning sloshes the food; steady riding lets it settle.
            float lean = Mathf.Abs(player.LeanDegrees) / Tuning.LeanMaxDegrees;
            if (lean > 0.85f && player.Speed01 > 0.5f)
            {
                orders.ApplyCargoDamage(Tuning.CargoTiltDamagePerSecond * dt * (lean - 0.85f) / 0.15f);
                _cargoSettleTimer = 0f;
            }
            else if (lean < 0.35f && player.IsGrounded && player.BrakeInput < 0.5f)
            {
                _cargoSettleTimer += dt;
                if (_cargoSettleTimer > 1.4f) orders.SettleCargo(dt);
            }
            else
            {
                _cargoSettleTimer = 0f;
            }
        }

        // ------------------------------------------------------------------ reactions

        void OnImpact(ImpactInfo info)
        {
            bool landing = !info.HitTraffic && !info.HitPedestrian && info.Other == null;

            if (landing)
            {
                Services.Fx?.PlayLanding(info.Point, info.Severity);
                Services.Orders?.ApplyCargoDamage(Tuning.CargoLandingDamagePerUnit * info.Severity * 20f);
                Services.Hud?.FlashDamage(info.Severity * 0.5f);
                return;
            }

            Services.Fx?.PlayImpact(info.Point, info.Normal, info.Severity);
            Services.Audio?.PlayImpact(info.Severity, info.Point, info.HitPedestrian);
            Services.Hud?.FlashDamage(info.Severity);

            // A crash's only real consequence is losing the combo - cargo condition and pay
            // are untouched. Brushing a pedestrian doesn't even do that.
            if (info.Severity > 0.30f && !info.HitPedestrian)
                Services.Combo?.RegisterCrash(info.Point);

            if (info.HitTraffic)
            {
                TrafficAgent agent = Services.Traffic?.FindAgent(info.Other);
                if (agent != null) Services.Traffic.KnockAgent(agent, info.Point, info.Severity);

                if (info.Severity > 0.30f)
                    Services.Hud?.ShowToast("CRASH!", Art.HudRed, 1.4f);
            }
            else if (info.HitPedestrian)
            {
                PedestrianAgent ped = Services.Pedestrians?.FindAgent(info.Other);
                if (ped != null)
                {
                    Vector3 away = ped.transform.position - info.Point;
                    away.y = 0f;
                    ped.Tumble(away.normalized * 4.5f);
                }

                Services.Hud?.ShowToast("WATCH THE PAVEMENT!", Art.HudRed, 1.4f);
            }
        }

        void OnRespawned()
        {
            Services.Hud?.ShowToast("BACK ON THE ROAD", Art.HudGold, 1.4f);
            Services.Fx?.ClearSkids();
        }

        void OnOffered(Order order)
        {
            if (Phase != GamePhase.Riding) return;
            Services.Hud?.ShowToast($"NEW ORDER  ·  {order.ShopName}", Art.BeaconAmber, 1.9f);
            Services.Audio?.PlayUiMove();
        }

        void OnPickedUp(Order order)
        {
            Services.Hud?.ShowToast($"{order.DishName.ToUpperInvariant()}  ·  TO {order.CustomerName}",
                Art.HudCyan, 2.1f);
            Services.Audio?.PlayPickup();
            Services.Fx?.PlayDelivery(order.PickupPosition);
        }

        void OnDelivered(Order order, int payout, int timeBonusSeconds)
        {
            ShiftEarnings += payout;
            _delivered++;
            if (order.ConditionTier == CargoCondition.Perfect) _perfect++;

            Services.Combo?.RegisterDelivery(order.DropPosition, timeBonusSeconds);
            Services.Fx?.PlayDelivery(order.DropPosition);
            Services.Audio?.PlayDelivery();
            Services.Audio?.PlayCoin();

            string tier = order.ConditionTier switch
            {
                CargoCondition.Perfect => "PERFECT",
                CargoCondition.Good => "GOOD",
                CargoCondition.Messy => "MESSY",
                _ => "RUINED"
            };

            Color tierColour = order.ConditionTier switch
            {
                CargoCondition.Perfect => Art.HudGold,
                CargoCondition.Good => Art.HudGreen,
                CargoCondition.Messy => Art.BeaconAmber,
                _ => Art.HudRed
            };

            Services.Hud?.ShowToast($"{tier} DELIVERY   +{MathX.FormatMoney(payout)}", tierColour, 2.3f);

            if (ShiftEarnings >= Quota && ShiftEarnings - payout < Quota)
                Services.Hud?.ShowToast("QUOTA MET - KEEP GOING FOR BONUS", Art.HudGold, 2.6f);
        }

        void OnExpired(Order order)
        {
            // A job the rival took and then lost is their problem, not the player's.
            if (order.CarriedBy == Courier.Rival) return;

            _expired++;
            int penalty = Mathf.RoundToInt(order.BaseFare * Tuning.ExpiredOrderPenaltyFraction);
            ShiftEarnings = Mathf.Max(0, ShiftEarnings - penalty);

            Services.Combo?.RegisterCrash(order.ActiveTarget);
            Services.Audio?.PlayExpire();
            Services.Hud?.ShowToast($"ORDER LOST  ·  -{MathX.FormatMoney(penalty)}", Art.HudRed, 2.2f);
        }

        void OnPopup(string text, Color colour, Vector3 world)
        {
            Services.Hud?.ShowPopup(text, colour, world);
        }
    }
}
