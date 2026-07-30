using UnityEngine;

namespace FormosaExpress.Core
{
    /// <summary>
    /// Single source of truth for every gameplay number. Kept as one file on purpose:
    /// balancing a delivery game means moving these values against each other, and that is
    /// much easier when they sit side by side.
    /// </summary>
    public static class Tuning
    {
        // ------------------------------------------------------------------ layers
        public const string LayerGround = "Ground";
        public const string LayerBuilding = "Building";
        public const string LayerTraffic = "Traffic";
        public const string LayerPedestrian = "Pedestrian";
        public const string LayerPlayer = "Player";
        public const string LayerTrigger = "DeliveryZone";
        public const string LayerProp = "Prop";

        // ------------------------------------------------------------------ city
        public const float CellSize = 62f;          // intersection spacing
        public const float RoadHalfWidth = 7.0f;    // asphalt half-width
        public const float LaneOffset = 3.4f;       // lane centre offset from road centre
        public const float SidewalkWidth = 3.4f;
        public const float CurbHeight = 0.16f;
        public const int GridX = 8;
        public const int GridZ = 8;
        public const float AlleyHalfWidth = 2.6f;

        public static float WorldSizeX => (GridX - 1) * CellSize;
        public static float WorldSizeZ => (GridZ - 1) * CellSize;

        // ------------------------------------------------------------------ scooter
        public const float BaseTopSpeed = 21.5f;        // m/s  (~77 km/h)
        public const float BaseAcceleration = 13.5f;
        public const float BaseBrakeForce = 26f;
        public const float ReverseSpeed = 4.5f;
        public const float BaseSteerAtRest = 115f;      // deg/s
        public const float BaseSteerAtTop = 42f;        // deg/s at top speed
        public const float BaseGrip = 9.5f;             // lateral friction
        public const float DriftGrip = 2.1f;
        public const float DriftSteerBoost = 1.55f;
        public const float BoostSpeedMultiplier = 1.42f;
        public const float BoostAccelMultiplier = 2.1f;
        public const float Gravity = 26f;               // heavier than real gravity: arcade snap
        public const float SuspensionRestLength = 0.52f;
        public const float SuspensionStrength = 190f;
        public const float SuspensionDamping = 16f;
        public const float LeanMaxDegrees = 30f;
        public const float MaxAirTiltDegrees = 22f;
        public const float CoyoteThrottleAir = 0.25f;   // how much air control the rider keeps

        // ------------------------------------------------------------------ boost / adrenaline
        public const float AdrenalineMax = 100f;
        public const float AdrenalineNearMiss = 13f;
        public const float AdrenalineDriftPerSecond = 17f;
        public const float AdrenalineAirPerSecond = 30f;
        public const float AdrenalineBoostDrain = 33f;   // per second while boosting
        public const float AdrenalineMinToStart = 18f;
        public const float NearMissRadius = 2.35f;
        public const float NearMissMinSpeed = 9f;
        public const float NearMissCooldown = 0.45f;

        // ------------------------------------------------------------------ combo
        public const float ComboWindow = 3.4f;          // seconds of grace before decay
        public const int ComboMaxStep = 7;              // x1 .. x8
        public const int ComboPointsPerStep = 60;
        public static readonly float[] ComboMultipliers = { 1f, 1.25f, 1.5f, 2f, 2.5f, 3f, 4f, 5f };

        // ------------------------------------------------------------------ cargo
        public const float CargoMax = 100f;
        public const float CargoLandingDamagePerUnit = 0.55f; // per m/s of impact above threshold
        public const float CargoLandingThreshold = 9f;
        public const float CargoTiltDamagePerSecond = 6f;     // while leaned hard with cargo
        public const float CargoRecoverPerSecond = 1.1f;      // gentle riding settles the food

