using System;
using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.Vehicle
{
    public struct ImpactInfo
    {
        public Vector3 Point;
        public Vector3 Normal;
        public float Severity;      // 0..1
        public float SpeedLost;
        public bool HitTraffic;
        public bool HitPedestrian;
        public Collider Other;
    }

    /// <summary>
    /// Arcade scooter handling. Translation is dynamic (so collisions, kerbs and jumps all
    /// feel physical) while rotation is authored (so the rider never spins out or tips over).
    /// That split is what separates a fun delivery scooter from a physics toy.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ScooterController : MonoBehaviour
    {
        public event Action<ImpactInfo> Impact;
        public event Action Respawned;

        [Header("Wiring")]
        public Transform VisualRoot;

        /// <summary>
        /// Where this scooter's intent comes from. Defaults to the human; the rival courier
        /// swaps in its own brain so both ride exactly the same handling model.
        /// </summary>
        public IScooterInputSource InputSource { get; set; } = new PlayerInputSource();

        // ------------------------------------------------------------------ state
        public VehicleStats Stats { get; private set; }
        public bool ControlEnabled { get; set; } = true;

        public float ForwardSpeed { get; private set; }
        public float SpeedKmh => MathX.ToKmh(Mathf.Abs(ForwardSpeed));
        public float Speed01 => Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / Mathf.Max(1f, Stats.TopSpeed));
        public float LateralSlip { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsDrifting { get; private set; }
        public bool IsBoosting { get; private set; }
        public bool IsAirborne => !IsGrounded;
        public float AirTime { get; private set; }
        public float DriftTime { get; private set; }
        public float LeanDegrees { get; private set; }
        public float Rpm01 { get; private set; }
        public float ThrottleInput { get; private set; }
        public float BrakeInput { get; private set; }
        public float SteerInput { get; private set; }
        public float SuspensionCompression { get; private set; }
        public float Adrenaline { get; private set; }
        public float Adrenaline01 => Mathf.Clamp01(Adrenaline / Mathf.Max(1f, Stats.AdrenalineCapacity));
        public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
        public float DistanceTravelled { get; private set; }

        Rigidbody _rb;
        CapsuleCollider _collider;
        ScooterInputState _input;
        float _yaw;
        float _pitch;
        float _visualLean;
        float _visualPitch;
        float _wheelSpin;
        float _boostHeld;
        float _groundedTimer;
        float _airborneTimer;
        float _stuckTimer;
        float _impactCooldown;
        Vector3 _groundNormal = Vector3.up;
        Vector3 _lastRespawnPoint;
        int _groundMask;
        int _wheelHits;

        // Suspension probe offsets, in local space.
        static readonly Vector3 FrontWheel = new Vector3(0f, 0.05f, 0.62f);
        static readonly Vector3 RearWheel = new Vector3(0f, 0.05f, -0.62f);

        public float WheelSpin => _wheelSpin;
        public float VisualLean => _visualLean;
        public float VisualPitch => _visualPitch;
        public Vector3 GroundNormal => _groundNormal;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 190f;
            _rb.useGravity = false;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.automaticCenterOfMass = false;
            _rb.centerOfMass = new Vector3(0f, -0.15f, 0f);

            _collider = GetComponent<CapsuleCollider>();
            if (_collider == null) _collider = gameObject.AddComponent<CapsuleCollider>();
            _collider.direction = 2;                 // along local Z
            _collider.height = 1.85f;
            _collider.radius = 0.42f;
            _collider.center = new Vector3(0f, 0.52f, 0f);

            _groundMask = LayerMask.GetMask(Tuning.LayerGround, Tuning.LayerBuilding, Tuning.LayerProp);
            Stats = VehicleStats.From(new SaveData());
        }

        public void ApplyStats(VehicleStats stats)
        {
            Stats = stats;
            Adrenaline = Mathf.Min(Adrenaline, stats.AdrenalineCapacity);
        }

        public void Teleport(Vector3 position, float yawDegrees)
        {
            _yaw = yawDegrees;
            _pitch = 0f;
            _visualLean = 0f;
            _visualPitch = 0f;
            _groundNormal = Vector3.up;
            _lastRespawnPoint = position;

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            if (_rb != null)
            {
                _rb.position = position;
                _rb.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            DriftTime = 0f;
            AirTime = 0f;
            _stuckTimer = 0f;
        }

        public void ResetRun()
        {
            Adrenaline = 0f;
            DistanceTravelled = 0f;
        }

        public void AddAdrenaline(float amount)
        {
            Adrenaline = Mathf.Clamp(Adrenaline + amount, 0f, Stats.AdrenalineCapacity);
        }

        // ------------------------------------------------------------------ tick

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // Read intent once per step, then let every subsystem work from the same snapshot.
            _input = ControlEnabled && InputSource != null
                ? InputSource.Read(this, dt)
                : ScooterInputState.Braking;

            ThrottleInput = _input.Throttle;
            BrakeInput = _input.Brake;
            SteerInput = _input.Steer;

            Suspension(dt);
            UpdateBoost(dt);
            Drive(dt);
            Steer(dt);
            Grip(dt);
            LimitSpeed(dt);
            UpdateStateTimers(dt);
            ApplyRotation(dt);
            CheckStuck(dt);

            if (_impactCooldown > 0f) _impactCooldown -= dt;
            DistanceTravelled += Mathf.Abs(ForwardSpeed) * dt;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Visual lean lags the physics for weight, and is exaggerated while drifting.
            float target = -SteerInput * Tuning.LeanMaxDegrees * Mathf.Lerp(0.35f, 1f, Speed01);
            if (IsDrifting) target *= 1.4f;
            if (IsAirborne) target *= 0.5f;
            _visualLean = MathX.ExpSmooth(_visualLean, Mathf.Clamp(target, -46f, 46f), 7f, dt);

            // Nose lifts under power, dips under braking.
            float pitchTarget = 0f;
            if (IsGrounded)
            {
                pitchTarget = -ThrottleInput * 5.5f * (1f - Speed01 * 0.6f) + BrakeInput * 6.5f * Speed01;
                if (IsBoosting) pitchTarget -= 3.5f;
            }
            else
            {
                pitchTarget = Mathf.Clamp(-Velocity.y * 1.6f, -Tuning.MaxAirTiltDegrees, Tuning.MaxAirTiltDegrees);
            }

            _visualPitch = MathX.ExpSmooth(_visualPitch, pitchTarget, 6f, dt);
            _wheelSpin += ForwardSpeed / 0.28f * dt * Mathf.Rad2Deg;

            float rpmTarget = Mathf.Clamp01(0.12f + Speed01 * 0.75f + ThrottleInput * 0.22f
                                            + (IsDrifting ? 0.15f : 0f) + (IsBoosting ? 0.2f : 0f));
            if (IsAirborne && ThrottleInput > 0.1f) rpmTarget = Mathf.Max(rpmTarget, 0.85f);
            Rpm01 = MathX.ExpSmooth(Rpm01, rpmTarget, 6f, dt);

            LeanDegrees = _visualLean;
        }

        // ------------------------------------------------------------------ suspension

        void Suspension(float dt)
        {
            _wheelHits = 0;
            Vector3 normalSum = Vector3.zero;
            float compressionSum = 0f;

            ProbeWheel(FrontWheel, ref normalSum, ref compressionSum);
            ProbeWheel(RearWheel, ref normalSum, ref compressionSum);

            IsGrounded = _wheelHits > 0;
            if (IsGrounded)
            {
                _groundNormal = MathX.ExpSmooth(_groundNormal, (normalSum / _wheelHits).normalized, 12f, dt);
                SuspensionCompression = compressionSum / _wheelHits;
            }
            else
            {
                _groundNormal = MathX.ExpSmooth(_groundNormal, Vector3.up, 3f, dt);
                SuspensionCompression = 0f;
            }

            // Gravity is applied unconditionally; the springs push back when grounded.
            _rb.AddForce(Vector3.down * Tuning.Gravity, ForceMode.Acceleration);
        }

        void ProbeWheel(Vector3 localOffset, ref Vector3 normalSum, ref float compressionSum)
        {
            Vector3 origin = transform.TransformPoint(localOffset);
            float maxDistance = Tuning.SuspensionRestLength + 0.35f;

            if (!Physics.Raycast(origin, -transform.up, out RaycastHit hit, maxDistance, _groundMask,
                    QueryTriggerInteraction.Ignore))
                return;

            _wheelHits++;
            normalSum += hit.normal;

            float compression = 1f - hit.distance / Tuning.SuspensionRestLength;
            compressionSum += Mathf.Clamp01(compression);

            // Spring plus damper along the suspension axis.
            float springForce = compression * Tuning.SuspensionStrength;
            float velocityAlongAxis = Vector3.Dot(_rb.GetPointVelocity(origin), transform.up);
            float damperForce = -velocityAlongAxis * Tuning.SuspensionDamping;

            _rb.AddForceAtPosition(transform.up * (springForce + damperForce), origin, ForceMode.Acceleration);
        }

        // ------------------------------------------------------------------ drive

        void UpdateBoost(float dt)
        {
            bool wants = _input.Boost && ControlEnabled;

            if (IsBoosting)
            {
                Adrenaline -= Tuning.AdrenalineBoostDrain * dt;
                if (Adrenaline <= 0f || !wants)
                {
                    Adrenaline = Mathf.Max(0f, Adrenaline);
                    IsBoosting = false;
                }
            }
            else if (wants && Adrenaline >= Tuning.AdrenalineMinToStart && ThrottleInput > 0.1f)
            {
                IsBoosting = true;
            }

            _boostHeld = IsBoosting ? _boostHeld + dt : 0f;

            // Drifting and airtime both feed the tank.
            if (IsDrifting) AddAdrenaline(Tuning.AdrenalineDriftPerSecond * dt);
            if (IsAirborne && AirTime > 0.25f) AddAdrenaline(Tuning.AdrenalineAirPerSecond * dt);
        }

        void Drive(float dt)
        {
            Vector3 velocity = _rb.linearVelocity;
            Vector3 forward = transform.forward;
            ForwardSpeed = Vector3.Dot(velocity, forward);

            float airFactor = IsGrounded ? 1f : Tuning.CoyoteThrottleAir;
            float accel = Stats.Acceleration * (IsBoosting ? Tuning.BoostAccelMultiplier : 1f);

            // Power falls off as speed approaches the cap, which makes the top end feel earned.
            float headroom = 1f - Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / Mathf.Max(1f, EffectiveTopSpeed));
            float power = ThrottleInput * accel * Mathf.Lerp(0.35f, 1f, headroom) * airFactor;
            if (power > 0f) _rb.AddForce(forward * power, ForceMode.Acceleration);

            if (BrakeInput > 0.01f && IsGrounded)
            {
                if (ForwardSpeed > 0.4f)
                {
                    _rb.AddForce(-forward * (BrakeInput * Stats.BrakeForce), ForceMode.Acceleration);
                }
                else if (ForwardSpeed > -Tuning.ReverseSpeed)
                {
                    // Reverse is deliberately slow: it exists to un-wedge yourself, not to drive.
                    _rb.AddForce(-forward * (BrakeInput * Stats.Acceleration * 0.45f), ForceMode.Acceleration);
                }
            }

            // Rolling resistance and air drag.
            if (IsGrounded && ThrottleInput < 0.05f && BrakeInput < 0.05f)
                _rb.AddForce(-forward * (ForwardSpeed * 1.15f), ForceMode.Acceleration);

            float dragCoefficient = 0.0022f;
            _rb.AddForce(-velocity * (velocity.magnitude * dragCoefficient), ForceMode.Acceleration);
        }

        float EffectiveTopSpeed => Stats.TopSpeed * (IsBoosting ? Tuning.BoostSpeedMultiplier : 1f);

        void Steer(float dt)
        {
            float speedFactor = Speed01;
            float steerRate = Mathf.Lerp(Tuning.BaseSteerAtRest, Tuning.BaseSteerAtTop, speedFactor);

            if (IsDrifting) steerRate *= Tuning.DriftSteerBoost;
            if (!IsGrounded) steerRate *= 0.55f;

            // Below walking pace the scooter should barely turn: no pivoting on the spot.
            float authority = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / 2.2f);
            float direction = ForwardSpeed < -0.3f ? -1f : 1f;

            _yaw += SteerInput * steerRate * authority * direction * dt;
            _yaw = Mathf.Repeat(_yaw, 360f);
        }

        void Grip(float dt)
        {
            Vector3 velocity = _rb.linearVelocity;
            Vector3 right = transform.right;
            float lateral = Vector3.Dot(velocity, right);
            LateralSlip = Mathf.Abs(lateral);

            bool wantsDrift = _input.Drift && ControlEnabled;
            IsDrifting = IsGrounded && wantsDrift && Mathf.Abs(ForwardSpeed) > 6.5f && Mathf.Abs(SteerInput) > 0.18f;

            if (!IsGrounded) return;

            float grip = IsDrifting ? Tuning.DriftGrip : Stats.Grip;

            // Cancel sideways velocity at the grip rate. Exponential so it is frame-rate safe.
            float retained = Mathf.Exp(-grip * dt);
            _rb.AddForce(-right * (lateral * (1f - retained) / dt), ForceMode.Acceleration);

            // A drift also pushes the nose round a little, so it reads as a slide not a skid.
            if (IsDrifting)
                _yaw += SteerInput * 34f * dt * Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / 12f);
        }

        void LimitSpeed(float dt)
        {
            Vector3 velocity = _rb.linearVelocity;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float top = EffectiveTopSpeed;

            if (flat.magnitude > top)
            {
                // A firm but not instant clamp keeps boost decay smooth.
                Vector3 clamped = flat.normalized * Mathf.Lerp(flat.magnitude, top, 1f - Mathf.Exp(-6f * dt));
                _rb.linearVelocity = new Vector3(clamped.x, velocity.y, clamped.z);
            }

            // Terminal fall speed, so a big drop is survivable.
            if (_rb.linearVelocity.y < -34f)
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, -34f, _rb.linearVelocity.z);
        }

        void UpdateStateTimers(float dt)
        {
            if (IsGrounded)
            {
                _groundedTimer += dt;

                if (_airborneTimer > 0.28f)
                {
                    // Landing: a hard one hurts the cargo and shakes the camera.
                    float impactSpeed = Mathf.Abs(_landingSpeed);
                    if (impactSpeed > Tuning.CargoLandingThreshold)
                    {
                        var info = new ImpactInfo
                        {
                            Point = transform.position,
                            Normal = Vector3.up,
                            Severity = Mathf.Clamp01((impactSpeed - Tuning.CargoLandingThreshold) / 18f),
                            SpeedLost = 0f,
                            HitTraffic = false,
                            HitPedestrian = false
                        };
                        Impact?.Invoke(info);
                    }
                }

                _airborneTimer = 0f;
                AirTime = 0f;
                _landingSpeed = 0f;
            }
            else
            {
                _airborneTimer += dt;
                _groundedTimer = 0f;
                if (_airborneTimer > 0.12f) AirTime = _airborneTimer;
                _landingSpeed = Mathf.Min(_landingSpeed, _rb.linearVelocity.y);
            }

            DriftTime = IsDrifting ? DriftTime + dt : 0f;
        }

        float _landingSpeed;

        void ApplyRotation(float dt)
        {
            // Align the chassis to the ground, then apply the authored yaw on top.
            Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 alignTarget = IsGrounded ? _groundNormal : Vector3.up;
            Quaternion align = Quaternion.FromToRotation(Vector3.up, alignTarget);

            Quaternion target = align * yawRotation;
            _rb.MoveRotation(MathX.ExpSmooth(_rb.rotation, target, 18f, dt));
            _rb.angularVelocity = Vector3.zero;
        }

        void CheckStuck(float dt)
        {
            bool wantsToMove = ThrottleInput > 0.4f || BrakeInput > 0.4f;
            bool barelyMoving = _rb.linearVelocity.sqrMagnitude < 0.6f;

            if (wantsToMove && barelyMoving && IsGrounded) _stuckTimer += dt;
            else _stuckTimer = Mathf.Max(0f, _stuckTimer - dt * 2f);

            bool fellOut = transform.position.y < -8f;

            if (_stuckTimer > 2.6f || fellOut)
            {
                RespawnOnRoad();
                _stuckTimer = 0f;
            }
        }

        /// <summary>Puts the rider back on the nearest lane, facing along it.</summary>
        public void RespawnOnRoad()
        {
            Vector3 target = _lastRespawnPoint;
            float yaw = _yaw;

            if (Services.City != null)
            {
                Vector3 road = Services.City.NearestRoadPoint(transform.position);
                int node = Services.City.NearestNode(road);
                if (node >= 0 && Services.City.Nodes[node].Edges.Count > 0)
                {
                    int edgeIndex = Services.City.Nodes[node].Edges[0];
                    Vector3 dir = Services.City.Edges[edgeIndex].Dir;
                    if (Services.City.Edges[edgeIndex].B == node) dir = -dir;
                    yaw = MathX.SignedYawTo(Vector3.forward, dir);
                    road += Vector3.Cross(Vector3.up, dir) * Tuning.LaneOffset;
                }

                target = road + Vector3.up * 0.8f;
            }

            Teleport(target, yaw);
            Respawned?.Invoke();
        }

        // ------------------------------------------------------------------ collisions

        void OnCollisionEnter(Collision collision) => HandleCollision(collision);

        void OnCollisionStay(Collision collision)
        {
            // Scraping along a wall should keep bleeding speed, but must not re-trigger the
            // full impact reaction every frame.
            if (collision.contactCount == 0) return;
            Vector3 normal = collision.GetContact(0).normal;
            Vector3 velocity = _rb.linearVelocity;
            float into = Vector3.Dot(velocity, -normal);
            if (into > 0.5f)
                _rb.AddForce(normal * (into * 3.5f), ForceMode.Acceleration);
        }

        void HandleCollision(Collision collision)
        {
            if (_impactCooldown > 0f) return;
            if (collision.contactCount == 0) return;

            ContactPoint contact = collision.GetContact(0);
            Vector3 velocity = _rb.linearVelocity;
            float closing = Vector3.Dot(velocity, -contact.normal);
            if (closing < 2.5f) return;

            int layer = collision.gameObject.layer;
            bool traffic = layer == LayerMask.NameToLayer(Tuning.LayerTraffic);
            bool pedestrian = layer == LayerMask.NameToLayer(Tuning.LayerPedestrian);

            // Glancing blows cost much less than a head-on hit.
            float head = Mathf.Clamp01(closing / Mathf.Max(3f, velocity.magnitude));
            float severity = Mathf.Clamp01(closing / 20f) * Mathf.Lerp(0.35f, 1f, head);
            if (pedestrian) severity *= 0.45f;

            float speedBefore = velocity.magnitude;

            // Bleed speed and push the rider away from the surface.
            float retain = Mathf.Lerp(0.86f, 0.34f, severity);
            Vector3 slide = velocity - contact.normal * Vector3.Dot(velocity, contact.normal);
            _rb.linearVelocity = slide * retain + contact.normal * Mathf.Lerp(1.2f, 4.5f, severity);

            IsBoosting = false;
            _impactCooldown = 0.22f;

            Impact?.Invoke(new ImpactInfo
            {
                Point = contact.point,
                Normal = contact.normal,
                Severity = severity,
                SpeedLost = Mathf.Max(0f, speedBefore - _rb.linearVelocity.magnitude),
                HitTraffic = traffic,
                HitPedestrian = pedestrian,
                Other = collision.collider
            });
        }
    }
}
