using System;
using System.Collections;
using System.Collections.Generic;
using SLG.Core;
using SLG.Grid;
using SLG.Skills;
using SLG.Visuals;
using UnityEngine;

namespace SLG.Units
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Renderer))]
    public sealed class Unit : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Definition")]
        [SerializeField] private UnitDefinition unitDefinition;

        [Header("Instance")]
        [SerializeField] private UnitFaction faction = UnitFaction.Player;
        [SerializeField] private GridCoordinate currentCoordinate;
        [SerializeField] private Tile occupiedTile;
        [SerializeField] private UnitSelectionController selectionController;
        [SerializeField] private float movementSpeed = 4f;

        /// <summary>Speed at which the unit moves (units per second)</summary>
        public float MovementSpeed => movementSpeed;
        [SerializeField] private float tileHeightOffset = 0.55f;

        [Header("Runtime Debug")]
        [SerializeField] private int currentHealth;

        [Header("Fallback Stats")]
        [Tooltip("Used only if Unit Definition is missing.")]
        [SerializeField] private string fallbackDisplayName = "Unit";
        [SerializeField] private int fallbackMovementRange = 3;
        [SerializeField] private int fallbackMaxHealth = 10;
        [SerializeField] private int fallbackAttackPower = 4;
        [SerializeField] private int fallbackDefense = 1;
        [SerializeField] private int fallbackMinimumAttackRange = 1;
        [SerializeField] private int fallbackAttackRange = 1;
        [SerializeField] private MovementProfile fallbackMovementProfile = MovementProfile.Ground;

        [Header("Visuals")]
        [SerializeField] private Color playerColor = new Color(0.18f, 0.38f, 0.9f, 1f);
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.3f, 1f);
        [SerializeField] private Color movingColor = new Color(0.9f, 0.7f, 1f, 1f);
        [SerializeField] private Color attackTargetColor = new Color(1f, 0.45f, 0.15f, 1f);
        [SerializeField] private Color previewTargetColor = new Color(1f, 0.85f, 0.15f, 1f);
        [SerializeField] private Color takingDamageColor = Color.white;
        [SerializeField] private float actedBrightness = 0.6f;

        [Header("Selection Ring")]
        [Tooltip("Brightness factor for the selection ring")]
        [SerializeField] private float selectionRingBrightness = 1.2f;
        [Tooltip("Ring pulse period in seconds")]
        [SerializeField] private float selectionRingPulsePeriod = 0.9f;
        [Tooltip("Ring pulse amplitude")]
        [Range(0f, 0.3f)]
        [SerializeField] private float selectionRingPulseAmplitude = 0.15f;

        [Header("Combat Feedback")]
        [Tooltip("Lunge distance as fraction of tile")]
        [Range(0.1f, 0.2f)]
        [SerializeField] private float lungeFraction = 0.15f;
        [Tooltip("Attacker lunge total duration in seconds")]
        [SerializeField] private float lungeDuration = 0.16f;
        [Tooltip("Damage flash duration for the target")]
        [SerializeField] private float damageFlashDuration = 0.1f;
        [Tooltip("HP bar change animation duration")]
        [SerializeField] private float hpBarAnimDuration = 0.25f;
        [Tooltip("Duration for floating damage numbers to display (seconds)")]
        [SerializeField]
        private float floatingDamageDuration = 0.6f;
        [Tooltip("Deceased fade duration")]
        [SerializeField] private float deathFadeDuration = 0.25f;

        private Renderer unitRenderer;
        private Renderer[] visualRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock visualBlock;
        private bool isSelected;
        private bool isMoving;
        private bool hasActed;
        private bool isAttackTarget;
        private bool isPreviewTarget;
        private bool isTakingDamage;
        private bool isDead;
        private TextMesh healthText;
        private GameObject selectionRing;
        private Renderer selectionRingRenderer;
        private MaterialPropertyBlock ringBlock;
        private float selectionRingPulseTime;
        private float hpBarAnimProgress;
        private int hpBarAnimFrom;
        private int hpBarAnimTo;
        private bool isHpBarAnimating;
        private TextMesh floatingDamageText;
        private Coroutine floatingDamageCoroutine;

        public UnitDefinition Definition => unitDefinition;
        public string DisplayName => unitDefinition != null ? unitDefinition.DisplayName : fallbackDisplayName;
        public string ArchetypeName => unitDefinition != null ? unitDefinition.ArchetypeName : "Unit";
        public UnitFaction Faction => faction;
        public GridCoordinate CurrentCoordinate => currentCoordinate;
        public int BaseMovementRange => unitDefinition != null ? unitDefinition.MovementRange : Mathf.Max(1, fallbackMovementRange);
        public int MovementRange => BaseMovementRange + GetEquipmentBonus(SLG.Items.EquipmentSlot.Accessory);
        public int MaxHealth => unitDefinition != null ? unitDefinition.MaxHealth : Mathf.Max(1, fallbackMaxHealth);
        public int CurrentHealth => currentHealth;
        public int BaseAttackPower => unitDefinition != null ? unitDefinition.AttackPower : fallbackAttackPower;
        public int AttackPower => BaseAttackPower + GetEquipmentBonus(SLG.Items.EquipmentSlot.Weapon);
        public int BaseDefense => unitDefinition != null ? unitDefinition.Defense : fallbackDefense;
        public int Defense => BaseDefense + GetEquipmentBonus(SLG.Items.EquipmentSlot.Armor);
        public int MinimumAttackRange => unitDefinition != null ? unitDefinition.MinimumAttackRange : Mathf.Max(1, fallbackMinimumAttackRange);
        public int AttackRange => unitDefinition != null ? unitDefinition.MaximumAttackRange : Mathf.Max(MinimumAttackRange, fallbackAttackRange);
        public MovementProfile MovementProfile => unitDefinition != null ? unitDefinition.MovementProfile : fallbackMovementProfile;
        public IReadOnlyList<SkillDefinition> Skills => unitDefinition != null ? unitDefinition.Skills : System.Array.Empty<SkillDefinition>();
        public Tile OccupiedTile => occupiedTile;
        public bool IsMoving => isMoving;
        public bool HasActed => hasActed;
        public bool IsAlive => !isDead && currentHealth > 0;

        private int GetEquipmentBonus(SLG.Items.EquipmentSlot slot)
        {
            if (Faction != UnitFaction.Player) return 0;
            if (string.IsNullOrEmpty(name)) return 0;
            string equippedId = SLG.Saves.GameShellServices.CampaignEquipment.GetEquipped(name, slot);
            if (string.IsNullOrEmpty(equippedId)) return 0;
            var def = SLG.Items.ItemCatalog.Get(equippedId);
            if (def == null) return 0;
            return slot switch
            {
                SLG.Items.EquipmentSlot.Weapon => def.AttackBonus,
                SLG.Items.EquipmentSlot.Armor => def.DefenseBonus,
                SLG.Items.EquipmentSlot.Accessory => def.MovementBonus,
                _ => 0
            };
        }

        public void Initialize(UnitSelectionController controller, GridCoordinate coordinate, Tile tile)
        {
            selectionController = controller;
            currentCoordinate = coordinate;
            occupiedTile = tile;
            ValidateDefinition();
            InitializeHealthForBattle();
            ApplySelectionState(false);
        }

        public void ConfigureRuntime(UnitDefinition definition, UnitFaction faction, GridCoordinate coordinate, int currentHealth = 0, float movementSpeed = 4f)
        {
            unitDefinition = definition;
            this.faction = faction;
            currentCoordinate = coordinate;
            this.currentHealth = currentHealth;
            this.movementSpeed = movementSpeed;
            InitializeHealthForBattle();
            ApplyDefinitionVisuals();
            EnsureCharacterVisual();
            RefreshVisualState();
        }

        public void RestoreRuntimeState(GridCoordinate coordinate, int health, bool acted)
        {
            currentCoordinate = coordinate;
            currentHealth = Mathf.Clamp(health, 0, MaxHealth);
            isDead = currentHealth <= 0;
            hasActed = acted;
            gameObject.SetActive(!isDead);
            UpdateHealthDisplay();
            RefreshVisualState();
            UpdateSelectionRing();
        }

        public void PlaceOnTile(Tile tile)
        {
            if (tile == null)
            {
                occupiedTile = null;
                return;
            }

            occupiedTile = tile;
            currentCoordinate = tile.Coordinate;
            transform.position = GetUnitPosition(tile);
        }

        public void MoveAlongPath(IReadOnlyList<Tile> path, Action<Unit, Tile> completed)
        {
            if (isMoving || path == null || path.Count < 2)
            {
                return;
            }

            StartCoroutine(MoveAlongPathRoutine(path, completed));
        }

        public void PlayAttack(Unit defender, Action completed)
        {
            if (isMoving || isDead || defender == null || !defender.IsAlive)
            {
                completed?.Invoke();
                return;
            }

            StartCoroutine(AttackRoutine(defender, completed));
        }

        public void SetAttackTargetHighlighted(bool highlighted)
        {
            isAttackTarget = highlighted;
            RefreshVisualState();
        }

        public void SetCombatPreviewHighlighted(bool highlighted)
        {
            isPreviewTarget = highlighted;
            RefreshVisualState();
        }

        public bool ReceiveDamage(int damage)
        {
            if (isDead)
            {
                return false;
            }

            int previousHealth = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));
            
            // Start HP bar animation
            StartHpBarAnimation(previousHealth, currentHealth);
            
            UpdateHealthDisplay();
            StartCoroutine(DamageFlashRoutine());

            if (currentHealth > 0)
            {
                return false;
            }

            Die();
            return true;
        }

        public int ReceiveHealing(int healing)
        {
            if (isDead || healing <= 0)
            {
                return 0;
            }

            int previousHealth = currentHealth;
            currentHealth = Mathf.Min(MaxHealth, currentHealth + healing);
            
            StartHpBarAnimation(previousHealth, currentHealth);
            
            UpdateHealthDisplay();
            RefreshVisualState();
            return currentHealth - previousHealth;
        }

        public void ApplySelectionState(bool selected)
        {
            isSelected = selected;
            RefreshVisualState();
            UpdateSelectionRing();
        }

        public void SetHasActed(bool acted)
        {
            hasActed = acted;
            RefreshVisualState();
        }

        public void InitializeHealthForBattle()
        {
            currentHealth = Mathf.Clamp(currentHealth <= 0 ? MaxHealth : currentHealth, 0, MaxHealth);
            isDead = currentHealth <= 0;
            UpdateHealthDisplay();
            RefreshVisualState();
        }

        /// <summary>Show a floating damage number above this unit</summary>
        public void PlayFloatingDamage(int damage, float duration = -1f)
        {
            if (duration <= 0f)
                duration = floatingDamageDuration;

            if (floatingDamageCoroutine != null)
                StopCoroutine(floatingDamageCoroutine);
            floatingDamageCoroutine = StartCoroutine(FloatingDamageRoutine(damage, duration));
        }

        private void Awake()
        {
            CacheRenderer();
            EnsureHealthDisplay();
            EnsureSelectionRing();
            ApplyDefinitionVisuals();
            InitializeHealth();
            RefreshVisualState();
        }

        private void Update()
        {
            // Update selection ring pulse
            if (selectionRing != null && selectionRing.activeSelf)
            {
                selectionRingPulseTime += Time.deltaTime;
                float period = selectionRingPulsePeriod > 0.01f ? selectionRingPulsePeriod : 0.9f;
                float pulse = Mathf.PingPong(selectionRingPulseTime, period) / period;
                float amplitude = selectionRingPulseAmplitude > 0f ? selectionRingPulseAmplitude : 0.15f;
                float scaleMultiplier = 1f + pulse * amplitude;
                Vector3 baseScale = new Vector3(0.6f, 0.05f, 0.6f) * scaleMultiplier;
                selectionRing.transform.localScale = baseScale;
            }

            // HP bar animation
            if (isHpBarAnimating)
            {
                hpBarAnimProgress += Time.deltaTime / Mathf.Max(0.001f, hpBarAnimDuration);
                if (hpBarAnimProgress >= 1f)
                {
                    hpBarAnimProgress = 1f;
                    isHpBarAnimating = false;
                }
                UpdateHealthDisplay();
            }
        }

        private void OnMouseDown()
        {
            selectionController?.HandleUnitClicked(this);
        }

        private void OnMouseEnter()
        {
            selectionController?.HandleUnitHoverEntered(this);
        }

        private void OnMouseOver()
        {
            selectionController?.HandleUnitHoverStayed(this);
        }

        private void OnMouseExit()
        {
            selectionController?.HandleUnitHoverExited(this);
        }

        private IEnumerator MoveAlongPathRoutine(IReadOnlyList<Tile> path, Action<Unit, Tile> completed)
        {
            isMoving = true;
            RefreshVisualState();
            Tile destination = path[path.Count - 1];

            for (int i = 1; i < path.Count; i++)
            {
                Vector3 start = transform.position;
                Vector3 end = GetUnitPosition(path[i]);
                float distance = Vector3.Distance(start, end);
                float duration = distance / Mathf.Max(0.01f, movementSpeed);
                float elapsed = 0f;

                // Face direction of travel
                Vector3 direction = (end - start).normalized;
                if (direction.x != 0f || direction.z != 0f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.5f);
                }

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }

                transform.position = end;
            }

            occupiedTile = destination;
            currentCoordinate = destination.Coordinate;
            isMoving = false;
            RefreshVisualState();
            completed?.Invoke(this, destination);
        }

        private IEnumerator AttackRoutine(Unit defender, Action completed)
        {
            isMoving = true;
            RefreshVisualState();

            Vector3 start = transform.position;
            Vector3 direction = (defender.transform.position - start).normalized;
            // Lunge by lungeFraction of tile size
            float lungeDistance = lungeFraction;
            Vector3 lunge = start + direction * lungeDistance;
            float elapsed = 0f;
            float halfDuration = lungeDuration * 0.5f;

            // Lunge toward target
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, lunge, Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }

            // Return
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(lunge, start, Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }

            transform.position = start;
            isMoving = false;
            RefreshVisualState();
            completed?.Invoke();
        }

        private IEnumerator DamageFlashRoutine()
        {
            isTakingDamage = true;
            RefreshVisualState();
            yield return new WaitForSeconds(damageFlashDuration);
            isTakingDamage = false;
            RefreshVisualState();
        }

        private IEnumerator FloatingDamageRoutine(int damage, float duration)
        {
            if (floatingDamageText == null)
            {
                floatingDamageText = EnsureFloatingDamageDisplay();
            }

            if (floatingDamageText != null)
            {
                float displayColor = faction == UnitFaction.Player ? new Color(1f, 0.2f, 0.2f, 1f).r : new Color(1f, 0.6f, 0.2f, 1f).r;
                floatingDamageText.text = damage.ToString();
                floatingDamageText.color = new Color(1f, 0.2f, 0.1f, 1f);
                floatingDamageText.gameObject.SetActive(true);
                
                // Reset position
                floatingDamageText.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                floatingDamageText.transform.localRotation = Quaternion.identity;

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float yOffset = 1.5f + t * 0.4f;
                    float alpha = 1f - t;
                    floatingDamageText.transform.localPosition = new Vector3(0f, yOffset, 0f);
                    floatingDamageText.color = new Color(1f, 0.2f, 0.1f, alpha);
                    yield return null;
                }
                
                floatingDamageText.gameObject.SetActive(false);
            }

            floatingDamageCoroutine = null;
        }

        private TextMesh EnsureFloatingDamageDisplay()
        {
            Transform existing = transform.Find("Floating Damage");
            if (existing == null)
            {
                GameObject floater = new GameObject("Floating Damage");
                floater.transform.SetParent(transform, false);
                var textMesh = floater.AddComponent<TextMesh>();
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.characterSize = 0.3f;
                textMesh.fontSize = 24;
                floater.SetActive(false);
                return textMesh;
            }
            
            return existing.GetComponent<TextMesh>();
        }

        private Vector3 GetUnitPosition(Tile tile)
        {
            return tile.transform.position + new Vector3(0f, tileHeightOffset, 0f);
        }

        private bool CacheRenderer()
        {
            if (unitRenderer == null)
            {
                if (!TryGetComponent(out unitRenderer))
                {
                    Debug.LogError("Unit requires a Renderer.", this);
                    return false;
                }
            }

            propertyBlock ??= new MaterialPropertyBlock();
            return true;
        }

        private void EnsureCharacterVisual()
        {
            if (!CacheRenderer())
            {
                return;
            }

            unitRenderer.enabled = false;

            Transform existing = transform.Find("Character Visual");
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            GameObject visualPrefab = UnitVisualCatalog.LoadVisual(unitDefinition, faction);
            if (visualPrefab == null)
            {
                visualRenderers = Array.Empty<Renderer>();
                return;
            }

            GameObject visual = Instantiate(visualPrefab, transform);
            visual.name = "Character Visual";
            visual.transform.localPosition = new Vector3(0f, -tileHeightOffset, 0f);
            visual.transform.localRotation = Quaternion.Euler(0f, faction == UnitFaction.Player ? 180f : 0f, 0f);
            visual.transform.localScale = Vector3.one * 0.55f;

            Collider[] childColliders = visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                childColliders[i].enabled = false;
            }

            visualRenderers = visual.GetComponentsInChildren<Renderer>(true);
            visualBlock ??= new MaterialPropertyBlock();
        }

        private void InitializeHealth()
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);

            isDead = currentHealth <= 0;
            UpdateHealthDisplay();
        }

        private void EnsureHealthDisplay()
        {
            Transform existing = transform.Find("Health Display");
            GameObject display = existing != null ? existing.gameObject : new GameObject("Health Display");
            display.transform.SetParent(transform, false);
            display.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            display.transform.localRotation = Quaternion.identity;
            display.transform.localScale = Vector3.one * 0.18f;

            healthText = display.GetComponent<TextMesh>();
            if (healthText == null)
            {
                healthText = display.AddComponent<TextMesh>();
            }

            healthText.anchor = TextAnchor.MiddleCenter;
            healthText.alignment = TextAlignment.Center;
            healthText.characterSize = 0.35f;
            healthText.color = Color.white;
        }

        private void StartHpBarAnimation(int fromHealth, int toHealth)
        {
            hpBarAnimFrom = fromHealth;
            hpBarAnimTo = toHealth;
            hpBarAnimProgress = 0f;
            isHpBarAnimating = true;
        }

        private void UpdateHealthDisplay()
        {
            if (healthText == null)
                return;

            int displayHp;
            if (isHpBarAnimating)
            {
                displayHp = Mathf.RoundToInt(Mathf.Lerp(hpBarAnimFrom, hpBarAnimTo, hpBarAnimProgress));
            }
            else
            {
                displayHp = currentHealth;
            }

            healthText.text = $"{displayHp}/{MaxHealth}";
            healthText.gameObject.SetActive(!isDead);
        }

        private void EnsureSelectionRing()
        {
            Transform existing = transform.Find("Selection Ring");
            if (existing != null)
            {
                selectionRing = existing.gameObject;
                selectionRingRenderer = selectionRing.GetComponent<Renderer>();
                return;
            }

            if (!TryGetComponent(out MeshFilter sourceMeshFilter) || sourceMeshFilter.sharedMesh == null)
            {
                return;
            }

            selectionRing = new GameObject("Selection Ring");
            selectionRing.transform.SetParent(transform, false);
            selectionRing.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            selectionRing.transform.localRotation = Quaternion.identity;
            selectionRing.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f);

            MeshFilter ringMesh = selectionRing.AddComponent<MeshFilter>();
            ringMesh.sharedMesh = sourceMeshFilter.sharedMesh;

            selectionRingRenderer = selectionRing.AddComponent<MeshRenderer>();
            if (unitRenderer != null && unitRenderer.sharedMaterial != null)
                selectionRingRenderer.sharedMaterial = unitRenderer.sharedMaterial;
            
            ringBlock ??= new MaterialPropertyBlock();
            selectionRing.gameObject.SetActive(false);
        }

        private void UpdateSelectionRing()
        {
            if (selectionRing == null)
                return;

            bool shouldShow = isSelected && !isDead && !isMoving;
            if (selectionRing.activeSelf == shouldShow)
            {
                if (shouldShow)
                    ApplyRingColor();
                return;
            }

            selectionRing.SetActive(shouldShow);
            if (shouldShow)
            {
                selectionRingPulseTime = 0f;
                ApplyRingColor();
            }
        }

        private void ApplyRingColor()
        {
            if (selectionRingRenderer == null)
                return;

            Color ringColor = faction == UnitFaction.Player
                ? new Color(0f, 1f, 0.95f, 0.9f) * selectionRingBrightness
                : new Color(1f, 0.35f, 0.15f, 0.9f) * selectionRingBrightness;

            selectionRingRenderer.GetPropertyBlock(ringBlock);
            ringBlock.SetColor(BaseColorId, ringColor);
            ringBlock.SetColor(ColorId, ringColor);
            selectionRingRenderer.SetPropertyBlock(ringBlock);
        }

        private void ValidateDefinition()
        {
            if (unitDefinition == null)
            {
                Debug.LogWarning($"Unit '{name}' has no UnitDefinition and is using fallback stats.", this);
                return;
            }

            if (unitDefinition.MaxHealth <= 0 || unitDefinition.MovementRange <= 0)
            {
                Debug.LogError($"Unit '{name}' has an invalid UnitDefinition '{unitDefinition.name}'.", this);
            }

            if (AttackRange < MinimumAttackRange)
            {
                Debug.LogError($"Unit '{name}' has invalid attack range.", this);
            }
        }

        private void ApplyDefinitionVisuals()
        {
            if (!CacheRenderer())
            {
                return;
            }

            unitRenderer.sharedMaterial = unitDefinition != null && unitDefinition.Material != null
                ? unitDefinition.Material
                : RuntimeVisualMaterials.UnitMaterial;
        }

        private void Die()
        {
            isDead = true;
            isSelected = false;
            isMoving = false;
            isAttackTarget = false;
            isPreviewTarget = false;
            occupiedTile?.SetOccupyingUnit(null);
            occupiedTile = null;
            UpdateHealthDisplay();
            
            // Death fade animation
            StartCoroutine(DeathFadeRoutine(deathFadeDuration));
        }

        private IEnumerator DeathFadeRoutine(float duration)
        {
            if (!CacheRenderer())
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / duration);
                Color currentColor = GetRendererColor();
                currentColor.a = alpha;
                ApplyColor(currentColor);
                yield return null;
            }

            // Keep unit visible for a moment, then deactivate
            yield return new WaitForSeconds(0.1f);
            gameObject.SetActive(false);
        }

        private Color GetRendererColor()
        {
            if (!CacheRenderer())
                return Color.white;

            // Try to get the color from the material
            try
            {
                if (unitRenderer.sharedMaterial != null)
                {
                    if (unitRenderer.sharedMaterial.HasProperty("_BaseColor"))
                        return unitRenderer.sharedMaterial.GetColor("_BaseColor");
                    return unitRenderer.sharedMaterial.color;
                }
            }
            catch (Exception)
            {
                // Fallback
            }
            
            return Color.white;
        }

        private void ApplyColor(Color color)
        {
            if (!CacheRenderer())
            {
                return;
            }

            unitRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            unitRenderer.SetPropertyBlock(propertyBlock);

            for (int i = 0; i < visualRenderers.Length; i++)
            {
                Renderer target = visualRenderers[i];
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(visualBlock);
                visualBlock.SetColor(BaseColorId, color);
                visualBlock.SetColor(ColorId, color);
                target.SetPropertyBlock(visualBlock);
            }
        }

        private void RefreshVisualState()
        {
            if (isDead)
            {
                return;
            }

            if (isTakingDamage)
            {
                ApplyColor(takingDamageColor);
                return;
            }

            if (isMoving)
            {
                ApplyColor(movingColor);
                return;
            }

            if (isPreviewTarget)
            {
                ApplyColor(previewTargetColor);
                return;
            }

            if (isAttackTarget)
            {
                ApplyColor(attackTargetColor);
                return;
            }

            if (isSelected)
            {
                ApplyColor(selectedColor);
                return;
            }

            Color factionColor = faction == UnitFaction.Player ? playerColor : enemyColor;
            Color definitionColor = unitDefinition != null ? unitDefinition.BaseDisplayColor : Color.white;
            Color color = Color.Lerp(definitionColor, factionColor, 0.45f);
            if (hasActed)
            {
                color *= Mathf.Clamp01(actedBrightness);
                color.a = 1f;
            }

            ApplyColor(color);
        }

        private void OnDestroy()
        {
            if (floatingDamageCoroutine != null)
                StopCoroutine(floatingDamageCoroutine);
        }
    }
}
