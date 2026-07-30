using System;
using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;
using FormosaExpress.City;

namespace FormosaExpress.Gameplay
{
    /// <summary>
    /// The delivery loop: offers jobs at food shops, tracks their clocks, handles pickups and
    /// drop-offs, and models how well the food survived the ride.
    /// </summary>
    public sealed class OrderManager : MonoBehaviour
    {
        public event Action<Order> Offered;
        public event Action<Order> PickedUp;
        public event Action<Order, int, int> Delivered;   // order, payout, timeBonusSeconds
        public event Action<Order> Expired;
        public event Action FocusChanged;

        public IReadOnlyList<Order> ActiveOrders => _orders;
        public Order Focus { get; private set; }
        public int CarriedCount { get; private set; }
        public int Capacity { get; private set; } = 2;
        public bool Running { get; private set; }

        /// <summary>Condition of the worst thing in the bag; 100 when empty.</summary>
        public float WorstCondition { get; private set; } = Tuning.CargoMax;

        readonly List<Order> _orders = new List<Order>(8);
        readonly List<DeliveryBeacon> _beacons = new List<DeliveryBeacon>(10);
        readonly Dictionary<Order, DeliveryBeacon> _orderBeacons = new Dictionary<Order, DeliveryBeacon>();

        CityModel _city;
        MaterialLibrary _mats;
        Rng _rng;
        int _nextId = 1;
        int _level = 1;
        float _offerCooldown;
        float _refocusTimer;
        float _pickupHintTimer;

        public void Initialise(CityModel city, MaterialLibrary mats, int seed)
        {
            _city = city;
            _mats = mats;
            _rng = new Rng(seed * 8191 + 5);

            for (int i = 0; i < 10; i++)
                _beacons.Add(DeliveryBeacon.Create(transform, mats));
        }

        public void BeginShift(int level, int capacity)
        {
            _level = Mathf.Max(1, level);
            Capacity = Mathf.Max(1, capacity);
            _orders.Clear();
            _orderBeacons.Clear();
            foreach (DeliveryBeacon beacon in _beacons) beacon.SetVisible(false);

            CarriedCount = 0;
            WorstCondition = Tuning.CargoMax;
            Focus = null;
            _offerCooldown = 0.4f;
            Running = true;

            TopUpOffers(immediate: true);
        }

        public void EndShift()
        {
            Running = false;
            _orders.Clear();
            _orderBeacons.Clear();
            foreach (DeliveryBeacon beacon in _beacons) beacon.SetVisible(false);
            Focus = null;
            CarriedCount = 0;
        }

        // ------------------------------------------------------------------ tick

        void Update()
        {
            if (!Running || _city == null) return;

            float dt = Time.deltaTime;
            Vector3 playerPos = Services.PlayerPosition;
            float playerSpeed = Services.Player != null ? Mathf.Abs(Services.Player.ForwardSpeed) : 0f;

            TickClocks(dt);
            TickInteractions(playerPos, playerSpeed);
            TickOffers(dt);
            TickFocus(dt);
            TickBeacons();
            RecomputeCondition();

            if (_pickupHintTimer > 0f) _pickupHintTimer -= dt;
        }

        void TickClocks(float dt)
        {
            bool bagFull = CarriedCount >= Capacity;

            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                Order order = _orders[i];

                // An offer the rider physically cannot accept must not run down its clock -
                // losing money to an order you had no way of collecting just feels broken.
                if (!order.PickedUp && bagFull) continue;

                order.TimeRemaining -= dt;
                if (order.TimeRemaining > 0f) continue;

                order.Expired = true;
                if (order.PickedUp) CarriedCount = Mathf.Max(0, CarriedCount - 1);
                ReleaseBeacon(order);
                _orders.RemoveAt(i);
                if (Focus == order) { Focus = null; FocusChanged?.Invoke(); }
                Expired?.Invoke(order);
            }
        }

