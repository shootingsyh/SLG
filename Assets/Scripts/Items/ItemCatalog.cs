using System.Collections.Generic;
using UnityEngine;

namespace SLG.Items
{
    public static class ItemCatalog
    {
        private static readonly Dictionary<string, ItemDefinition> registry = new Dictionary<string, ItemDefinition>();
        private static bool initialized;

        public static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            RegisterDefaults();
        }

        private static void RegisterDefaults()
        {
            Register(CreatePotion());
            Register(CreateLargePotion());
            Register(CreateBomb());
            Register(CreateIronSword());
            Register(CreateIronArmor());
            Register(CreateTravelerCharm());
        }

        private static void Register(ItemDefinition def)
        {
            if (def != null) registry[def.ItemId] = def;
        }

        public static ItemDefinition Get(string itemId)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(itemId)) return null;
            if (registry.TryGetValue(itemId, out var def))
            {
                if (def != null && def.ItemId == itemId) return def;
                registry.Remove(itemId);
            }
            // Key not found or stale, try force re-init once
            if (!registry.ContainsKey(itemId))
            {
                // Check if registry is stale (has 6 entries but not the requested one)
                bool needsRebuild = false;
                foreach (var kv in registry)
                {
                    if (kv.Value == null || kv.Value.ItemId != kv.Key)
                    {
                        needsRebuild = true;
                        break;
                    }
                }
                if (needsRebuild || registry.Count == 0)
                {
                    EnsureInitialized(true);
                    registry.TryGetValue(itemId, out def);
                    return def;
                }
            }
            registry.TryGetValue(itemId, out def);
            return def;
        }

        private static void EnsureInitialized(bool force = false)
        {
            if (initialized && !force) return;
            if (force) { registry.Clear(); initialized = false; }
            if (initialized) return;
            initialized = true;
            RegisterDefaults();
        }

        public static bool Has(string itemId) => Get(itemId) != null;

        public static IEnumerable<ItemDefinition> GetAll()
        {
            EnsureInitialized();
            return registry.Values;
        }

        public static int GetAllCount()
        {
            EnsureInitialized();
            return registry.Count;
        }

        private static ItemDefinition CreatePotion()
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.ConfigureRuntime("potion", "Potion", "Heals 5 HP to an ally.", ItemCategory.Consumable,
                99, true, true,
                ItemEffectType.Heal, ItemTargetType.Unit, 1, 3, 5,
                true, true, false, false,
                0, 0, 0);
            def.name = "potion";
            return def;
        }

        private static ItemDefinition CreateLargePotion()
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.ConfigureRuntime("large-potion", "Large Potion", "Heals 10 HP to an ally.", ItemCategory.Consumable,
                99, true, true,
                ItemEffectType.Heal, ItemTargetType.Unit, 1, 3, 10,
                true, true, false, false,
                0, 0, 0);
            def.name = "large-potion";
            return def;
        }

        private static ItemDefinition CreateBomb()
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.ConfigureRuntime("bomb", "Bomb", "Deals 5 fixed damage to an enemy. No counterattack.", ItemCategory.Consumable,
                99, true, false,
                ItemEffectType.Damage, ItemTargetType.Unit, 1, 3, 5,
                false, false, true, false,
                0, 0, 0);
            def.name = "bomb";
            return def;
        }

        private static ItemDefinition CreateIronSword()
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.ConfigureRuntime("iron-sword", "Iron Sword", "+2 Attack.", ItemCategory.Weapon,
                1, false, true,
                ItemEffectType.None, ItemTargetType.Unit, 0, 0, 0,
                false, false, false, false,
                2, 0, 0);
            def.name = "iron-sword";
            return def;
        }

        private static ItemDefinition CreateIronArmor()
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.ConfigureRuntime("iron-armor", "Iron Armor", "+2 Defense.", ItemCategory.Armor,
                1, false, true,
                ItemEffectType.None, ItemTargetType.Unit, 0, 0, 0,
                false, false, false, false,
                0, 2, 0);
            def.name = "iron-armor";
            return def;
        }

        private static ItemDefinition CreateTravelerCharm()
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.ConfigureRuntime("traveler-charm", "Traveler Charm", "+1 Movement.", ItemCategory.Accessory,
                1, false, true,
                ItemEffectType.None, ItemTargetType.Unit, 0, 0, 0,
                false, false, false, false,
                0, 0, 1);
            def.name = "traveler-charm";
            return def;
        }
    }
}