        // ------------------------------------------------------------------ orders
        // Generous enough to ride through anywhere in the near lane; the speed gate below is what
        // supplies the challenge, not pixel-perfect positioning.
        public const float PickupRadius = 7.5f;
        public const float DropoffRadius = 8.0f;
        public const float PickupMaxSpeed = 13.0f;      // must slow down to grab an order
        public const float OrderTimePerMetre = 0.115f;  // generous-but-tightening budget
        public const float OrderTimeFloor = 26f;
        public const int OrderBaseFare = 45;
        public const int OrderFarePerMetre = 1;         // + this much per 10 m, see OrderManager
        public const float ExpiredOrderPenaltyFraction = 0.5f;

        // Payout multipliers by cargo condition.
        public const float CargoPerfectThreshold = 92f;
        public const float CargoGoodThreshold = 68f;
        public const float CargoMessyThreshold = 32f;
        public const float PayoutPerfect = 1.25f;
        public const float PayoutGood = 1.0f;
        public const float PayoutMessy = 0.6f;
        public const float PayoutRuined = 0.3f;

        // ------------------------------------------------------------------ shift / progression
        public const float ShiftBaseDuration = 210f;
        public const float ShiftDurationPerLevel = 9f;
        // A first shift is comfortably four or five deliveries; the growth curve then outpaces
        // the extra time you are given, so combos and upgrades have to make up the difference.
        public const int QuotaBase = 480;
        public const float QuotaGrowth = 1.18f;
        public const int MaxSimultaneousOffers = 4;

        // Extra "metres" charged per degree of direction change at a junction, so route-finding
        // breaks ties toward the straighter street instead of a zig-zag of equal length.
        public const float RouteTurnPenaltyPerDegree = 0.35f;

        public static float ShiftDuration(int level) => ShiftBaseDuration + ShiftDurationPerLevel * Mathf.Min(level - 1, 8);
        public static int ShiftQuota(int level) => Mathf.RoundToInt(QuotaBase * Mathf.Pow(QuotaGrowth, level - 1));

        /// <summary>Traffic ramps up with level so the city feels progressively hostile.</summary>
        public static int TrafficCount(int level) => Mathf.Clamp(70 + level * 8, 70, 150);
        public static int PedestrianCount(int level) => Mathf.Clamp(70 + level * 6, 70, 140);

        /// <summary>
        /// -1 = bright midday, 0 = golden-hour dusk, 1 = full night. Early shifts start in
        /// daylight; the city only turns properly neon-lit by the later levels.
        /// </summary>
        public static float NightFactor(int level) => Mathf.Clamp((level - 4) / 3f, -1f, 1f);

        // ------------------------------------------------------------------ rival race
        public const float RaceDuration = 260f;
        public const int RaceTargetDeliveries = 5;

        /// <summary>Head start, in seconds, before the rival is allowed to move.</summary>
        public const float RaceRivalHandicap = 2.0f;

        /// <summary>How close the rival gets to the player's stock performance, by level.</summary>
        public static float RivalSpeedFactor(int level) => Mathf.Lerp(0.86f, 1.14f, Mathf.InverseLerp(1f, 9f, level));
        public static float RivalGripFactor(int level) => Mathf.Lerp(0.90f, 1.15f, Mathf.InverseLerp(1f, 9f, level));

        /// <summary>Chance per second that the rival lifts off and loses a little time.</summary>
        public static float RivalMistakeRate(int level) => Mathf.Lerp(0.11f, 0.02f, Mathf.InverseLerp(1f, 9f, level));

        /// <summary>Seconds the rival dawdles at a shop before setting off again.</summary>
        public static float RivalHandlingDelay(int level) => Mathf.Lerp(1.5f, 0.4f, Mathf.InverseLerp(1f, 9f, level));

        public static readonly string[] RivalNames =
        {
            "KUAI-KUAI EXPRESS", "LIGHTNING LU", "TURBO TSAI", "MIDNIGHT MA",
            "NEON NINJA", "TYPHOON YANG", "GHOST RIDER HO"
        };

        // ------------------------------------------------------------------ scoring
        public const int ScoreNearMiss = 25;
        public const int ScoreDriftPerSecond = 40;
        public const int ScoreAirPerSecond = 90;
        public const int ScoreDeliveryBase = 300;
        public const int ScorePerSecondSaved = 12;
        public const int ScorePenaltyCrash = 60;

