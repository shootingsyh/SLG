using UnityEngine;

namespace SLG.Items
{
    public enum ItemCategory
    {
        Consumable,
        Weapon,
        Armor,
        Accessory
    }

    public enum ItemEffectType
    {
        None,
        Heal,
        Damage
    }

    public enum ItemTargetType
    {
        Unit,
        Ground
    }

    [CreateAssetMenu(menuName = "SLG/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string itemId = "item";
        [SerializeField] private string displayName = "Item";
        [TextArea] [SerializeField] private string description;
        [SerializeField] private ItemCategory category = ItemCategory.Consumable;

        [Header("Stacking")]
        [Min(1)] [SerializeField] private int maxStackSize = 99;
        [SerializeField] private bool usableInBattle = true;
        [SerializeField] private bool usableBetweenBattles = false;

        [Header("Consumable Effect")]
        [SerializeField] private ItemEffectType effectType = ItemEffectType.Heal;
        [SerializeField] private ItemTargetType targetType = ItemTargetType.Unit;
        [Min(0)] [SerializeField] private int minimumRange = 1;
        [Min(0)] [SerializeField] private int maximumRange = 1;
        [SerializeField] private int power = 5;
        [SerializeField] private bool canTargetSelf = true;
        [SerializeField] private bool canTargetAllies = true;
        [SerializeField] private bool canTargetEnemies;
        [SerializeField] private bool canTargetEmptyGround;

        [Header("Equipment Bonus")]
        [SerializeField] private int attackBonus;
        [SerializeField] private int defenseBonus;
        [SerializeField] private int movementBonus;

        [Header("Display")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Color previewColor = Color.white;

        public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public ItemCategory Category => category;
        public int MaxStackSize => Mathf.Max(1, maxStackSize);
        public bool UsableInBattle => usableInBattle;
        public bool UsableBetweenBattles => usableBetweenBattles;
        public ItemEffectType EffectType => effectType;
        public ItemTargetType TargetType => targetType;
        public int MinimumRange => Mathf.Max(0, minimumRange);
        public int MaximumRange => Mathf.Max(MinimumRange, maximumRange);
        public int Power => power;
        public bool CanTargetSelf => canTargetSelf;
        public bool CanTargetAllies => canTargetAllies;
        public bool CanTargetEnemies => canTargetEnemies;
        public bool CanTargetEmptyGround => canTargetEmptyGround;
        public int AttackBonus => attackBonus;
        public int DefenseBonus => defenseBonus;
        public int MovementBonus => movementBonus;
        public Sprite Icon => icon;
        public Color PreviewColor => previewColor;

        public bool IsConsumable => category == ItemCategory.Consumable;
        public bool IsEquipment => category != ItemCategory.Consumable;

        public void ConfigureRuntime(string itemId, string displayName, string description, ItemCategory category,
            int maxStackSize, bool usableInBattle, bool usableBetweenBattles,
            ItemEffectType effectType, ItemTargetType targetType, int minRange, int maxRange, int power,
            bool canTargetSelf, bool canTargetAllies, bool canTargetEnemies, bool canTargetEmptyGround,
            int attackBonus, int defenseBonus, int movementBonus)
        {
            this.itemId = itemId;
            this.displayName = displayName;
            this.description = description;
            this.category = category;
            this.maxStackSize = Mathf.Max(1, maxStackSize);
            this.usableInBattle = usableInBattle;
            this.usableBetweenBattles = usableBetweenBattles;
            this.effectType = effectType;
            this.targetType = targetType;
            this.minimumRange = Mathf.Max(0, minRange);
            this.maximumRange = Mathf.Max(this.minimumRange, maxRange);
            this.power = power;
            this.canTargetSelf = canTargetSelf;
            this.canTargetAllies = canTargetAllies;
            this.canTargetEnemies = canTargetEnemies;
            this.canTargetEmptyGround = canTargetEmptyGround;
            this.attackBonus = attackBonus;
            this.defenseBonus = defenseBonus;
            this.movementBonus = movementBonus;
        }

        private void OnValidate()
        {
            maxStackSize = Mathf.Max(1, maxStackSize);
            minimumRange = Mathf.Max(0, minimumRange);
            maximumRange = Mathf.Max(minimumRange, maximumRange);
        }
    }
}
