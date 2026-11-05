# 轻击主连段与重击分支验证

本轮实现目标：斜劈 → 突刺 → 回挑反刀 → 空跃 → 三段暗红远程斩 → 下冲与暗蓝沙石。轻击节点分别衔接反向大斜劈、樱花突刺、后撤三月刃、垂直插剑下落；插剑落地后轻击衔接樱花突刺。

## 已完成验证

Unity 6000.3.27f2 的运行中编辑器执行以下检查，均产生通过记录：

| 检查 | 结果 | 报告 |
| --- | --- | --- |
| 新连招专项 | 42 项通过 | `Logs/Trial/SwordComboValidation.txt` |
| 原有综合回归 | 153 项通过 | `Logs/Trial/RequestedCombatValidation.txt` |
| 独立 Telepathy 客户端实际 TCP | 15 项通过 | `Logs/Trial/SwordComboNetworkValidation.txt` |

专项包含完整八段输入顺序、实际升空与浮空高度、空跃无伤害、每次发射次数、两种下落与落地接樱花突刺、停止输入落地、受击中断恢复、远程实际伤害、寿命和墙体阻挡、樱花伤害、Mirror 消息序列化以及运行时异常检查。

TCP 检查通过真实按钮消息驱动主机角色，确认八段连招、角色高度快照、恰好三道暗红斩、一段落地事件、十二道沙石，以及后撤位移与三道月刃事件均到达独立客户端。此检查不等同于两台设备上的延迟、渲染或移动端测试。

可在 Play Mode 下通过 `Tools → Tomorin → Verify ordered sword combo` 运行专项；TCP 入口为 `Verify ordered sword combo over TCP`，从 InkStart 菜单开始。

## 并发修改边界

上述报告对应本轮已验证的连招版本。随后同一工作区出现另一项并发战斗改动，新增 `TrialPlayerParryArts.cs`、弹反衍生编号、扩展战斗消息，并调整突进、后撤与空中下冲距离。上述通过记录不能作为这些后续改动的验证结果。

2026-11-05 16:12（Asia/Singapore）检查时，后续代码的 `TrialNetworkManager.BroadcastSwordCombat` 尚未实现，项目有一项 CS1061 编译错误。此前本轮连招代码的 Unity 编译结果为零错误、零警告。需要并发改动完成后重新编译；距离变化后，相关位移断言也需按最终设计核对。

未生成 Windows 或 Android 玩家构建。
