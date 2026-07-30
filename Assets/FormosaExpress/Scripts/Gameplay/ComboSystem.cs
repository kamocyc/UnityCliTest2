using System;
using UnityEngine;
using FormosaExpress.Core;
using FormosaExpress.Traffic;

namespace FormosaExpress.Gameplay
{
    /// <summary>
    /// The risk/reward engine. Squeezing past traffic, holding a drift and catching air all
    /// feed a decaying multiplier that pays out on delivery; a crash wipes it. This is what
    /// makes the shortest route and the best route different questions.
    /// </summary>
    public sealed class ComboSystem : MonoBehaviour
    {
        public event Action<string, Color, Vector3> Popup;
        public event Action<int> StepChanged;

        public int Step { get; private set; }
        public float Multiplier => Tuning.ComboMultipliers[Mathf.Clamp(Step, 0, Tuning.ComboMultipliers.Length - 1)];
        public float WindowRemaining { get; private set; }
        public float Window01 => Mathf.Clamp01(WindowRemaining / Tuning.ComboWindow);
        public int Score { get; private set; }
        public int BestStep { get; private set; }
        public int NearMissCount { get; private set; }
        public int Deliveries { get; private set; }
        public float TopSpeedKmh { get; private set; }

        const int CreditSlots = 48;
        readonly int[] _creditIds = new int[CreditSlots];
        readonly float[] _creditTimes = new float[CreditSlots];
        int _creditCursor;

        float _driftAccumulator;
        float _airAccumulator;
        float _nearMissCooldown;

        public void ResetRun()
        {
            Step = 0;
            WindowRemaining = 0f;
            Score = 0;
            BestStep = 0;
            NearMissCount = 0;
            Deliveries = 0;
            TopSpeedKmh = 0f;
            _driftAccumulator = 0f;
            _airAccumulator = 0f;
            Array.Clear(_creditIds, 0, CreditSlots);
            Array.Clear(_creditTimes, 0, CreditSlots);
            StepChanged?.Invoke(Step);
        }

        void Update()
        {
            var player = Services.Player;
            if (player == null) return;

            float dt = Time.deltaTime;
            TopSpeedKmh = Mathf.Max(TopSpeedKmh, player.SpeedKmh);

            if (WindowRemaining > 0f)
            {
                WindowRemaining -= dt;
                if (WindowRemaining <= 0f) Decay();
            }

            TickNearMisses(player, dt);
            TickStunts(player, dt);
        }

        // ------------------------------------------------------------------ near misses

        void TickNearMisses(Vehicle.ScooterController player, float dt)
        {
            if (_nearMissCooldown > 0f) _nearMissCooldown -= dt;

            float speed = Mathf.Abs(player.ForwardSpeed);
            if (speed < Tuning.NearMissMinSpeed) return;

            Vector3 position = player.transform.position;
            float radiusSqr = Tuning.NearMissRadius * Tuning.NearMissRadius;

            TrafficSystem traffic = Services.Traffic;
            if (traffic != null)
            {
                foreach (TrafficAgent agent in traffic.Agents)
                {
                    if (!agent.Active || agent.IsKnocked) continue;

                    Vector3 delta = agent.transform.position - position;
                    delta.y = 0f;

                    // Measure to the vehicle's flank, not its centre, so a bus counts sooner.
                    float extent = agent.Variant != null
                        ? Mathf.Max(agent.Variant.ColliderSize.x, agent.Variant.ColliderSize.z) * 0.42f
                        : 1f;
                    float reach = Tuning.NearMissRadius + extent;

                    if (delta.sqrMagnitude > reach * reach) continue;
                    if (!TryCredit(agent.GetInstanceID())) continue;

                    AwardNearMiss(agent.transform.position, speed);
                    return;   // one per frame keeps the popups readable
                }
            }

            // Squeezing past the rival counts too, and it is the most satisfying one to land.
            RivalCourier rival = Services.Rival;
            if (rival != null && rival.Active)
            {
                Vector3 delta = rival.Scooter.transform.position - position;
                delta.y = 0f;
                float reach = Tuning.NearMissRadius + 0.8f;

                if (delta.sqrMagnitude <= reach * reach && TryCredit(rival.GetInstanceID()))
                {
                    AwardNearMiss(rival.Scooter.transform.position, speed);
                    return;
                }
            }

            PedestrianSystem peds = Services.Pedestrians;
            if (peds == null) return;

            foreach (PedestrianAgent ped in peds.Agents)
            {
                if (!ped.Active || ped.IsTumbling) continue;

                Vector3 delta = ped.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSqr) continue;
                if (!TryCredit(ped.GetInstanceID())) continue;

                AwardNearMiss(ped.transform.position, speed);
                return;
            }
        }

