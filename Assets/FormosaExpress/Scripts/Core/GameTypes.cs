using System;
using UnityEngine;

namespace FormosaExpress.Core
{
    public enum GamePhase
    {
        Boot,
        Title,
        Briefing,
        Riding,
        Paused,
        ShiftResult,
        Garage
    }

    public enum CargoCondition
    {
        Perfect,
        Good,
        Messy,
        Ruined
    }

    public enum UpgradeKind
    {
        Engine,
        Tyres,
        Suspension,
        Bag,
        Tank
    }

    public enum GameMode
    {
        /// <summary>Shift after shift against the clock and a cash quota.</summary>
        Career,

        /// <summary>Head to head with a rival courier over the same pool of orders.</summary>
        RivalRace
    }

    /// <summary>Who is holding, or has claimed, an order.</summary>
    public enum Courier
    {
        None,
        Player,
        Rival
    }

    public enum ComboEventKind
    {
        NearMiss,
        Drift,
        Airtime,
        Delivery,
        Crash
    }

    /// <summary>Everything about one food order.</summary>
    public sealed class Order
    {
        public int Id;
        public string ShopName;
        public string DishName;
        public string CustomerName;
        public int ShopSiteIndex;
        public int CustomerSiteIndex;
        public Vector3 PickupPosition;
        public Vector3 DropPosition;

        public float TimeLimit;
        public float TimeRemaining;
        public int BaseFare;
        public float RouteDistance;

        public bool PickedUp;
        public bool Delivered;
        public bool Expired;

        /// <summary>
        /// Who is carrying this order. In career mode this is always the player; in a rival race
        /// it is whoever reached the shop first, and only they can complete it.
        /// </summary>
        public Courier CarriedBy = Courier.None;

        /// <summary>Cargo integrity is tracked per order, from the moment it is picked up.</summary>
        public float Condition = Tuning.CargoMax;

        public Color Tint = Color.white;

        public Vector3 ActiveTarget => PickedUp ? DropPosition : PickupPosition;
        public float Urgency => TimeLimit <= 0f ? 0f : 1f - Mathf.Clamp01(TimeRemaining / TimeLimit);

        public CargoCondition ConditionTier
        {
            get
            {
                if (Condition >= Tuning.CargoPerfectThreshold) return CargoCondition.Perfect;
                if (Condition >= Tuning.CargoGoodThreshold) return CargoCondition.Good;
                if (Condition >= Tuning.CargoMessyThreshold) return CargoCondition.Messy;
                return CargoCondition.Ruined;
            }
        }

        public float ConditionPayoutMultiplier
        {
            get
            {
                switch (ConditionTier)
                {
                    case CargoCondition.Perfect: return Tuning.PayoutPerfect;
                    case CargoCondition.Good: return Tuning.PayoutGood;
                    case CargoCondition.Messy: return Tuning.PayoutMessy;
                    default: return Tuning.PayoutRuined;
                }
            }
        }
    }

    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int money;
        public int highestLevelUnlocked = 1;
        public int bestScore;
        public int totalDeliveries;
        public int perfectDeliveries;
        public float bestShiftEarnings;

        public int engineLevel;
        public int tyreLevel;
        public int suspensionLevel;
        public int bagLevel;
        public int tankLevel;

        public bool tutorialSeen;
        public int racesWon;
        public int racesLost;

        public int GetUpgrade(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Engine: return engineLevel;
                case UpgradeKind.Tyres: return tyreLevel;
                case UpgradeKind.Suspension: return suspensionLevel;
                case UpgradeKind.Bag: return bagLevel;
                default: return tankLevel;
            }
        }

        public void SetUpgrade(UpgradeKind kind, int value)
        {
            switch (kind)
            {
                case UpgradeKind.Engine: engineLevel = value; break;
                case UpgradeKind.Tyres: tyreLevel = value; break;
                case UpgradeKind.Suspension: suspensionLevel = value; break;
                case UpgradeKind.Bag: bagLevel = value; break;
                default: tankLevel = value; break;
            }
        }
    }

    /// <summary>Results of one shift, handed to the results screen.</summary>
    public struct ShiftReport
    {
        public GameMode Mode;
        public int Level;
        public int Earnings;
        public int Quota;
        public int Score;
        public int Delivered;
        public int Expired;
        public int PerfectDeliveries;
        public int BestCombo;
        public float TopSpeedKmh;
        public int NearMisses;
        public bool QuotaMet;

        // Race only.
        public int RivalDelivered;
        public int RivalEarnings;
        public int RaceTarget;
        public bool RaceWon;
        public string RivalName;
    }

    /// <summary>Derived scooter performance after upgrades are applied.</summary>
    public struct VehicleStats
    {
        public float TopSpeed;
        public float Acceleration;
        public float BrakeForce;
        public float Grip;
        public float DamageResist;
        public int BagCapacity;
        public float CargoResist;
        public float AdrenalineCapacity;

        public static VehicleStats From(SaveData save)
        {
            return new VehicleStats
            {
                TopSpeed = Tuning.BaseTopSpeed * Tuning.EngineTopSpeedBonus(save.engineLevel),
                Acceleration = Tuning.BaseAcceleration * Tuning.EngineAccelBonus(save.engineLevel),
                BrakeForce = Tuning.BaseBrakeForce * Tuning.TyreBrakeBonus(save.tyreLevel),
                Grip = Tuning.BaseGrip * Tuning.TyreGripBonus(save.tyreLevel),
                DamageResist = Tuning.SuspensionDamageResist(save.suspensionLevel),
                BagCapacity = Tuning.BagCapacity(save.bagLevel),
                CargoResist = Tuning.BagInsulation(save.bagLevel),
                AdrenalineCapacity = Tuning.AdrenalineMax * Tuning.TankCapacity(save.tankLevel)
            };
        }
    }
}
