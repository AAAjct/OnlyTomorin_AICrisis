# 水墨胜负字形

生成方式：内置 ImageGen，透明背景 PNG，原始透明度保留。颜色、晕染、劈开和动画由 Unity 的 InkVerdict.shader 与 TrialHudInkResult.cs 控制。

素材路径：
- Assets/Art/UI/Resources/UI/InkVerdictVictory.png（斩）
- Assets/Art/UI/Resources/UI/InkVerdictDefeat.png（菜）

最终生成提示词：

## 斩
Use case: stylized-concept. Asset type: Unity game UI calligraphy glyph sprite. Create ONLY the single simplified Chinese character "斩" (exact text, correct simplified glyph, left component 车 and right component 斤), huge and centered in a square with 12 percent transparent padding all around. Bold masterful Chinese ink brush calligraphy, powerful controlled brushwork, beautiful flying-white dry brush streaks within solid jet black strokes, subtle organic watery ink feathering at the stroke edges. Readable correct Chinese character structure. Black and charcoal monochrome only; no red, no white paint, no background, no paper, no lighting/shadows, no other words, no seal, no framing, no decorative large splash around it. Genuine transparent background. This is an isolated flat ink glyph whose silhouette will later be animated and tinted red by a shader.

## 菜
Use case: stylized-concept. Asset type: Unity game UI calligraphy glyph sprite. Create ONLY the single simplified Chinese character "菜" (exact text and correct glyph, grass radical 艹 above 采, upper claws 爫 over 木). Huge centered in a square, 12 percent transparent margin. Bold Chinese master brush calligraphy, black ink, powerful beautiful controlled strokes, dry brush flying-white gaps and watery ink feathering at the edges, same weight as a dramatic sword duel victory calligraphy. Correct easily readable Chinese glyph. Black/charcoal monochrome, no color, no paper, no shadows, no seal, no other writing, no decoration or large splashes. Genuine transparent background. Flat isolated ink glyph to be cut into halves and animated with a shader.

## 时间轴
0–0.56 秒：墨字扩散显形；0.64–0.82 秒：从中线斜劈；0.78–1.34 秒：左右两半错开；0.98–2.25 秒：由切口向外渐变朱红；2.62–3.2 秒：淡出。

Unity 验证菜单：Tools > Tomorin > Verify ink verdict artwork and split。运行时需在试炼场景的 Play Mode 中。帧截图与结果保存至 Logs/Trial/InkVerdict。
