using System;
using System.Collections;
using System.Collections.Generic;
using SLG.Core;
using SLG.Grid;
using UnityEngine;

namespace SLG.Units
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Renderer))]
    public sealed class Unit : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private string displayName = "Unit";
        [SerializeField] private UnitFaction faction = UnitFaction.Player;
        [SerializeField] private GridCoordinate currentCoordinate;
        [SerializeField] private int movementRange = 3;
        [SerializeField] private int maxHealth = 10;
        [SerializeField] private int currentHealth;
        [SerializeField] private int attackPower = 4;
        [SerializeField] private int defense = 1;
        [SerializeField] private int attackRange = 1;
        [SerializeField] private Tile occupiedTile;
        [SerializeField] private UnitSelectionController selectionController;
        [SerializeField] private float movementSpeed = 4f;
        [SerializeField] private float tileHeightOffset = 0.55f;
        [SerializeField] private Color playerColor = new Color(0.18f, 0.38f, 0.9f, 1f);
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.95f, 0.3f, 1f);
        [SerializeField] private Color movingColor = new Color(0.9f, 0.7f, 1f, 1f);
        [SerializeField] private Color attackTargetColor = new Color(1f, 0.45f, 0.15f, 1f);
        [SerializeField] private Color takingDamageColor = Color.white;
        [SerializeField] private float actedBrightness = 0.45f;

        private Renderer unitRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool isSelected;
        private bool isMoving;
        private bool hasActed;
        private bool isAttackTarget;
        private bool isTakingDamage;
        private bool isDead;
        private TextMesh healthText;

        public string DisplayName => displayName;
        public UnitFaction Faction => faction;
        public GridCoordinate CurrentCoordinate => currentCoordinate;
        public int MovementRange => movementRange;
        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public int AttackPower => attackPower;
        public int Defense => defense;
        public int AttackRange => attackRange;
        public Tile OccupiedTile => occupiedTile;
        public bool IsMoving => isMoving;
        public bool HasActed => hasActed;
        public bool IsAlive => !isDead && currentHealth > 0;

        public void Initialize(UnitSelectionController controller, GridCoordinate coordinate, Tile tile)
        {
            selectionController = controller;
            currentCoordinate = coordinate;
            occupiedTile = tile;
            InitializeHealth();
            ApplySelectionState(false);
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

        public bool ReceiveDamage(int damage)
        {
            if (isDead)
            {
                return false;
            }

            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));
            UpdateHealthDisplay();
            StartCoroutine(DamageFlashRoutine());

            if (currentHealth > 0)
            {
                return false;
            }

            Die();
            return true;
        }

        public void ApplySelectionState(bool selected)
        {
            isSelected = selected;
            RefreshVisualState();
        }

        public void SetHasActed(bool acted)
        {
            hasActed = acted;
            RefreshVisualState();
        }

        public void InitializeHealthForBattle()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 0, maxHealth);
            isDead = currentHealth <= 0;
            UpdateHealthDisplay();
            RefreshVisualState();
        }

        private void Awake()
        {
            CacheRenderer();
            EnsureHealthDisplay();
            InitializeHealth();
            RefreshVisualState();
        }

        private void OnMouseDown()
        {
            selectionController?.HandleUnitClicked(this);
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
            Vector3 lunge = start + direction * 0.18f;
            float elapsed = 0f;

            while (elapsed < 0.08f)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, lunge, Mathf.Clamp01(elapsed / 0.08f));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.08f)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(lunge, start, Mathf.Clamp01(elapsed / 0.08f));
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
            yield return new WaitForSeconds(0.12f);
            isTakingDamage = false;
            RefreshVisualState();
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

        private void InitializeHealth()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

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

        private void UpdateHealthDisplay()
        {
            if (healthText != null)
            {
                healthText.text = $"{currentHealth}/{maxHealth}";
                healthText.gameObject.SetActive(!isDead);
            }
        }

        private void Die()
        {
            isDead = true;
            isSelected = false;
            isMoving = false;
            isAttackTarget = false;
            occupiedTile?.SetOccupyingUnit(null);
            occupiedTile = null;
            UpdateHealthDisplay();
            gameObject.SetActive(false);
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

            Color color = faction == UnitFaction.Player ? playerColor : enemyColor;
            if (hasActed)
            {
                color *= Mathf.Clamp01(actedBrightness);
                color.a = 1f;
            }

            ApplyColor(color);
        }
    }
}
