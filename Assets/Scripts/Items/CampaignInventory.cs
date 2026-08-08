using System;
using System.Collections.Generic;
using SLG.Saves;

namespace SLG.Items
{
    [Serializable]
    public sealed class CampaignInventory
    {
        private readonly Dictionary<string, int> quantities = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> Quantities => quantities;

        public int GetQuantity(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            return quantities.TryGetValue(itemId, out int q) ? q : 0;
        }

        public bool Has(string itemId, int amount = 1)
        {
            return GetQuantity(itemId) >= amount;
        }

        public bool Add(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
            var def = ItemCatalog.Get(itemId);
            if (def == null) return false;
            int cur = GetQuantity(itemId);
            int next = cur + amount;
            if (next > 9999) next = 9999;
            quantities[itemId] = next;
            return true;
        }

        public bool Remove(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
            int cur = GetQuantity(itemId);
            if (cur < amount) return false;
            int next = cur - amount;
            if (next <= 0) quantities.Remove(itemId);
            else quantities[itemId] = next;
            return true;
        }

        public void Clear() => quantities.Clear();

        public List<CampaignInventoryEntry> ToEntries()
        {
            var list = new List<CampaignInventoryEntry>();
            foreach (var kv in quantities)
            {
                if (kv.Value > 0) list.Add(new CampaignInventoryEntry { ItemId = kv.Key, Quantity = kv.Value });
            }
            list.Sort((a,b) => string.CompareOrdinal(a.ItemId, b.ItemId));
            return list;
        }

