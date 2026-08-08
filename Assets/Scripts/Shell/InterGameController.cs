using System.Collections.Generic;
using SLG.Items;
using SLG.Saves;
using SLG.Scenarios;
using UnityEngine;

namespace SLG.Shell
{
    public sealed class InterGameController : MonoBehaviour
    {
        private string gameId = string.Empty;
        // private string completedBattleCount = "0"; // Unused - use completedCount instead
        private string nextBattleId = string.Empty;
        private string message = string.Empty;
        private readonly GameFlowService flow = new GameFlowService();
        private bool saved;
        private int completedCount;

        // Double-click prevention
        private float lastNextBattleClickTime;
        private float lastSaveClickTime;
        private readonly float cooldownDelay = 0.5f;

        // Save status display
        private int savedSlotIndex = 0;
        private float saveStatusTimer;
        private const float saveStatusDuration = 3f;

        // Inventory/Equipment UI
        private string selectedUnitId = "knight";
        private Vector2 inventoryScrollPos;
        private string inventoryMessage = string.Empty;
        private float inventoryMessageTimer;

        private void Awake()
        {
            gameId = GameShellServices.ActiveGameId;
            completedCount = 0;
            ParseGameState();
        }

        private void ParseGameState()
        {
            if (string.IsNullOrEmpty(gameId))
                return;

            GameDefinition gameDef = GameDefinitions.Get(gameId);
            if (!string.IsNullOrEmpty(GameShellServices.InterGameCompletedBattleId) || !string.IsNullOrEmpty(GameShellServices.InterGameNextBattleId))
            {
                completedCount = ResolveCompletedCount(gameDef, GameShellServices.InterGameCompletedBattleId, GameShellServices.InterGameNextBattleId);
                nextBattleId = GameShellServices.InterGameNextBattleId ?? string.Empty;
                return;
            }

            CampaignSaveData progress = LoadGameProgress(gameId);
            if (progress != null)
            {
                completedCount = ResolveCompletedCount(gameDef, progress.LastCompletedChapterId, progress.NextBattleId);
                nextBattleId = progress.NextBattleId ?? string.Empty;
            }
        }

        private static CampaignSaveData LoadGameProgress(string gameId)
        {
            foreach (SaveSlotInfo slot in GameShellServices.Repository.ListCampaignSlots())
            {
                if (slot.CanLoad && slot.Metadata != null && slot.Metadata.GameId == gameId)
                {
                    GameShellServices.Repository.TryLoadCampaign(slot.FileName, out CampaignSaveData data, out _);
                    return data;
                }
            }
            return null;
        }

        private static int ResolveCompletedCount(GameDefinition gameDef, string completedBattleId, string nextBattleId)
        {
            if (gameDef == null) return 0;
            if (!string.IsNullOrEmpty(nextBattleId))
            {
                for (int i = 0; i < gameDef.BattleCount; i++)
                {
                    GameBattleDefinition battle = gameDef.GetBattleAt(i);
                    if (battle != null && battle.BattleId == nextBattleId)
                        return i;
                }
            }

            if (!string.IsNullOrEmpty(completedBattleId))
            {
                for (int i = 0; i < gameDef.BattleCount; i++)
                {
                    GameBattleDefinition battle = gameDef.GetBattleAt(i);
                    if (battle != null && battle.BattleId == completedBattleId)
                        return i + 1;
                }
            }

            return 0;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(gameId))
            {
                GUILayout.BeginArea(new Rect(Screen.width * 0.2f, Screen.height * 0.35f, Screen.width * 0.6f, 100f), GUI.skin.box);
                GUILayout.Label("Inter-Game");
                GUILayout.Label("No active game. Returning to title.");
                if (GUILayout.Button("Return to Title")) flow.TryLoadTitleScene();
                GUILayout.EndArea();
                return;
            }

            GameDefinition gameDef = GameDefinitions.Get(gameId);
            string gameName = gameDef != null ? gameDef.DisplayName : "Unknown Game";

            // Save status timer
            if (saveStatusTimer > 0f)
                saveStatusTimer -= Time.deltaTime;

            GUILayout.BeginArea(new Rect(Screen.width * 0.15f, Screen.height * 0.08f, Screen.width * 0.7f, 600f), GUI.skin.box);

            GUILayout.Label("Inter-Game");
            if (!string.IsNullOrEmpty(gameName))
                GUILayout.Label($"Game: {gameName}");
            GUILayout.Label($"Completed Battles: {completedCount}");

            GameBattleDefinition nextBattle = null;
            if (gameDef != null)
            {
                nextBattle = gameDef.GetNextBattle(completedCount);
                if (nextBattle != null)
                {
                    GUILayout.Space(8f);
                    GUILayout.Label($"Next Battle: {nextBattle.BattleName}");
                    GUILayout.Label($"Battle ID: {nextBattle.BattleId}");
                }
            }

