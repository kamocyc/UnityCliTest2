using UnityEngine;
using FormosaExpress.Core;
using FormosaExpress.City;

namespace FormosaExpress.Traffic
{
    /// <summary>
    /// One vehicle on the lane graph. Movement is kinematic along the lane polyline, which keeps
    /// traffic orderly and cheap; being hit by the player switches it into a short, showy
    /// "knocked" state before it recovers onto the nearest lane.
    /// </summary>
    public sealed class TrafficAgent : MonoBehaviour
    {
        public VehicleMeshVariant Variant;
        public int PathIndex;
        public float Distance;
        public float Speed;
        public float DesiredSpeed;
        public bool Active;

        public Vector3 Forward { get; private set; } = Vector3.forward;
        public bool IsKnocked => _knockTimer > 0f;

        Transform _body;
        Rigidbody _rb;
        BoxCollider _collider;
        MeshFilter _surfaceFilter;
        MeshFilter _glowFilter;
        MeshRenderer _glowRenderer;

        float _knockTimer;
        Vector3 _knockVelocity;
        Vector3 _knockSpin;
        float _lean;
        float _stopTimer;
        float _hornCooldown;

        public static TrafficAgent Create(Transform parent, MaterialLibrary mats)
        {
            var go = new GameObject("TrafficAgent");
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer(Tuning.LayerTraffic);

            var agent = go.AddComponent<TrafficAgent>();
            agent.Build(mats);
            return agent;
        }

        void Build(MaterialLibrary mats)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _collider = gameObject.AddComponent<BoxCollider>();

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            var surfaceGo = new GameObject("Surface");
            surfaceGo.transform.SetParent(_body, false);
            surfaceGo.layer = gameObject.layer;
            _surfaceFilter = surfaceGo.AddComponent<MeshFilter>();
            surfaceGo.AddComponent<MeshRenderer>().sharedMaterial = mats.Surface;

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(_body, false);
            glowGo.layer = gameObject.layer;
            _glowFilter = glowGo.AddComponent<MeshFilter>();
            _glowRenderer = glowGo.AddComponent<MeshRenderer>();
            _glowRenderer.sharedMaterial = mats.GlowSoft;
            _glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glowRenderer.receiveShadows = false;

            SetActive(false);
        }

        public void SetActive(bool value)
        {
            Active = value;
            if (gameObject.activeSelf != value) gameObject.SetActive(value);
        }

        public void Assign(VehicleMeshVariant variant, TrafficPath path, float distance, ref Rng rng)
        {
            Variant = variant;
            _surfaceFilter.sharedMesh = variant.Surface;
            _glowFilter.sharedMesh = variant.Glow;
            _glowRenderer.enabled = variant.Glow != null;

            _collider.size = variant.ColliderSize;
            _collider.center = variant.ColliderCentre;

            PathIndex = path.Index;
            Distance = distance;
            DesiredSpeed = variant.DesiredSpeed * rng.Range(0.82f, 1.12f);
            Speed = DesiredSpeed * 0.7f;
            _knockTimer = 0f;
            _stopTimer = 0f;
            _lean = 0f;

            SetActive(true);
            Place(path);
        }

        void Place(TrafficPath path)
        {
            Vector3 position = path.Sample(Distance, out Vector3 tangent);
            Forward = tangent;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(tangent, Vector3.up));
        }

        /// <summary>Advances along the lane graph. Returns false when the agent should recycle.</summary>
        public bool Advance(CityModel city, float dt, float speedLimit, bool mustStop)
        {
            if (_hornCooldown > 0f) _hornCooldown -= dt;

            if (_knockTimer > 0f)
            {
                UpdateKnocked(city, dt);
                return true;
            }

            float target = Mathf.Min(DesiredSpeed, speedLimit);
            if (mustStop) target = 0f;

            // Braking is much sharper than acceleration, as it is on a real street.
            float rate = target < Speed ? 11f : 4.2f;
            Speed = MathX.ExpSmooth(Speed, target, rate, dt);
            if (Speed < 0.05f) Speed = 0f;

            _stopTimer = Speed < 0.4f ? _stopTimer + dt : 0f;

            TrafficPath path = city.Paths[PathIndex];
            Distance += Speed * dt;

            int guard = 0;
            while (Distance >= path.Length && guard++ < 4)
            {
                Distance -= path.Length;

                if (path.Next.Count == 0) return false;
                int next = path.Next[Random.Range(0, path.Next.Count)];
                if (next == path.Index) return false;

                path = city.Paths[next];
                PathIndex = next;
            }

            Vector3 position = path.Sample(Distance, out Vector3 tangent);
            Forward = tangent;

            var rotation = Quaternion.LookRotation(tangent, Vector3.up);

            // Scooters lean into the turn, which reads well at a glance.
            if (Variant != null && Variant.Kind == VehicleKind.Scooter)
            {
                float turn = MathX.SignedYawTo(transform.forward, tangent);
                _lean = MathX.ExpSmooth(_lean, Mathf.Clamp(turn * 2.4f, -22f, 22f), 6f, dt);
                _body.localRotation = Quaternion.Euler(0f, 0f, _lean);
            }

            transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        void UpdateKnocked(CityModel city, float dt)
        {
            _knockTimer -= dt;

            _knockVelocity += Vector3.down * 22f * dt;
            transform.position += _knockVelocity * dt;
            transform.rotation *= Quaternion.Euler(_knockSpin * dt);

            // Land back on the street.
            if (transform.position.y < 0f)
            {
                Vector3 p = transform.position;
                p.y = 0f;
                transform.position = p;
                _knockVelocity.y = Mathf.Abs(_knockVelocity.y) * 0.28f;
                _knockVelocity.x *= 0.72f;
                _knockVelocity.z *= 0.72f;
                _knockSpin *= 0.6f;
            }

            if (_knockTimer > 0f) return;

            // Recover: snap to the closest point on the closest lane and carry on.
            int bestPath = -1;
            float bestDistance = 0f;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < city.Paths.Count; i++)
            {
                TrafficPath candidate = city.Paths[i];
                if (candidate.IsConnector) continue;

                for (int s = 1; s < candidate.Points.Length; s++)
                {
                    Vector3 p = MathX.ClosestPointOnSegment(candidate.Points[s - 1], candidate.Points[s],
                        transform.position, out float t);
                    float sqr = (p - transform.position).sqrMagnitude;
                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    bestPath = i;
                    bestDistance = Mathf.Lerp(candidate.Cumulative[s - 1], candidate.Cumulative[s], t);
                }
            }

            if (bestPath >= 0)
            {
                PathIndex = bestPath;
                Distance = bestDistance;
                Speed = 0f;
                _body.localRotation = Quaternion.identity;
                Place(city.Paths[bestPath]);
            }
        }

        /// <summary>Sends the vehicle tumbling after a hit from the player.</summary>
        public void Knock(Vector3 impulse, float severity)
        {
            _knockTimer = Mathf.Lerp(0.7f, 2.1f, severity);
            _knockVelocity = impulse + Vector3.up * Mathf.Lerp(1.5f, 4.5f, severity);
            _knockSpin = new Vector3(
                Random.Range(-60f, 60f),
                Random.Range(-260f, 260f) * severity,
                Random.Range(-90f, 90f)) * Mathf.Lerp(0.4f, 1f, severity);
            Speed = 0f;
        }

        public bool TryHonk()
        {
            if (_hornCooldown > 0f) return false;
            _hornCooldown = Random.Range(2.5f, 7f);
            return true;
        }

        public bool IsStuck => _stopTimer > 3.5f;
    }
}
