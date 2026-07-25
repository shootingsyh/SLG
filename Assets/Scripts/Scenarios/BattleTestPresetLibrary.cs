using System;
using System.Collections.Generic;
using SLG.Core;
using SLG.Skills;
using SLG.Units;
using UnityEngine;

namespace SLG.Scenarios
{
    public sealed class BattleTestPresetMetadata
    {
        public BattleTestPresetId Id;
        public string DisplayName;
        public BattleObjectiveType[] ExpectedObjectives = Array.Empty<BattleObjectiveType>();
        public int ExpectedReinforcements;
        public string VerificationNotes;
    }

    public static class BattleTestPresetLibrary
    {
        private static readonly BattleTestPresetMetadata[] presets =
        {
            Preset(BattleTestPresetId.MovementBasic, "Movement Basic", 0, "Verify basic movement and Wait."),
            Preset(BattleTestPresetId.TerrainCosts, "Terrain Costs", 0, "Verify Forest costs more movement."),
            Preset(BattleTestPresetId.FlyingOverWater, "Flying over Water", 0, "Verify Flyer can cross Water and Knight cannot."),
            Preset(BattleTestPresetId.MovementRollback, "Movement Rollback", 0, "Verify Cancel returns the unit to its original tile."),
            Preset(BattleTestPresetId.NormalAttack, "Normal Attack", 0, "Verify melee attack damage."),
            Preset(BattleTestPresetId.Counterattack, "Counterattack", 0, "Verify defender counterattacks when alive and in range."),
            Preset(BattleTestPresetId.TerrainDefense, "Terrain Defense", 0, "Verify terrain defense reduces damage."),
            Preset(BattleTestPresetId.DamageSkill, "Damage Skill", 0, "Verify Fire Bolt damages enemies."),
            Preset(BattleTestPresetId.HealingSkill, "Healing Skill", 0, "Verify Heal restores ally HP."),
            Preset(BattleTestPresetId.CrossAreaSkill, "Cross Area Skill", 0, "Verify cross area damages only enemies in area."),
            Preset(BattleTestPresetId.TurnFlow, "Turn Flow", 0, "Verify Player and Enemy phases alternate."),
            Preset(BattleTestPresetId.EnemyAI, "Enemy AI", 0, "Verify enemy moves and attacks nearest player."),
            Preset(BattleTestPresetId.Victory, "Victory", 0, "Verify defeating all enemies wins."),
            Preset(BattleTestPresetId.Defeat, "Defeat", 0, "Verify all players defeated loses."),
            Preset(BattleTestPresetId.EliminateNoReinforcements, "Eliminate with No Reinforcements", 0, "Verify Victory after all starting enemies are defeated."),
            Preset(BattleTestPresetId.EliminateRound3Reinforcements, "Eliminate with Round 3 Reinforcements", 1, "Verify Victory waits for Round 3 reinforcements."),
            Preset(BattleTestPresetId.ReachAreaKnight, "Reach Area with Knight", 0, "Verify Knight must commit action in eastern zone."),
            Preset(BattleTestPresetId.ReachAreaWrongUnitPresent, "Reach Area with Wrong Unit Present", 0, "Verify Mage entering zone does not complete Knight reach objective."),
            Preset(BattleTestPresetId.Survive3Rounds, "Survive 3 Rounds", 0, "Verify Victory after three completed Enemy phases."),
            Preset(BattleTestPresetId.ProtectHealer, "Protect Healer", 0, "Verify Healer death causes Defeat."),
            Preset(BattleTestPresetId.ReachAndEliminate, "Reach + Eliminate", 1, "Verify Knight reaches exit and all enemies including reinforcements are defeated."),
            Preset(BattleTestPresetId.ProtectAndSurvive, "Protect + Survive", 0, "Verify Healer must live until survive objective completes."),
            Preset(BattleTestPresetId.ReinforcementSpawnOccupiedFallback, "Reinforcement Spawn Occupied Fallback", 1, "Verify blocked spawn uses nearest deterministic fallback."),
            Preset(BattleTestPresetId.FullScenarioSmoke, "Full Scenario Smoke", 2, "Verify reach, protect, survive, eliminate, and reinforcements together.")
        };

