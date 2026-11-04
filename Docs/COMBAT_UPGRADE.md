# Combat, co-op flow and resource budget update — 2026-11-04

Implemented in the existing procedural rig and Mirror host simulation:

- Four light cuts, three chained heavy cuts, light/heavy transition techniques, held-heavy charge and released jumping cleave. A short frontal interception window on the light-to-heavy cut counters eligible enemy strikes. Gameplay collision remains host-owned.
- Sprinting and healing work in either input order. Healing has a non-interactive green vignette fade. Rolling uses bounded afterimages and a ground ring; blocking adds stronger sparks/slash feedback. Heavy contact has a longer actor hold.
- PvE tolerance and slightly inset boss collision are implemented without tutorial text or HUD disclosure. PvP does not receive the PvE survival adjustments.
- All named room members must be loaded, ready and finished choosing boons before simulation advances. The barrier is replicated to peers. Missing members and disconnections use the existing roster lifecycle.
- Victory has a return-to-room button. The shared scene transition preserves room connections and names. Only the host may restart; remote restart requests are rejected by the server.
- Frame limit applies on desktop and mobile, defaulting to 60 FPS; selectable 30/60/90/120/144/display refresh. The previous preference key is retained.
- Master volume plus SFX master, attack/hit, block/parry and boss/warning levels. Boss cues use a reserved voice bank, reduced attenuation, and replicated skill-entry playback on clients. Nine additional original synthesized charge/counter/landing WAV variants are included; generator: Tools/create_combat_audio.py.
- Static world batches remove redundant MeshRenderer/MeshFilter components, release CPU mesh/texture copies after upload, and use spatial grouping. Unused level-model template references are cleared after spawning and unloaded once. Ghost mesh baking has a shared time budget and five reusable slots.

Validation: Logs/Trial/CombatUpgradeValidation.txt and Logs/Trial/CombatUpgradeNetworkValidation.txt. The TCP suite passed 10 checks covering shared waiting, held/released heavy input, rejected remote retry, host retry and repeated room/challenge transitions without reconnecting. Editable clips and preview rig are generated with Tools/Tomorin/Bake original sword animations.

No player build was produced, following the user's instruction. CPU/GPU utilization and process working-set reductions in an exported player have not been measured. Editor profiling is not a substitute for that measurement. Subjective animation feel, audio mix and cross-machine latency still need playtesting.

Local tooling: the Pipeline control port was changed to 18213 after the previous port failed to bind. No networking/gameplay transport port preference was changed by the implementation.
`nFinal validation: Unity compilation completed with no errors; 23 gameplay/settings checks passed, and 10 independent TCP checks passed. No build was run.
