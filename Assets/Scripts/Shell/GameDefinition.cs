using System;
using System.Collections.Generic;
using SLG.Scenarios;

namespace SLG.Shell
{
    public sealed class GameDefinition
    {
        public string GameId;
        public string DisplayName;
        public GameBattleDefinition[] Battles;

        public int BattleCount => Battles?.Length ?? 0;

        public bool IsComplete(int completedCount) => completedCount >= BattleCount;

        public string CompletedSaveFileName => $"campaign-{GameId}-progress.json";

        public int CompletedCount(int completedCount) => completedCount;

        public GameBattleDefinition GetBattleAt(int index)
        {
            if (Battles == null || index < 0 || index >= Battles.Length)
                return null;
            return Battles[index];
        }

        public GameBattleDefinition GetBattleById(string battleId)
        {
            if (Battles == null) return null;
            for (int i = 0; i < Battles.Length; i++)
            {
                if (Battles[i].BattleId == battleId)
                    return Battles[i];
            }
            return null;
        }

        public GameBattleDefinition GetFirstBattle()
        {
            return BattleCount > 0 ? Battles[0] : null;
        }

        public GameBattleDefinition GetNextBattle(int completedCount)
        {
            if (Battles == null || completedCount >= Battles.Length)
                return null;
            return Battles[completedCount];
        }

        public int CompletedBattleCount(string completedBattleIds)
        {
            if (Battles == null || string.IsNullOrEmpty(completedBattleIds))
                return 0;
            int count = 0;
            foreach (string id in completedBattleIds.Split(','))
            {
                string trimmed = id.Trim();
                for (int i = 0; i < Battles.Length; i++)
                {
                    if (Battles[i].BattleId == trimmed)
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        public string NextBattleId(Dictionary<string, CampaignBattleDefinition> battleMap, int completedCount)
        {
            GameBattleDefinition next = GetNextBattle(completedCount);
            if (next == null) return null;
            CampaignBattleDefinition def = battleMap.GetValueOrDefault(next.BattleId);
            return def?.NextBattleId ?? next.BattleId;
        }

        public bool IsLastBattle(int completedCount)
        {
            return completedCount >= BattleCount - 1;
        }
    }

    public sealed class GameBattleDefinition
    {
        public string BattleId;
        public string BattleName;
        public BattleTestPresetId Preset;
    }

    public static class GameDefinitions
    {
        static readonly Dictionary<string, GameDefinition> registry = new Dictionary<string, GameDefinition>();

        static GameDefinitions()
        {
            RegisterProductionGame();
#if UNITY_EDITOR
            RegisterTestGame();
            RegisterItemTestGame();
#endif
        }

        static void RegisterProductionGame()
        {
            GameDefinition game = new GameDefinition
            {
                GameId = "default",
                DisplayName = "Production Campaign",
                Battles = new[]
                {
                    new GameBattleDefinition { BattleId = "battle-1", BattleName = "Eliminate All Enemies", Preset = BattleTestPresetId.DemoBattle1Eliminate },
                    new GameBattleDefinition { BattleId = "battle-2", BattleName = "Protect The Healer", Preset = BattleTestPresetId.DemoBattle2Protect }
                }
            };

            registry[game.GameId] = game;
        }

#if UNITY_EDITOR
        static void RegisterTestGame()
        {
            GameDefinition game = new GameDefinition
            {
                GameId = "test-1",
                DisplayName = "Test Game (3 Battles)",
                Battles = new[]
                {
                    new GameBattleDefinition { BattleId = "test-battle-1", BattleName = "Swift Victory 1", Preset = BattleTestPresetId.TestSwiftVictory1 },
                    new GameBattleDefinition { BattleId = "test-battle-2", BattleName = "Swift Victory 2", Preset = BattleTestPresetId.TestSwiftVictory2 },
                    new GameBattleDefinition { BattleId = "test-battle-3", BattleName = "Swift Victory 3", Preset = BattleTestPresetId.TestSwiftVictory3 }
                }
            };

            registry[game.GameId] = game;
        }

        static void RegisterItemTestGame()
        {
            GameDefinition game = new GameDefinition
            {
                GameId = "item-test",
                DisplayName = "Item Test Game (3 Battles)",
                Battles = new[]
                {
                    new GameBattleDefinition { BattleId = "item-test-battle-1", BattleName = "Item Test 1 - Potion", Preset = BattleTestPresetId.ItemTestBattle1 },
                    new GameBattleDefinition { BattleId = "item-test-battle-2", BattleName = "Item Test 2 - Sword", Preset = BattleTestPresetId.ItemTestBattle2 },
                    new GameBattleDefinition { BattleId = "item-test-battle-3", BattleName = "Item Test 3 - Armor", Preset = BattleTestPresetId.ItemTestBattle3 }
                }
            };
            registry[game.GameId] = game;
        }
#endif

        public static GameDefinition Get(string gameId)
        {
            registry.TryGetValue(gameId, out GameDefinition def);
            return def;
        }

        public static GameDefinition GetDefault()
        {
            return Get("default");
        }

#if UNITY_EDITOR
        public static GameDefinition GetTest()
        {
            return Get("test-1");
        }

        public static GameDefinition GetItemTest()
        {
            return Get("item-test");
        }
#endif

        public static GameDefinition[] GetAll()
        {
            GameDefinition[] list = new GameDefinition[registry.Count];
            int i = 0;
            foreach (GameDefinition def in registry.Values)
            {
                list[i++] = def;
            }
            return list;
        }

        public static bool Has(string gameId)
        {
            return registry.ContainsKey(gameId);
        }
    }
}
