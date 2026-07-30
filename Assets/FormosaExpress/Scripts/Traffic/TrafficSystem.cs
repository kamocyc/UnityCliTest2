using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;
using FormosaExpress.City;

namespace FormosaExpress.Traffic
{
    /// <summary>
    /// Spawns, recycles and steers the city's traffic. Agents live in a pool and are kept in a
    /// ring around the player: far ones are quietly teleported ahead of the rider so the street
    /// always looks busy without simulating the whole city.
    /// </summary>
    public sealed class TrafficSystem : MonoBehaviour
    {
        const float RecycleDistance = 260f;
        const float SpawnMinDistance = 55f;
        const float SpawnMaxDistance = 185f;
        const float FollowLookAhead = 22f;
        const float PlayerLookAhead = 16f;
        const float IntersectionEntryZone = 7.5f;

        public IReadOnlyList<TrafficAgent> Agents => _agents;

        readonly List<TrafficAgent> _agents = new List<TrafficAgent>(64);
        List<VehicleMeshVariant> _variants;
        float[] _variantCumulativeWeight;
        float _variantTotalWeight;
        CityModel _city;
        MaterialLibrary _mats;
        Rng _rng;

        int[] _nodeOccupancy;
        int[] _nodeClaim;
        int _targetCount;
        float _spawnCooldown;

        public void Initialise(CityModel city, MaterialLibrary mats, int seed)
        {
            _city = city;
            _mats = mats;
            _rng = new Rng(seed * 5077 + 91);
            _variants = VehicleFactory.BuildLibrary(mats.Palette);
            _nodeOccupancy = new int[city.Nodes.Count];
            _nodeClaim = new int[city.Nodes.Count];

            _variantCumulativeWeight = new float[_variants.Count];
            _variantTotalWeight = 0f;
            for (int i = 0; i < _variants.Count; i++)
            {
                _variantTotalWeight += Mathf.Max(0.0001f, _variants[i].SpawnWeight);
                _variantCumulativeWeight[i] = _variantTotalWeight;
            }
        }

        /// <summary>Weighted pick across the whole library, favouring high-<see cref="VehicleMeshVariant.SpawnWeight"/> kinds like scooters.</summary>
        VehicleMeshVariant RandomVariant()
        {
            float target = _rng.Range(0f, _variantTotalWeight);
            for (int i = 0; i < _variantCumulativeWeight.Length; i++)
                if (target <= _variantCumulativeWeight[i]) return _variants[i];

            return _variants[_variants.Count - 1];
        }

        public void SetDensity(int count)
        {
            _targetCount = Mathf.Max(0, count);
            while (_agents.Count < _targetCount)
                _agents.Add(TrafficAgent.Create(transform, _mats));

            for (int i = 0; i < _agents.Count; i++)
                if (i >= _targetCount && _agents[i].Active)
                    _agents[i].SetActive(false);
        }

        public void Clear()
        {
            foreach (TrafficAgent agent in _agents) agent.SetActive(false);
        }

        /// <summary>Fills the streets around <paramref name="around"/> before a shift starts.</summary>
        public void Populate(Vector3 around)
        {
            for (int i = 0; i < _targetCount && i < _agents.Count; i++)
            {
                if (!TrySpawn(_agents[i], around, 20f, SpawnMaxDistance))
                    _agents[i].SetActive(false);
            }
        }

        bool TrySpawn(TrafficAgent agent, Vector3 around, float minDistance, float maxDistance)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                TrafficPath path = _city.RandomStraightPath(ref _rng);
                float distance = _rng.Range(2f, Mathf.Max(3f, path.Length - 2f));
                Vector3 position = path.Sample(distance, out _);

                float toPlayer = Vector3.Distance(position, around);
                if (toPlayer < minDistance || toPlayer > maxDistance) continue;
                if (IsOccupied(position, 7f, agent)) continue;

                VehicleMeshVariant variant = PickVariant(path);
                agent.Assign(variant, path, distance, ref _rng);
                return true;
            }

