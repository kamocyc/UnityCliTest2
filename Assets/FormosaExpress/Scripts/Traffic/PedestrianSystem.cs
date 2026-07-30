using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;
using FormosaExpress.City;

namespace FormosaExpress.Traffic
{
    /// <summary>One pedestrian: walks a block's pavement loop, crosses roads, dives out of the way.</summary>
    public sealed class PedestrianAgent : MonoBehaviour
    {
        public bool Active { get; private set; }
        public int BlockIndex;
        public int Corner;
        public float EdgeT;
        public float Speed;
        public bool Crossing;
        public Vector3 CrossFrom;
        public Vector3 CrossTo;
        public float CrossT;
        public bool IsTumbling => _tumbleTimer > 0f;

        Transform _body;
        float _bob;
        float _bobPhase;
        float _tumbleTimer;
        Vector3 _tumbleVelocity;
        Vector3 _tumbleSpin;
        float _dodgeTimer;
        Vector3 _dodgeDirection;

        public static PedestrianAgent Create(Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject("Pedestrian");
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer(Tuning.LayerPedestrian);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.75f;
            capsule.radius = 0.28f;
            capsule.center = new Vector3(0f, 0.88f, 0f);

            var agent = go.AddComponent<PedestrianAgent>();
            agent._body = new GameObject("Body").transform;
            agent._body.SetParent(go.transform, false);

            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(agent._body, false);
            meshGo.layer = go.layer;
            meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            agent.SetActive(false);
            return agent;
        }

        public void SetMesh(Mesh mesh)
        {
            var filter = _body.GetChild(0).GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = mesh;
        }

        public void SetActive(bool value)
        {
            Active = value;
            if (gameObject.activeSelf != value) gameObject.SetActive(value);
        }

        public void Spawn(Vector3 position, float yaw, float speed, int blockIndex, int corner, float edgeT)
        {
            BlockIndex = blockIndex;
            Corner = corner;
            EdgeT = edgeT;
            Speed = speed;
            Crossing = false;
            _tumbleTimer = 0f;
            _dodgeTimer = 0f;
            _bobPhase = Random.value * 10f;
            _body.localRotation = Quaternion.identity;
            _body.localPosition = Vector3.zero;

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            SetActive(true);
        }

        public void Tumble(Vector3 impulse)
        {
            _tumbleTimer = Random.Range(1.1f, 1.9f);
            _tumbleVelocity = impulse + Vector3.up * 3.4f;
            _tumbleSpin = new Vector3(Random.Range(-400f, 400f), Random.Range(-300f, 300f), Random.Range(-400f, 400f));
        }

        /// <summary>Sidesteps an oncoming scooter. Comedy, not carnage.</summary>
        public void Dodge(Vector3 away)
        {
            if (_tumbleTimer > 0f) return;
            _dodgeTimer = 0.65f;
            _dodgeDirection = new Vector3(away.x, 0f, away.z).normalized;
        }

        public void Tick(CityModel city, float dt)
        {
            if (_tumbleTimer > 0f)
            {
                _tumbleTimer -= dt;
                _tumbleVelocity += Vector3.down * 20f * dt;
                transform.position += _tumbleVelocity * dt;
                _body.localRotation *= Quaternion.Euler(_tumbleSpin * dt);

                float floor = Tuning.CurbHeight;
                if (transform.position.y < floor)
                {
                    Vector3 p = transform.position;
                    p.y = floor;
                    transform.position = p;
                    _tumbleVelocity *= 0.4f;
                    _tumbleVelocity.y = Mathf.Abs(_tumbleVelocity.y) * 0.3f;
                }

                if (_tumbleTimer <= 0f)
                {
                    // Dust themselves off and carry on.
                    _body.localRotation = Quaternion.identity;
                    SnapToLoop(city);
                }

                return;
            }

            if (_dodgeTimer > 0f)
            {
                _dodgeTimer -= dt;
                transform.position += _dodgeDirection * (5.5f * dt);
                _bobPhase += dt * 16f;
            }
            else if (Crossing)
            {
                CrossT += dt * (Speed * 1.55f) / Mathf.Max(1f, Vector3.Distance(CrossFrom, CrossTo));
                transform.position = Vector3.Lerp(CrossFrom, CrossTo, Mathf.Clamp01(CrossT));
                Vector3 dir = CrossTo - CrossFrom;
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z), Vector3.up);

