using System;
using System.Collections.Generic;
using SLG.Core;
using SLG.Scenarios;
using SLG.Units;
using UnityEngine;

namespace SLG.Saves
{
    public sealed class SaveRepository
    {
        private readonly ISaveStorage storage;

        public SaveRepository(ISaveStorage storage)
        {
            this.storage = storage;
        }

        public ISaveStorage Storage => storage;

        public SaveOperationResult SaveCampaign(CampaignSaveData data, int slot, string timestampUtc = null)
        {
            if (data == null)
            {
                return SaveOperationResult.Fail("Campaign data is missing.");
            }

            string fileName = SavePathUtility.CampaignSlotFileName(slot);
            data.SlotId = $"slot-{slot:00}";
            FillCampaignMetadata(data, data.SlotId, timestampUtc);
            string json = SaveSerializer.SerializePayload(SaveConstants.CampaignSaveType, data, timestampUtc);
            return storage.TryWriteTextAtomic(fileName, json, out string error) ? SaveOperationResult.Ok("Campaign saved.") : SaveOperationResult.Fail(error);
        }

        public SaveOperationResult SaveCampaignAutosave(CampaignSaveData data, string timestampUtc = null)
        {
            if (data == null)
            {
                return SaveOperationResult.Fail("Campaign data is missing.");
            }

            data.SlotId = "autosave";
            FillCampaignMetadata(data, data.SlotId, timestampUtc);
            string json = SaveSerializer.SerializePayload(SaveConstants.CampaignSaveType, data, timestampUtc);
            return storage.TryWriteTextAtomic(SaveConstants.CampaignAutosaveFileName, json, out string error) ? SaveOperationResult.Ok("Autosave written.") : SaveOperationResult.Fail(error);
        }

        public SaveOperationResult SaveBattle(BattleSaveData data, string timestampUtc = null)
        {
            if (data == null)
            {
                return SaveOperationResult.Fail("Battle save data is missing.");
            }

            FillBattleMetadata(data, timestampUtc);
            string json = SaveSerializer.SerializePayload(SaveConstants.BattleSaveSaveType, data, timestampUtc);
            return storage.TryWriteTextAtomic(SaveConstants.BattleSaveFileName, json, out string error) ? SaveOperationResult.Ok("Battle saved.") : SaveOperationResult.Fail(error);
        }

        public bool TryLoadCampaign(string fileName, out CampaignSaveData data, out SaveSlotInfo info)
        {
            return TryLoad(fileName, SaveConstants.CampaignSaveType, out data, out info);
        }

        public bool TryLoadBattle(out BattleSaveData data, out SaveSlotInfo info)
        {
            return TryLoad(SaveConstants.BattleSaveFileName, SaveConstants.BattleSaveSaveType, out data, out info);
        }

        public bool TryLoadBattleWithCrossBattleCheck(BattleTestPresetId currentPreset, out BattleSaveData data, out SaveSlotInfo info)
        {
            bool loaded = TryLoadBattle(out data, out info);
            if (loaded && data != null)
            {
                BattleTestPresetId savedPreset;
                if (Enum.TryParse(data.BattlePresetId, out savedPreset) && savedPreset != currentPreset)
                {
                    info.CrossBattleWarning = $"Loading from different battle '{data.BattleName}'. Current battle runtime will be torn down and rebuilt.";
                }
            }

            return loaded;
        }

        public IReadOnlyList<SaveSlotInfo> ListCampaignSlots()
        {
            List<SaveSlotInfo> results = new List<SaveSlotInfo>();
            for (int i = 1; i <= SaveConstants.ManualCampaignSlotCount; i++)
            {
                string file = SavePathUtility.CampaignSlotFileName(i);
                TryLoadCampaign(file, out _, out SaveSlotInfo info);
                info.SlotId = $"slot-{i:00}";
                info.FileName = file;
                results.Add(info);
            }

            TryLoadCampaign(SaveConstants.CampaignAutosaveFileName, out _, out SaveSlotInfo autosave);
            autosave.SlotId = "autosave";
            autosave.FileName = SaveConstants.CampaignAutosaveFileName;
            results.Add(autosave);
            return results;
        }

        public SaveSlotInfo ReadBattleSaveInfo()
        {
            TryLoadBattle(out _, out SaveSlotInfo info);
            return info;
        }

        public ContinueResolution ResolveContinue()
        {
            SaveSlotInfo battleSave = ReadBattleSaveInfo();
            if (battleSave.CanLoad)
            {
                string dest = battleSave.Metadata != null ? battleSave.Metadata.ProgressLabel : "Battle Save";
                return new ContinueResolution { Kind = ContinueKind.BattleSave, Slot = battleSave, DestinationLabel = dest };
            }

            SaveSlotInfo bestCampaign = null;
            IReadOnlyList<SaveSlotInfo> slots = ListCampaignSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                SaveSlotInfo slot = slots[i];
                if (!slot.CanLoad)
                {
                    continue;
                }

                if (bestCampaign == null || string.CompareOrdinal(slot.Metadata.SavedAtUtc, bestCampaign.Metadata.SavedAtUtc) > 0)
                {
                    bestCampaign = slot;
                }
            }

