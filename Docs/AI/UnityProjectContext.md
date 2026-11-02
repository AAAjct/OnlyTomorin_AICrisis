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
