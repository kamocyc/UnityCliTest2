using UnityEngine;
using FormosaExpress.Core;
using FormosaExpress.Vehicle;

namespace FormosaExpress.Gameplay
{
    /// <summary>
    /// The competing courier. A full second scooter - same physics, same order board, same
    /// pickup rules - driven by <see cref="RivalBrain"/>. It picks whichever job it can reach
    /// first, so the two of you end up genuinely racing for the same parcels.
    /// </summary>
    public sealed class RivalCourier : MonoBehaviour
    {
        public ScooterController Scooter { get; private set; }
        public string RivalName { get; private set; } = "RIVAL";
        public int Delivered { get; private set; }
        public int Earnings { get; private set; }
        public Order Carrying { get; private set; }
        public Order Chasing { get; private set; }
        public bool Active { get; private set; }

        readonly RouteTracker _route = new RouteTracker();
        RivalBrain _brain;
        int _level = 1;
        float _retargetTimer;
        float _hintTimer;

        public static RivalCourier Create(Transform parent, MaterialLibrary mats)
        {
            var root = new GameObject("Rival");
            root.transform.SetParent(parent, false);

            var rival = root.AddComponent<RivalCourier>();
            rival.Build(mats);
            rival.SetActive(false);
            return rival;
        }

        void Build(MaterialLibrary mats)
        {
            var body = new GameObject("RivalScooter");
            body.transform.SetParent(transform, false);
            body.layer = LayerMask.NameToLayer(Tuning.LayerPlayer);

            body.AddComponent<Rigidbody>();
            Scooter = body.AddComponent<ScooterController>();
            ScooterVisual.Create(Scooter, mats, ScooterLivery.Rival);

            // A nameplate light so you can pick the rival out of the traffic at night.
            var beaconGo = new GameObject("RivalBeacon");
            beaconGo.transform.SetParent(body.transform, false);
            beaconGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            var light = beaconGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Art.RivalTint;

            // Kept tight and dim: it marks the rival without throwing a spotlight pool onto the
            // road that reads as a rendering artefact.
            light.range = 5.5f;
            light.intensity = 1.3f;
            light.shadows = LightShadows.None;
        }

        public void SetActive(bool value)
        {
            Active = value;
            if (gameObject.activeSelf != value) gameObject.SetActive(value);
            if (Scooter != null) Scooter.ControlEnabled = value;
        }

        public void BeginRace(int level, Vector3 startPosition, float startYaw, ref Rng rng)
        {
            _level = Mathf.Max(1, level);
            RivalName = Tuning.RivalNames[rng.Range(0, Tuning.RivalNames.Length)];
            Delivered = 0;
            Earnings = 0;
            Carrying = null;
            Chasing = null;
            _retargetTimer = 0f;

            // Same stock scooter as the player's, then a small skill-based handling edge either
            // way. Deliberately modest: the rival should beat you on decisions, not on hardware.
            var stats = VehicleStats.From(new SaveData());
            stats.TopSpeed *= Tuning.RivalSpeedFactor(_level);
            stats.Acceleration *= Tuning.RivalSpeedFactor(_level);
            stats.Grip *= Tuning.RivalGripFactor(_level);
            stats.BagCapacity = 1;

            SetActive(true);
            Scooter.ApplyStats(stats);
            Scooter.ResetRun();

            // Start alongside the player, a lane over.
            Vector3 offset = Quaternion.Euler(0f, startYaw, 0f) * Vector3.right * -3.2f;
            Scooter.Teleport(startPosition + offset, startYaw);

            _brain = new RivalBrain(_route, _level) { HoldTimer = Tuning.RaceRivalHandicap };
            Scooter.InputSource = _brain;
            _route.ClearTarget();
        }

        public void EndRace()
        {
            _route.ClearTarget();
            Carrying = null;
            Chasing = null;
            SetActive(false);
        }

        void Update()
        {
            if (!Active || Services.Orders == null || Services.Director == null) return;
            if (Services.Director.Phase != GamePhase.Riding) return;

            float dt = Time.deltaTime;
            Vector3 position = Scooter.transform.position;

            ChooseTarget(dt, position);

            Order target = Carrying ?? Chasing;
            if (target != null)
            {
                Vector3 wanted = target.ActiveTarget;
                if (!_route.HasTarget || (_route.Target - wanted).sqrMagnitude > 0.25f) _route.SetTarget(wanted);

                _route.Tick(position, dt);
                _brain.TargetDistance = Vector3.Distance(position, wanted);
                TryInteract(target, position);
            }
            else
            {
                _route.ClearTarget();
                _brain.TargetDistance = 999f;
            }

            if (_hintTimer > 0f) _hintTimer -= dt;
        }

        void ChooseTarget(float dt, Vector3 position)
        {
            // Carrying something? Finish it. Nothing else matters.
            if (Carrying != null)
            {
                if (Carrying.Delivered || Carrying.Expired || Carrying.CarriedBy != Courier.Rival) Carrying = null;
                else return;
            }

            _retargetTimer -= dt;
            if (Chasing != null)
            {
                bool stillAvailable = !Chasing.Delivered && !Chasing.Expired && Chasing.CarriedBy == Courier.None;
                if (!stillAvailable) Chasing = null;
                else if (_retargetTimer > 0f) return;
            }

            _retargetTimer = 1.5f;

            // Go for the nearest unclaimed job. Ties on distance mean the player and the rival
            // will regularly want the same parcel, which is the whole point of the mode.
            Order best = null;
            float bestScore = float.MaxValue;

            foreach (Order order in Services.Orders.ActiveOrders)
            {
                if (order.Delivered || order.Expired) continue;
                if (order.CarriedBy != Courier.None) continue;

                float score = Vector3.Distance(position, order.PickupPosition);

                // Prefer jobs that will not expire before it can get there.
                if (order.TimeRemaining < score / 14f) score *= 2.4f;

                if (score >= bestScore) continue;
                bestScore = score;
                best = order;
            }

            Chasing = best;
        }

        void TryInteract(Order order, Vector3 position)
        {
            bool carrying = order.CarriedBy == Courier.Rival && order.PickedUp;
            Vector3 zone = order.ActiveTarget;
            float radius = carrying ? Tuning.DropoffRadius : Tuning.PickupRadius;

            float dx = position.x - zone.x;
            float dz = position.z - zone.z;
            if (dx * dx + dz * dz > radius * radius) return;
            if (Mathf.Abs(Scooter.ForwardSpeed) > Tuning.PickupMaxSpeed) return;

            if (carrying)
            {
                int payout = Services.Orders.CompleteRivalDelivery(order);
                Earnings += payout;
                Delivered++;
                Carrying = null;
                Chasing = null;

                // Give the player a heads-up: losing a race quietly is no fun.
                if (_hintTimer <= 0f)
                {
                    _hintTimer = 2f;
                    Services.Hud?.ShowToast(string.Format(Localization.T("{0} DELIVERED  ({1})"), Localization.T(RivalName), Delivered), Art.RivalTint, 2.1f);
                }

                _brain.HoldTimer = Tuning.RivalHandlingDelay(_level);
                return;
            }

            if (!Services.Orders.ClaimForRival(order)) return;

            Carrying = order;
            Chasing = null;
            _brain.HoldTimer = Tuning.RivalHandlingDelay(_level);

            if (_hintTimer <= 0f)
            {
                _hintTimer = 2f;
                Services.Hud?.ShowToast(string.Format(Localization.T("{0} TOOK {1}"), Localization.T(RivalName), Localization.T(order.ShopName).ToUpperInvariant()), Art.RivalTint, 1.9f);
            }
        }
    }
}
