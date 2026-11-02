# 残火 · 雾庭 / Tomorin: Ashen Blade

一个在 Unity 6000.3.27f2 / URP 中制作的单人第三人称剑术类魂试炼原型。使用项目原有 Tomorin 模型，关卡、武器、敌人、动作、纹理、天空和合成音效均在本次实现中制作；没有新增下载素材。

## 开始游玩

打开 `Assets/Scenes/AshenCourt.unity`，按 Unity Play，再按 Enter 或点击「踏入雾庭」。玩家从庭院南侧开始；左侧香炉可以休息与设置重生点。斩断三名剑卫后，北侧封印解除。击败「大肥鱼 · DeepSeek」完成试炼。标题页的「直接挑战大肥鱼」可跳过剑卫，满状态进入 Boss 战。

Windows 独立版若已构建，入口为 `Builds/AshenCourt/Tomorin_AshenCourt.exe`。编辑器菜单 `Tools > Tomorin > Build Windows player` 可以重建。

| 操作 | 键鼠 | 手柄 |
| --- | --- | --- |
| 移动 / 视角 | WASD / 鼠标 | 左 / 右摇杆 |
| 疾跑 | 左 Shift | 按下左摇杆 |
| 三连斩 | 左键或 J，衔接时再次输入 | X / 西侧按钮 |
| 重斩 | Q | RB / R1 |
| 格挡 / 弹反 | 按住右键或 K | LB / L1 |
| 闪避翻滚 | Space | B / 东侧按钮 |
| 锁定 / 解除 | Tab 或鼠标中键 | 按下右摇杆 |
| 饮用灵露 | R | 方向键上 |
| 香炉休息 | E | A / 南侧按钮 |
| 开始 / 死亡后重生 | Enter | A / 南侧按钮 |
| 暂停 | Esc | Start |

## 战斗规则

- 玩家有 160 生命、120 气力、3 次灵露。轻斩消耗 19、重斩 35、闪避 25 气力。停手后恢复气力。
- 普通移动速度 4.5 m/s，疾跑 7.2 m/s。移动带快速加减速与反向制动，锁定时面向敌人横移。疾跑消耗降为每秒 8 气力；耗尽后先恢复气力，避免走跑反复抖动。
- 轻斩、重斩、闪避有 0.28 秒输入缓冲。轻斩约 0.52 秒，第三斩 0.64 秒，重斩 0.88 秒；轻斩 76% 进度后可以接下一斩。轻斩 58%、重斩 66% 进度后可用闪避取消收招。
- 格挡起手的 0.20 秒是弹反窗口。成功弹反会打断敌人并造成追击破绽；持续格挡消耗气力并承受少量伤害。
- 翻滚缩短至 0.46 秒，0.025–0.32 秒有无敌窗口；后段可以恢复移动。受伤后有 0.42 秒保护，避免重叠弹幕瞬间连续扣血。Boss 红色危招无法格挡，应闪避或离开范围。
- 灵露在喝药动作完成约 63% 时生效，受击打断前不会消耗。
- 敌人有追击、起手、攻击、收招、架势崩溃与死亡状态。Boss 有 960 生命，低于 55% 后进入「深海觉醒」，加快出招、增加泡泡密度并解锁三重涡流。
- 香炉恢复资源并重置敌人；死亡后可在最近香炉重生。重生点仅在本次试炼运行期间保存。

## 动画与资产

`Assets/Scripts/Trial/TrialRig.cs` 为现有 16 根加权骨骼实现专用动作，以手臂双骨 IK 配合躯干扭转、腿部摆动和翻滚驱动；剑跟随右手。奔跑根据实际位移速度驱动步频，并增加抬腿、摆臂、身体前倾与转向侧倾；锁定横移使用移动方向驱动腿部。原始模型没有完整膝关节与手指骨骼，因此仍采用适配该模型的夸张动作。

