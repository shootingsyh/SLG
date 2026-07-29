using System;
using System.Collections.Generic;
using SLG.Core;
using SLG.Scenarios;
using SLG.Units;

namespace SLG.Saves
{
    public static class SaveConstants
    {
        public const int FormatVersion = 2;
        public const string GameVersion = "0.2";
        public const string ContentVersion = "scenario-presets-v2";
        public const int ManualCampaignSlotCount = 5;
        public const string CampaignSaveType = "Campaign";
        public const string BattleSaveSaveType = "BattleSave";
        public const string CampaignAutosaveFileName = "campaign-autosave.json";
        public const string BattleSaveFileName = "battle-save.json";
    }

    public enum CampaignSlotKind
    {
        Manual,
        Autosave
    }

    public enum SaveSlotState
    {
        Empty,
        Valid,
        UnsupportedVersion,
        MissingContent,
        Corrupt,
        ChecksumMismatch,
        ReadFailure
    }

    public enum ContinueKind
    {
        None,
        BattleSave,
        Campaign
    }

    public enum DemoFlowState
    {
        NotStarted,
        Battle1Complete,
        Battle2Complete,
        DemoComplete
    }

    [Serializable]
    public sealed class SaveFileEnvelope
    {
        public int FormatVersion;
        public string SaveType;
        public string GameVersion;
        public string ContentVersion;
        public string SavedAtUtc;
        public string PayloadJson;
        public string Checksum;
    }

    [Serializable]
    public sealed class SaveMetadata
    {
        public string SaveType;
        public string SlotId;
        public string ChapterId;
        public string ChapterName;
        public string BattleId;
        public string BattleName;
        public string ProgressLabel;
        public string ObjectiveSummary;
        public string SavedAtUtc;
        public string VersionStatus;
        public int FormatVersion;
        public int Round;
        public DemoFlowState FlowScreen;
        public string GameId = string.Empty;
        public string DestinationScene = string.Empty;
    }

    [Serializable]
    public sealed class CampaignSaveData
    {
        public string SlotId;
        public string RunId = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string GameId = string.Empty;
        public string ChapterId = "chapter-1";
        public string ChapterName;
        public string BattleId;
        public string BattleName;
        public string LastCompletedChapterId = string.Empty;
        public string NextChapterId = "chapter-1";
        public string NextBattleId;
        public string LastSelectedChapterId = "chapter-1";
        public int TotalPlaySeconds;
        public DemoFlowState FlowScreen = DemoFlowState.NotStarted;
        public List<string> UnlockedChapterIds = new List<string> { "chapter-1" };
        public List<CampaignRosterEntry> Roster = new List<CampaignRosterEntry>();
        public List<CampaignInventoryEntry> Inventory = new List<CampaignInventoryEntry>();
        public bool DemoCompleted;
        public SaveMetadata Metadata = new SaveMetadata();
    }

    [Serializable]
    public sealed class BattleSaveData
    {
        public string RunId;
        public string GameId = string.Empty;
        public string ScenarioId;
        public string ChapterId = "chapter-1";
        public string ChapterName;
        public string BattleId;
        public string BattleName;
        public string BattlePresetId;
        public int CurrentRound = 1;
        public int CompletedRounds;
        public BattlePhase CurrentPhase = BattlePhase.PlayerTurn;
        public List<UnitRuntimeSaveData> Units = new List<UnitRuntimeSaveData>();
        public List<int> CompletedObjectiveIndices = new List<int>();
        public List<ReinforcementRuntimeSaveData> Reinforcements = new List<ReinforcementRuntimeSaveData>();
        public List<CampaignRosterEntry> CampaignRoster = new List<CampaignRosterEntry>();
        public List<CampaignInventoryEntry> CampaignInventory = new List<CampaignInventoryEntry>();
        public string CampaignNextChapterId;
        public string CampaignNextBattleId;
        public string ObjectiveSummary;
        public SaveMetadata Metadata = new SaveMetadata();
    }

    [Serializable]
    public sealed class CampaignRosterEntry
    {
        public string UnitId;
        public string UnitName;
        public int Level;
        public int MaxHp;
    }

    [Serializable]
    public sealed class CampaignInventoryEntry
    {
        public string ItemId;
        public int Quantity;
    }

    [Serializable]
    public sealed class UnitRuntimeSaveData
    {
        public string UnitInstanceId;
        public string UnitDefinitionId;
        public UnitFaction Faction;
        public int X;
        public int Y;
        public int CurrentHp;
        public int MaxHp;
        public bool HasActed;
        public bool IsAlive;
    }

    [Serializable]
    public sealed class ReinforcementRuntimeSaveData
    {
        public string WaveId;
        public ReinforcementWaveState State;
    }

    public sealed class SaveSlotInfo
    {
        public string SlotId;
        public string FileName;
        public SaveSlotState State;
        public SaveMetadata Metadata;
        public string Error;
        public string CrossBattleWarning;

        public bool CanLoad => State == SaveSlotState.Valid;
        public bool IsCrossBattle => !string.IsNullOrEmpty(CrossBattleWarning);
    }

    public sealed class ContinueResolution
    {
        public ContinueKind Kind;
        public SaveSlotInfo Slot;
        public string Warning;
        public string DestinationLabel;

        public bool CanContinue => Kind != ContinueKind.None && Slot != null && Slot.CanLoad;
    }

    public readonly struct SaveOperationResult
    {
        public readonly bool Success;
        public readonly string Message;

        public SaveOperationResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static SaveOperationResult Ok(string message = "OK") => new SaveOperationResult(true, message);
        public static SaveOperationResult Fail(string message) => new SaveOperationResult(false, message);
    }
}