        // ------------------------------------------------------------------ camera
        public const float CamDistance = 5.7f;
        public const float CamHeight = 2.35f;
        public const float CamLookHeight = 1.85f;
        public const float CamFovBase = 62f;
        public const float CamFovAtTop = 74f;
        public const float CamFovBoost = 82f;
        public const float CamFollowSpeed = 7.5f;
        public const float CamYawSpeed = 5.2f;

        // ------------------------------------------------------------------ upgrades
        public const int UpgradeMaxLevel = 5;

        public static int UpgradeCost(int currentLevel) => 180 + currentLevel * currentLevel * 220;

        public static float EngineTopSpeedBonus(int lvl) => 1f + 0.075f * lvl;
        public static float EngineAccelBonus(int lvl) => 1f + 0.10f * lvl;
        public static float TyreGripBonus(int lvl) => 1f + 0.11f * lvl;
        public static float TyreBrakeBonus(int lvl) => 1f + 0.13f * lvl;
        public static float SuspensionDamageResist(int lvl) => 1f - 0.13f * lvl;
        public static int BagCapacity(int lvl) => 1 + lvl;                       // 1 .. 6 orders
        public static float BagInsulation(int lvl) => 1f - 0.10f * lvl;          // cargo damage taken
        public static float TankCapacity(int lvl) => 1f + 0.16f * lvl;           // adrenaline pool
    }

    /// <summary>The art direction, in one place.</summary>
    public static class Art
    {
        public static readonly Color[] BuildingWalls =
        {
            new Color(0.78f, 0.62f, 0.47f), // warm plaster
            new Color(0.55f, 0.63f, 0.58f), // faded jade
            new Color(0.85f, 0.79f, 0.66f), // cream tile
            new Color(0.66f, 0.44f, 0.38f), // terracotta
            new Color(0.47f, 0.53f, 0.62f), // grey blue
            new Color(0.80f, 0.70f, 0.54f), // sand
            new Color(0.58f, 0.49f, 0.46f), // weathered brown
            new Color(0.72f, 0.55f, 0.55f), // dusty rose
            new Color(0.50f, 0.58f, 0.53f), // olive concrete
            new Color(0.86f, 0.68f, 0.50f)  // apricot
        };

        public static readonly Color[] AwningColours =
        {
            new Color(0.85f, 0.24f, 0.22f),
            new Color(0.12f, 0.42f, 0.34f),
            new Color(0.92f, 0.72f, 0.20f),
            new Color(0.18f, 0.34f, 0.58f),
            new Color(0.90f, 0.90f, 0.86f),
            new Color(0.62f, 0.20f, 0.40f)
        };

        public static readonly Color[] NeonColours =
        {
            new Color(1.00f, 0.24f, 0.28f), // red
            new Color(1.00f, 0.55f, 0.10f), // orange
            new Color(1.00f, 0.86f, 0.28f), // amber
            new Color(0.28f, 1.00f, 0.68f), // mint
            new Color(0.30f, 0.78f, 1.00f), // cyan
            new Color(0.86f, 0.36f, 1.00f), // magenta
            new Color(1.00f, 0.95f, 0.85f)  // white
        };

        public static readonly Color[] CarColours =
        {
            new Color(0.82f, 0.20f, 0.18f),
            new Color(0.92f, 0.92f, 0.90f),
            new Color(0.16f, 0.18f, 0.22f),
            new Color(0.36f, 0.44f, 0.56f),
            new Color(0.60f, 0.62f, 0.64f),
            new Color(0.20f, 0.42f, 0.32f),
            new Color(0.88f, 0.72f, 0.24f),
            new Color(0.44f, 0.26f, 0.52f)
        };

        public static readonly Color[] ClothColours =
        {
            new Color(0.86f, 0.32f, 0.34f),
            new Color(0.26f, 0.44f, 0.72f),
            new Color(0.94f, 0.82f, 0.44f),
            new Color(0.34f, 0.62f, 0.46f),
            new Color(0.82f, 0.52f, 0.30f),
            new Color(0.72f, 0.74f, 0.78f),
            new Color(0.52f, 0.32f, 0.60f),
            new Color(0.90f, 0.62f, 0.68f)
        };