        public void FromEntries(IEnumerable<CampaignInventoryEntry> entries)
        {
            quantities.Clear();
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.ItemId) || e.Quantity <= 0) continue;
                if (ItemCatalog.Get(e.ItemId) == null) continue;
                quantities[e.ItemId] = e.Quantity;
            }
        }

        public CampaignInventory Clone()
        {
            var c = new CampaignInventory();
            foreach (var kv in quantities) c.quantities[kv.Key] = kv.Value;
            return c;
        }

        public bool Equals(CampaignInventory other)
        {
            if (other == null) return false;
            if (quantities.Count != other.quantities.Count) return false;
            foreach (var kv in quantities)
            {
                if (!other.quantities.TryGetValue(kv.Key, out int v) || v != kv.Value) return false;
            }
            return true;
        }
    }

    public enum EquipmentSlot
    {
        Weapon,
        Armor,
        Accessory
    }

    [Serializable]
    public sealed class CampaignEquipmentEntry
    {
        public string UnitId;
        public string WeaponId = string.Empty;
        public string ArmorId = string.Empty;
        public string AccessoryId = string.Empty;
    }

    public sealed class CampaignEquipment
    {
        private readonly Dictionary<string, CampaignEquipmentEntry> map = new Dictionary<string, CampaignEquipmentEntry>();

        public IReadOnlyDictionary<string, CampaignEquipmentEntry> Entries => map;

        public CampaignEquipmentEntry GetOrCreate(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            if (!map.TryGetValue(unitId, out var e))
            {
                e = new CampaignEquipmentEntry { UnitId = unitId };
                map[unitId] = e;
            }
            return e;
        }

        public string GetEquipped(string unitId, EquipmentSlot slot)
        {
            if (!map.TryGetValue(unitId, out var e)) return string.Empty;
            return slot switch
            {
                EquipmentSlot.Weapon => e.WeaponId ?? string.Empty,
                EquipmentSlot.Armor => e.ArmorId ?? string.Empty,
                EquipmentSlot.Accessory => e.AccessoryId ?? string.Empty,
                _ => string.Empty
            };
        }

        public bool CanEquip(string unitId, string itemId, CampaignInventory inventory)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(itemId)) return false;
            var def = ItemCatalog.Get(itemId);
            if (def == null || def.IsConsumable) return false;
            var slot = SlotFor(def.Category);
            if (slot == null) return false;
            if (!inventory.Has(itemId)) return false;
            // quantity check: equipped count <= owned quantity
            int owned = inventory.GetQuantity(itemId);
            int equipped = CountEquipped(itemId);
            string currentlyEquipped = GetEquipped(unitId, slot.Value);
            if (currentlyEquipped == itemId) return true; // already equipped
            if (equipped >= owned) return false;
            return true;
        }

        public bool Equip(string unitId, string itemId, CampaignInventory inventory)
        {
            if (!CanEquip(unitId, itemId, inventory)) return false;
            var def = ItemCatalog.Get(itemId);
            var slot = SlotFor(def.Category);
            if (slot == null) return false;
            var entry = GetOrCreate(unitId);
            switch (slot.Value)
            {
                case EquipmentSlot.Weapon: entry.WeaponId = itemId; break;
                case EquipmentSlot.Armor: entry.ArmorId = itemId; break;
                case EquipmentSlot.Accessory: entry.AccessoryId = itemId; break;
            }
            return true;
        }

        public bool Unequip(string unitId, EquipmentSlot slot)
        {
            if (!map.TryGetValue(unitId, out var e)) return false;
            bool had = false;
            switch (slot)
            {
                case EquipmentSlot.Weapon: had = !string.IsNullOrEmpty(e.WeaponId); e.WeaponId = string.Empty; break;
                case EquipmentSlot.Armor: had = !string.IsNullOrEmpty(e.ArmorId); e.ArmorId = string.Empty; break;
                case EquipmentSlot.Accessory: had = !string.IsNullOrEmpty(e.AccessoryId); e.AccessoryId = string.Empty; break;
            }
            return had;
        }

        public bool Swap(string unitId, EquipmentSlot slot, string newItemId, CampaignInventory inventory)
        {
            // Unequip old, equip new; ensure ownership
            string old = GetEquipped(unitId, slot);
            if (string.IsNullOrEmpty(newItemId))
                return Unequip(unitId, slot);
            // Temporarily unequip old to free quantity
            Unequip(unitId, slot);
            if (!Equip(unitId, newItemId, inventory))
            {
                // restore old if failed
                if (!string.IsNullOrEmpty(old)) Equip(unitId, old, inventory);
                return false;
            }
            return true;
        }

        public int CountEquipped(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int c = 0;
            foreach (var kv in map)
            {
                if (kv.Value.WeaponId == itemId) c++;
                if (kv.Value.ArmorId == itemId) c++;
                if (kv.Value.AccessoryId == itemId) c++;
            }
            return c;
        }

        public List<CampaignEquipmentEntry> ToEntries()
        {
            var list = new List<CampaignEquipmentEntry>();
            foreach (var kv in map)
            {
                var e = kv.Value;
                if (!string.IsNullOrEmpty(e.WeaponId) || !string.IsNullOrEmpty(e.ArmorId) || !string.IsNullOrEmpty(e.AccessoryId))
                    list.Add(new CampaignEquipmentEntry { UnitId = e.UnitId, WeaponId = e.WeaponId ?? string.Empty, ArmorId = e.ArmorId ?? string.Empty, AccessoryId = e.AccessoryId ?? string.Empty });
            }
            list.Sort((a,b) => string.CompareOrdinal(a.UnitId, b.UnitId));
            return list;
        }

        public void FromEntries(IEnumerable<CampaignEquipmentEntry> entries, CampaignInventory inventory)
        {
            map.Clear();
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.UnitId)) continue;
                // Validate each equipped item exists and owned
                var copy = new CampaignEquipmentEntry { UnitId = e.UnitId, WeaponId = string.Empty, ArmorId = string.Empty, AccessoryId = string.Empty };
                if (!string.IsNullOrEmpty(e.WeaponId))
                {
                    var d = ItemCatalog.Get(e.WeaponId);
                    if (d != null && d.Category == ItemCategory.Weapon && inventory.Has(e.WeaponId))
                        copy.WeaponId = e.WeaponId;
                }
                if (!string.IsNullOrEmpty(e.ArmorId))
                {
                    var d = ItemCatalog.Get(e.ArmorId);
                    if (d != null && d.Category == ItemCategory.Armor && inventory.Has(e.ArmorId))
                        copy.ArmorId = e.ArmorId;
                }
                if (!string.IsNullOrEmpty(e.AccessoryId))
                {
                    var d = ItemCatalog.Get(e.AccessoryId);
                    if (d != null && d.Category == ItemCategory.Accessory && inventory.Has(e.AccessoryId))
                        copy.AccessoryId = e.AccessoryId;
                }
                // Enforce quantity: if duplicate exceeds owned, drop extras
                // Simple: count and if exceeds, clear that slot
                map[e.UnitId] = copy;
            }
            // Second pass: enforce quantity limits globally
            var counts = new Dictionary<string,int>();
            foreach (var kv in new List<CampaignEquipmentEntry>(map.Values))
            {
                foreach (var id in new[] { kv.WeaponId, kv.ArmorId, kv.AccessoryId })
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    counts.TryGetValue(id, out int c);
                    c++;
                    counts[id] = c;
                    int owned = inventory.GetQuantity(id);
                    if (c > owned)
                    {
                        // remove this occurrence
                        if (kv.WeaponId == id) kv.WeaponId = string.Empty;
                        else if (kv.ArmorId == id) kv.ArmorId = string.Empty;
                        else if (kv.AccessoryId == id) kv.AccessoryId = string.Empty;
                        counts[id]--;
                    }
                }
            }
        }

        public CampaignEquipment Clone()
        {
            var c = new CampaignEquipment();
            foreach (var kv in map)
                c.map[kv.Key] = new CampaignEquipmentEntry { UnitId = kv.Value.UnitId, WeaponId = kv.Value.WeaponId, ArmorId = kv.Value.ArmorId, AccessoryId = kv.Value.AccessoryId };
            return c;
        }

        public static EquipmentSlot? SlotFor(ItemCategory cat)
        {
            return cat switch
            {
                ItemCategory.Weapon => EquipmentSlot.Weapon,
                ItemCategory.Armor => EquipmentSlot.Armor,
                ItemCategory.Accessory => EquipmentSlot.Accessory,
                _ => null
            };
        }
    }
}
