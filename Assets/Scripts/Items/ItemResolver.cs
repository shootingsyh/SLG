using System.Collections.Generic;
using System.Text;
using SLG.Core;
using SLG.Grid;
using SLG.Units;
using SLG.Saves;

namespace SLG.Items
{
    public static class ItemResolver
    {
        public static bool IsTileInRange(Unit caster, ItemDefinition item, Tile tile)
        {
            if (caster == null || item == null || caster.OccupiedTile == null || tile == null) return false;
            int d = GridPathfinder.GetManhattanDistance(caster.CurrentCoordinate, tile.Coordinate);
            return d >= item.MinimumRange && d <= item.MaximumRange;
        }

        public static bool CanTargetUnit(Unit caster, ItemDefinition item, Unit target, CampaignInventory inventory = null)
        {
            if (caster == null || item == null || target == null || !caster.IsAlive || !target.IsAlive || target.OccupiedTile == null || !IsTileInRange(caster, item, target.OccupiedTile))
                return false;
            if (inventory != null && inventory.GetQuantity(item.ItemId) <= 0) return false;

            if (target == caster)
            {
                if (!item.CanTargetSelf) return false;
            }
            else
            {
                bool isAlly = target.Faction == caster.Faction;
                if (isAlly && !item.CanTargetAllies) return false;
                if (!isAlly && !item.CanTargetEnemies) return false;
            }

            if (item.EffectType == ItemEffectType.Heal)
            {
                if (target.CurrentHealth >= target.MaxHealth) return false;
                // Potion cannot target full health, also check heal amount >0
            }

            return true;
        }

        public static bool CanTargetTile(Unit caster, ItemDefinition item, Tile tile, CampaignInventory inventory = null)
        {
            if (caster == null || item == null || tile == null || !IsTileInRange(caster, item, tile)) return false;
            if (item.TargetType == ItemTargetType.Ground)
                return item.CanTargetEmptyGround || tile.OccupyingUnit != null;
            return tile.OccupyingUnit != null && CanTargetUnit(caster, item, tile.OccupyingUnit, inventory);
        }

        public static int CalculateHealing(Unit target, ItemDefinition item)
        {
            if (item == null || target == null || !target.IsAlive) return 0;
            if (item.EffectType != ItemEffectType.Heal) return 0;
            return System.Math.Min(item.Power, target.MaxHealth - target.CurrentHealth);
        }

        public static int CalculateDamage(ItemDefinition item)
        {
            if (item == null) return 0;
            if (item.EffectType != ItemEffectType.Damage) return 0;
            return System.Math.Max(1, item.Power);
        }

        public static string BuildPreview(Unit caster, ItemDefinition item, Tile targetTile)
        {
            var sb = new StringBuilder(256);
            sb.AppendLine(item.DisplayName);
            if (caster != null) sb.AppendLine($"User: {caster.DisplayName}");
            Unit target = targetTile?.OccupyingUnit;
            if (target == null) { sb.Append("No target"); return sb.ToString(); }
            sb.AppendLine($"Target: {target.DisplayName} HP {target.CurrentHealth}/{target.MaxHealth}");
            if (item.EffectType == ItemEffectType.Heal)
            {
                int h = CalculateHealing(target, item);
                sb.Append($"Heal: {h} -> {target.CurrentHealth + h}/{target.MaxHealth}");
            }
            else if (item.EffectType == ItemEffectType.Damage)
            {
                int d = CalculateDamage(item);
                sb.Append($"Damage: {d} -> {System.Math.Max(0, target.CurrentHealth - d)}/{target.MaxHealth} (no counter)");
            }
            return sb.ToString();
        }

        public static bool Resolve(Unit caster, ItemDefinition item, Unit target)
        {
            if (caster == null || item == null || target == null || !target.IsAlive) return false;
            if (item.EffectType == ItemEffectType.Heal)
            {
                int h = CalculateHealing(target, item);
                if (h <= 0) return false;
                target.ReceiveHealing(h);
                return true;
            }
            else if (item.EffectType == ItemEffectType.Damage)
            {
                int d = CalculateDamage(item);
                target.ReceiveDamage(d);
                return true;
            }
            return false;
        }
    }
}