            if (bestCampaign != null)
            {
                return new ContinueResolution
                {
                    Kind = ContinueKind.Campaign,
                    Slot = bestCampaign,
                    DestinationLabel = bestCampaign.Metadata?.ProgressLabel ?? "Campaign Save",
                    Warning = battleSave.State != SaveSlotState.Empty && !battleSave.CanLoad ? battleSave.Error : string.Empty
                };
            }

            return new ContinueResolution
            {
                Kind = ContinueKind.None,
                Slot = null,
                Warning = battleSave.State != SaveSlotState.Empty && !battleSave.CanLoad ? battleSave.Error : string.Empty
            };
        }

        public SaveOperationResult DeleteCampaign(string fileName)
        {
            return storage.TryDelete(fileName, out string error) ? SaveOperationResult.Ok("Campaign save deleted.") : SaveOperationResult.Fail(error);
        }

        public SaveOperationResult DeleteBattleSave()
        {
            return storage.TryDelete(SaveConstants.BattleSaveFileName, out string error) ? SaveOperationResult.Ok("Battle save deleted.") : SaveOperationResult.Fail(error);
        }

        private bool TryLoad<T>(string fileName, string saveType, out T data, out SaveSlotInfo info)
        {
            data = default;
            info = new SaveSlotInfo { FileName = fileName, SlotId = fileName, State = SaveSlotState.Empty, Metadata = null, Error = string.Empty, CrossBattleWarning = null };
            if (!storage.Exists(fileName))
            {
                return false;
            }

            if (!storage.TryReadText(fileName, out string text, out string readError))
            {
                info.State = SaveSlotState.ReadFailure;
                info.Error = readError;
                return false;
            }

            bool ok = SaveSerializer.TryDeserializePayload(text, saveType, out data, out SaveMetadata metadata, out SaveSlotState state, out string error);
            info.State = state;
            info.Metadata = metadata;
            info.Error = error;
            if (ok)
            {
                PopulateMetadata(data, info);
            }

            return ok;
        }

        private static void FillCampaignMetadata(CampaignSaveData data, string slotId, string timestampUtc)
        {
            string destScene = "ChapterResult";
            if (!string.IsNullOrEmpty(data.NextBattleId) && data.GameId != "default")
                destScene = "InterGame";

            data.Metadata = new SaveMetadata
            {
                SaveType = SaveConstants.CampaignSaveType,
                SlotId = slotId,
                ChapterId = data.NextChapterId,
                ChapterName = data.ChapterName,
                BattleId = data.NextBattleId,
                BattleName = data.BattleName,
                ProgressLabel = string.IsNullOrEmpty(data.LastCompletedChapterId) ? "New Campaign" : $"After {data.LastCompletedChapterId}",
                SavedAtUtc = timestampUtc ?? DateTime.UtcNow.ToString("O"),
                VersionStatus = "Current",
                FormatVersion = SaveConstants.FormatVersion,
                GameId = data.GameId,
                DestinationScene = destScene
            };
        }

        private static void FillBattleMetadata(BattleSaveData data, string timestampUtc)
        {
            data.Metadata = new SaveMetadata
            {
                SaveType = SaveConstants.BattleSaveSaveType,
                SlotId = "battle-save",
                ChapterId = data.ChapterId,
                ChapterName = data.ChapterName,
                BattleId = data.BattleId,
                BattleName = data.BattleName,
                ProgressLabel = $"{data.BattleName ?? data.ChapterId} · Round {data.CurrentRound}",
                SavedAtUtc = timestampUtc ?? DateTime.UtcNow.ToString("O"),
                VersionStatus = "Current",
                FormatVersion = SaveConstants.FormatVersion,
                Round = data.CurrentRound
            };
        }

        private static void PopulateMetadata<T>(T data, SaveSlotInfo info)
        {
            if (data is CampaignSaveData campaign)
            {
                info.Metadata = campaign.Metadata != null ? campaign.Metadata : new SaveMetadata();
            }
            else if (data is BattleSaveData battle)
            {
                info.Metadata = battle.Metadata != null ? battle.Metadata : new SaveMetadata();
            }
        }