        void TickInteractions(Vector3 playerPos, float playerSpeed)
        {
            bool slowEnough = playerSpeed <= Tuning.PickupMaxSpeed;

            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                Order order = _orders[i];

                // In a race, a parcel the rival has already claimed is none of the player's
                // business - they cannot collect it and must not be prompted to.
                if (order.CarriedBy == Courier.Rival) continue;

                Vector3 target = order.ActiveTarget;
                float radius = order.PickedUp ? Tuning.DropoffRadius : Tuning.PickupRadius;

                float dx = playerPos.x - target.x;
                float dz = playerPos.z - target.z;
                if (dx * dx + dz * dz > radius * radius) continue;

                if (!slowEnough)
                {
                    // Tell the player why nothing happened, but only occasionally.
                    if (_pickupHintTimer <= 0f)
                    {
                        _pickupHintTimer = 2.2f;
                        Services.Hud?.ShowToast("SLOW DOWN TO " + (order.PickedUp ? "DELIVER" : "COLLECT"), Art.HudGold);
                    }

                    continue;
                }

                if (order.PickedUp) CompleteDelivery(order, i);
                else CollectOrder(order);
            }
        }

        void CollectOrder(Order order)
        {
            if (CarriedCount >= Capacity)
            {
                if (_pickupHintTimer <= 0f)
                {
                    _pickupHintTimer = 2.5f;
                    Services.Hud?.ShowToast("BAG FULL - DELIVER FIRST", Art.HudRed);
                }

                return;
            }

            order.PickedUp = true;
            order.CarriedBy = Courier.Player;
            order.Condition = Tuning.CargoMax;
            CarriedCount++;

            // Re-budget: the clock now covers only the run to the customer, plus a bonus for
            // having got here quickly.
            float remainingDistance = _city.EstimateRouteDistance(order.PickupPosition, order.DropPosition);
            float budget = Mathf.Max(Tuning.OrderTimeFloor, remainingDistance * TimePerMetre());
            order.TimeRemaining = Mathf.Max(order.TimeRemaining * 0.35f + budget, budget * 0.9f);
            order.TimeLimit = order.TimeRemaining;
            order.RouteDistance = remainingDistance;

            ReleaseBeacon(order);
            PickedUp?.Invoke(order);

            // Re-target immediately rather than waiting for the next focus tick, so the arrow
            // swings to the customer on the same frame the parcel is collected.
            Services.Routes?.SetTarget(order.DropPosition);
            RefocusNow();
        }

        void CompleteDelivery(Order order, int index)
        {
            order.Delivered = true;
            CarriedCount = Mathf.Max(0, CarriedCount - 1);

            int timeBonusSeconds = Mathf.Max(0, Mathf.FloorToInt(order.TimeRemaining));
            float timeFraction = order.TimeLimit > 0f ? Mathf.Clamp01(order.TimeRemaining / order.TimeLimit) : 0f;

            float comboMultiplier = Services.Combo != null ? Services.Combo.Multiplier : 1f;
            float payoutFloat = order.BaseFare
                                * order.ConditionPayoutMultiplier
                                * (1f + timeFraction * 0.45f)
                                * Mathf.Lerp(1f, comboMultiplier, 0.35f);

            int payout = Mathf.Max(1, Mathf.RoundToInt(payoutFloat));

            ReleaseBeacon(order);
            _orders.RemoveAt(index);
            if (Focus == order) Focus = null;

            Delivered?.Invoke(order, payout, timeBonusSeconds);
            RefocusNow();
        }

        // ------------------------------------------------------------------ rival hooks

        /// <summary>
        /// The rival reaching a shop first. Returns false if the player already has it, which is
        /// what makes a photo-finish at the counter resolve cleanly.
        /// </summary>
        public bool ClaimForRival(Order order)
        {
            if (order == null || order.Delivered || order.Expired) return false;
            if (order.CarriedBy != Courier.None) return false;

            order.PickedUp = true;
            order.CarriedBy = Courier.Rival;
            order.Condition = Tuning.CargoMax;

            if (Focus == order)
            {
                Focus = null;
                FocusChanged?.Invoke();
                RefocusNow();
            }

            AssignBeaconForRival(order);
            return true;
        }

        /// <summary>Books a rival delivery and returns what it earned them.</summary>
        public int CompleteRivalDelivery(Order order)
        {
            if (order == null || order.Delivered) return 0;

            order.Delivered = true;
            order.CarriedBy = Courier.Rival;

            float timeFraction = order.TimeLimit > 0f ? Mathf.Clamp01(order.TimeRemaining / order.TimeLimit) : 0f;
            int payout = Mathf.Max(1, Mathf.RoundToInt(order.BaseFare * (1f + timeFraction * 0.45f)));

            ReleaseBeacon(order);
            _orders.Remove(order);
            if (Focus == order) Focus = null;

            RefocusNow();
            return payout;
        }

        void AssignBeaconForRival(Order order)
        {
            // Show where the rival is taking it, in their colour, so the player can watch the
            // race unfold instead of just seeing their own job vanish.
            if (_orderBeacons.TryGetValue(order, out DeliveryBeacon existing))
            {
                existing.Configure(order.DropPosition, false, Art.RivalTint);
                return;
            }

            foreach (DeliveryBeacon beacon in _beacons)
            {
                if (_orderBeacons.ContainsValue(beacon)) continue;
                _orderBeacons[order] = beacon;
                beacon.Configure(order.DropPosition, false, Art.RivalTint);
                return;
            }
        }

        void TickOffers(float dt)
        {
            _offerCooldown -= dt;
            if (_offerCooldown > 0f) return;

            TopUpOffers(immediate: false);
        }

        int TargetOfferCount()
        {
            // Never advertise more jobs than the rider could conceivably juggle.
            int wanted = Mathf.Min(Tuning.MaxSimultaneousOffers, Capacity + 1);
            return Mathf.Max(1, wanted);
        }

        void TopUpOffers(bool immediate)
        {
            int offered = 0;
            foreach (Order order in _orders) if (!order.PickedUp) offered++;

            int slots = TargetOfferCount() - offered;
            if (_orders.Count >= Capacity + Tuning.MaxSimultaneousOffers) slots = 0;

            for (int i = 0; i < slots; i++)
            {
                Order order = CreateOrder();
                if (order == null) break;

                _orders.Add(order);
                AssignBeacon(order);
                Offered?.Invoke(order);
                if (!immediate) break;   // stagger, so jobs trickle in
            }

            _offerCooldown = immediate ? 1.2f : _rng.Range(2.5f, 6.0f);
            if (Focus == null) RefocusNow();
        }

        float TimePerMetre()
        {
            // Tighter budgets as the levels climb: the same route, less slack.
            float tightness = Mathf.InverseLerp(1f, 9f, _level);
            return Tuning.OrderTimePerMetre * Mathf.Lerp(1f, 0.70f, tightness);
        }

        Order CreateOrder()
        {
            if (_city.Sites.Count == 0) return null;

            Vector3 playerPos = Services.PlayerPosition;
            Site shop = _city.PickSite(ref _rng, SiteKind.FoodShop, playerPos, 45f);
            if (shop == null) return null;

            SiteKind dropKind = _rng.Chance(0.7f) ? SiteKind.Residence : SiteKind.Office;
            Site customer = _city.PickSite(ref _rng, dropKind, shop.Position, 110f);
            if (customer == null || customer.Index == shop.Index) return null;

            float toShop = _city.EstimateRouteDistance(playerPos, shop.Position);
            float shopToCustomer = _city.EstimateRouteDistance(shop.Position, customer.Position);
            float total = toShop + shopToCustomer;

            var order = new Order
            {
                Id = _nextId++,
                ShopName = shop.Name,
                CustomerName = customer.Name,
                DishName = CityNames.Dishes[_rng.Range(0, CityNames.Dishes.Length)],
                ShopSiteIndex = shop.Index,
                CustomerSiteIndex = customer.Index,
                PickupPosition = shop.Position,
                DropPosition = customer.Position,
                RouteDistance = shopToCustomer,
                Tint = shop.Tint
            };

            order.TimeLimit = Mathf.Max(Tuning.OrderTimeFloor, total * TimePerMetre());
            order.TimeRemaining = order.TimeLimit;
            order.BaseFare = Tuning.OrderBaseFare + Mathf.RoundToInt(shopToCustomer * 0.42f);

            return order;
        }

        // ------------------------------------------------------------------ focus

        void TickFocus(float dt)
        {
            _refocusTimer -= dt;

            InputRouter input = Services.Input;
            if (input != null && input.CycleTargetPressed && _orders.Count > 1)
            {
                CycleFocus();
                return;
            }

            if (Focus == null || _refocusTimer <= 0f) RefocusNow();

            // An order's target moves from the shop to the customer the moment it is collected,
            // and the focused order object does not change. Keep the route in step, or the
            // navigation carries on pointing at the shop you just left.
            if (Focus != null && Services.Routes != null)
            {
                Vector3 wanted = Focus.ActiveTarget;
                if ((Services.Routes.Target - wanted).sqrMagnitude > 0.25f)
                    Services.Routes.SetTarget(wanted);
            }
        }

        void RefocusNow()
        {
            _refocusTimer = 2.0f;
            if (_orders.Count == 0)
            {
                if (Focus != null) { Focus = null; FocusChanged?.Invoke(); }
                Services.Routes?.ClearTarget();
                return;
            }

            Vector3 playerPos = Services.PlayerPosition;
            bool bagFull = CarriedCount >= Capacity;
            Order best = null;
            float bestScore = float.MaxValue;

            foreach (Order order in _orders)
            {
                // Never navigate to something the rival is already carrying.
                if (order.CarriedBy == Courier.Rival) continue;

                // With a full bag the only useful target is a customer, so never point the
                // navigation at a shop the rider cannot collect from.
                if (bagFull && !order.PickedUp) continue;

                // Prefer close targets, and lean towards whatever is closest to expiring.
                float distance = Vector3.Distance(playerPos, order.ActiveTarget);
                float score = distance * Mathf.Lerp(1f, 0.45f, order.Urgency);

                // A parcel already in the bag is worth more than a job still on the board.
                if (order.PickedUp) score *= 0.72f;

                if (score >= bestScore) continue;
                bestScore = score;
                best = order;
            }

            SetFocus(best);
        }

        public void CycleFocus()
        {
            if (_orders.Count == 0) return;

            bool bagFull = CarriedCount >= Capacity;
            int start = Focus != null ? _orders.IndexOf(Focus) : -1;

            for (int step = 1; step <= _orders.Count; step++)
            {
                Order candidate = _orders[(start + step + _orders.Count) % _orders.Count];
                if (candidate.CarriedBy == Courier.Rival) continue;
                if (bagFull && !candidate.PickedUp) continue;

                SetFocus(candidate);
                _refocusTimer = 6f;   // respect the player's choice for a while
                return;
            }
        }

        void SetFocus(Order order)
        {
            if (Focus == order) return;

            Focus = order;
            if (order != null) Services.Routes?.SetTarget(order.ActiveTarget);
            else Services.Routes?.ClearTarget();

            FocusChanged?.Invoke();
        }

        // ------------------------------------------------------------------ beacons

        void AssignBeacon(Order order)
        {
            foreach (DeliveryBeacon beacon in _beacons)
            {
                if (_orderBeacons.ContainsValue(beacon)) continue;
                _orderBeacons[order] = beacon;
                beacon.Configure(order.ActiveTarget, !order.PickedUp,
                    order.PickedUp ? Art.BeaconGreen : Art.BeaconAmber);
                return;
            }
        }

        void ReleaseBeacon(Order order)
        {
            if (_orderBeacons.TryGetValue(order, out DeliveryBeacon beacon))
            {
                beacon.SetVisible(false);
                _orderBeacons.Remove(order);
            }

            // A collected order immediately needs a beacon at the customer instead.
            if (order.PickedUp && !order.Delivered && !order.Expired) AssignBeacon(order);
        }

        void TickBeacons()
        {
            foreach (Order order in _orders)
            {
                if (!_orderBeacons.TryGetValue(order, out DeliveryBeacon beacon))
                {
                    AssignBeacon(order);
                    continue;
                }

                beacon.SetUrgency(order.Urgency);
            }
        }

        // ------------------------------------------------------------------ cargo

        /// <summary>Shakes up everything in the bag. Amount is before insulation is applied.</summary>
        public void ApplyCargoDamage(float amount)
        {
            if (amount <= 0f || CarriedCount == 0) return;

            float resist = Services.Player != null ? Services.Player.Stats.CargoResist : 1f;
            float scaled = amount * Mathf.Clamp01(resist);

            foreach (Order order in _orders)
            {
                if (order.CarriedBy != Courier.Player) continue;
                order.Condition = Mathf.Max(0f, order.Condition - scaled);
            }

            RecomputeCondition();
        }

        /// <summary>Gentle riding lets the food settle back down a little.</summary>
        public void SettleCargo(float dt)
        {
            if (CarriedCount == 0) return;

            foreach (Order order in _orders)
            {
                if (order.CarriedBy != Courier.Player) continue;
                order.Condition = Mathf.Min(Tuning.CargoMax, order.Condition + Tuning.CargoRecoverPerSecond * dt);
            }
        }

        void RecomputeCondition()
        {
            float worst = Tuning.CargoMax;
            foreach (Order order in _orders)
                if (order.CarriedBy == Courier.Player) worst = Mathf.Min(worst, order.Condition);

            WorstCondition = worst;
        }
    }
}