`Assets/Anims/Trial/` 包含 13 个可检查与重新导出的 Generic 动画片段：Idle、Walk、Run、Sprint、Slash01/02/03、Heavy、Dodge、Guard、Drink、Hit、Death。`TomorinSword.controller` 和 `Assets/Prefabs/Trial/Tomorin_SwordRig.prefab` 可预览这些动作。运行时由同一套动作方程混合骨骼，战斗判定独立于动画事件。

`TrialBossRig.cs` 使用 DeepSeek 原模型的躯干、手臂、腿、膝盖、尾巴、耳朵及头发骨骼。`Assets/Anims/DeepSeek/` 导出 Idle、Swim、TailSweep、TidalCharge、BellySlam、BubbleBarrage、Maelstrom、Enrage、Stagger、Death 共 10 个动画，附 `DeepSeekBoss.controller` 和 `Assets/Prefabs/Trial/DeepSeek_BossRig.prefab`。编辑器菜单 `Tools > Tomorin > Update movement and DeepSeek boss assets` 可以重新导出并更新场景引用。

## 大肥鱼技能

| 技能 | 表现与应对 |
| --- | --- |
| 摆尾断浪 | 蓄力扭身后扫出前方扇形水刃；金色预警，可格挡和弹反，弹反后获得追击破绽。 |
| 大肥鱼冲撞 | 矩形红色预警后沿固定方向高速冲撞；向侧面闪避，不能格挡。 |
| 鱼跃压顶 | 锁定起手时的位置，跃起后砸向红圈，并产生扩散冲击波；先移出落点，再闪避水环。 |
| 深海泡泡阵 | 连续三轮扇形泡泡，轮次之间偏转射角；侧移找空隙，也可格挡和闪避。 |
| 涡流三重奏 | 觉醒后连续释放三道扩散水环；按水环接近的节奏闪避。 |

技能都有起手、命中与收招阶段，HUD 显示当前技能。红色代表不可格挡，金色代表可格挡 / 弹反。Boss 离开战斗条件、被弹反、玩家重生或 Boss 死亡时会清除残留弹幕；玩家退回封印门外会重置 Boss。所有泡泡和水环使用固定容量 32 的对象池。

`TrialWorld.cs` 在启动时生成整座寺庭，包括可碰撞地面与墙壁、寺殿、枫树、灯笼、封印、香炉、庭心圆阵和远山，并按材质合并静态网格。原始材质与 Tomorin FBX 不被改写。

## 代码分工

- `TrialGame`：明确的场景组合与试炼流程。
- `TrialPlayer` / `TrialEnemy` / `TrialFighter`：角色状态、移动、近战与资源。
- `TrialInput`：复用原 Player Input Actions 并增加剑术按键，支持设备筛选。
- `TrialCamera`：第三人称跟随、锁定与遮挡收近。
- `TrialRig`：Tomorin 骨骼动作。
- `TrialBoss` / `TrialBossRig`：DeepSeek 技能、预警、弹幕池和骨骼动作。
- `TrialWorld` / `TrialEffects`：关卡、美术、粒子、停顿与原创合成音效。
- `TrialHud`：中文 Canvas HUD、标题、暂停、死亡与通关界面。
- `Assets/Editor/Trial/`：场景生成、动画导出、Play Mode 集成验证与截图工具。

## 验证

`Tools > Tomorin > Run gameplay smoke checks (Play Mode)` 会临时接管角色并重置进度，以虚拟键盘实际驱动输入。`Verify movement and DeepSeek boss (Play Mode)` 会在原有检查之后继续验证奔跑骨骼、急停、反向、锁定横移、连招取消、Boss 五种技能、半血觉醒、暂停、弹幕清理和重生。结果写入 `Logs/Trial/Validation.txt`。

该作品定位为可玩的单关卡原型，尚无商业级动作捕捉、复杂地形导航、装备系统、存档、过场或 AAA 美术。当前 AI 适配此开放庭院，使用局部碰撞避让。角色与 Boss 的本次验证记录见 `Docs/COMBAT_VALIDATION.md`。

为本地编辑器操作新增 `com.unity.pipeline`；默认端口范围在此机器不可用，编辑器服务使用 18080。本地控制服务不会启用在发行构建中。保留了用户原有 Mobile_RPAsset.asset 修改。
