# Boss AI, warning meshes, and display settings

Validated in Unity 6000.3.27f2 on 2026-11-05.

## Encounter changes

- Recovery clocks now advance while independent hazards and the other twin are active. Previously, an entire projectile lifetime could be followed by another full recovery interval.
- Extra regrouping triggered by prolonged pursuit has been replaced with route and gap-closer reevaluation. Recovery movement approaches distant targets.
- Champions exclude clearly unreachable melee attacks when selecting a move. If all reachable moves in the selection bag are spent, the bag refreshes instead of forcing remaining short attacks.
- Big fish uses distance-sensitive weighted choices, discourages immediate repeats, and selects reachable phase-two followups.
- Authored attack windups, damage, hit heights, and deflection windows were retained. Recovery and positioning between attacks are more assertive.

## Performance evidence

Warning meshes previously made 65 or 195 native Transform.InverseTransformPoint calls per update. Each mesh now reads one world-to-local matrix and transforms its reused vertex buffer in managed code. Bounds are still recalculated after vertex updates to preserve culling correctness.

Local Play Mode microbenchmark: five passes of 2,000 warmed updates, median milliseconds per component update:

| Component | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| TrialWaveCrest | 0.034482 | 0.020722 | 40% |
| TrialSkillSurface | 0.008776 | 0.004054 | 54% |

These measurements describe those two functions, not total frame time or a promised FPS gain. An initial CrimsonSwordTerrace Editor snapshot reported 230 draw calls and 511,202 submitted triangles. The unfocused Editor was capped at 15 FPS; that snapshot does not identify a CPU/GPU bottleneck. GPU rendering, frame pacing, and memory still need representative Player-build profiling on the target hardware before claiming an overall performance diagnosis.

Raw evidence and reusable benchmark script: `Logs/Trial/WarningMeshBenchmark-before.txt`, `Logs/Trial/WarningMeshBenchmark-after.txt`, and `Logs/Trial/BenchmarkWarningMeshes.cs` (local ignored logs).

## Display settings

- Main-menu and pause-menu settings share a VSync on/off button.
- Cap cycle: 30 -> 60 -> 120 -> 144 -> unlimited -> 30.
- Desktop VSync takes precedence over the selected cap. Disabling it restores the saved cap immediately.
- Unlimited uses Application.targetFrameRate = -1 in combat. The power-budget component preserves it, including in LAN sessions.
- Explicit 30 FPS is honored in LAN; automatic solo idle/background throttling is not applied to LAN sessions. Networking currently advances from the rendered update, so the chosen host frame cap also limits its update cadence.
- With VSync disabled, solo menus and unfocused windows retain the existing 30/15 FPS power-saving behavior.
- Mobile VSync remains system-controlled; explicit mobile frame caps still apply. The panel explains this platform behavior.
- Existing frame-cap preference keys are retained; the retired 90 FPS value maps to 120 FPS. New VSync choices are saved as `Ink.VSync`.

## Verification

`Tools/Tomorin/Validate adaptive boss pressure (Play Mode)` passed 113 checks across all seven encounter levels and the main menu. Coverage includes reachable move selection, pursuit, hazard/partner-independent recovery clocks, twin attack sequencing, deflection, transformed warning footprints, mesh size transitions and bounds, both settings entry points, exact cap order, unlimited budgeting, VSync precedence, preference saving/restoration, and explanation text fitting.

Report: `Logs/Trial/AiPressureValidation.txt`. Settings preview: `Logs/Trial/SettingsVSync.png`. The validation restores the user's saved display preferences.
