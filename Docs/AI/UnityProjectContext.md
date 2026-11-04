# Unity Project Context

- Inspected 2026-11-02, baseline commit 25cbfbd.
- Root: D:/unity/unity_file/project/OnlyTomorin_AICrisis.
- Confirmed Unity 6000.3.27f2, URP 17.3.0, PC quality overrides pipeline; Input System 1.20.1 only.
- Initial build/startup scene: Assets/Scenes/SampleScene.unity (template only).
- Tomorin: Assets/Art/Models/Tomorin/Tomorin.fbx, Generic rig, external URP materials and four textures. No authored gameplay/animation scripts at baseline.
- Empty first-party roots: Assets/Scripts, Assets/Anims, Assets/Prefabs, Assets/Editor.
- Vendor code: Mirror, DOTween, SaveGameFree. No first-party multiplayer, save, or tween usage. Do not change vendor code.
- Unity Test Framework 1.6.0 installed; no first-party tests at baseline.
- No repository AGENTS.md or architecture conventions found. Use TomorinTrial namespace, private serialized fields, direct composition and explicit initialization.
- Existing user modification: Assets/Settings/Mobile_RPAsset.asset. Preserve it.
- Unity Editor is open for this exact project. Pipeline initially absent; CLI available. No UnitySkills server registered.
- Evidence: ProjectSettings/ProjectVersion.txt, QualitySettings.asset, ProjectSettings.asset, EditorBuildSettings.asset; Packages/manifest.json; Assets file inventory; Tomorin.fbx.meta.

## Implemented trial

- Startup scene is now Assets/Scenes/AshenCourt.unity. SampleScene is preserved.
- Assets/Scripts/Trial contains the single-player TomorinTrial feature. TrialGame explicitly creates and wires world, player, enemies, camera, input, effects and uGUI HUD on Start.
- Original generic-skeleton animation is driven by TrialRig with arm IK. Thirteen matching .anim assets, a preview controller and rig prefab are exported by TrialBuilder. Animation files are compressed by removing redundant constant keys.
- Input uses a clone of the original Player action map and a local SwordCombat map. Device filtering supports isolated input validation. No original bindings are overwritten.
- TrialWorld owns original runtime geometry/materials/textures and batches static meshes by material. Sky, Lit and particle shaders have explicit serialized references so player builds retain them.
- New editor tools are under Assets/Editor/Trial. Tools/Tomorin builds scenes, exports animations, runs 21 gameplay checks and builds a Windows player.
- Unity Pipeline 0.9.1-exp.1 is installed for local Editor control, on loopback port 18080 because the default port range failed on this machine. No runtime service is enabled.
- Validation: Unity compilation passed without errors or warnings; 21 real-input Play Mode checks passed. See Docs/PLAY.md and Docs/VALIDATION.md for details and final build evidence.
- During work, EditorSettings enterPlayModeOptions was changed externally from 0 to 3. This agent did not change or revert that preference.

## Current combat networking (2026-11-02)

- Startup menu is InkStart; gameplay is AshenCourt. TrialNetworkManager uses the vendored Mirror/Telepathy TCP transport. The host simulates actors and combat; clients send controls and render world snapshots.
- TrialCombatEvent carries player ownership and transient parry/block/guard-break/death feedback. World effects are shared; HUD and camera effects belong to the identified local player. Combat authority remains on the host.
- Input edges send immediately; movement changes target 60 Hz with idle 10 Hz heartbeat. World snapshots target 30 Hz, contain only active boss hazards, and skip the host's local connection. Client action clocks advance at most 100 ms beyond the last received snapshot.
- TrialGame remembers entering network simulation and does not fall back to standalone simulation while disconnection loads the menu.
- Combat network regression tool: Assets/Editor/Trial/TrialNetworkValidation.cs. Evidence and limits: Docs/COMBAT_NETWORK_VALIDATION.md. No network package/settings migration was required.

## Courtyard / arena and combat feel update (2026-11-02)

