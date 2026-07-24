using UnityEngine;

namespace SLG.Skills
{
    [CreateAssetMenu(menuName = "SLG/Skill Definition", fileName = "SkillDefinition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string skillId = "skill";
        [SerializeField] private string displayName = "Skill";
        [TextArea]
        [SerializeField] private string description;

        [Header("Rules")]
        [SerializeField] private SkillTargetType targetType = SkillTargetType.Unit;
        [SerializeField] private SkillEffectType effectType = SkillEffectType.Damage;
        [Min(0)]
        [SerializeField] private int minimumRange = 1;
        [Min(0)]
        [SerializeField] private int maximumRange = 1;
        [SerializeField] private SkillAreaShape areaShape = SkillAreaShape.Single;
        [Min(0)]
        [SerializeField] private int areaSize;
        [SerializeField] private int power = 2;
        [SerializeField] private bool canTargetSelf;
        [SerializeField] private bool canTargetAllies;
        [SerializeField] private bool canTargetEnemies = true;
        [SerializeField] private bool canTargetEmptyGround;
        [SerializeField] private bool allowsCounterattack;

        [Header("Display")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Color previewColor = new Color(0.65f, 0.35f, 1f, 1f);

        public string SkillId => string.IsNullOrWhiteSpace(skillId) ? name : skillId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public SkillTargetType TargetType => targetType;
        public SkillEffectType EffectType => effectType;
        public int MinimumRange => Mathf.Max(0, minimumRange);
        public int MaximumRange => Mathf.Max(MinimumRange, maximumRange);
        public SkillAreaShape AreaShape => areaShape;
        public int AreaSize => Mathf.Max(0, areaSize);
        public int Power => power;
        public bool CanTargetSelf => canTargetSelf;
        public bool CanTargetAllies => canTargetAllies;
        public bool CanTargetEnemies => canTargetEnemies;
        public bool CanTargetEmptyGround => canTargetEmptyGround;
        public bool AllowsCounterattack => allowsCounterattack;
        public Sprite Icon => icon;
        public Color PreviewColor => previewColor;

        private void OnValidate()
        {
            minimumRange = Mathf.Max(0, minimumRange);
            maximumRange = Mathf.Max(minimumRange, maximumRange);
            areaSize = Mathf.Max(0, areaSize);
        }
    }
}
