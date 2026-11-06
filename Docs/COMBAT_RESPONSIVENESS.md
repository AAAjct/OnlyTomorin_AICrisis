# Responsive combat and skill presentation — 2026-11-04

## Player-facing changes

- LAN entry selects Create Room. The lobby has a cyclic, explicit navigation order which omits disabled room controls. Input fields no longer start editing merely because navigation selects them: A/Enter edits, mouse click still edits, controller movement leaves editing, and B/Esc ends input. Tab and Shift+Tab traverse the same available controls.
- Basic light swings last 0.38 s (finisher 0.48 s, dodge follow-up 0.36 s). Light contact spans 22–54% of the action; its procedural pose uses the matching accelerated swing and recovery. Each swing retains its struck-target set.
- Light attacks accept dodge/guard at 40%, another light/heavy after 55%, healing after 62%, and movement can leave recovery after 72%. Early guard cancellation before 12% forfeits the attack's stamina cost and damage. Heavy attacks retain their commitment, with guard/dodge after 56% and light follow-up after 72%. Input buffer remains 0.36 s. Cancelling a swing closes its damage window.
- Guard walk speed is 3.8 m/s; unlocked guard rotation is 1800 degrees/s, with a 100-degree frontal half-angle. Holding guard does not refresh its 0.20 s perfect-parry window. Normal blocked attacks cost clamp(damage × 0.42, 10, 24) stamina and inflict 4% chip damage. Unblockable attack rules remain in place.
- Health loss is visible through a soft red screen-edge overlay and a central damage amount. A short bearing indicator points toward the hit source. Local hurt audio and controller rumble use an owner-specific combat event. Below 30% health a restrained edge tint and heartbeat warn the player. Overlays do not intercept clicks/touch; reduced-motion mode suppresses camera impulses.

## Original art and audio

- `Assets/Shaders/Resources/Trial/VFX`、`Assets/Art/Materials/Resources/Trial/VFX`、`Assets/Art/Models/Resources/Trial/VFX`: URP Tide Sigil and Tide Energy shaders, three tidal materials, FrostIce material, TidePearl and TideCrown meshes. Ground sigils follow the actual warning polygon; annular champion attacks retain their safe inner radius. Travelling tidal sheets follow the authoritative shockwave ring with constant width, rather than expanding the apparent hit width as radius increases.
- DeepSeek's projectile shells use the authored mesh, Fresnel/flow material and short trails. Skill detonations use a bounded 12-slot pool of water crowns and pressure domes. Warning colors continue to communicate defense rules; decorative layers do not change collision shapes.
- `Assets/Art/Models/Resources/Trial/Models/FrostRelic.fbx`: original forged frost hammer, 14,270 source polygons grouped into six material roles. Used by Penguin's held hammer and pooled flying hammer. Bevelled head, striking seals, rivets, leather bindings, coolant channels and a core crest replace the original boxes. Editable source: `Tools/FrostRelic.blend`; rebuild: Blender `--background --python Tools/create_frost_relic.py`.
- `Assets/Audio/Resources/Trial/Audio`: 54 original mono 44.1 kHz PCM sounds, three variations for each of 18 cues: sword swing, hit, heavy hit, finish, parry, block, hurt, warning, dodge, heal, death, heavy skill, tidal impact, bubble cast/pop, tidal charge, heartbeat and stone footstep. Rebuild with `python Tools/create_combat_audio.py`. No downloaded samples or third-party asset licenses were introduced.
- Sound variants load once at initialization; transient playback reuses the existing bounded voice pool. Generated fallback/music clips have explicit ownership and are disposed separately from imported resource clips. The existing master volume setting remains effective.

## Networking and asset setup

- Host combat authority is unchanged. Hurt and SkillImpact enum values are appended to the existing transient combat event. Hurt direction is carried in `forward`; the shared SkillImpact event uses `forward` as its RGB tint and has no local-player camera/HUD effects. All peers should use the same updated build.
- All generated materials, meshes, FBX, WAV files and Unity `.meta` files are included. No scene migration, Inspector wiring or package installation is required. `Tools/Tomorin/Build original combat VFX assets` regenerates the editable Unity mesh/material assets.

## Verification

- `TrialResponsivenessValidation`: 29 checks passed in Unity Play Mode. Real Input System controller events verify lobby navigation and deliberate editing/escape. The remaining checks exercise real actors, hit windows, single-hit behavior, guard turning/chip cost, health feedback, imported resources, shader errors, replicated hazards and pooled impact activation. Report: `Logs/Trial/ResponsivenessValidation.txt`.
- `TrialResponsiveNetworkValidation`: 12 real TCP checks passed, including a separate Telepathy peer connected to the real host and a real Unity client connected to a separate authority. Verified guard costs, victim-specific hurt direction, no observer HUD/camera leakage, network light-to-heavy chaining and shared skill detonations. Report: `Logs/Trial/ResponsiveNetworkValidation.txt`.
- Existing `TrialValidation`: all 21 gameplay smoke checks passed, including movement, hit contact, dodge, parry/block, healing, shrine rest, target lock, death/respawn, gate progression, phase two, victory and pause/resume. The fixture now selects the run boon and waits for the guard action clock / post-selection input suppression instead of assuming old menu timing. Report: `Logs/Trial/Validation.txt`.
- Final Unity script compilation succeeded; all `Tomorin/` shaders report no errors. Visual inspection covered ground warning alignment, water crown/projectiles, damage overlay and imported hammer proportions/materials. A direct runtime check confirmed the hammer's emission keyword is active and the authored emission remains `(0.08, 1.1, 1.6)` after a hit flash decays.
- No player build was generated, as requested. Build/export/hardware validation is not claimed.

## Captured Unity views

These are actual Unity renders, not concept art:

- [LAN controller navigation](Images/LanNavigation.png)
- [Layered tidal warning](Images/TidalWarning.png)
- [Tidal detonation and projectiles](Images/TidalImpact.png)
- [Health loss feedback](Images/DamageFeedback.png)
- [Forged frost hammer](Images/FrostRelic.png)

## Scope

This is a concrete gameplay and presentation upgrade, not a claim of AAA production quality. The existing character art, environment, procedural animation and overall content scope still constrain the result. Audio is original synthesized sound design rather than recorded Foley. Automated checks cannot establish subjective combat feel, physical-controller haptic quality, headset mix quality, cross-machine LAN latency or production hardware performance.