                if (CrossT >= 1f)
                {
                    Crossing = false;
                    SnapToLoop(city);
                }

                _bobPhase += dt * 11f;
            }
            else
            {
                WalkLoop(city, dt);
                _bobPhase += dt * 8f;
            }

            // A simple bob-and-sway sells walking without a skeleton.
            _bob = Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.055f;
            _body.localPosition = new Vector3(0f, _bob, 0f);
            _body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_bobPhase) * 4.5f);
        }

        void WalkLoop(CityModel city, float dt)
        {
            if (BlockIndex < 0 || BlockIndex >= city.Blocks.Count) return;
            List<Vector3> loop = city.Blocks[BlockIndex].SidewalkLoop;
            if (loop.Count < 2) return;

            Vector3 a = loop[Corner % loop.Count];
            Vector3 b = loop[(Corner + 1) % loop.Count];
            float length = Vector3.Distance(a, b);
            if (length < 0.01f) return;

            EdgeT += Speed * dt / length;
            while (EdgeT >= 1f)
            {
                EdgeT -= 1f;
                Corner = (Corner + 1) % loop.Count;
                a = loop[Corner];
                b = loop[(Corner + 1) % loop.Count];
                length = Mathf.Max(0.01f, Vector3.Distance(a, b));
            }

            Vector3 position = Vector3.Lerp(a, b, EdgeT);
            Vector3 dir = (b - a).normalized;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(dir, Vector3.up));
        }

        void SnapToLoop(CityModel city)
        {
            if (BlockIndex < 0 || BlockIndex >= city.Blocks.Count) return;
            List<Vector3> loop = city.Blocks[BlockIndex].SidewalkLoop;

            float best = float.MaxValue;
            for (int i = 0; i < loop.Count; i++)
            {
                Vector3 a = loop[i];
                Vector3 b = loop[(i + 1) % loop.Count];
                Vector3 p = MathX.ClosestPointOnSegment(a, b, transform.position, out float t);
                float sqr = (p - transform.position).sqrMagnitude;
                if (sqr >= best) continue;

                best = sqr;
                Corner = i;
                EdgeT = t;
            }

            WalkLoop(city, 0f);
        }
    }

    /// <summary>
    /// Keeps a crowd alive in a ring around the player. Pedestrians are the main source of
    /// "this city is inhabited", and the main reason to brake.
    /// </summary>
    public sealed class PedestrianSystem : MonoBehaviour
    {
        const float RecycleDistance = 145f;
        const float SpawnMinDistance = 25f;
        const float SpawnMaxDistance = 115f;

        public IReadOnlyList<PedestrianAgent> Agents => _agents;

        readonly List<PedestrianAgent> _agents = new List<PedestrianAgent>(128);
        readonly List<Mesh> _meshes = new List<Mesh>(20);
        CityModel _city;
        MaterialLibrary _mats;
        Rng _rng;
        int _targetCount;
        float _spawnCooldown;

        public void Initialise(CityModel city, MaterialLibrary mats, int seed)
        {
            _city = city;
            _mats = mats;
            _rng = new Rng(seed * 3313 + 17);
            BuildMeshes(mats.Palette);
        }

        void BuildMeshes(Palette pal)
        {
            var rng = new Rng(24601);

            for (int i = 0; i < 18; i++)
            {
                var mb = new MeshBuilder(pal);

                Color shirtColour = Art.ClothColours[i % Art.ClothColours.Length];
                int shirt = pal.Add(rng.Vary(shirtColour, 0.02f, 0.1f, 0.1f));
                int shirtDark = pal.AddShaded(shirtColour, 0.7f);
                int trouser = pal.Add(rng.Vary(new Color(0.24f, 0.26f, 0.34f), 0.03f, 0.1f, 0.15f));
                int trouserDark = pal.AddShaded(new Color(0.24f, 0.26f, 0.34f), 0.7f);
                int skin = pal.Add(Art.SkinTones[i % Art.SkinTones.Length]);
                int hair = pal.Add(new Color(0.10f, 0.08f, 0.07f));

                float height = rng.Range(1.58f, 1.80f);
                float shoulders = rng.Range(0.36f, 0.46f);

                // Legs.
                for (int s = -1; s <= 1; s += 2)
                    mb.AddBox(new Vector3(s * 0.10f, height * 0.24f, 0f),
                        new Vector3(0.14f, height * 0.48f, 0.16f), trouser, trouserDark, trouserDark);

                // Torso.
                mb.AddTaperedBox(new Vector3(0f, height * 0.66f, 0f),
                    new Vector3(shoulders, height * 0.36f, 0.24f), 1.05f, 1f,
                    Quaternion.identity, shirt, shirtDark);

                // Arms.
                for (int s = -1; s <= 1; s += 2)
                    mb.AddBox(new Vector3(s * (shoulders * 0.5f + 0.05f), height * 0.64f, 0f),
                        new Vector3(0.10f, height * 0.34f, 0.11f), shirt, shirtDark, shirtDark);

                // Head and hair.
                mb.AddBox(new Vector3(0f, height * 0.90f, 0f), new Vector3(0.19f, 0.21f, 0.19f), skin);
                mb.AddBox(new Vector3(0f, height * 0.985f, -0.01f), new Vector3(0.21f, 0.08f, 0.21f), hair);

                // A shopping bag or umbrella, on some of them.
                if (rng.Chance(0.45f))
                {
                    int bag = pal.Add(rng.Pick(Art.ClothColours));
                    mb.AddBox(new Vector3(0.22f, height * 0.40f, 0.02f), new Vector3(0.20f, 0.26f, 0.12f), bag);
                }
                else if (rng.Chance(0.25f))
                {
                    int umbrella = pal.Add(rng.Pick(Art.ClothColours));
                    mb.AddBeam(new Vector3(-0.24f, height * 0.42f, 0f), new Vector3(-0.24f, height * 1.15f, 0f),
                        0.03f, hair);
                    mb.AddCylinder(new Vector3(-0.24f, height * 1.05f, 0f), 0.42f, 0.02f, 0.20f, 8,
                        Quaternion.identity, umbrella, umbrella);
                }

                _meshes.Add(mb.ToMesh($"FE_Pedestrian_{i}"));
            }
        }

        public void SetDensity(int count)
        {
            _targetCount = Mathf.Max(0, count);
            while (_agents.Count < _targetCount)
                _agents.Add(PedestrianAgent.Create(transform, _meshes[_agents.Count % _meshes.Count], _mats.Surface));

            for (int i = _targetCount; i < _agents.Count; i++)
                if (_agents[i].Active) _agents[i].SetActive(false);
        }

        public void Clear()
        {
            foreach (PedestrianAgent agent in _agents) agent.SetActive(false);
        }

        public void Populate(Vector3 around)
        {
            for (int i = 0; i < _targetCount && i < _agents.Count; i++)
                if (!TrySpawn(_agents[i], around, 8f, SpawnMaxDistance))
                    _agents[i].SetActive(false);
        }

        bool TrySpawn(PedestrianAgent agent, Vector3 around, float minDistance, float maxDistance)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int blockIndex = _rng.Range(0, _city.Blocks.Count);
                CityBlock block = _city.Blocks[blockIndex];
                if (block.SidewalkLoop.Count < 2) continue;

                int corner = _rng.Range(0, block.SidewalkLoop.Count);
                float t = _rng.Value;
                Vector3 a = block.SidewalkLoop[corner];
                Vector3 b = block.SidewalkLoop[(corner + 1) % block.SidewalkLoop.Count];
                Vector3 position = Vector3.Lerp(a, b, t);

                // Spread across the pavement width so they do not walk in single file.
                Vector3 along = (b - a).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, along);
                position += side * _rng.Range(-Tuning.SidewalkWidth * 0.32f, Tuning.SidewalkWidth * 0.32f);

                float toPlayer = Vector3.Distance(position, around);
                if (toPlayer < minDistance || toPlayer > maxDistance) continue;

                agent.SetMesh(_meshes[_rng.Range(0, _meshes.Count)]);
                agent.Spawn(position, MathX.SignedYawTo(Vector3.forward, along), _rng.Range(1.0f, 1.9f),
                    blockIndex, corner, t);
                return true;
            }

            return false;
        }

        void Update()
        {
            if (_city == null || _targetCount == 0) return;

            float dt = Time.deltaTime;
            Vector3 playerPos = Services.PlayerPosition;
            Vector3 playerVel = Services.Player != null ? Services.Player.Velocity : Vector3.zero;
            float playerSpeed = playerVel.magnitude;

            for (int i = 0; i < _agents.Count && i < _targetCount; i++)
            {
                PedestrianAgent agent = _agents[i];

                if (!agent.Active)
                {
                    _spawnCooldown -= dt;
                    if (_spawnCooldown <= 0f)
                    {
                        _spawnCooldown = 0.04f;
                        TrySpawn(agent, playerPos, SpawnMinDistance, SpawnMaxDistance);
                    }

                    continue;
                }

                Vector3 delta = agent.transform.position - playerPos;
                float distance = delta.magnitude;

                if (distance > RecycleDistance)
                {
                    agent.SetActive(false);
                    continue;
                }

                // Get out of the way when a scooter is bearing down on them.
                if (playerSpeed > 6f && distance < 7.5f && !agent.IsTumbling)
                {
                    float closing = Vector3.Dot(-delta.normalized, playerVel.normalized);
                    if (closing > 0.55f)
                    {
                        Vector3 side = Vector3.Cross(Vector3.up, playerVel.normalized);
                        float sign = Vector3.Dot(delta, side) >= 0f ? 1f : -1f;
                        agent.Dodge(side * sign);
                    }
                }

                // Occasionally decide to cross the road.
                if (!agent.Crossing && !agent.IsTumbling && distance > 22f && _rng.Chance(0.0016f))
                    StartCrossing(agent);

                agent.Tick(_city, dt);
            }
        }

        void StartCrossing(PedestrianAgent agent)
        {
            CityBlock block = _city.Blocks[agent.BlockIndex];
            Vector3 position = agent.transform.position;
            Vector3 outward = position - block.Centre;
            outward.y = 0f;

            // Push out along whichever axis is closer to the kerb.
            outward = Mathf.Abs(outward.x) > Mathf.Abs(outward.z)
                ? new Vector3(Mathf.Sign(outward.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(outward.z));

            Vector3 target = position + outward * (Tuning.RoadHalfWidth * 2f + Tuning.SidewalkWidth);

            // Only cross if there is actually pavement on the far side.
            int targetBlock = FindBlockAt(target);
            if (targetBlock < 0) return;

            agent.Crossing = true;
            agent.CrossFrom = position;
            agent.CrossTo = new Vector3(target.x, Tuning.CurbHeight + 0.02f, target.z);
            agent.CrossT = 0f;
            agent.BlockIndex = targetBlock;
        }

        int FindBlockAt(Vector3 position)
        {
            for (int i = 0; i < _city.Blocks.Count; i++)
            {
                CityBlock block = _city.Blocks[i];
                if (Mathf.Abs(position.x - block.Centre.x) > block.Size.x * 0.5f) continue;
                if (Mathf.Abs(position.z - block.Centre.z) > block.Size.y * 0.5f) continue;
                return i;
            }

            return -1;
        }

        public PedestrianAgent FindAgent(Collider collider)
        {
            if (collider == null) return null;
            return collider.GetComponentInParent<PedestrianAgent>();
        }
    }
}
