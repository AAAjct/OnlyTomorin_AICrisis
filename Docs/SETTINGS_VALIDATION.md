# Shared settings and rebinding validation

Validated in Unity 6000.3.27f2 on 2026-11-02 through the running project Editor.

- Main menu and pause use the same `TrialSettingsPanel` component, paper style, hierarchy, dimensions, controls, and rebinding behavior.
- 37 Play Mode checks passed through `Tools > Tomorin > Verify shared settings and rebinding (Play Mode)` from InkStart.
- Real Input System keyboard and gamepad events exercise capture, duplicate rejection and its UI message, Escape cancellation, alternate bindings, movement composite overrides, scene persistence, actual gameplay attacks/movement/guarding, HUD labels, and restoring defaults.
- Pause validation covers frozen time, preferences, capture isolation, Escape returning to pause then resuming, closing during capture, and reopening.
- Main/pause layout signatures match. Both panels were rendered at 1600 x 900 and visually inspected. Binding captions shrink to fit long control names.
- Final compilation: 0 errors, 0 warnings. `git diff --check` passed for the affected tracked files.
- Existing bindings, sprint, audio and motion preferences were restored by validation; Editor returned to Edit Mode.
- No scene/prefab wiring or packages required. Saved main menu scenes disable their old settings hierarchy and create the shared view at runtime.
- Keyboard/mouse and gamepad button bindings are editable. Fixed menu confirmation/back shortcuts and analog movement/look are preserved. Touch gameplay controls keep their existing actions.
- Physical controllers/mobile devices and a two-peer network session were not exercised. Networking still sends the resolved control state through the existing protocol.

Evidence: `Logs/Trial/Validation.txt`, `Temp/settings-main.png`, `Temp/settings-pause.png`.