        public static readonly Color[] SkinTones =
        {
            new Color(0.94f, 0.80f, 0.68f),
            new Color(0.86f, 0.68f, 0.54f),
            new Color(0.72f, 0.54f, 0.40f),
            new Color(0.56f, 0.40f, 0.30f)
        };

        public static readonly Color Asphalt = new Color(0.185f, 0.180f, 0.196f);
        public static readonly Color AsphaltWorn = new Color(0.225f, 0.218f, 0.232f);
        public static readonly Color RoadPaint = new Color(0.92f, 0.90f, 0.84f);
        public static readonly Color RoadPaintYellow = new Color(0.95f, 0.78f, 0.24f);
        public static readonly Color Sidewalk = new Color(0.52f, 0.50f, 0.49f);
        public static readonly Color SidewalkEdge = new Color(0.62f, 0.60f, 0.58f);
        public static readonly Color WindowLit = new Color(1.00f, 0.86f, 0.58f);
        public static readonly Color WindowDark = new Color(0.16f, 0.19f, 0.24f);
        public static readonly Color BeaconGreen = new Color(0.24f, 1.00f, 0.52f);
        public static readonly Color BeaconAmber = new Color(1.00f, 0.72f, 0.18f);

        public static readonly Color PlayerOrange = new Color(0.96f, 0.42f, 0.10f);
        public static readonly Color PlayerBagBlue = new Color(0.15f, 0.42f, 0.78f);
        public static readonly Color RiderGreen = new Color(0.16f, 0.46f, 0.28f);

        // The rival reads as the opposite of the player at a glance: cold body, hot cargo box.
        public static readonly Color RivalPurple = new Color(0.42f, 0.16f, 0.62f);
        public static readonly Color RivalBagRed = new Color(0.84f, 0.16f, 0.22f);
        public static readonly Color RivalRider = new Color(0.10f, 0.12f, 0.18f);
        public static readonly Color RivalTint = new Color(1.00f, 0.34f, 0.42f);

        // HUD
        public static readonly Color HudPanel = new Color(0.07f, 0.07f, 0.09f, 0.80f);
        public static readonly Color HudPanelSolid = new Color(0.10f, 0.10f, 0.13f, 0.94f);
        public static readonly Color HudText = new Color(0.96f, 0.96f, 0.94f);
        public static readonly Color HudGold = new Color(1.00f, 0.82f, 0.28f);
        public static readonly Color HudGreen = new Color(0.36f, 0.95f, 0.52f);
        public static readonly Color HudRed = new Color(1.00f, 0.36f, 0.33f);
        public static readonly Color HudDim = new Color(0.68f, 0.68f, 0.72f);
        public static readonly Color HudCyan = new Color(0.42f, 0.86f, 1.00f);

        // Sky gradients, indexed by time of day: Noon (-1) -> Day/dusk golden hour (0) -> Night (1).
        public static readonly Color SkyNoonZenith = new Color(0.25f, 0.55f, 0.94f);
        public static readonly Color SkyNoonHorizon = new Color(0.74f, 0.84f, 0.94f);
        public static readonly Color SkyDayZenith = new Color(0.30f, 0.48f, 0.78f);
        public static readonly Color SkyDayHorizon = new Color(1.00f, 0.62f, 0.36f);
        public static readonly Color SkyNightZenith = new Color(0.05f, 0.06f, 0.14f);
        public static readonly Color SkyNightHorizon = new Color(0.32f, 0.16f, 0.24f);

        public static readonly Color SunNoon = new Color(1.00f, 0.98f, 0.93f);
        public static readonly Color SunDay = new Color(1.00f, 0.78f, 0.55f);
        public static readonly Color SunNight = new Color(0.42f, 0.48f, 0.78f);
        public static readonly Color AmbientNoon = new Color(0.54f, 0.57f, 0.62f);
        public static readonly Color AmbientDay = new Color(0.40f, 0.42f, 0.52f);
        public static readonly Color AmbientNight = new Color(0.13f, 0.14f, 0.22f);
    }
}
