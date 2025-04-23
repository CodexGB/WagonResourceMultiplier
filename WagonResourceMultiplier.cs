using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("WagonResourceMultiplier", "Codex", "1.0.2")]
    [Description("Multiplies ore and fuel amounts in train wagons based on config multipliers without modifying Rust's natural loot.")]
    public class WagonResourceMultiplier : RustPlugin
    {
        private Dictionary<string, float> PrefabMultiplierMap;
        private readonly Dictionary<string, string> ConfigToPrefab = new()
        {
            { "Wagon Ore Multiplier", "assets/content/vehicles/trains/wagons/trainwagonunloadable.entity.prefab" },
            { "Wagon Fuel Multiplier", "assets/content/vehicles/trains/wagons/trainwagonunloadablefuel.entity.prefab" }
        };

        private bool EnableLogging = false;
        private bool ClampToStackLimit = true;
        private const string LogFilePrefix = "WagonResourceMultiplier";

        private HashSet<ulong> ProcessedWagons = new HashSet<ulong>();

        protected override void LoadDefaultConfig()
        {
            Config["EnableLogging"] = false;
            Config["ClampToStackLimit"] = true;
            Config["WagonMultipliers"] = new Dictionary<string, object>
            {
                { "Wagon Ore Multiplier", 1.0 },
                { "Wagon Fuel Multiplier", 1.0 }
            };
        }

        void OnServerInitialized()
        {
            EnableLogging = GetConfig("EnableLogging", false);
            ClampToStackLimit = GetConfig("ClampToStackLimit", true);
            LoadMultipliers();
        }

        private void LoadMultipliers()
        {
            PrefabMultiplierMap = new();

            var multipliers = Config["WagonMultipliers"] as Dictionary<string, object>;
            if (multipliers == null)
            {
                PrintError("Invalid config format.");
                return;
            }

            foreach (var entry in ConfigToPrefab)
            {
                string configKey = entry.Key;
                string prefab = entry.Value.ToLower();

                if (multipliers.TryGetValue(configKey, out object raw) &&
                    float.TryParse(raw.ToString(), out float mult))
                {
                    PrefabMultiplierMap[prefab] = mult;
                }
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || entity.PrefabName == null) return;

            string prefab = entity.PrefabName.ToLower();
            if (!PrefabMultiplierMap.TryGetValue(prefab, out float multiplier)) return;

            if (entity is not BaseEntity baseEntity || baseEntity.net?.ID == null) return;
            if (ProcessedWagons.Contains(baseEntity.net.ID.Value)) return;

            ProcessedWagons.Add(baseEntity.net.ID.Value);

            timer.Once(5f, () =>
            {
                if (baseEntity == null || baseEntity.IsDestroyed) return;

                var containers = baseEntity.GetComponentsInChildren<StorageContainer>(true);
                foreach (var container in containers)
                    MultiplyExistingLoot(container, multiplier, baseEntity.ShortPrefabName);
            });
        }

        private void MultiplyExistingLoot(StorageContainer container, float multiplier, string wagonName)
        {
            if (container?.inventory?.itemList == null || container.inventory.itemList.Count == 0)
                return;

            foreach (var item in container.inventory.itemList)
            {
                int original = item.amount;
                int multiplied = Mathf.CeilToInt(original * multiplier);

                object hook = Interface.CallHook("OnMaxStackable", item);
                int maxStack = (hook is int hookValue && hookValue > 0)
                    ? hookValue
                    : item.info.stackable;

                item.amount = ClampToStackLimit
                    ? Mathf.Min(multiplied, maxStack)
                    : multiplied;

                item.MarkDirty();

                if (EnableLogging)
                    LogToFile(LogFilePrefix, $"[WAGON] {wagonName} | Item: {item.info.shortname} | Vanilla: {original} → {item.amount} (x{multiplier})", this);
            }

            container.inventory.MarkDirty();
            container.SendNetworkUpdate();
        }

        private T GetConfig<T>(string key, T defaultValue)
        {
            if (Config[key] is T value) return value;
            return defaultValue;
        }
    }
}
