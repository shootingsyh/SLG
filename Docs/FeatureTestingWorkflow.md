# Feature Testing Workflow

For every gameplay feature:

1. Implement the smallest production behavior that satisfies the feature.
2. Add or update EditMode unit tests for deterministic logic.
3. Add or update headless PlayMode integration tests for battle flow.
4. Add at least one deterministic Battle Test Lab preset.
5. Add manual verification notes to the preset or related test metadata.
6. Run relevant filtered tests while iterating.
7. Run the complete EditMode and PlayMode suites.
8. Report untestable behavior honestly, including the blocker and recommended next step.

Battle Test Lab presets and headless PlayMode tests should share `BattleSetupConfiguration` wherever practical. Do not create a separate manual-only scenario format when the behavior can be represented by the shared configuration model.
