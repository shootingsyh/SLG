using SLG.Units;
using UnityEngine;

namespace SLG.Visuals
{
    public static class UnitVisualCatalog
    {
        private const string KnightVisual = "UnitVisuals/FreeManTall";
        private const string ArcherVisual = "UnitVisuals/FreeMan";
        private const string MageVisual = "UnitVisuals/FreeWomanTall";
        private const string HealerVisual = "UnitVisuals/FreeWoman";

        public static GameObject LoadVisual(UnitDefinition definition, UnitFaction faction)
        {
            string id = definition != null && definition.UnitDefinitionId != null ? definition.UnitDefinitionId.ToLowerInvariant() : string.Empty;
            string archetype = definition != null && definition.ArchetypeName != null ? definition.ArchetypeName.ToLowerInvariant() : string.Empty;

            string path;
            if (id.Contains("healer") || archetype.Contains("healer"))
                path = HealerVisual;
            else if (id.Contains("mage") || archetype.Contains("mage"))
                path = MageVisual;
            else if (id.Contains("archer") || archetype.Contains("archer"))
                path = ArcherVisual;
            else if (id.Contains("knight") || archetype.Contains("knight") || archetype.Contains("soldier"))
                path = KnightVisual;
            else
                path = faction == UnitFaction.Player ? KnightVisual : ArcherVisual;

            return Resources.Load<GameObject>(path);
        }
    }
}
