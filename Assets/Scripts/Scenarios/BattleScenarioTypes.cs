using System;
using System.Collections.Generic;
using SLG.Core;
using SLG.Units;

namespace SLG.Scenarios
{
    public enum BattleMapPreset
    {
        OpenField,
        TerrainCosts,
        WaterCrossing,
        NarrowPass,
        CombatArena
    }

    public enum BattleFormationPreset
    {
        None,
        KnightOnly,
        KnightHealer,
        KnightMage,
        GroundFlying,
        FullTestParty,
        SingleMelee,
        TwoMelee,
        MeleeRanged,
        ClusterForArea,
        FullTestGroup
    }

    public enum BattleSkillLoadoutPreset
    {
        BasicCombatOnly,
        DamageSkill,
        HealingSkill,
        AreaSkill,
        AllCurrentSkills
    }

    public enum BattleObjectiveType
    {
        EliminateAllEnemies,
        ReachArea,
        SurviveRounds,
        ProtectUnit
    }

    public enum BattleUnitRole
    {
        None,
        Knight,
        Healer,
        Mage,
        Flyer,
        Ally,
        Enemy,
        Reinforcement
    }

    public enum BattleDestinationZonePreset
    {
        None,
        EastExit,
        Center,
        NorthExit
    }

    public enum BattleReinforcementPreset
    {
        None,
        Round2OneEnemy,
        Round3TwoEnemies,
        Round2AndRound4,
        OccupiedFallback,
        FailedPlacement
    }

    public enum BattleTestPresetId
    {
        MovementBasic,
        TerrainCosts,
        FlyingOverWater,
        MovementRollback,
        NormalAttack,
        Counterattack,
        TerrainDefense,
        DamageSkill,
        HealingSkill,
        CrossAreaSkill,
        TurnFlow,
        EnemyAI,
        Victory,
        Defeat,
        EliminateNoReinforcements,
        EliminateRound3Reinforcements,
        ReachAreaKnight,
        ReachAreaWrongUnitPresent,
        Survive3Rounds,
        ProtectHealer,
        ReachAndEliminate,
        ProtectAndSurvive,
        ReinforcementSpawnOccupiedFallback,
        FullScenarioSmoke
    }

    [Serializable]
    public sealed class BattleSetupConfiguration
    {
        public string ScenarioName = "Scenario";
        public string ManualVerificationNotes = string.Empty;
        public BattleMapPreset MapPreset = BattleMapPreset.OpenField;
        public BattleFormationPreset PlayerFormation = BattleFormationPreset.KnightOnly;
        public BattleFormationPreset EnemyFormation = BattleFormationPreset.SingleMelee;
        public BattleSkillLoadoutPreset SkillLoadout = BattleSkillLoadoutPreset.BasicCombatOnly;
        public int Width = 5;
        public int Height = 5;
        public string[] TerrainRows = { "PPPPP", "PPPPP", "PPPPP", "PPPPP", "PPPPP" };
        public bool AiEnabled = true;
        public bool RequireEliminateAllEnemies;
        public int FallbackRadius = 2;
        public List<BattleUnitSetup> Units = new List<BattleUnitSetup>();
        public List<BattleObjectiveSetup> Objectives = new List<BattleObjectiveSetup>();
        public List<BattleReinforcementWaveSetup> Reinforcements = new List<BattleReinforcementWaveSetup>();

        public BattleSetupConfiguration Clone()
        {
            BattleSetupConfiguration copy = new BattleSetupConfiguration
            {
                ScenarioName = ScenarioName,
                ManualVerificationNotes = ManualVerificationNotes,
                MapPreset = MapPreset,
                PlayerFormation = PlayerFormation,
                EnemyFormation = EnemyFormation,
                SkillLoadout = SkillLoadout,
                Width = Width,
                Height = Height,
                TerrainRows = TerrainRows != null ? (string[])TerrainRows.Clone() : null,
                AiEnabled = AiEnabled,
                RequireEliminateAllEnemies = RequireEliminateAllEnemies,
                FallbackRadius = FallbackRadius
            };

            for (int i = 0; i < Units.Count; i++)
            {
                copy.Units.Add(Units[i].Clone());
            }

            for (int i = 0; i < Objectives.Count; i++)
            {
                copy.Objectives.Add(Objectives[i].Clone());
            }

            for (int i = 0; i < Reinforcements.Count; i++)
            {
                copy.Reinforcements.Add(Reinforcements[i].Clone());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class BattleUnitSetup
    {
        public string Key;
        public string DisplayName;
        public BattleUnitRole Role;
        public UnitDefinition Definition;
        public UnitFaction Faction;
        public GridCoordinate Coordinate;
        public int CurrentHealth;

        public BattleUnitSetup Clone()
        {
            return (BattleUnitSetup)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class BattleObjectiveSetup
    {
        public BattleObjectiveType Type;
        public BattleUnitRole UnitRole;
        public BattleDestinationZonePreset DestinationZone;
        public List<GridCoordinate> DestinationCoordinates = new List<GridCoordinate>();
        public int RequiredRounds;
        public bool DesignatedUnitRequired = true;

        public BattleObjectiveSetup Clone()
        {
            BattleObjectiveSetup copy = (BattleObjectiveSetup)MemberwiseClone();
            copy.DestinationCoordinates = new List<GridCoordinate>(DestinationCoordinates);
            return copy;
        }
    }

    [Serializable]
    public sealed class BattleReinforcementWaveSetup
    {
        public string Key;
        public int ArrivalRound = 2;
        public BattlePhase ArrivalPhase = BattlePhase.EnemyTurn;
        public UnitDefinition UnitDefinition;
        public UnitFaction Faction = UnitFaction.Enemy;
        public BattleUnitRole Role = BattleUnitRole.Reinforcement;
        public GridCoordinate SpawnCoordinate;
        public bool RequiredForEliminateAllEnemies = true;
        public int Count = 1;

        public BattleReinforcementWaveSetup Clone()
        {
            return (BattleReinforcementWaveSetup)MemberwiseClone();
        }
    }
}
