using UnityEngine;
using FormosaExpress.Core;
using FormosaExpress.Vehicle;

namespace FormosaExpress.Fx
{
    /// <summary>
    /// Chase camera tuned for speed reading: it trails the scooter, swings wide on drifts,
    /// pushes FOV with velocity, and shakes on impact. Also handles the pre-shift orbit.
    /// </summary>
    public sealed class ChaseCamera : MonoBehaviour
    {
        public enum Mode
        {
            Chase,
            Wide,
            Low
        }

        public Camera Camera { get; private set; }
        public Mode CurrentMode { get; private set; } = Mode.Chase;

        ScooterController _target;
        float _yaw;
        float _distance;
        float _height;
        float _fov;
        float _shake;
        float _shakeSeed;
        float _driftOffset;
        float _lookBackBlend;
        bool _orbiting;
        float _orbitAngle;
        Vector3 _orbitCentre;
        Vector3 _smoothedFocus;
        int _obstructionMask;

        public void Initialise(Camera camera, ScooterController target)
        {
            Camera = camera;
            _target = target;
            _distance = Tuning.CamDistance;
            _height = Tuning.CamHeight;
            _fov = Tuning.CamFovBase;
            _shakeSeed = Random.value * 100f;
            _obstructionMask = LayerMask.GetMask(Tuning.LayerBuilding);

            if (target != null)
            {
                _yaw = target.transform.eulerAngles.y;
                _smoothedFocus = target.transform.position;
                SnapBehindTarget();
            }
        }

        public void SnapBehindTarget()
        {
            if (_target == null) return;

            _yaw = _target.transform.eulerAngles.y;
            _smoothedFocus = _target.transform.position;
            Vector3 focus = _smoothedFocus + Vector3.up * Tuning.CamLookHeight;
            Vector3 back = Quaternion.Euler(0f, _yaw, 0f) * Vector3.back;
            transform.position = focus + back * _distance + Vector3.up * _height;
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        }

        /// <summary>Slow orbit used on the title screen and between shifts.</summary>
        public void BeginOrbit(Vector3 centre)
        {
            _orbiting = true;
            _orbitCentre = centre;
            _orbitAngle = 0f;

            // Place immediately, then let UpdateOrbit ease from there: without this the camera
            // visibly flies across the city on the first frame of the title screen.
            float baseYaw = _target != null ? _target.transform.eulerAngles.y : 0f;
            transform.position = centre + Quaternion.Euler(0f, baseYaw + 180f, 0f) * Vector3.forward * 8.4f
                                 + Vector3.up * 3.3f;
            transform.rotation = Quaternion.LookRotation((centre + Vector3.up * 1.15f) - transform.position, Vector3.up);
        }

        public void EndOrbit()
        {
            _orbiting = false;
            SnapBehindTarget();
        }

        public void Shake(float amount)
        {
            _shake = Mathf.Min(1.6f, _shake + amount);
        }

        public void CycleMode()
        {
            CurrentMode = (Mode)(((int)CurrentMode + 1) % 3);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            if (_orbiting)
            {
                UpdateOrbit(dt);
                return;
            }

            if (_target == null || Camera == null) return;

            InputRouter input = Services.Input;
            if (input != null && input.CycleCameraPressed) CycleMode();

            float targetDistance, targetHeight, baseFov;
            switch (CurrentMode)
            {
                case Mode.Wide:
                    targetDistance = Tuning.CamDistance * 1.55f;
                    targetHeight = Tuning.CamHeight * 1.75f;
                    baseFov = Tuning.CamFovBase - 4f;
                    break;
                case Mode.Low:
                    targetDistance = Tuning.CamDistance * 0.78f;
                    targetHeight = Tuning.CamHeight * 0.55f;
                    baseFov = Tuning.CamFovBase + 6f;
                    break;
                default:
                    targetDistance = Tuning.CamDistance;
                    targetHeight = Tuning.CamHeight;
                    baseFov = Tuning.CamFovBase;
                    break;
            }

            float speed01 = _target.Speed01;

            // Pull back and lift a touch at speed so more of the street is visible.
            targetDistance += speed01 * 1.5f + (_target.IsBoosting ? 1.1f : 0f);
            targetHeight += speed01 * 0.35f;

            _distance = MathX.ExpSmooth(_distance, targetDistance, 4.5f, dt);
            _height = MathX.ExpSmooth(_height, targetHeight, 4.5f, dt);

            // Look back on demand.
            bool lookBack = input != null && input.LookBack;
            _lookBackBlend = MathX.ExpSmooth(_lookBackBlend, lookBack ? 1f : 0f, 9f, dt);

            // While drifting the camera slides opposite the slide, which sells the angle.
            float driftTarget = _target.IsDrifting ? -_target.SteerInput * 17f : 0f;
            _driftOffset = MathX.ExpSmooth(_driftOffset, driftTarget, 5f, dt);

            float targetYaw = _target.transform.eulerAngles.y + _driftOffset + _lookBackBlend * 180f;
            float yawSpeed = Tuning.CamYawSpeed * Mathf.Lerp(0.55f, 1.5f, speed01);
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-yawSpeed * dt));