        public static IReadOnlyList<BattleTestPresetMetadata> Presets => presets;

        public static BattleSetupConfiguration Create(BattleTestPresetId id)
        {
            RuntimeDefinitions defs = RuntimeDefinitions.Create();
            BattleSetupConfiguration config = BaseConfig(id, defs);
            ApplyPreset(id, config, defs);
            return config;
        }

        public static BattleTestPresetMetadata GetMetadata(BattleTestPresetId id)
        {
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i].Id == id)
                {
                    return presets[i];
                }
            }

            return presets[0];
        }

        private static BattleTestPresetMetadata Preset(BattleTestPresetId id, string name, int reinforcements, string notes)
        {
            return new BattleTestPresetMetadata
            {
                Id = id,
                DisplayName = name,
                ExpectedReinforcements = reinforcements,
                VerificationNotes = notes
            };
        }

        private static BattleSetupConfiguration BaseConfig(BattleTestPresetId id, RuntimeDefinitions defs)
        {
            BattleSetupConfiguration config = new BattleSetupConfiguration
            {
                ScenarioName = GetMetadata(id).DisplayName,
                ManualVerificationNotes = GetMetadata(id).VerificationNotes,
                MapPreset = BattleMapPreset.OpenField,
                PlayerFormation = BattleFormationPreset.KnightOnly,
                EnemyFormation = BattleFormationPreset.SingleMelee,
                SkillLoadout = BattleSkillLoadoutPreset.BasicCombatOnly,
                Width = 5,
                Height = 5,
                TerrainRows = new[] { "PPPPP", "PPPPP", "PPPPP", "PPPPP", "PPPPP" },
                AiEnabled = true,
                RequireEliminateAllEnemies = true,
                FallbackRadius = 2
            };

            AddUnit(config, "knight", "Knight", BattleUnitRole.Knight, defs.Knight, UnitFaction.Player, 0, 2);
            AddUnit(config, "enemy", "Enemy", BattleUnitRole.Enemy, defs.Enemy, UnitFaction.Enemy, 3, 2);
            Objective(config, BattleObjectiveType.EliminateAllEnemies);
            return config;
        }

        private static void ApplyPreset(BattleTestPresetId id, BattleSetupConfiguration config, RuntimeDefinitions defs)
        {
            switch (id)
            {
                case BattleTestPresetId.TerrainCosts:
                case BattleTestPresetId.TerrainDefense:
                    config.MapPreset = BattleMapPreset.TerrainCosts;
                    config.TerrainRows = new[] { "PPPPP", "PFFFP", "PPPPP", "PPPPP", "PPPPP" };
                    break;
                case BattleTestPresetId.FlyingOverWater:
                    config.MapPreset = BattleMapPreset.WaterCrossing;
                    config.TerrainRows = new[] { "PPPPP", "PWWWP", "PPPPP", "PPPPP", "PPPPP" };
                    AddUnit(config, "flyer", "Flyer", BattleUnitRole.Flyer, defs.Flyer, UnitFaction.Player, 0, 1);
                    config.PlayerFormation = BattleFormationPreset.GroundFlying;
                    break;
                case BattleTestPresetId.DamageSkill:
                    config.Units[0].Definition = defs.Mage;
                    config.Units[0].Role = BattleUnitRole.Mage;
                    config.SkillLoadout = BattleSkillLoadoutPreset.DamageSkill;
                    break;
                case BattleTestPresetId.HealingSkill:
                    AddUnit(config, "healer", "Healer", BattleUnitRole.Healer, defs.Healer, UnitFaction.Player, 0, 1);
                    config.PlayerFormation = BattleFormationPreset.KnightHealer;
                    config.SkillLoadout = BattleSkillLoadoutPreset.HealingSkill;
                    break;
                case BattleTestPresetId.CrossAreaSkill:
                    config.Units[0].Definition = defs.Mage;
                    config.Units[0].Role = BattleUnitRole.Mage;
                    AddUnit(config, "areaEnemy", "Area Enemy", BattleUnitRole.Enemy, defs.Enemy, UnitFaction.Enemy, 3, 1);
                    config.EnemyFormation = BattleFormationPreset.ClusterForArea;
                    config.SkillLoadout = BattleSkillLoadoutPreset.AreaSkill;
                    break;
                case BattleTestPresetId.EliminateRound3Reinforcements:
                    AddWave(config, defs.Enemy, 3, 4, 2, 1);
                    break;
                case BattleTestPresetId.ReachAreaKnight:
                case BattleTestPresetId.ReachAreaWrongUnitPresent:
                    config.RequireEliminateAllEnemies = false;
                    config.Objectives.Clear();
                    config.Units.RemoveAll(unit => unit.Faction == UnitFaction.Enemy);
                    ReachObjective(config, BattleUnitRole.Knight, 4, 2);
                    if (id == BattleTestPresetId.ReachAreaWrongUnitPresent)
                    {
                        AddUnit(config, "mage", "Mage", BattleUnitRole.Mage, defs.Mage, UnitFaction.Player, 0, 1);
                    }
                    break;
                case BattleTestPresetId.Survive3Rounds:
                    config.RequireEliminateAllEnemies = false;
                    config.Objectives.Clear();
                    Objective(config, BattleObjectiveType.SurviveRounds, BattleUnitRole.None, 3);
                    break;
                case BattleTestPresetId.ProtectHealer:
                    AddUnit(config, "healer", "Healer", BattleUnitRole.Healer, defs.Healer, UnitFaction.Player, 0, 1);
                    config.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.ProtectUnit, UnitRole = BattleUnitRole.Healer });
                    break;
                case BattleTestPresetId.ReachAndEliminate:
                    ReachObjective(config, BattleUnitRole.Knight, 4, 2);
                    AddWave(config, defs.Enemy, 3, 4, 1, 1);
                    break;
                case BattleTestPresetId.ProtectAndSurvive:
                    config.RequireEliminateAllEnemies = false;
                    config.Objectives.Clear();
                    AddUnit(config, "healer", "Healer", BattleUnitRole.Healer, defs.Healer, UnitFaction.Player, 0, 1);
                    Objective(config, BattleObjectiveType.SurviveRounds, BattleUnitRole.None, 3);
                    config.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.ProtectUnit, UnitRole = BattleUnitRole.Healer });
                    break;
                case BattleTestPresetId.ReinforcementSpawnOccupiedFallback:
                    AddUnit(config, "blocker", "Blocker", BattleUnitRole.Ally, defs.Knight, UnitFaction.Player, 4, 2);
                    AddWave(config, defs.Enemy, 2, 4, 2, 1);
                    break;
                case BattleTestPresetId.FullScenarioSmoke:
                    config.SkillLoadout = BattleSkillLoadoutPreset.AllCurrentSkills;
                    AddUnit(config, "healer", "Healer", BattleUnitRole.Healer, defs.Healer, UnitFaction.Player, 0, 1);
                    AddUnit(config, "mage", "Mage", BattleUnitRole.Mage, defs.Mage, UnitFaction.Player, 1, 2);
                    ReachObjective(config, BattleUnitRole.Knight, 4, 2);
                    Objective(config, BattleObjectiveType.SurviveRounds, BattleUnitRole.None, 2);
                    config.Objectives.Add(new BattleObjectiveSetup { Type = BattleObjectiveType.ProtectUnit, UnitRole = BattleUnitRole.Healer });
                    AddWave(config, defs.Enemy, 2, 4, 1, 1);
                    AddWave(config, defs.Enemy, 4, 4, 3, 1);
                    break;
            }
        }

        private static void Objective(BattleSetupConfiguration config, BattleObjectiveType type, BattleUnitRole role = BattleUnitRole.None, int rounds = 0)
        {
            config.Objectives.Add(new BattleObjectiveSetup { Type = type, UnitRole = role, RequiredRounds = rounds });
        }

        private static void ReachObjective(BattleSetupConfiguration config, BattleUnitRole role, int x, int y)
        {
            config.Objectives.Add(new BattleObjectiveSetup
            {
                Type = BattleObjectiveType.ReachArea,
                UnitRole = role,
                DestinationZone = BattleDestinationZonePreset.EastExit,
                DestinationCoordinates = new List<GridCoordinate> { new GridCoordinate(x, y) },
                DesignatedUnitRequired = true
            });
        }

        private static void AddWave(BattleSetupConfiguration config, UnitDefinition definition, int round, int x, int y, int count)
        {
            config.Reinforcements.Add(new BattleReinforcementWaveSetup
            {
                Key = $"wave{config.Reinforcements.Count + 1}",
                ArrivalRound = round,
                ArrivalPhase = BattlePhase.EnemyTurn,
                UnitDefinition = definition,
                Faction = UnitFaction.Enemy,
                SpawnCoordinate = new GridCoordinate(x, y),
                Count = count,
                RequiredForEliminateAllEnemies = true
            });
        }

        private static void AddUnit(BattleSetupConfiguration config, string key, string displayName, BattleUnitRole role, UnitDefinition definition, UnitFaction faction, int x, int y)
        {
            config.Units.Add(new BattleUnitSetup
            {
                Key = key,
                DisplayName = displayName,
                Role = role,
                Definition = definition,
                Faction = faction,
                Coordinate = new GridCoordinate(x, y),
                CurrentHealth = definition != null ? definition.MaxHealth : 0
            });
        }

        private sealed class RuntimeDefinitions
        {
            public UnitDefinition Knight;
            public UnitDefinition Healer;
            public UnitDefinition Mage;
            public UnitDefinition Flyer;
            public UnitDefinition Enemy;

            public static RuntimeDefinitions Create()
            {
                SkillDefinition fire = Skill("fire", "Fire Bolt", SkillEffectType.Damage, SkillTargetType.Unit, SkillAreaShape.Single, 0, 1, 3, 3, false, false, true, false);
                SkillDefinition heal = Skill("heal", "Heal", SkillEffectType.Heal, SkillTargetType.Unit, SkillAreaShape.Single, 0, 0, 2, 5, true, true, false, false);
                SkillDefinition cross = Skill("cross", "Flame Cross", SkillEffectType.Damage, SkillTargetType.Ground, SkillAreaShape.Cross, 1, 1, 3, 2, false, false, true, true);
                return new RuntimeDefinitions
                {
                    Knight = Unit("Knight", MovementProfile.Ground, 18, 7, 2, 4, 1, 1, null),
                    Healer = Unit("Healer", MovementProfile.Ground, 14, 2, 1, 4, 1, 1, new[] { heal }),
                    Mage = Unit("Mage", MovementProfile.Ground, 12, 5, 1, 4, 1, 2, new[] { fire, cross }),
                    Flyer = Unit("Flyer", MovementProfile.Flying, 14, 4, 1, 5, 1, 1, null),
                    Enemy = Unit("Enemy", MovementProfile.Ground, 10, 4, 1, 3, 1, 1, null)
                };
            }

            private static UnitDefinition Unit(string name, MovementProfile profile, int hp, int attack, int defense, int move, int minRange, int maxRange, IReadOnlyList<SkillDefinition> skills)
            {
                UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
                definition.name = name;
                definition.ConfigureRuntime(name, name, hp, attack, defense, move, profile, minRange, maxRange, skills);
                return definition;
            }

            private static SkillDefinition Skill(string id, string name, SkillEffectType effect, SkillTargetType target, SkillAreaShape area, int areaSize, int minRange, int maxRange, int power, bool self, bool allies, bool enemies, bool ground)
            {
                SkillDefinition skill = ScriptableObject.CreateInstance<SkillDefinition>();
                skill.name = name;
                skill.ConfigureRuntime(id, name, name, effect, target, area, areaSize, minRange, maxRange, power, self, allies, enemies, ground);
                return skill;
            }
        }
    }
}
