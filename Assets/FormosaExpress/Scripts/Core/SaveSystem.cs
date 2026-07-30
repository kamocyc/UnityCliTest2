using System;
using System.IO;
using UnityEngine;

namespace FormosaExpress.Core
{
    /// <summary>JSON persistence for the meta game (money, upgrades, records).</summary>
    public static class SaveSystem
    {
        const string FileName = "formosa_express_save.json";

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(Path))
                {
                    string json = File.ReadAllText(Path);
                    var data = JsonUtility.FromJson<SaveData>(json);
                    if (data != null) return Sanitise(data);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FormosaExpress] Could not read save file: {e.Message}");
            }

            return new SaveData();
        }

        public static void Save(SaveData data)
        {
            if (data == null) return;
            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(Sanitise(data), true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FormosaExpress] Could not write save file: {e.Message}");
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FormosaExpress] Could not delete save file: {e.Message}");
            }
        }

        static SaveData Sanitise(SaveData d)
        {
            d.money = Mathf.Max(0, d.money);
            d.highestLevelUnlocked = Mathf.Clamp(d.highestLevelUnlocked, 1, 99);
            foreach (UpgradeKind kind in (UpgradeKind[])Enum.GetValues(typeof(UpgradeKind)))
                d.SetUpgrade(kind, Mathf.Clamp(d.GetUpgrade(kind), 0, Tuning.UpgradeMaxLevel));
            return d;
        }
    }
}
