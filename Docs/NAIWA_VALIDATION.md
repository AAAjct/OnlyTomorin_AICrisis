# 奶蛙动作与尸体生命周期修复

日期：2026-11-02。沿用项目 Unity 6000.3.27f2，不导出游戏。

## 原因与修复

此前待机和出拳共用向前伸展的手部目标，缺少自然垂手、蓄力、挥击和收手的区别；死亡时 MoveBlend 未清零，LateUpdate 仍按该权重摆腿。尸体也没有淡出流程。

现在待机采用下垂的双手目标，近身戒备时抬手，左右攻击沿蓄力、接触、收回三段轨迹驱动真实加权骨骼。死亡立即清零移动权重，并走独立的固定肢体倒地姿态。倒地后保留至约 2.2 秒，随后在 2.2–4.4 秒间伴随微光逐渐淡出，下沉并关闭渲染；重生恢复原始材质、阴影和可见状态。客户端依据服务器同步的 ActionTime 呈现消散。

按用户最新要求，九重天游戏开场的代码、头像副本、预制体、启动接入与临时预览已移除，保留原启动流程。

## 验证

通过 Input System 与实际 Play Mode AI 驱动，检查真实手骨位置、对玩家的伤害、移动中死亡、静止尸体、透明度变化、暂停、重复重生及同步死亡状态。手部实际挥动距离约 0.28 米。

尚未验证独立多进程联机和手机真机，也未导出安装包。

```text
2026-11-02T10:08:38.0916135Z
Passed 15 checks
Combat bootstrap is ready
Idle NaiWa lowers both weighted hands from the T-pose
NaiWa AI enters its hand attack at melee range
Weighted attacking hand swings through a visible arc (travel=0.28)
Hand attack contact deals damage to the player
NaiWa is walking before the lethal hit
Death immediately clears locomotion weight
Settled corpse no longer cycles the walking legs
Corpse remains visible briefly after falling
Pause freezes the corpse lifecycle
Corpse gradually fades between the hold and disappearance
Finished corpse fade disables its renderers
Respawn restores visible materials and the living pose
Replicated death clears gait and resumes the synchronized corpse fade
Repeated death and restoration leave no hidden mesh
```
