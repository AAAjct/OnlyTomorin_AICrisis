# 剑术动作与强化分支

本次调整覆盖前三段刀路、两段樱花突进、长后撤月刃、空中斜落、完美格挡后的四种强化输入，以及完美闪避后的后跃落劈火龙。操作和数值见 [CombatBranches.md](CombatBranches.md)。

## 验证记录

- Unity 6000.3.27f2 编译通过。
- `Logs/Trial/SwordComboValidation.txt`：102 项通过，包含新后跃的真实高度、后移、落地前不发射、落地后单次释放、背闪穿过敌人抵达背后，以及所有相关动作的闪避 / 格挡取消。
- `Logs/Trial/RequestedCombatValidation.txt`：153 项既有战斗回归通过；执行时间在新增完美闪避后跃分支之前。
- 樱花、格挡强化、蓄力火龙及回旋刀的真实 TCP 检查曾通过 19 项。追加完美闪避后跃检查后的复跑在场景初始化等待阶段遇到编辑器连接中断，最终 22 项版本尚未完成验证；当前 `SwordComboNetworkValidation.txt` 记录该次初始化超时，不能视为通过报告。
- `Logs/Trial/SwordPoseValidation.txt`：实际骨骼和刀尖方向检查；第一击接触段刀尖始终在正前方，轨迹从左下到右上；第二击水平划过 170 度；第三击刀尖水平朝前。
- `Logs/Trial/SakuraPolishPreview.png`、`FireDragonPolishPreview.png`：运行时几何、花瓣和火焰效果检查。
- `Logs/Trial/PerfectDodgeLeapPreview.png`、`PerfectDodgeDragonPreview.png`：后跃与落地火龙效果检查。

编辑器自动验证入口：`Tools → Tomorin → Verify ordered sword combo`。真实 TCP 入口：`Verify ordered sword combo over TCP`，从 InkStart 菜单运行。

TCP 使用独立 Telepathy 客户端连接主机，检查真实按钮消息、主机位移、快照高度、路径起终点和火龙强度。该验证覆盖本机 TCP 消息链路；未进行双设备画面或移动端性能测试。
