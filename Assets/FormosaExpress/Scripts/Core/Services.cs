using UnityEngine;

namespace FormosaExpress.Core
{
    /// <summary>
    /// A deliberately small service locator. The game builds itself in one pass at boot, so
    /// wiring by reference is simpler and cheaper than scene lookups, and this keeps those
    /// references discoverable from anywhere without singletons on every class.
    /// </summary>
    public static class Services
    {
        public static Palette Palette;
        public static MaterialLibrary Materials;
        public static InputRouter Input;
        public static SaveData Save;

        public static City.CityModel City;
        public static Gameplay.GameDirector Director;
        public static Gameplay.OrderManager Orders;
        public static Gameplay.ComboSystem Combo;
        public static Gameplay.RouteService Routes;
        public static Gameplay.RivalCourier Rival;
        public static Vehicle.ScooterController Player;
        public static Traffic.TrafficSystem Traffic;
        public static Traffic.PedestrianSystem Pedestrians;
        public static Fx.FxDirector Fx;
        public static Fx.ChaseCamera Camera;
        public static Audio.AudioDirector Audio;
        public static UI.HudRoot Hud;
        public static UI.ScreenStack Screens;

        public static bool Ready { get; internal set; }

        public static void Reset()
        {
            Ready = false;
            Palette = null; Materials = null; Input = null; Save = null;
            City = null; Director = null; Orders = null; Combo = null; Routes = null;
            Player = null; Traffic = null; Pedestrians = null; Fx = null; Camera = null;
            Audio = null; Hud = null; Screens = null; Rival = null;
        }

        /// <summary>Convenience for the many systems that only need the player's position.</summary>
        public static Vector3 PlayerPosition => Player != null ? Player.transform.position : Vector3.zero;
    }
}
