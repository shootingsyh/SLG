using SLG.Units;

namespace SLG.Core
{
    public static class CombatResolver
    {
        public static int CalculateDamage(Unit attacker, Unit defender)
        {
            if (attacker == null || defender == null)
            {
                return 0;
            }

            return System.Math.Max(1, attacker.AttackPower - defender.Defense);
        }
    }
}
