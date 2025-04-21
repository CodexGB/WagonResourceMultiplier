using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("TrainWagonLootMultiplier", "Codex", "1.0")]
    [Description("Multiplies loot in ore wagons and fuel crates using prefab matching and proximity+contents.")]
    public class TrainWagonLootMultiplier : RustPlugin
    {
        private Dictionary<string, float> ConfigMultipliers;
        private Dictionary<string, float> PrefabMultiplierMap;

        private readonly Dictionary<string, string> PrefabMap = new()
        {
            { "Wagon Ore Multiplier", "assets/content/vehicles/trains/wagons/trainwagonunloadable.entity.prefab" },
            { "Wagon Fuel Multiplier", "assets/content/vehicles/trains/wagons/trainwagonunloadablefuel.entity.prefab" }
        };

        private readonly HashSet<ulong> ProcessedWagons = new();
        private readonly HashSet<ulong> ProcessedContainers = new();
        private readonly List<BaseEntity> ActiveFuelWagons = new();

        private bool EnableLogging = false;
        private const string LogFilePrefix = "TrainWagonLootMultiplier";

        protected override void LoadDefaultConfig()
        {
            Config["EnableLogging"] = false;
            Config["WagonMultipliers"] = new Dictionary<string, object>
            {
                { "Wagon Ore Multiplier", 2.0 },
                { "Wagon Fuel Multiplier", 3.5 }
            };
        }

        void OnServerInitialized()
        {
            EnableLogging = GetConfig("EnableLogging", false);
            LoadMultipliers();
        }

        private void LoadMultipliers()
        {
            PrefabMultiplierMap = new();
            ConfigMultipliers = new();

            var raw = Config["WagonMultipliers"] as Dictionary<string, object>;
            if (raw == null)
            {
                PrintError("Invalid config format for WagonMultipliers.");
                return;
            }

            foreach (var entry in raw)
            {
                string key = entry.Key.Trim();
                if (!PrefabMap.ContainsKey(key)) continue;

                string prefab = PrefabMap[key].ToLower();
                if (float.TryParse(entry.Value.ToString(), out float multiplier))
                {
                    PrefabMultiplierMap[prefab] = multiplier;
                    ConfigMultipliers[key] = multiplier;
                }
            }

            Config["WagonMultipliers"] = ConfigMultipliers;
            SaveConfig();
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || entity.PrefabName == null) return;
            string prefab = entity.PrefabName.ToLower();

            if (prefab == "assets/content/vehicles/trains/wagons/trainwagonunloadablefuel.entity.prefab")
            {
                var baseEntity = entity as BaseEntity;
                if (baseEntity != null && !ActiveFuelWagons.Contains(baseEntity))
                {
                    ActiveFuelWagons.Add(baseEntity);
                    if (EnableLogging)
                        LogToFile(LogFilePrefix, $"[DEBUG] Tracked fuel wagon at {baseEntity.transform.position}", this);
                }
                return;
            }

            if (PrefabMultiplierMap.TryGetValue(prefab, out float multiplier))
            {
                var baseEntity = entity as BaseEntity;
                if (baseEntity == null) return;

                timer.Once(5f, () => MultiplyOreWagon(baseEntity, multiplier));
            }

            if (entity is StorageContainer container)
            {
                timer.Once(3f, () => TryMatchFuelCrate(container));
            }
        }

        private void MultiplyOreWagon(BaseEntity wagon, float multiplier)
        {
            if (wagon.net?.ID == null || ProcessedWagons.Contains(wagon.net.ID.Value)) return;
            ProcessedWagons.Add(wagon.net.ID.Value);

            var containers = wagon.GetComponentsInChildren<StorageContainer>(true);
            foreach (var container in containers)
            {
                MultiplyItems(container, multiplier, wagon.ShortPrefabName);
            }
        }

        private void TryMatchFuelCrate(StorageContainer container)
        {
            if (container == null || container.net?.ID == null || ProcessedContainers.Contains(container.net.ID.Value)) return;

            bool containsFuel = false;
            foreach (var item in container.inventory?.itemList ?? new List<Item>())
            {
                if (item.info.shortname == "lowgradefuel")
                {
                    containsFuel = true;
                    break;
                }
            }

            if (!containsFuel) return;

            BaseEntity closestWagon = null;
            float closestDist = float.MaxValue;

            foreach (var wagon in ActiveFuelWagons)
            {
                if (wagon == null || wagon.IsDestroyed) continue;

                float dist = Vector3.Distance(container.transform.position, wagon.transform.position);
                if (dist < 30f && dist < closestDist)
                {
                    closestWagon = wagon;
                    closestDist = dist;
                }
            }

            if (closestWagon == null)
            {
                if (EnableLogging)
                    LogToFile(LogFilePrefix, $"[DEBUG] Fuel crate found but no wagon matched. Pos: {container.transform.position}", this);
                return;
            }

            float multiplier = PrefabMultiplierMap["assets/content/vehicles/trains/wagons/trainwagonunloadablefuel.entity.prefab"];
            ProcessedContainers.Add(container.net.ID.Value);
            if (EnableLogging)
                LogToFile(LogFilePrefix, $"[DEBUG] Matched fuel crate to wagon at {closestWagon.transform.position} (dist: {closestDist})", this);

            MultiplyItems(container, multiplier, closestWagon.ShortPrefabName);
        }

        private void MultiplyItems(StorageContainer container, float multiplier, string wagonName)
        {
            if (container?.inventory?.itemList == null) return;

            foreach (var item in container.inventory.itemList)
            {
                object hook = Interface.CallHook("OnMaxStackable", item);
                int maxStack = (hook is int) ? (int)hook : item.info.stackable;

                int original = item.amount;
                item.amount = Mathf.Min(Mathf.CeilToInt(original * multiplier), maxStack);
                item.MarkDirty();

                if (EnableLogging)
                    LogToFile(LogFilePrefix, $"[WAGON] {wagonName} | Item: {item.info.shortname} | {original} → {item.amount}", this);
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
