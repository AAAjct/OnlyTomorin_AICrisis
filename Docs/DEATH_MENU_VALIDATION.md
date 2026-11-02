# Death menu

Validated in Unity 6000.3.27f2 on 2026-11-02. The death menu now offers `重来` and `退出到主菜单`, using the existing secondary button and explicit up/down navigation. Exit uses the existing `ReturnToTitle` route, preserving the main menu's game-wide exit button.

Death-screen confirmation now belongs to the EventSystem's selected button. Removed the direct Enter/gamepad-confirm respawn path that would otherwise compete with selecting Exit.

Also stopped the menu's reveal coroutine once loading begins. Returning from death and immediately starting again previously allowed reveal and fade-out to compete indefinitely at alpha zero.

12 Play Mode checks passed: visible labels, explicit navigation, default selection, keyboard retry, virtual gamepad selection/highlight and retry, keyboard exit after the old respawn delay, restarting from the main menu, and mouse pointer-click exit. No standalone build or physical gamepad was exercised. Results are in `Logs/Trial/DeathMenuValidation.txt`. Repeat with `Tools/Tomorin/Verify death menu (Play Mode)` while the trial is running.
