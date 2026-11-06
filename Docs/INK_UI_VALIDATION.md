# Ink title and settings focus

Validated on 2026-11-02 in the running Unity 6000.3.27f2 Editor.

- Settings buttons previously multiplied their selected tint by a background with alpha 0.07. The volume slider only tinted its red handle. `TrialSettingsFocus` now paints an opaque warm gold background and a cinnabar outline for selection/hover, without ColorTint multiplication. Disabled rebinding controls clear the focus decoration. Both title-screen and pause settings use `TrialSettingsPanel`.
- Added transparent calligraphy at `Assets/Art/UI/Resources/UI/InkTitleCalligraphy.png`, generated using the built-in image generation tool with the user's image as a style reference. Disabled mipmaps and compression, enabled alpha transparency, and clamped texture edges.
- `InkMainMenu.InstallTitleArtwork` updates existing menus and newly built menus. The saved `InkStart` scene includes the title; runtime loading from Resources includes it in player builds.
- 50 Play Mode checks passed through `Tools/Tomorin/Verify settings focus and ink title (Play Mode)`: live frames, keyboard navigation, virtual gamepad D-pad navigation, volume adjustments, all binding buttons, disabled rebinding state, Escape cancellation, focus restoration, repeated opening, paused time scale, and visible title texture. Added virtual mouse checks for hovering another button, leaving controls for empty space, and switching back to keyboard/gamepad while the pointer remains over another control.
- Input modes now share one highlight policy: mouse activity shows only the hovered control; keyboard/gamepad navigation shows only the selected control. Retaining EventSystem selection allows navigation to resume without leaving the sprint button visually highlighted during mouse use.
- Final Editor Console: zero current errors and warnings. Title and volume focus screenshots were visually inspected. Physical controller hardware and a standalone player build were not exercised.
- Results: `Logs/Trial/InkSettingsValidation.txt`; previews: `Logs/Trial/InkTitle.png` and `Logs/Trial/SettingsFocus.png`.

## Image generation prompt

Built-in image generation; transparent background; one reference image (the user's attachment).

> Use case: logo-brand. Asset type: transparent title artwork for a Chinese ink painting Unity game menu. Input image 1 is STYLE REFERENCE ONLY: its powerful expressive Chinese brush calligraphy, irregular dry-brush edges, flowing strokes and hand-painted energy. Create ORIGINAL title lettering with exact Chinese text "只鹅·智械危机". Layout: two lines, very large "只鹅" on first line and smaller "·智械危机" on second, tightly composed as a wide 2:1 title lockup. Deep charcoal black ink on genuinely transparent background (menu background is pale ivory). Bold expressive semi-cursive calligraphy but all six Chinese characters clearly correct and legible, explosive tapered strokes, flying-white dry-brush texture within strokes, subtle ink flecks near letters. No scenery, no paper background, no English, no extra text, no seal, no border. Keep all strokes inside canvas with small safe padding. Reference white lettering is style only; output lettering must be near-black. Save a production-quality PNG with alpha.