            return false;
        }

        VehicleMeshVariant PickVariant(TrafficPath path)
        {
            // Buses and trucks only on avenues, so the narrow streets stay passable.
            bool wideRoad = path.EdgeIndex >= 0 && _city.Edges[path.EdgeIndex].IsAvenue;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                VehicleMeshVariant candidate = RandomVariant();
                bool big = candidate.Kind == VehicleKind.Bus || candidate.Kind == VehicleKind.Truck;
                if (big && !wideRoad) continue;
                if (big && !_rng.Chance(0.35f)) continue;
                return candidate;
            }

            return _variants[0];
        }

        bool IsOccupied(Vector3 position, float radius, TrafficAgent ignore)
        {
            float sqr = radius * radius;
            foreach (TrafficAgent other in _agents)
            {
                if (other == ignore || !other.Active) continue;
                if ((other.transform.position - position).sqrMagnitude < sqr) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ tick

        void Update()
        {
            if (_city == null || _targetCount == 0) return;

            float dt = Time.deltaTime;
            Vector3 playerPos = Services.PlayerPosition;
            Vector3 playerVel = Services.Player != null ? Services.Player.Velocity : Vector3.zero;

            BuildIntersectionClaims();

            for (int i = 0; i < _agents.Count; i++)
            {
                TrafficAgent agent = _agents[i];
                if (i >= _targetCount) continue;

                if (!agent.Active)
                {
                    // Stagger respawns so a wave of traffic never pops in at once.
                    _spawnCooldown -= dt;
                    if (_spawnCooldown <= 0f)
                    {
                        _spawnCooldown = 0.08f;
                        TrySpawn(agent, playerPos, SpawnMinDistance, SpawnMaxDistance);
                    }

                    continue;
                }

                if (agent.IsKnocked)
                {
                    agent.Advance(_city, dt, 0f, false);
                    continue;
                }

                float distanceToPlayer = Vector3.Distance(agent.transform.position, playerPos);
                if (distanceToPlayer > RecycleDistance)
                {
                    agent.SetActive(false);
                    continue;
                }

                float limit = agent.DesiredSpeed;
                bool mustStop = false;

                // Car-following.
                float gap = NearestObstacleAhead(agent, playerPos, playerVel);
                if (gap < FollowLookAhead)
                {
                    float safe = Mathf.Max(2.6f, agent.Variant.ColliderSize.z * 0.75f + 1.8f);
                    if (gap <= safe) mustStop = true;
                    else limit = Mathf.Min(limit, agent.DesiredSpeed * Mathf.InverseLerp(safe, FollowLookAhead, gap));
                }

                // Intersection yielding.
                TrafficPath path = _city.Paths[agent.PathIndex];
                if (!path.IsConnector && path.ToNode >= 0)
                {
                    float toEnd = path.Length - agent.Distance;
                    if (toEnd < IntersectionEntryZone)
                    {
                        bool busy = _nodeOccupancy[path.ToNode] > 0;
                        bool mine = _nodeClaim[path.ToNode] == i;
                        if ((busy || !mine) && !agent.IsStuck) mustStop = true;
                    }
                }

                // Slow through turns so nothing corners like it is on rails.
                if (path.IsConnector) limit = Mathf.Min(limit, agent.DesiredSpeed * 0.55f);

                if (!agent.Advance(_city, dt, limit, mustStop))
                    agent.SetActive(false);
            }
        }

        void BuildIntersectionClaims()
        {
            for (int i = 0; i < _nodeOccupancy.Length; i++)
            {
                _nodeOccupancy[i] = 0;
                _nodeClaim[i] = -1;
            }

            // Vehicles already inside a junction occupy it.
            for (int i = 0; i < _agents.Count && i < _targetCount; i++)
            {
                TrafficAgent agent = _agents[i];
                if (!agent.Active || agent.IsKnocked) continue;

                TrafficPath path = _city.Paths[agent.PathIndex];
                if (path.IsConnector && path.FromNode >= 0 && path.FromNode < _nodeOccupancy.Length)
                    _nodeOccupancy[path.FromNode]++;
            }

            // Of everyone waiting to enter a junction, the lowest index goes first. Deterministic,
            // cheap, and it cannot deadlock because IsStuck overrides it after a few seconds.
            for (int i = 0; i < _agents.Count && i < _targetCount; i++)
            {
                TrafficAgent agent = _agents[i];
                if (!agent.Active || agent.IsKnocked) continue;

                TrafficPath path = _city.Paths[agent.PathIndex];
                if (path.IsConnector || path.ToNode < 0 || path.ToNode >= _nodeClaim.Length) continue;
                if (path.Length - agent.Distance > IntersectionEntryZone) continue;

                if (_nodeClaim[path.ToNode] < 0) _nodeClaim[path.ToNode] = i;
            }
        }

        /// <summary>Distance to the nearest thing in this agent's path, including the player.</summary>
        float NearestObstacleAhead(TrafficAgent agent, Vector3 playerPos, Vector3 playerVel)
        {
            Vector3 origin = agent.transform.position;
            Vector3 forward = agent.Forward;
            float best = float.MaxValue;

            foreach (TrafficAgent other in _agents)
            {
                if (other == agent || !other.Active) continue;

                Vector3 delta = other.transform.position - origin;
                float along = Vector3.Dot(delta, forward);
                if (along <= 0.2f || along > FollowLookAhead) continue;

                float lateral = (delta - forward * along).magnitude;
                if (lateral > 2.4f) continue;

                float gap = along - other.Variant.ColliderSize.z * 0.5f - agent.Variant.ColliderSize.z * 0.5f;
                best = Mathf.Min(best, Mathf.Max(0f, gap));
            }

            // The player counts too: traffic brakes (and honks) for a scooter cutting in.
            Vector3 toPlayer = playerPos - origin;
            float playerAlong = Vector3.Dot(toPlayer, forward);
            if (playerAlong > 0.2f && playerAlong < PlayerLookAhead)
            {
                float lateral = (toPlayer - forward * playerAlong).magnitude;
                if (lateral < 2.2f)
                {
                    best = Mathf.Min(best, Mathf.Max(0f, playerAlong - 1.6f));
                    if (best < 8f && agent.TryHonk()) Services.Audio?.PlayTrafficHorn(origin);
                }
            }

            return best;
        }

        // ------------------------------------------------------------------ interaction

        /// <summary>Finds the agent behind a collider so a hit can be turned into a knock.</summary>
        public TrafficAgent FindAgent(Collider collider)
        {
            if (collider == null) return null;
            return collider.GetComponentInParent<TrafficAgent>();
        }

        public void KnockAgent(TrafficAgent agent, Vector3 fromPosition, float severity)
        {
            if (agent == null) return;

            Vector3 away = agent.transform.position - fromPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = agent.Forward;
            away.Normalize();

            agent.Knock(away * Mathf.Lerp(4f, 13f, severity), severity);
        }
    }
}
