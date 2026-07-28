# Save System

## Save Types

- Campaign manual slots: `campaign-slot-01.json` through `campaign-slot-05.json`.
- Campaign autosave: `campaign-autosave.json`.
- Battle save: `battle-save.json`, exactly one active in-battle save record.

Campaign saves represent stable between-chapter progression. Battle saves represent one active battle and are rebuilt from deterministic scenario presets plus runtime unit/objective/reinforcement state.

## Continue Priority

1. Valid battle save.
2. Most recent valid campaign save, including autosave.
3. Disabled when no valid save exists.

Invalid, corrupt, or unsupported saves are ignored for Continue and reported through slot metadata.

## Format And Corruption Policy

- Current format version: `2`.
- Files are human-readable JSON envelopes with explicit save type, format version, game/content version, UTC timestamp, payload JSON, and checksum.
- The checksum is SHA-256 over save type plus payload JSON. It detects accidental corruption and test mutations; it is not an anti-cheat system.
- Writes use a temporary file and replace/move to the final file only after serialization succeeds.

## Battle Save Eligibility

Allowed only when battle is active, Player phase is active, Player interaction FSM is `Idle`, no unit is selected, no provisional movement exists, no Enemy action is resolving, no modal is open, and no save/load/scene transition is active.

Blocked states include: Enemy phase, movement selection, provisional action menu, attack targeting, Skill selection/targeting, rollback, combat resolution, Skill resolution, battle ended, modal open, transition in progress, and duplicate save operations.

## Lifecycle

- Manual Save writes/overwrites `battle-save.json` and keeps the battle playable.
- Loading save keeps save file until deleted or overwritten.
- Restart battle rebuilds original scenario and keeps previous save until overwritten.
- Defeat keeps previous save so player can Resume from it.
- Victory campaign save deletes battle save after campaign progress is saved successfully.
- New Game shows warning before replacing active save; campaign slots are not deleted automatically.

## FlowScreen Metadata

`SaveMetadata.FlowScreen` records current flow destination for campaign navigation. Uses `SceneLoadedReason` enum values from `SLG.Core`.

### Flow Screens

| FlowScreen | Battle | Destination |
|---|---|---|
| `Battle1Victory` | `Battle1Id` (DemoBattle1Eliminate) | Intermission |
| `Battle1Defeat` | `Battle1Id` | Defeat screen (retry or main menu) |
| `Battle2Victory` | `Battle2Id` (DemoBattle2Protect) | DemoComplete |
| `Battle2Defeat` | `Battle2Id` | Defeat screen (retry or main menu) |

## Auto-Save Behavior

### Intermission Controller

When `IntermissionController.Awake()` runs:
1. Reads current campaign progress from `SaveRepository`
2. Writes `CampaignSaveData` to autosave file (`chapter-1`, battle ID from flow service)
3. Saves flow screen metadata for `GameFlowService.TryContinue()` routing

### Victory Processing

When `CampaignFlowService.TryProcessVictory()` succeeds:
1. Calls `SaveRepository.SaveCampaign()` with battle context data
2. Updates campaign metadata (chapter ID, battle ID, flow screen)
3. Sets slot state to `SaveSlotState.Valid`

### Idempotency

All auto-save operations are idempotent. Multiple calls produce same save data without duplicating writes.

## Demo Flow

The demo follows a linear flow:
1. Battle 1 (DemoBattle1Eliminate) → Intermission → Save campaign → Battle 2 (DemoBattle2Protect) → DemoComplete → Title
2. `GameFlowService.TryContinueToIntermission()` loads Intermission scene and deletes battle save after campaign save.
3. `GameFlowService.TryCompleteDemo()` loads DemoComplete scene and deletes battle save.

## Headless Round-Trip Testing

### Test Workflow

1. `CampaignFlowService` processes victory/defeat
2. `SaveRepository.SaveCampaign()` writes autosave
3. `InMemorySaveStorage.ClearAll()` resets for next test
4. PlayMode tests verify runtime integration via `BattleScenarioRuntimeBuilder`

### Key Test Assertions

| Test | Verification |
|---|---|
| `Battle1Eliminate_Victory_SaveDataVerified` | `CampaignSaveData` fields match expected values |
| `DemoBattle2Protect_Victory_ProcessedEndToEnd` | Round objectives (SurviveRounds) advance correctly |
| `CampaignFlowTests` (EditMode) | Idempotency, destination resolution, save data fields |
| `CampaignFlowPlayModeTests` (PlayMode) | Runtime battle integration, flow processing, save verification |

## Cross-Battle Load

When loading a battle save from a different battle preset, a confirmation modal is shown explaining that the current battle runtime will be torn down and rebuilt from the saved preset.

## Stable IDs

Save data uses logical IDs: campaign slot IDs, chapter IDs, battle preset IDs, unit setup keys, `UnitDefinitionId`, `SkillId`, objective indices within the scenario configuration, and reinforcement wave keys. It never stores Unity InstanceID, object references, delegates, coroutines, or scene hierarchy indices.

## Future Persistent State Rule

Every future feature that adds persistent runtime state must define a stable save representation, add serialization tests, add load-validation tests, add a headless round-trip test, add or update a Battle Test Lab preset, and confirm old saves fail safely or migrate.
