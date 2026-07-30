using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.Vehicle
{
    /// <summary>One frame of intent for a scooter, whoever or whatever produced it.</summary>
    public struct ScooterInputState
    {
        public float Throttle;
        public float Brake;
        public float Steer;
        public bool Drift;
        public bool Boost;

        public static ScooterInputState Idle => default;

        /// <summary>Coasting to a halt: used between shifts and while paused.</summary>
        public static ScooterInputState Braking => new ScooterInputState { Brake = 1f };
    }

    /// <summary>
    /// Where a <see cref="ScooterController"/> gets its intent. The player and the rival courier
    /// ride identical physics; the only difference between them is which of these is plugged in.
    /// </summary>
    public interface IScooterInputSource
    {
        ScooterInputState Read(ScooterController scooter, float dt);
    }

    /// <summary>Adapts the human's device input for the player's scooter.</summary>
    public sealed class PlayerInputSource : IScooterInputSource
    {
        public ScooterInputState Read(ScooterController scooter, float dt)
        {
            InputRouter router = Services.Input;
            if (router == null) return ScooterInputState.Idle;

            return new ScooterInputState
            {
                Throttle = router.Throttle,
                Brake = router.Brake,
                Steer = router.Steer,
                Drift = router.Drift,
                Boost = router.Boost
            };
        }
    }
}
