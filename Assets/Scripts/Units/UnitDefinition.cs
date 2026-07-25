using System.Collections.Generic;
using SLG.Skills;
using UnityEngine;

namespace SLG.Units
{
    [CreateAssetMenu(menuName = "SLG/Unit Definition", fileName = "UnitDefinition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Unit";
        [SerializeField] private string archetypeName = "Class";
        [TextArea]
        [SerializeField] private string description;

        [Header("Stats")]
        [Min(1)]
        [SerializeField] private int maxHealth = 10;
        [SerializeField] private int attackPower = 4;
        [SerializeField] private int defense = 1;
        [Min(1)]
        [SerializeField] private int minimumAttackRange = 1;
        [Min(1)]
        [SerializeField] private int maximumAttackRange = 1;
        [Min(1)]
        [SerializeField] private int movementRange = 3;
        [SerializeField] private MovementProfile movementProfile = MovementProfile.Ground;

        [Header("Skills")]
        [SerializeField] private List<SkillDefinition> skills = new List<SkillDefinition>();

        [Header("Display")]
        [SerializeField] private Color baseDisplayColor = Color.white;
        [SerializeField] private Material material;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string ArchetypeName => archetypeName;
        public string Description => description;
        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int AttackPower => attackPower;
        public int Defense => defense;
        public int MinimumAttackRange => Mathf.Max(1, minimumAttackRange);
        public int MaximumAttackRange => Mathf.Max(MinimumAttackRange, maximumAttackRange);
        public int MovementRange => Mathf.Max(1, movementRange);
        public MovementProfile MovementProfile => movementProfile;
        public IReadOnlyList<SkillDefinition> Skills => skills;
        public Color BaseDisplayColor => baseDisplayColor;
        public Material Material => material;

        public void ConfigureRuntime(string displayName, string archetypeName, int maxHealth, int attackPower, int defense, int movementRange, MovementProfile movementProfile, int minimumAttackRange = 1, int maximumAttackRange = 1, IReadOnlyList<SkillDefinition> skills = null)
        {
            this.displayName = displayName;
            this.archetypeName = archetypeName;
            this.maxHealth = Mathf.Max(1, maxHealth);
            this.attackPower = attackPower;
            this.defense = defense;
            this.movementRange = Mathf.Max(1, movementRange);
            this.movementProfile = movementProfile;
            this.minimumAttackRange = Mathf.Max(1, minimumAttackRange);
            this.maximumAttackRange = Mathf.Max(this.minimumAttackRange, maximumAttackRange);
            this.skills.Clear();
            if (skills != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    if (skills[i] != null)
                    {
                        this.skills.Add(skills[i]);
                    }
                }
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            movementRange = Mathf.Max(1, movementRange);
            minimumAttackRange = Mathf.Max(1, minimumAttackRange);
            maximumAttackRange = Mathf.Max(minimumAttackRange, maximumAttackRange);
        }
    }
}