        void AwardNearMiss(Vector3 worldPosition, float speed)
        {
            if (_nearMissCooldown > 0f) return;
            _nearMissCooldown = Tuning.NearMissCooldown;

            NearMissCount++;
            Services.Player?.AddAdrenaline(Tuning.AdrenalineNearMiss);
            Register(ComboEventKind.NearMiss, Tuning.ScoreNearMiss, worldPosition, Localization.T("NEAR MISS"), Art.HudCyan);
            Services.Audio?.PlayWhoosh(Mathf.Clamp01(speed / 22f));
        }

        bool TryCredit(int id)
        {
            float now = Time.time;
            for (int i = 0; i < CreditSlots; i++)
                if (_creditIds[i] == id && now - _creditTimes[i] < 2.0f) return false;

            _creditIds[_creditCursor] = id;
            _creditTimes[_creditCursor] = now;
            _creditCursor = (_creditCursor + 1) % CreditSlots;
            return true;
        }

        // ------------------------------------------------------------------ stunts

        void TickStunts(Vehicle.ScooterController player, float dt)
        {
            if (player.IsDrifting)
            {
                _driftAccumulator += dt;
                if (_driftAccumulator >= 0.9f)
                {
                    _driftAccumulator = 0f;
                    Register(ComboEventKind.Drift, Mathf.RoundToInt(Tuning.ScoreDriftPerSecond * 0.9f),
                        player.transform.position, Localization.T("DRIFT"), Art.HudGold);
                }
            }
            else
            {
                _driftAccumulator = 0f;
            }

            if (player.IsAirborne && player.AirTime > 0.3f)
            {
                _airAccumulator += dt;
                if (_airAccumulator >= 0.5f)
                {
                    _airAccumulator = 0f;
                    Register(ComboEventKind.Airtime, Mathf.RoundToInt(Tuning.ScoreAirPerSecond * 0.5f),
                        player.transform.position, Localization.T("AIRBORNE"), Art.HudGreen);
                }
            }
            else if (player.IsGrounded)
            {
                _airAccumulator = 0f;
            }
        }

        // ------------------------------------------------------------------ scoring

        /// <summary>Books a scoring event, extends the combo window and steps the multiplier.</summary>
        public void Register(ComboEventKind kind, int basePoints, Vector3 worldPosition, string label, Color colour)
        {
            if (kind == ComboEventKind.Crash)
            {
                Score = Mathf.Max(0, Score - Tuning.ScorePenaltyCrash);
                Break();
                Popup?.Invoke(string.Format(Localization.T("CRASH  -{0}"), Tuning.ScorePenaltyCrash), Art.HudRed, worldPosition);
                return;
            }

            int points = Mathf.RoundToInt(basePoints * Multiplier);
            Score += points;

            if (kind == ComboEventKind.Delivery) Deliveries++;

            // Every event refreshes the window; only "risky" ones raise the multiplier.
            WindowRemaining = Tuning.ComboWindow;
            if (kind != ComboEventKind.Delivery || Step < 2) Bump();

            string text = Step > 0 ? $"{label}  +{points}  x{Multiplier:0.##}" : $"{label}  +{points}";
            Popup?.Invoke(text, colour, worldPosition);
        }

        public void RegisterDelivery(Vector3 worldPosition, int timeBonusSeconds)
        {
            int points = Tuning.ScoreDeliveryBase + timeBonusSeconds * Tuning.ScorePerSecondSaved;
            Register(ComboEventKind.Delivery, points, worldPosition, Localization.T("DELIVERED!"), Art.HudGreen);
        }

        public void RegisterCrash(Vector3 worldPosition)
        {
            Register(ComboEventKind.Crash, 0, worldPosition, "CRASH", Art.HudRed);
        }

        void Bump()
        {
            if (Step >= Tuning.ComboMaxStep) return;
            Step++;
            BestStep = Mathf.Max(BestStep, Step);
            StepChanged?.Invoke(Step);
            Services.Audio?.PlayComboStep(Step);
        }

        void Decay()
        {
            if (Step <= 0) return;
            Step--;
            WindowRemaining = Step > 0 ? Tuning.ComboWindow * 0.6f : 0f;
            StepChanged?.Invoke(Step);
        }

        void Break()
        {
            if (Step == 0 && WindowRemaining <= 0f) return;
            Step = 0;
            WindowRemaining = 0f;
            StepChanged?.Invoke(Step);
        }
    }
}
