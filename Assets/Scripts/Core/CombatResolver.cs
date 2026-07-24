using SLG.Units;

namespace SLG.Core
{
    public readonly struct CombatPreview
    {
        public CombatPreview(Unit attacker, Unit defender, int attackerDamage, bool canCounter, int counterDamage, int defenderTerrainDefenseBonus, int attackerTerrainDefenseBonus, int defenderEffectiveDefense, int attackerEffectiveDefense)
        {
            Attacker = attacker;
            Defender = defender;
            AttackerDamage = attackerDamage;
            CanCounter = canCounter;
            CounterDamage = counterDamage;
            DefenderTerrainDefenseBonus = defenderTerrainDefenseBonus;
            AttackerTerrainDefenseBonus = attackerTerrainDefenseBonus;
            DefenderEffectiveDefense = defenderEffectiveDefense;
            AttackerEffectiveDefense = attackerEffectiveDefense;
        }

        public Unit Attacker { get; }
        public Unit Defender { get; }
        public int AttackerDamage { get; }
        public bool CanCounter { get; }
        public int CounterDamage { get; }
        public int DefenderTerrainDefenseBonus { get; }
        public int AttackerTerrainDefenseBonus { get; }
        public int DefenderEffectiveDefense { get; }
        public int AttackerEffectiveDefense { get; }
    }

    public static class CombatResolver
    {
        public static int CalculateDamage(Unit attacker, Unit defender)
        {
            if (attacker == null || defender == null)
            {
                return 0;
            }

            return System.Math.Max(1, attacker.AttackPower - GetEffectiveDefense(defender));
        }

        public static int GetTerrainDefenseBonus(Unit defender)
        {
            return defender != null && defender.OccupiedTile != null ? defender.OccupiedTile.GetDefenseBonus(defender) : 0;
        }

        public static int GetEffectiveDefense(Unit defender)
        {
            return defender != null ? defender.Defense + GetTerrainDefenseBonus(defender) : 0;
        }

        public static bool CanAttack(Unit attacker, Unit defender)
        {
            if (attacker == null || defender == null || !attacker.IsAlive || !defender.IsAlive || attacker == defender || attacker.Faction == defender.Faction)
            {
                return false;
            }

            int distance = Grid.GridPathfinder.GetManhattanDistance(attacker.CurrentCoordinate, defender.CurrentCoordinate);
            return distance >= attacker.MinimumAttackRange && distance <= attacker.AttackRange;
        }

        public static CombatPreview BuildPreview(Unit attacker, Unit defender)
        {
            int attackerDamage = CanAttack(attacker, defender) ? CalculateDamage(attacker, defender) : 0;
            bool canCounter = attackerDamage > 0 && defender.CurrentHealth > attackerDamage && CanCounterAttack(defender, attacker);
            int counterDamage = canCounter ? CalculateDamage(defender, attacker) : 0;
            return new CombatPreview(attacker, defender, attackerDamage, canCounter, counterDamage, GetTerrainDefenseBonus(defender), GetTerrainDefenseBonus(attacker), GetEffectiveDefense(defender), GetEffectiveDefense(attacker));
        }

        public static bool CanCounterAttack(Unit defender, Unit attacker)
        {
            if (defender == null || attacker == null || !defender.IsAlive || !attacker.IsAlive || defender == attacker || defender.Faction == attacker.Faction)
            {
                return false;
            }

            int distance = Grid.GridPathfinder.GetManhattanDistance(defender.CurrentCoordinate, attacker.CurrentCoordinate);
            return distance >= defender.MinimumAttackRange && distance <= defender.AttackRange;
        }
    }
}
