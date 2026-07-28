# Game Flow

Supported logical flow:

`Boot -> Press Any Key -> Main Menu -> New Game -> Chapter Select -> Battle -> Chapter Result -> Campaign Save -> Title`

## Demo Flow

`Boot -> Press Any Key -> Main Menu -> New Game -> Battle 1 -> Intermission -> Save -> Battle 2 -> DemoComplete -> Title`

## Save System Flow

- Continue chooses a valid battle save before campaign saves.
- Load Game lists campaign slots without entering a battle.
- Battle Test Lab remains a development-only shortcut into deterministic scenario configurations.
- New Game shows a warning if an active battle save exists, allowing the player to cancel or proceed.

## In-Battle System Menu

The in-Battle System Menu opens only from Player Idle. It provides Save, Load, Restart, and Title commands through production command methods so tests do not depend on rendered buttons.

### Save/Load Modals

- Standard Load: confirmation modal before loading a save.
- Cross-Battle Load: warning modal when loading from a different battle preset, explaining that the current runtime will be torn down and rebuilt.
- Restart: confirmation modal.
- Return to Title: confirmation modal with Save/Don't Save options.

## Save Lifecycle

- Defeat: battle save retained, player can Continue from title.
- Victory: battle save deleted after campaign save succeeds.
- Restart: battle save retained until overwritten.
- Load: save file retained until deleted.

## Campaign Progression Flow

### Wiring

```
BattleTurnController.EndBattle()
  → CampaignFlowService.TryProcessVictory() / TryProcessDefeat()
    → GameFlowService.TryContinue() (routes to Intermission/Defeat screen)
      → SaveRepository.SaveCampaign() (auto-saves progress)
```

### Key Components

| Component | Responsibility |
|---|---|
| `CampaignFlowService` | Idempotent result processing, campaign save auto-write, scene routing |
| `BattleTurnController.SetCampaignFlowProcessor` | Runtime integration — called by `BattleTestTemplateController` |
| `BattleTestTemplateController` | Detects demo presets, wires `CampaignFlowService` to runtime |
| `GameFlowService.TryContinue()` | Routes by `Metadata.FlowScreen`; properties setable for testing |

### Flow Screens

`GameFlowService.TryContinue()` routes to destination based on `Metadata.FlowScreen`:

| FlowScreen | Destination |
|---|---|
| `Battle1Victory` | Intermission |
| `Battle1Defeat` | Defeat screen (retry or main menu) |
| `Battle2Victory` | DemoComplete |
| `Battle2Defeat` | Defeat screen (retry or main menu) |

### Idempotency

All result processors are idempotent — calling multiple times produces same outcome. Battle results are processed exactly once per `EndBattle()` call.

### Centralized Identifiers

| Type | Location |
|---|---|
| Scene names | `CampaignSceneNames` |
| Battle IDs | `CampaignBattleIds` |
| Result enums | `BattleResultType` in `SLG.Core` |
| Reason enums | `SceneLoadedReason` in `SLG.Core` |

## Demo Test Flow

1. `Battle 1 (DemoBattle1Eliminate)` starts with `DemoFlowState.NotStarted`
2. `EndBattle("Victory")` → `CampaignFlowService.TryProcessVictory()` → `GameFlowService.TryContinueToIntermission()`
3. `Battle 2 (DemoBattle2Protect)` starts with `DemoFlowState.Battle1Complete`
4. `EndBattle("Victory")` → `CampaignFlowService.TryProcessVictory()` → `GameFlowService.TryCompleteDemo()`
5. Battle save deleted; flow returns to Title

## Headless PlayMode Tests

### Configuration

| File | Purpose |
|---|---|
| `Assets/Tests/PlayMode/Testables.asset` | ProcessRunner mode configuration |
| `Assets/Tests/PlayMode/SLG.PlayModeTests.asmdef` | Assembly definition with `testableAssemblies` |
| `Assets/Tests/EditMode/SLG.Tests.asmdef` | EditMode test assembly definition |

### Running via MCP

```
MCP run_tests(mode=PlayMode)
```

Tests run in `ProcessRunner` mode configured via `testableAssemblies` in `SLG.PlayModeTests.asmdef`.

### Running via Command Line (CLI)

```bash
"C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" \
  -batchmode -nographics -projectPath "C:\home\shootingsyh\games\SLG" \
  -runTests -testPlatform PlayMode \
  -testFilter "CampaignFlowPlayModeTests" \
  -logFile headless_tests.log
```

### Running All Tests

```bash
"C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe" \
  -batchmode -nographics -projectPath "C:\home\shootingsyh\games\SLG" \
  -runTests -testPlatform PlayMode \
  -assemblyNames "SLG.PlayModeTests" \
  -logFile all_tests.log
```

### Test Workflow

1. `CampaignFlowService` processes victory/defeat
2. `SaveRepository.SaveCampaign()` writes autosave
3. `InMemorySaveStorage.ClearAll()` resets for next test
4. PlayMode tests verify runtime integration via `BattleScenarioRuntimeBuilder`

### Key Test Assertions

| Test Suite | Tests | Verification |
|---|---|---|
| `CampaignFlowTests` (EditMode) | 20 | Idempotency, destination resolution, save data fields |
| `CampaignFlowPlayModeTests` (PlayMode) | 4 | Runtime battle integration, flow processing, save verification |
| `TurnAndBattleEndPlayModeTests` (PlayMode) | 35 | Battle runtime, turn tracking, combat resolution |
| **Total tests** | **112** | **All passing** |