            GUILayout.Space(12f);
            GUILayout.Label("Save Campaign Progress:");
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= SaveConstants.ManualCampaignSlotCount; i++)
            {
                // Secondary button styling (smaller, gray borders)
                GUI.enabled = Time.time - lastSaveClickTime >= cooldownDelay;
                GUILayout.BeginVertical(GUILayout.Width(80f));
                if (GUILayout.Button($"Slot {i}", GUILayout.Height(32f)))
                    SaveCampaignToSlot(i);
                GUILayout.EndVertical();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            // Save status with visual feedback
            if (saveStatusTimer > 0f && saved)
            {
                GUILayout.Label($"✓ Saved to Slot {savedSlotIndex}");
            }
            else if (!string.IsNullOrEmpty(message))
            {
                GUILayout.Label(message);
            }

            GUILayout.Space(8f);
            DrawInventoryEquipmentPanel();

            GUILayout.FlexibleSpace();

            // Primary action buttons (Next Battle or Complete, with cooldown)
            GUILayout.BeginHorizontal();
            GUI.enabled = Time.time - lastNextBattleClickTime >= cooldownDelay;

            if (nextBattle != null)
            {
                // Primary button - wider, larger
                Rect buttonRect = GUILayoutUtility.GetRect(120, 40);
                GUI.skin.button.fontSize = 16;
                if (GUI.Button(buttonRect, "Next Battle ▶"))
                {
                    lastNextBattleClickTime = Time.time;
                    flow.TryNextBattle();
                }
                GUI.skin.button.fontSize = 13;
            }

            if (completedCount >= 0)
            {
                if (nextBattle == null && gameDef != null && completedCount >= gameDef.BattleCount)
                {
                    Rect buttonRect = GUILayoutUtility.GetRect(200, 40);
                    GUI.skin.button.fontSize = 14;
                    if (GUI.Button(buttonRect, "Game Complete"))
                    {
                        lastNextBattleClickTime = Time.time;
                        flow.TryCompleteTestGame();
                    }
                    GUI.skin.button.fontSize = 13;
                }
            }
            GUI.enabled = true;

            // Return to Title as tertiary (smaller, right-aligned)
            if (GUILayout.Button("Return", GUILayout.Height(32f)))
            {
                flow.TryLoadTitleScene();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void SaveCampaignToSlot(int slot)
        {
            if (Time.time - lastSaveClickTime < cooldownDelay)
                return;

            CampaignSaveData data = BuildCampaignSave();
            SaveOperationResult result = GameShellServices.Repository.SaveCampaign(data, slot);
            message = result.Message;
            if (result.Success)
            {
                saved = true;
                savedSlotIndex = slot;
                saveStatusTimer = saveStatusDuration;
                lastSaveClickTime = Time.time;
                message = string.Empty;
                GameShellServices.Repository.DeleteBattleSave();
            }
            else
            {
                lastSaveClickTime = Time.time;
            }
        }

        private CampaignSaveData BuildCampaignSave()
        {
            CampaignSaveData data = new CampaignSaveData
            {
                GameId = gameId,
                LastCompletedChapterId = string.Empty,
                NextChapterId = ParseNextBattleChapter(),
                NextBattleId = GetNextBattleId(),
                FlowScreen = DemoFlowState.Battle1Complete,
                UnlockedChapterIds = new List<string> { "chapter-1" },
                Inventory = GameShellServices.CampaignInventory.ToEntries(),
                Equipment = GameShellServices.CampaignEquipment.ToEntries(),
                ClaimedRewardBattleIds = new List<string>(GameShellServices.ClaimedRewards)
            };

            GameDefinition def = GameDefinitions.Get(gameId);
            if (def != null)
            {
                for (int i = 0; i < completedCount && i < def.BattleCount; i++)
                {
                    GameBattleDefinition battle = def.GetBattleAt(i);
                    if (battle != null)
                        data.LastCompletedChapterId = battle.BattleId;
                }

                GameBattleDefinition next = def.GetNextBattle(completedCount);
                if (next != null)
                    data.NextBattleId = next.BattleId;
            }

            return data;
        }

        private string ParseNextBattleChapter()
        {
            GameDefinition def = GameDefinitions.Get(gameId);
            if (def == null) return "chapter-1";
            GameBattleDefinition next = def.GetNextBattle(completedCount);
            if (next == null) return "chapter-complete";
            return $"chapter-{completedCount + 1}";
        }

        private string GetNextBattleId()
        {
            GameDefinition def = GameDefinitions.Get(gameId);
            if (def == null) return string.Empty;
            GameBattleDefinition next = def.GetNextBattle(completedCount);
            return next != null ? next.BattleId : string.Empty;
        }

        private void DrawInventoryEquipmentPanel()
        {
            if (inventoryMessageTimer > 0f) inventoryMessageTimer -= Time.deltaTime;
            else inventoryMessage = string.Empty;

            GUILayout.Space(6f);
            GUILayout.Label("Inventory & Equipment", GUI.skin.box);

            // Inventory view
            var inv = GameShellServices.CampaignInventory;
            GUILayout.Label("Inventory:");
            inventoryScrollPos = GUILayout.BeginScrollView(inventoryScrollPos, GUILayout.Height(80f));
            if (inv.Quantities.Count == 0)
            {
                GUILayout.Label("(empty)");
            }
            else
            {
                foreach (var kv in inv.Quantities)
                {
                    var def = ItemCatalog.Get(kv.Key);
                    string name = def != null ? def.DisplayName : kv.Key;
                    GUILayout.Label($"{name} x{kv.Value} ({kv.Key})");
                }
            }
            GUILayout.EndScrollView();

            // Unit selection
            GUILayout.Space(4f);
            GUILayout.Label($"Selected Unit: {selectedUnitId}");
            GUILayout.BeginHorizontal();
            foreach (var uid in new[] { "knight", "healer", "mage" })
            {
                GUI.enabled = selectedUnitId != uid;
                if (GUILayout.Button(uid, GUILayout.Width(80f))) selectedUnitId = uid;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // Equipment slots
            var equip = GameShellServices.CampaignEquipment;
            string weapon = equip.GetEquipped(selectedUnitId, EquipmentSlot.Weapon);
            string armor = equip.GetEquipped(selectedUnitId, EquipmentSlot.Armor);
            string accessory = equip.GetEquipped(selectedUnitId, EquipmentSlot.Accessory);
            GUILayout.Space(4f);
            GUILayout.Label($"Weapon: {(string.IsNullOrEmpty(weapon) ? "(none)" : ItemCatalog.Get(weapon)?.DisplayName ?? weapon)}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Equip Sword", GUILayout.Width(110f)))
            {
                if (equip.Equip(selectedUnitId, "iron-sword", inv)) inventoryMessage = "Equipped Iron Sword";
                else inventoryMessage = "Cannot equip Sword (not owned or slot occupied)";
                inventoryMessageTimer = 3f;
            }
            if (GUILayout.Button("Unequip", GUILayout.Width(80f)))
            {
                equip.Unequip(selectedUnitId, EquipmentSlot.Weapon);
                inventoryMessage = "Unequipped Weapon";
                inventoryMessageTimer = 3f;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Armor: {(string.IsNullOrEmpty(armor) ? "(none)" : ItemCatalog.Get(armor)?.DisplayName ?? armor)}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Equip Armor", GUILayout.Width(110f)))
            {
                if (equip.Equip(selectedUnitId, "iron-armor", inv)) inventoryMessage = "Equipped Iron Armor";
                else inventoryMessage = "Cannot equip Armor";
                inventoryMessageTimer = 3f;
            }
            if (GUILayout.Button("Unequip", GUILayout.Width(80f)))
            {
                equip.Unequip(selectedUnitId, EquipmentSlot.Armor);
                inventoryMessage = "Unequipped Armor";
                inventoryMessageTimer = 3f;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Accessory: {(string.IsNullOrEmpty(accessory) ? "(none)" : ItemCatalog.Get(accessory)?.DisplayName ?? accessory)}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Equip Charm", GUILayout.Width(110f)))
            {
                if (equip.Equip(selectedUnitId, "traveler-charm", inv)) inventoryMessage = "Equipped Traveler Charm";
                else inventoryMessage = "Cannot equip Charm";
                inventoryMessageTimer = 3f;
            }
            if (GUILayout.Button("Unequip", GUILayout.Width(80f)))
            {
                equip.Unequip(selectedUnitId, EquipmentSlot.Accessory);
                inventoryMessage = "Unequipped Accessory";
                inventoryMessageTimer = 3f;
            }
            GUILayout.EndHorizontal();

            // Derived stats preview
            GetBaseStats(selectedUnitId, out int baseAtk, out int baseDef, out int baseMove);
            int atkBonus = 0, defBonus = 0, moveBonus = 0;
            if (!string.IsNullOrEmpty(weapon)) atkBonus = ItemCatalog.Get(weapon)?.AttackBonus ?? 0;
            if (!string.IsNullOrEmpty(armor)) defBonus = ItemCatalog.Get(armor)?.DefenseBonus ?? 0;
            if (!string.IsNullOrEmpty(accessory)) moveBonus = ItemCatalog.Get(accessory)?.MovementBonus ?? 0;
            GUILayout.Space(4f);
            GUILayout.Label($"Stats: Atk {baseAtk}→{baseAtk + atkBonus}  Def {baseDef}→{baseDef + defBonus}  Move {baseMove}→{baseMove + moveBonus}");

            if (!string.IsNullOrEmpty(inventoryMessage)) GUILayout.Label(inventoryMessage);
        }

        private static void GetBaseStats(string unitId, out int atk, out int def, out int move)
        {
            switch (unitId)
            {
                case "knight": atk = 7; def = 2; move = 4; break;
                case "healer": atk = 2; def = 1; move = 4; break;
                case "mage": atk = 5; def = 1; move = 4; break;
                default: atk = 4; def = 1; move = 3; break;
            }
        }

        public GameDefinition GetCurrentGameDefinition() => GameDefinitions.Get(gameId);
        public int GetCompletedBattleCount() => completedCount;
        public string GetGameId() => gameId;
    }
}