            // Focus point leads the scooter slightly in its direction of travel.
            Vector3 velocity = _target.Velocity;
            Vector3 lead = new Vector3(velocity.x, 0f, velocity.z) * 0.16f;
            Vector3 focusTarget = _target.transform.position + lead;
            _smoothedFocus = MathX.ExpSmooth(_smoothedFocus, focusTarget, Tuning.CamFollowSpeed, dt);

            Vector3 focus = _smoothedFocus + Vector3.up * Tuning.CamLookHeight;
            Quaternion orbit = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 desired = focus + orbit * Vector3.back * _distance + Vector3.up * _height;

            // Keep the camera out of buildings.
            Vector3 fromFocus = desired - focus;
            if (Physics.SphereCast(focus, 0.45f, fromFocus.normalized, out RaycastHit hit,
                    fromFocus.magnitude, _obstructionMask, QueryTriggerInteraction.Ignore))
                desired = focus + fromFocus.normalized * Mathf.Max(1.6f, hit.distance - 0.3f);

            // Never let the camera clip below the street.
            desired.y = Mathf.Max(desired.y, _target.transform.position.y + 0.85f);

            transform.position = desired;

            Vector3 lookAt = focus + Vector3.up * (speed01 * 0.35f);
            transform.rotation = Quaternion.LookRotation((lookAt - desired).normalized, Vector3.up);

            // Shake, decaying fast so it punctuates rather than nauseates.
            if (_shake > 0.0005f)
            {
                float t = Time.unscaledTime * 26f + _shakeSeed;
                var offset = new Vector3(
                    (Mathf.PerlinNoise(t, 0f) - 0.5f),
                    (Mathf.PerlinNoise(0f, t) - 0.5f),
                    (Mathf.PerlinNoise(t, t) - 0.5f));

                transform.position += offset * (_shake * 0.55f);
                transform.rotation *= Quaternion.Euler(offset.y * _shake * 3.2f, offset.x * _shake * 3.2f,
                    offset.z * _shake * 2.2f);
                _shake = Mathf.Max(0f, _shake - dt * 3.4f);
            }

            // FOV: rises with speed, punches on boost.
            float fovTarget = Mathf.Lerp(baseFov, Tuning.CamFovAtTop, speed01);
            if (_target.IsBoosting) fovTarget = Mathf.Lerp(fovTarget, Tuning.CamFovBoost, 0.75f);
            if (_target.IsDrifting) fovTarget += 2.5f;
            _fov = MathX.ExpSmooth(_fov, fovTarget, _target.IsBoosting ? 7f : 3.2f, dt);
            Camera.fieldOfView = _fov;
        }

        /// <summary>
        /// A hero shot rather than a true orbit: the camera hangs behind and above the scooter
        /// and sways through a limited arc, so on a narrow street it stays out of the buildings
        /// and keeps the neon frontage in shot.
        /// </summary>
        void UpdateOrbit(float dt)
        {
            _orbitAngle += dt * 10f;

            float baseYaw = _target != null ? _target.transform.eulerAngles.y : 0f;
            float sway = Mathf.Sin(_orbitAngle * Mathf.Deg2Rad * 0.55f) * 38f;
            var rotation = Quaternion.Euler(0f, baseYaw + 180f + sway, 0f);

            Vector3 focus = _orbitCentre + Vector3.up * 1.15f;
            float radius = 8.4f;
            float height = 3.3f + Mathf.Sin(_orbitAngle * Mathf.Deg2Rad * 0.31f) * 0.7f;

            Vector3 desired = focus + rotation * Vector3.forward * radius + Vector3.up * height;

            // Same obstruction handling as the chase camera, so a shopfront never fills the frame.
            Vector3 fromFocus = desired - focus;
            if (Physics.SphereCast(focus, 0.5f, fromFocus.normalized, out RaycastHit hit,
                    fromFocus.magnitude, _obstructionMask, QueryTriggerInteraction.Ignore))
                desired = focus + fromFocus.normalized * Mathf.Max(2.6f, hit.distance - 0.4f);

            desired.y = Mathf.Max(desired.y, focus.y + 0.6f);

            transform.position = MathX.ExpSmooth(transform.position, desired, 6f, dt);
            transform.rotation = Quaternion.LookRotation((focus - transform.position).normalized, Vector3.up);
            if (Camera != null) Camera.fieldOfView = MathX.ExpSmooth(Camera.fieldOfView, 56f, 3f, dt);
        }
    }
}
