# Boss victory menu

Validated on 2026-11-02 in Unity 6000.3.27f2.

- Boss death immediately enters Victory, pauses time, freezes the camera and releases the cursor.
- The menu displays exactly two buttons: 重新挑战 and 返回标题. Retry is selected by default.
- Movement, attack, look input and Escape cannot resume gameplay; the timer and boss presentation remain frozen.
- Mouse retry resets the trial, restores time and camera control, revives combatants and permits movement.
- Keyboard navigation and confirmation return to InkStart with normal time scale.

Unity compilation completed without errors. All 16 dedicated Play Mode checks passed; the run is recorded in Logs/Trial/Validation.txt. Repeat using Tools > Tomorin > Verify victory menu (Play Mode) in AshenCourt outside a network session.

Network integration uses the same victory state, blocks look/control input and lets a connected participant request an authoritative retry. The host resets the shared encounter and companions; peers resume when the next non-victory snapshot arrives. Two-endpoint validation of this change remains outstanding.

No scene or Inspector wiring is required; the existing runtime HUD creates the buttons.
