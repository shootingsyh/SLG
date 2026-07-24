using System.Collections.Generic;
using System.Text;
using SLG.Core;
using SLG.Grid;
using SLG.Units;

namespace SLG.Skills
{
    public static class SkillResolver
    {
        public static int CalculateDamage(Unit caster, SkillDefinition skill, Unit target)
        {
            if (caster == null || skill == null || target == null)
            {
                return 0;
            }

            return System.Math.Max(1, caster.AttackPower + skill.Power - CombatResolver.GetEffectiveDefense(target));
        }

        public static int CalculateRawHealing(Unit caster, SkillDefinition skill)
        {
            return caster != null && skill != null ? System.Math.Max(0, caster.AttackPower + skill.Power) : 0;
        }

        public static int CalculateActualHealing(Unit caster, SkillDefinition skill, Unit target)
        {
            if (target == null || !target.IsAlive)
            {
                return 0;
            }

            return System.Math.Min(CalculateRawHealing(caster, skill), target.MaxHealth - target.CurrentHealth);
        }

        public static bool IsTileInRange(Unit caster, SkillDefinition skill, Tile tile)
        {
            if (caster == null || skill == null || caster.OccupiedTile == null || tile == null)
            {
                return false;
            }

            int distance = GridPathfinder.GetManhattanDistance(caster.CurrentCoordinate, tile.Coordinate);
            return distance >= skill.MinimumRange && distance <= skill.MaximumRange;
        }

        public static bool CanTargetUnit(Unit caster, SkillDefinition skill, Unit target)
        {
            if (caster == null || skill == null || target == null || !caster.IsAlive || !target.IsAlive || target.OccupiedTile == null || !IsTileInRange(caster, skill, target.OccupiedTile))
            {
                return false;
            }

            if (target == caster)
            {
                if (!skill.CanTargetSelf)
                {
                    return false;
                }
            }
            else
            {
                bool isAlly = target.Faction == caster.Faction;
                if (isAlly && !skill.CanTargetAllies)
                {
                    return false;
                }

                if (!isAlly && !skill.CanTargetEnemies)
                {
                    return false;
                }
            }

            return skill.EffectType != SkillEffectType.Heal || CalculateActualHealing(caster, skill, target) > 0;
        }

        public static bool CanTargetTile(Unit caster, SkillDefinition skill, Tile tile)
        {
            if (caster == null || skill == null || tile == null || !IsTileInRange(caster, skill, tile))
            {
                return false;
            }

            if (skill.TargetType == SkillTargetType.Ground)
            {
                return skill.CanTargetEmptyGround || tile.OccupyingUnit != null;
            }

            return tile.OccupyingUnit != null && CanTargetUnit(caster, skill, tile.OccupyingUnit);
        }

        public static void FillAreaTiles(GridSystem gridSystem, Tile center, SkillDefinition skill, List<Tile> results)
        {
            results.Clear();
            if (gridSystem == null || center == null || skill == null)
            {
                return;
            }

            AddTile(gridSystem, center.X, center.Y, results);
            if (skill.AreaShape != SkillAreaShape.Cross || skill.AreaSize <= 0)
            {
                return;
            }

            for (int i = 1; i <= skill.AreaSize; i++)
            {
                AddTile(gridSystem, center.X + i, center.Y, results);
                AddTile(gridSystem, center.X - i, center.Y, results);
                AddTile(gridSystem, center.X, center.Y + i, results);
                AddTile(gridSystem, center.X, center.Y - i, results);
            }
        }

        public static void FillAffectedUnits(Unit caster, SkillDefinition skill, IReadOnlyList<Tile> areaTiles, List<Unit> results)
        {
            results.Clear();
            if (caster == null || skill == null || areaTiles == null)
            {
                return;
            }

            for (int i = 0; i < areaTiles.Count; i++)
            {
                Unit unit = areaTiles[i] != null ? areaTiles[i].OccupyingUnit : null;
                if (unit != null && CanAffectUnit(caster, skill, unit))
                {
                    results.Add(unit);
                }
            }
        }

        public static string BuildPreview(Unit caster, SkillDefinition skill, Tile targetTile, IReadOnlyList<Tile> areaTiles, IReadOnlyList<Unit> affectedUnits)
        {
            StringBuilder builder = new StringBuilder(256);
            builder.AppendLine(skill.DisplayName);
            builder.AppendLine($"Caster: {caster.DisplayName}");

            if (skill.TargetType == SkillTargetType.Ground)
            {
                builder.AppendLine($"Center: {targetTile.Coordinate}");
                builder.AppendLine($"Affected Enemies: {affectedUnits.Count}");
                for (int i = 0; i < affectedUnits.Count; i++)
                {
                    Unit unit = affectedUnits[i];
                    int damage = CalculateDamage(caster, skill, unit);
                    builder.AppendLine($"{unit.DisplayName}: {damage} dmg ({unit.CurrentHealth}->{System.Math.Max(0, unit.CurrentHealth - damage)})");
                }
                return builder.ToString();
            }

            Unit target = targetTile != null ? targetTile.OccupyingUnit : null;
            if (target == null)
            {
                builder.Append("No target");
                return builder.ToString();
            }

            builder.AppendLine($"Target: {target.DisplayName}");
            if (skill.EffectType == SkillEffectType.Heal)
            {
                int healing = CalculateActualHealing(caster, skill, target);
                builder.AppendLine($"Heal: {healing}");
                builder.Append($"HP: {target.CurrentHealth}->{System.Math.Min(target.MaxHealth, target.CurrentHealth + healing)}/{target.MaxHealth}");
            }
            else
            {
                int damage = CalculateDamage(caster, skill, target);
                builder.AppendLine($"Damage: {damage}");
                builder.AppendLine($"HP: {target.CurrentHealth}->{System.Math.Max(0, target.CurrentHealth - damage)}/{target.MaxHealth}");
                builder.Append($"Terrain Def: +{CombatResolver.GetTerrainDefenseBonus(target)} ({CombatResolver.GetEffectiveDefense(target)})");
            }

            return builder.ToString();
        }

        public static bool Resolve(Unit caster, SkillDefinition skill, IReadOnlyList<Unit> affectedUnits)
        {
            if (caster == null || skill == null || !caster.IsAlive || affectedUnits == null)
            {
                return false;
            }

            bool applied = false;
            for (int i = 0; i < affectedUnits.Count; i++)
            {
                Unit target = affectedUnits[i];
                if (!CanAffectUnit(caster, skill, target))
                {
                    continue;
                }

                if (skill.EffectType == SkillEffectType.Heal)
                {
                    int healing = CalculateActualHealing(caster, skill, target);
                    if (healing > 0)
                    {
                        target.ReceiveHealing(healing);
                        applied = true;
                    }
                }
                else
                {
                    target.ReceiveDamage(CalculateDamage(caster, skill, target));
                    applied = true;
                }
            }

            return applied;
        }

        private static bool CanAffectUnit(Unit caster, SkillDefinition skill, Unit target)
        {
            if (target == null || !target.IsAlive)
            {
                return false;
            }

            if (target == caster)
            {
                return skill.CanTargetSelf;
            }

            bool isAlly = target.Faction == caster.Faction;
            return (isAlly && skill.CanTargetAllies) || (!isAlly && skill.CanTargetEnemies);
        }

        private static void AddTile(GridSystem gridSystem, int x, int y, List<Tile> results)
        {
            if (gridSystem.TryGetTile(new GridCoordinate(x, y), out Tile tile))
            {
                results.Add(tile);
            }
        }
    }
}