- Shared runtime level list: InkLevelSelection partial extends InkMainMenu. Solo offers Courtyard (Boss: Big Fish); LAN dropdown opens the same list with Arena first. Both solo and co-op Courtyard start before the three guards; only editor/debug StartBossChallenge skips them.
- Host owns run-boon offers/validation, PvP damage, scores and respawn. TrialRunBoons defines modest player and boss modifiers. TrialHudRunChoices renders choices and other-player nameplates; local nickname replaces the fixed name in the vitality HUD.
- Boss health is 1400 times room population, preserving remaining health percentage on joins/leaves. No four-player cap; TrialPayloadParts splits large rosters/snapshots under Telepathy's message limit.
- Boss has reactive and chained skills, moving-blade tail collision, separate front/rear safety sectors, low-health frenzy, and dynamic player marks. DeepSeek.fbx is replaced with the user-provided NewDeepSeek binary while preserving asset GUID; phase two drives the real TiePen blend shape.
- TrialImpactEffects adds pooled silhouette afterimages, slash/ring lines, stronger hit/clash/kill audio, original synthesized music, controller haptics and a short countercut slow-motion cue. Actor hit holds use unscaled time and are cleared on restore/reset. Online pause remains local; the authoritative countercut cue produces the shared short slow motion.
- Keyboard/gamepad prompts follow the most recently used gameplay input. Roll rotation follows the actual ground-plane dodge direction, which is included in network state. Camera shoulder collision and close-range local-renderer hiding prevent viewing inside the local model.
- New validation entry points: TrialCombatFeelValidation and TrialEncounterValidation, sharing the existing real-TCP validation harness. Current behavior and verification limits are documented in Docs/ENCOUNTER_FEEL_VALIDATION.md.


## Difficulty and mobile healing (2026-11-03)

- TrialDifficultySettings in TrialNetworkMessages.cs owns Normal/Easy/Hard labels and enemy health/damage multipliers. Normal is enum zero for backward-safe defaults. Solo selection survives scene changes in a static property; it is not saved to disk.
- InkLevelSelection adds difficulty buttons to the existing shared level picker. TrialNetworkManager owns host selection and transmits difficulty in both lobby and world state. PvP damage is unchanged.
- TrialGame combines difficulty with party scaling; TrialEnemy.ScaleForParty preserves current HP ratio. TrialPlayer shares TickLocomotion between Free and Heal; healing disables sprint and retains existing timing, interruptions and consumption. TrialRig blends gait with the drinking pose.
- Validation: Unity compilation succeeded; three solo UI buttons and 21 runtime/state/serialization checks passed. No cross-device network test for this change. See Docs/DIFFICULTY_HEALING_VALIDATION.md.

## Responsive combat and original effects (2026-11-04)

- LAN uses explicit, availability-aware navigation; selecting an input field no longer auto-enters editing. TrialInputFieldNavigation allows controller movement to leave deliberate text editing. A/Enter edits; B/Esc exits editing.
- TrialPlayer reduces ordinary light-action duration/contact startup, adds early defensive links, chained light/heavy actions, buffered healing and recovery steering. Guard movement/rotation/coverage and stamina/chip costs are revised. Host authority and per-swing target deduplication remain in place.
- TrialHudDamage shows health-loss screen edges, damage amount, source bearing and low-health feedback. Hurt and shared SkillImpact are appended to TrialCombatCue; the former is victim-local, the latter is world presentation. TrialFighter preserves authored material emission when hit flashes end.
- Resources/Trial includes original tidal shaders, materials, shell/crown meshes, 54 WAV variants and a Blender-authored FrostRelic hammer used by Penguin and flying-hammer clones. Source generators and editable .blend are in Tools. All references load through existing initialization; no scene wiring or new packages are required.
- Validation: 29 responsiveness/presentation checks, 12 real TCP checks and 21 gameplay smoke checks passed. Unity compilation and custom shader checks passed; actual Unity captures were visually inspected. No player build was made. See Docs/COMBAT_RESPONSIVENESS.md for timings, art sources, evidence and limitations.
