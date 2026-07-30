using UnityEngine;
using FormosaExpress.Core;
using FormosaExpress.Vehicle;

namespace FormosaExpress.Gameplay
{
    /// <summary>
    /// The rival courier's driving. It rides the same <see cref="ScooterController"/> the player
    /// does and only produces the same five inputs, so anything it can do the player can do too.
    /// Skill is expressed as cornering discipline, boost judgement and how often it fumbles -
    /// never as extra grip or a bigger engine.
    /// </summary>
    public sealed class RivalBrain : IScooterInputSource
    {
        readonly RouteTracker _route;
        readonly int _level;

        float _mistakeTimer;
        float _recoverTimer;
        float _stallTimer;
        float _reverseTimer;
        float _steerSmoothed;

        public RivalBrain(RouteTracker route, int level)
        {
            _route = route;
            _level = Mathf.Max(1, level);
        }

        /// <summary>Set while the rival is waiting at a shop, or serving its head start.</summary>
        public float HoldTimer { get; set; }

        /// <summary>Distance at which it starts slowing for its target, mirroring the player's task.</summary>
        public float TargetDistance { get; set; } = 999f;

        public ScooterInputState Read(ScooterController scooter, float dt)
        {
            if (HoldTimer > 0f)
            {
                HoldTimer -= dt;
                return ScooterInputState.Braking;
            }

            // Occasional fumbles: without these a perfect line makes the rival feel like a rail.
            _mistakeTimer -= dt;
            if (_mistakeTimer <= 0f)
            {
                _mistakeTimer = 1f;
                if (Random.value < Tuning.RivalMistakeRate(_level))
                    _recoverTimer = Random.Range(0.35f, 1.1f);
            }

            Vector3 heading = _route.HasRoute || _route.HasTarget ? _route.Heading : scooter.transform.forward;

            // Aim straight at the zone once it is in sight, the way a human would.
            if (TargetDistance < 26f && _route.HasTarget)
            {
                Vector3 direct = _route.Target - scooter.transform.position;
                direct.y = 0f;
                if (direct.sqrMagnitude > 0.5f) heading = direct.normalized;
            }

            float rawSteer = Mathf.Clamp(MathX.SignedYawTo(scooter.transform.forward, heading) / 35f, -1f, 1f);
            _steerSmoothed = MathX.ExpSmooth(_steerSmoothed, rawSteer, 12f, dt);

            // Choose a speed for the corner ahead, then drive the error.
            float cornerCeiling = Mathf.Lerp(30f, 78f, 1f - Mathf.Abs(_steerSmoothed)) * Tuning.RivalSpeedFactor(_level);
            float targetKmh = cornerCeiling;

            if (TargetDistance < 12f) targetKmh = 22f;
            else if (TargetDistance < 34f) targetKmh = Mathf.Min(targetKmh, 44f);

            if (_recoverTimer > 0f)
            {
                _recoverTimer -= dt;
                targetKmh *= 0.45f;
            }

            float error = targetKmh - scooter.SpeedKmh;
            float throttle = Mathf.Clamp01(error / 12f);
            float brake = Mathf.Clamp01(-error / 16f);

            // Back off a wall if it has stopped making progress.
            if (scooter.IsGrounded && scooter.SpeedKmh < 4f && throttle > 0.3f) _stallTimer += dt;
            else _stallTimer = 0f;

            if (_stallTimer > 1.2f)
            {
                _reverseTimer = 0.9f;
                _stallTimer = 0f;
            }

            if (_reverseTimer > 0f)
            {
                _reverseTimer -= dt;
                return new ScooterInputState
                {
                    Brake = 1f,
                    Steer = Mathf.Sign(_steerSmoothed == 0f ? 1f : _steerSmoothed)
                };
            }

            return new ScooterInputState
            {
                Throttle = throttle,
                Brake = brake,
                Steer = _steerSmoothed,
                Boost = Mathf.Abs(_steerSmoothed) < 0.08f && TargetDistance > 90f && scooter.SpeedKmh > 45f,
                Drift = Mathf.Abs(_steerSmoothed) > 0.7f && scooter.SpeedKmh > 40f
            };
        }
    }
}
