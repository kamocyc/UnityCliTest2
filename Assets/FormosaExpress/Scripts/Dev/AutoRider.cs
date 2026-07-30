using UnityEngine;
using FormosaExpress.Gameplay;

namespace FormosaExpress.Core
{
    /// <summary>
    /// A development aid, not a game feature: an autopilot that follows the navigation heading,
    /// slows for pickups and boosts on the straights. Attach it at runtime to soak-test a long
    /// session (order flow, traffic, respawns, memory) without a human at the controls.
    /// </summary>
    public sealed class AutoRider : MonoBehaviour
    {
        [Tooltip("Metres from the active target at which the rider starts braking to collect.")]
        public float ApproachDistance = 22f;

        float _unstickTimer;
        float _reverseTimer;

        void Update()
        {
            InputRouter input = Services.Input;
            var player = Services.Player;
            RouteService routes = Services.Routes;
            OrderManager orders = Services.Orders;

            if (input == null || player == null) return;

            input.ScriptedActive = true;

            // Accept every prompt so the autopilot can walk the whole shift loop unattended.
            if (Services.Director != null && Services.Director.Phase != GamePhase.Riding)
            {
                input.ScriptedConfirm = true;
                input.ScriptedThrottle = 0f;
                input.ScriptedSteer = 0f;
                return;
            }

            float distance = orders?.Focus != null
                ? Vector3.Distance(player.transform.position, orders.Focus.ActiveTarget)
                : 999f;

            // Follow the route at range, then aim straight at the zone once it is in sight.
            Vector3 heading = routes != null && routes.HasRoute ? routes.Heading : player.transform.forward;
            if (orders?.Focus != null && distance < 26f)
            {
                Vector3 direct = orders.Focus.ActiveTarget - player.transform.position;
                direct.y = 0f;
                if (direct.sqrMagnitude > 0.5f) heading = direct.normalized;
            }

            float steer = Mathf.Clamp(MathX.SignedYawTo(player.transform.forward, heading) / 35f, -1f, 1f);

            // Pick a speed for the corner ahead, then brake or accelerate towards it. Driving
            // flat out into every junction is what stopped the earlier version ever finishing
            // a delivery.
            float cornerLimit = Mathf.Lerp(30f, 78f, 1f - Mathf.Abs(steer));
            float targetKmh = cornerLimit;

            if (distance < 12f) targetKmh = 22f;
            else if (distance < ApproachDistance + 14f) targetKmh = Mathf.Min(targetKmh, 42f);

            float error = targetKmh - player.SpeedKmh;
            float throttle = Mathf.Clamp01(error / 12f);
            float brake = Mathf.Clamp01(-error / 16f);

            // Back off a wall if forward progress has stalled.
            if (player.IsGrounded && player.SpeedKmh < 4f && throttle > 0.3f) _unstickTimer += Time.deltaTime;
            else _unstickTimer = 0f;

            if (_unstickTimer > 1.2f)
            {
                _reverseTimer = 0.9f;
                _unstickTimer = 0f;
            }

            if (_reverseTimer > 0f)
            {
                _reverseTimer -= Time.deltaTime;
                throttle = 0f;
                brake = 1f;
                steer = Mathf.Sign(steer == 0f ? 1f : steer);
            }

            input.ScriptedThrottle = throttle;
            input.ScriptedBrake = brake;
            input.ScriptedSteer = steer;

            // Boost only on a genuinely long straight; drift through the tight stuff.
            input.ScriptedBoost = Mathf.Abs(steer) < 0.08f && distance > 90f && player.SpeedKmh > 45f;
            input.ScriptedDrift = Mathf.Abs(steer) > 0.7f && player.SpeedKmh > 40f;
        }

        void OnDisable()
        {
            InputRouter input = Services.Input;
            if (input == null) return;

            input.ScriptedActive = false;
            input.ScriptedThrottle = 0f;
            input.ScriptedBrake = 0f;
            input.ScriptedSteer = 0f;
            input.ScriptedBoost = false;
            input.ScriptedDrift = false;
        }
    }
}
