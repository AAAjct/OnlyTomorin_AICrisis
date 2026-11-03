# Boss pressure escape and lock camera

Implemented 2026-11-03 for all seven bosses through the shared TrialEnemy combat entry point.

- Four or more player hits spanning at least 1.2 seconds, with no gap exceeding 1.1 seconds, queue a pressure escape. The triggering hit still deals its normal damage. Escape has a seven-second cooldown.
- A genuine posture break retains its entire execution window. The queued escape starts after that window; further damage no longer restarts Big Fish's stagger timer.
- Escape provides 0.7 seconds of damage immunity followed by 1.8 seconds of damageable armor. Ordinary hits cannot accumulate posture or apply hit stop during this protection. A successful parry removes it, preserving defensive counterplay.
- Big Fish counterattacks with ReverseTail. Melee champions use their existing close-range attacks; Taffy and YaYa back-leap into an attack. Blue emission and a local lock-on notice identify the protection. Retry clears every pressure/protection timer.
- Protection time is host-authoritative and replicated in TrialActorState.bossResolve. Mirror automatically generates its serializer; multiplayer peers must use matching builds.
- Penguin's timing multipliers now use 0.43 for quick windups, 0.56 for area windups, 0.48 for thrust windups, 0.57 for beat intervals, and 0.48 for recovery. Its opening double hammer is approximately 0.47 seconds windup, 0.51 seconds between beats, 0.62 seconds recovery, with 10% more damage. Minimum recovery stays at 0.45 seconds. Approach during swings is faster and between-skill pauses are shorter.
- Camera uses a smoothed relative lock offset, clamps target height contribution, limits orbit yaw to 110 degrees/second, and uses 1.8/2.6 metre hysteresis to hold heading through close crossings. Lock changes and Snap clear smoothing history. World collision remains enabled.

Validation: Unity Editor compiled without errors. Tools/Tomorin/Validate boss pressure and lock camera (Play Mode) passed 80 behavior checks across all six stages and seven bosses. Checks include actual skill/evasive follow-ups, immunity, damageable armor, parry cancellation, complete posture-break windows, reset, Mirror serialization round trips, Penguin tuning, and camera stability under airborne, crossing, and near-overlapping targets. Detailed results: Logs/Trial/BossPressureValidation.txt.

Validation used the real Unity runtime and CharacterControllers with controlled combat clocks. Camera was exercised over actual frames. This change has not been tested between separate devices or through a new standalone player build; final combat balance still benefits from playtesting.
