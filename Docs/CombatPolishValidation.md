> 当前路线、格挡扩展与镜返连招以 [ComboFlow.md](ComboFlow.md) 为准。下文保留此前版本的扩展与验证记录，不代表当前连招顺序。

# 剑术动作与强化分支

本次调整修复快速二轻接重丢输入、同帧 TCP 重复轻击合并、立即衔接与收招后分支不一致的问题。新增回身横扫、进步上挑、月刃追刺及地面 / 空中 / 强化技能之间的续接，骨骼、持剑手、剑向和模型根姿势共同过渡。后续加入猩红发光月牙，以及跃起、回转、下落、落地和地面派生刀路的分阶段特效。操作和数值见 [CombatBranches.md](CombatBranches.md)。

## 验证记录

- Unity 6000.3.27f2 编译通过。
- `Logs/Trial/SwordComboValidation.txt`：158 项通过。覆盖 30 / 60 / 144 FPS 快速二轻接重、长停顿保留输入、0.55 秒衔接与超时重置、新路线真实位移 / 伤害 / 单次命中 / 防御取消、招式快照序列化、姿势连续及剑柄与手部贴合。追加检查起跳 / 回转 / 下落特效只触发一次、取消后停止、落地保留蓝沙、几何端点有限值及四种新增事件序列化；错误统计包含渲染断言。
- `Logs/Trial/SwordComboNetworkValidation.txt`：28 项真实 TCP 检查通过。包含同一批数据包中的“轻、轻、重”、樱花接横扫再上挑、新动作快照、起跳 / 回转 / 下落事件，以及原有空中、强化、蓄力与后跃落地火龙链路。
- `Logs/Trial/ComboFlowMenuValidation.txt`、`ComboFlowPausePreview.png`：暂停页显示全部 15 行操作与新路线说明，正文与“继续”按钮之间保留 20 像素；已检查实际渲染。
- `Logs/Trial/RequestedCombatValidation.txt`：当前工作区综合回归 169 项通过，普通闪避残影检查也已通过。
- `Logs/Trial/SwordLeapVfxPreview.png`、`SwordTurnVfxPreview.png`、`SwordDescentVfxPreview.png`、`SwordLandingVfxPreview.png`：使用实际连招状态、实际高度和运行时效果检查四种动作的渲染；预览暂停在对应动作阶段。
- `Logs/Trial/SwordPoseValidation.txt`：实际骨骼和刀尖方向检查；第一击接触段刀尖始终在正前方，轨迹从左下到右上；第二击水平划过 170 度；第三击刀尖水平朝前。
- `Logs/Trial/SakuraPolishPreview.png`、`FireDragonPolishPreview.png`：运行时几何、花瓣和火焰效果检查。
- `Logs/Trial/PerfectDodgeLeapPreview.png`、`PerfectDodgeDragonPreview.png`：后跃与落地火龙效果检查。

编辑器自动验证入口：`Tools → Tomorin → Verify ordered sword combo`。真实 TCP 入口：`Verify ordered sword combo over TCP`，从 InkStart 菜单运行。

TCP 使用独立 Telepathy 客户端连接主机，检查真实按钮消息、主机位移、快照高度、路径起终点和火龙强度。该验证覆盖本机 TCP 消息链路；未进行双设备画面或移动端性能测试。

## 本次连招扩展验证（2026-11-05）

- `Logs/Trial/SwordComboValidation.txt`：**276 项通过**。包含原有空中与完美格挡路线、30 / 60 / 144 FPS 翻滚取消、气力不足、前突刺墙体阻挡、新地面路线、飞刃次数与取消、1.25 米低空悬停、蹬墙借步和落地、轻重长按、格挡变招、移动 Boss 转向、突进减速及新增输入 / 特效消息序列化；运行期间错误为零。
- `Logs/Trial/CrescentValidation.txt`：**596 项通过**。圆弧等半径、有限顶点、包围盒、实际飞行拖尾、黑色刃体、默认猩红描边、寿命、墙体阻挡与对象池复用继续通过。
- `Logs/Trial/SwordComboNetworkValidation.txt`：**34 项真实 TCP 检查通过**。新增翻滚轻 / 重取消、派生飞刃、低空实际高度与空中连续飞刃；原有三飞刃、强化、火龙蓄力及落地释放继续通过。
- `Logs/Trial/InkArcPreview.png`、`Logs/Trial/SwordPalettePreview.png`：以实际运行时网格 / 材质离屏渲染，检查圆弧、黑色刃体、晕墨内缘与六种刃缘样式。预览入口 `Tools/PreviewInkCombat.cs` 可通过 Unity Pipeline `script run` 再次生成。
- 未做双设备画面与移动端性能验证。

## 最新结果：近身变化与挑空飞刃衔接

`Logs/Trial/SwordComboValidation.txt`：**373 项通过**。追加覆盖十种近身动作的实际命中与不同节奏、30 / 60 / 144 FPS 连续身体连段、弧线绕步、低跃与取消后落地、5 点气力起跳、近 Boss 时普通空跃转近身腾扫、格挡长按不误取消反击，以及三个地面挑空入口、腾身重击接空中三飞刃、飞刃后回到近身空中路线。运行期间错误为零。

`Tools/PreviewMartialCombat.cs` 使用实际角色骨骼与运行时姿态生成 `Logs/Trial/MartialPoses/` 中的动作预览；已检查柄击、扫腿、低跃前翻、腾身回旋与空中剪脚的身体轮廓差异。新版操作说明已更新到暂停菜单与 `Docs/CombatBranches.md`。

诊断过程中遇到 Unity Pipeline 固定端口在 Domain Reload 后冲突；使用临时诊断脚本恢复本地连接，临时脚本与放在 Assets 下的预览图均已清理。

收尾验证：最新单机 373 项、圆弧刀光 596 项、真实 TCP 34 项通过；Unity Console 当前编译错误与警告均为零。`Logs/Trial/FinalComboHelp.png` 已检查最终暂停菜单的全部 14 行说明及按钮间距；预览图已移出 Assets，避免进入游戏资源。Pipeline 的临时端口配置已恢复原值，诊断脚本未保留。