        public CampaignSaveData LoadCampaignDataForTests(string fileName)
        {
            TryLoadCampaign(fileName, out CampaignSaveData data, out _);
            return data;
        }
    }

    public static class BattleSaveSnapshot
    {
        public static BattleSaveData Create(BattleRuntimeContext context, BattleTestPresetId presetId, string chapterId = "chapter-1", string chapterName = "", string battleId = "", string battleName = "", string runId = "", string objectiveSummary = "")
        {
            BattleSaveData data = new BattleSaveData
            {
                RunId = runId,
                ScenarioId = context.Scenario.Configuration.ScenarioName,
                ChapterId = chapterId,
                ChapterName = chapterName,
                BattleId = battleId,
                BattleName = battleName,
                BattlePresetId = presetId.ToString(),
                CurrentRound = context.Scenario.CurrentRound,
                CompletedRounds = context.Scenario.CompletedRounds,
                CurrentPhase = context.Turns.CurrentPhase,
                ObjectiveSummary = objectiveSummary
            };

            foreach (KeyValuePair<string, Unit> pair in context.UnitsByKey)
            {
                Unit unit = pair.Value;
                if (unit == null)
                {
                    continue;
                }

                data.Units.Add(new UnitRuntimeSaveData
                {
                    UnitInstanceId = pair.Key,
                    UnitDefinitionId = unit.Definition != null ? unit.Definition.UnitDefinitionId : string.Empty,
                    Faction = unit.Faction,
                    X = unit.CurrentCoordinate.X,
                    Y = unit.CurrentCoordinate.Y,
                    CurrentHp = unit.CurrentHealth,
                    MaxHp = unit.MaxHealth,
                    HasActed = unit.HasActed,
                    IsAlive = unit.IsAlive
                });
            }

            BattleSetupConfiguration config = context.Scenario.Configuration;
            for (int i = 0; i < config.Objectives.Count; i++)
            {
                if (context.Scenario.State.CompletedObjectives.Contains(config.Objectives[i]))
                {
                    data.CompletedObjectiveIndices.Add(i);
                }
            }

            for (int i = 0; i < config.Reinforcements.Count; i++)
            {
                BattleReinforcementWaveSetup wave = config.Reinforcements[i];
                context.Scenario.State.ReinforcementStates.TryGetValue(wave, out ReinforcementWaveState state);
                data.Reinforcements.Add(new ReinforcementRuntimeSaveData { WaveId = wave.Key, State = state });
            }

            return data;
        }

        public static bool TryValidate(BattleSaveData data, BattleSetupConfiguration config, out string error)
        {
            if (data == null)
            {
                error = "Battle save payload is missing.";
                return false;
            }

            if (data.CurrentPhase != BattlePhase.PlayerTurn)
            {
                error = "Only Player phase battle saves are supported.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>();
            HashSet<GridCoordinate> livingCoordinates = new HashSet<GridCoordinate>();
            for (int i = 0; i < data.Units.Count; i++)
            {
                UnitRuntimeSaveData unit = data.Units[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.UnitInstanceId) || !ids.Add(unit.UnitInstanceId))
                {
                    error = "Duplicate or missing unit stable ID.";
                    return false;
                }

                if (unit.CurrentHp < 0 || unit.CurrentHp > Mathf.Max(1, unit.MaxHp))
                {
                    error = $"Invalid HP for unit '{unit.UnitInstanceId}'.";
                    return false;
                }

                GridCoordinate coordinate = new GridCoordinate(unit.X, unit.Y);
                if (!BattleSetupValidator.IsInside(config, coordinate))
                {
                    error = $"Unit '{unit.UnitInstanceId}' is outside the grid.";
                    return false;
                }

                if (unit.IsAlive && !livingCoordinates.Add(coordinate))
                {
                    error = "Duplicate living unit coordinate.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static bool TryRestore(BattleSaveData data, out BattleRuntimeContext context, out string error)
        {
            context = null;
            error = string.Empty;
            if (!Enum.TryParse(data.BattlePresetId, out BattleTestPresetId presetId))
            {
                error = "Battle preset ID does not resolve.";
                return false;
            }

            BattleSetupConfiguration config = BattleTestPresetLibrary.Create(presetId);
            if (!TryValidate(data, config, out error))
            {
                return false;
            }

            context = BattleScenarioRuntimeBuilder.Build(config, null, true, GameShellServices.Repository, presetId);
            ClearOccupancy(context);
            for (int i = 0; i < data.Units.Count; i++)
            {
                UnitRuntimeSaveData saved = data.Units[i];
                if (!context.UnitsByKey.TryGetValue(saved.UnitInstanceId, out Unit unit))
                {
                    continue;
                }

                GridCoordinate coordinate = new GridCoordinate(saved.X, saved.Y);
                unit.RestoreRuntimeState(coordinate, saved.IsAlive ? saved.CurrentHp : 0, saved.HasActed);
                if (saved.IsAlive && context.Grid.TryGetTile(coordinate, out SLG.Grid.Tile tile))
                {
                    unit.PlaceOnTile(tile);
                    tile.SetOccupyingUnit(unit);
                    unit.Initialize(context.Player, coordinate, tile);
                    unit.RestoreRuntimeState(coordinate, saved.CurrentHp, saved.HasActed);
                }
            }

            context.Scenario.RestoreRuntimeState(data.CurrentRound, data.CompletedRounds, data.CompletedObjectiveIndices, data.Reinforcements);
            context.Turns.RestoreLoadedBattleState(data.CurrentRound);
            return true;
        }

        private static void ClearOccupancy(BattleRuntimeContext context)
        {
            for (int y = 0; y < context.Grid.Height; y++)
            {
                for (int x = 0; x < context.Grid.Width; x++)
                {
                    if (context.Grid.TryGetTile(new GridCoordinate(x, y), out SLG.Grid.Tile tile))
                    {
                        tile.SetOccupyingUnit(null);
                    }
                }
            }
        }
    }
}
