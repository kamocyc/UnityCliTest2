using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FormosaExpress.Core
{
    /// <summary>
    /// One place that turns raw devices into the handful of intents the game cares about.
    /// Works with the new Input System, and falls back to the legacy manager when a project
    /// is configured for it, so the gameplay code never sees a #if.
    /// </summary>
    public sealed class InputRouter
    {
        public float Steer { get; private set; }        // -1 .. 1
        public float Throttle { get; private set; }     // 0 .. 1
        public float Brake { get; private set; }        // 0 .. 1
        public bool Drift { get; private set; }
        public bool Boost { get; private set; }
        public bool HornPressed { get; private set; }
        public bool LookBack { get; private set; }
        public bool PausePressed { get; private set; }
        public bool ConfirmPressed { get; private set; }
        public bool CancelPressed { get; private set; }
        public bool RestartPressed { get; private set; }
        public bool CycleCameraPressed { get; private set; }
        public bool CycleTargetPressed { get; private set; }
        public int MenuVertical { get; private set; }   // -1 / 0 / 1, edge triggered
        public int MenuHorizontal { get; private set; }

        // ------------------------------------------------------------------ scripted input
        // Lets automated playtests and the editor tooling drive the game without a device.
        // Inert unless ScriptedActive is set.
        public bool ScriptedActive;
        public float ScriptedThrottle;
        public float ScriptedBrake;
        public float ScriptedSteer;
        public bool ScriptedBoost;
        public bool ScriptedDrift;
        public bool ScriptedConfirm;
        public bool ScriptedPause;
        public int ScriptedMenuVertical;

        float _steerRaw;
        bool _prevMenuUp, _prevMenuDown, _prevMenuLeft, _prevMenuRight;

        public void Poll(float dt)
        {
            float steerTarget = 0f;
            float throttle = 0f;
            float brake = 0f;
            bool drift = false, boost = false, horn = false, lookBack = false;
            bool pause = false, confirm = false, cancel = false, restart = false, cycleCam = false;
            bool cycleTarget = false;
            bool menuUp = false, menuDown = false, menuLeft = false, menuRight = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[Key.A].isPressed || kb[Key.LeftArrow].isPressed) steerTarget -= 1f;
                if (kb[Key.D].isPressed || kb[Key.RightArrow].isPressed) steerTarget += 1f;
                if (kb[Key.W].isPressed || kb[Key.UpArrow].isPressed) throttle = 1f;
                if (kb[Key.S].isPressed || kb[Key.DownArrow].isPressed) brake = 1f;
                drift |= kb[Key.Space].isPressed;
                boost |= kb[Key.LeftShift].isPressed || kb[Key.RightShift].isPressed;
                horn |= kb[Key.E].wasPressedThisFrame;
                lookBack |= kb[Key.Q].isPressed;
                pause |= kb[Key.Escape].wasPressedThisFrame || kb[Key.P].wasPressedThisFrame;
                confirm |= kb[Key.Enter].wasPressedThisFrame || kb[Key.Space].wasPressedThisFrame
                           || kb[Key.NumpadEnter].wasPressedThisFrame;
                cancel |= kb[Key.Backspace].wasPressedThisFrame;
                restart |= kb[Key.R].wasPressedThisFrame;
                cycleCam |= kb[Key.C].wasPressedThisFrame;
                cycleTarget |= kb[Key.Tab].wasPressedThisFrame;
                menuUp |= kb[Key.W].isPressed || kb[Key.UpArrow].isPressed;
                menuDown |= kb[Key.S].isPressed || kb[Key.DownArrow].isPressed;
                menuLeft |= kb[Key.A].isPressed || kb[Key.LeftArrow].isPressed;
                menuRight |= kb[Key.D].isPressed || kb[Key.RightArrow].isPressed;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (Mathf.Abs(stick.x) > 0.15f) steerTarget += stick.x;
                throttle = Mathf.Max(throttle, pad.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, pad.leftTrigger.ReadValue());
                drift |= pad.buttonEast.isPressed || pad.rightShoulder.isPressed;
                boost |= pad.buttonSouth.isPressed || pad.leftShoulder.isPressed;
                horn |= pad.buttonNorth.wasPressedThisFrame;
                lookBack |= pad.rightStickButton.isPressed;
                pause |= pad.startButton.wasPressedThisFrame;
                confirm |= pad.buttonSouth.wasPressedThisFrame;
                cancel |= pad.buttonEast.wasPressedThisFrame;
                cycleCam |= pad.selectButton.wasPressedThisFrame;
                cycleTarget |= pad.buttonWest.wasPressedThisFrame;
                menuUp |= stick.y > 0.5f || pad.dpad.up.isPressed;
                menuDown |= stick.y < -0.5f || pad.dpad.down.isPressed;
                menuLeft |= stick.x < -0.5f || pad.dpad.left.isPressed;
                menuRight |= stick.x > 0.5f || pad.dpad.right.isPressed;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            steerTarget = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            throttle = Mathf.Max(0f, v);
            brake = Mathf.Max(0f, -v);
            drift = Input.GetKey(KeyCode.Space);
            boost = Input.GetKey(KeyCode.LeftShift);
            horn = Input.GetKeyDown(KeyCode.E);
            lookBack = Input.GetKey(KeyCode.Q);
            pause = Input.GetKeyDown(KeyCode.Escape);
            confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
            cancel = Input.GetKeyDown(KeyCode.Backspace);
            restart = Input.GetKeyDown(KeyCode.R);
            cycleCam = Input.GetKeyDown(KeyCode.C);
            cycleTarget = Input.GetKeyDown(KeyCode.Tab);
            menuUp = v > 0.5f; menuDown = v < -0.5f;
            menuLeft = steerTarget < -0.5f; menuRight = steerTarget > 0.5f;
#endif

            if (ScriptedActive)
            {
                steerTarget = Mathf.Clamp(steerTarget + ScriptedSteer, -1f, 1f);
                throttle = Mathf.Max(throttle, ScriptedThrottle);
                brake = Mathf.Max(brake, ScriptedBrake);
                boost |= ScriptedBoost;
                drift |= ScriptedDrift;

                if (ScriptedConfirm) { confirm = true; ScriptedConfirm = false; }
                if (ScriptedPause) { pause = true; ScriptedPause = false; }

                if (ScriptedMenuVertical != 0)
                {
                    if (ScriptedMenuVertical > 0) { menuUp = true; _prevMenuUp = false; }
                    else { menuDown = true; _prevMenuDown = false; }

                    ScriptedMenuVertical = 0;
                }
            }

            steerTarget = Mathf.Clamp(steerTarget, -1f, 1f);

            // A short ramp keeps keyboard steering from feeling like an on/off switch while
            // still snapping back to centre quickly.
            float rate = Mathf.Abs(steerTarget) > 0.01f ? 7.5f : 14f;
            _steerRaw = MathX.ExpSmooth(_steerRaw, steerTarget, rate, dt);
            if (Mathf.Abs(_steerRaw) < 0.002f) _steerRaw = 0f;

            Steer = _steerRaw;
            Throttle = throttle;
            Brake = brake;
            Drift = drift;
            Boost = boost;
            HornPressed = horn;
            LookBack = lookBack;
            PausePressed = pause;
            ConfirmPressed = confirm;
            CancelPressed = cancel;
            RestartPressed = restart;
            CycleCameraPressed = cycleCam;
            CycleTargetPressed = cycleTarget;

            MenuVertical = (menuUp && !_prevMenuUp ? 1 : 0) + (menuDown && !_prevMenuDown ? -1 : 0);
            MenuHorizontal = (menuRight && !_prevMenuRight ? 1 : 0) + (menuLeft && !_prevMenuLeft ? -1 : 0);
            _prevMenuUp = menuUp; _prevMenuDown = menuDown;
            _prevMenuLeft = menuLeft; _prevMenuRight = menuRight;
        }

        /// <summary>Zeroes driving intent, used when the game is paused or between shifts.</summary>
        public void SuppressDriving()
        {
            Steer = 0f; Throttle = 0f; Brake = 0f;
            Drift = false; Boost = false; _steerRaw = 0f;
        }
    }
}
