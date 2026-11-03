# 大肥鱼步态与耐力恢复

2026-11-03，Unity 6000.3.27f2，Windows 编辑器。

## T 字形平移追加修复

后续复查确认：当前 DeepSeek.fbx 的 body、clothing、clothing.001、hair.001 是静态 MeshRenderer；hair 与 head.001 虽有 SkinnedMeshRenderer，但 bones 和 bindposes 都为空。此前的腿部角度、支撑脚高度检查只证明骨骼运动；之前摆尾网格采样实际只采集到两个不带骨骼的表情网格，不能证明身体动画有效。这是上轮验证遗漏。

从 Git `06c5ca3` 中恢复原始 FBX 权重（LFS SHA-256 `3ce133027fb7de30ac8a1af6e4d32883877f8f8d4a9aa8f8b6993c4d7502635f`），使用 `TrialBossSkinRepair.Repair` 转移到新版几何。六个网格的基础顶点位置可精确对应，保留新版 TiePen 与 16 个面部 blend shapes。生成 `DeepSeekAnimatedMeshes.asset` 与 `DeepSeek_Animated.prefab`，AshenCourt 场景改用该预制体，动画导出工具与验证工具同步使用它。临时源 FBX 已删除，输出不依赖该临时文件。

修复后的复验：36 项姿态及蒙皮检查通过，覆盖六个网格各有 146 根绑定骨骼、走路/冲撞/折返冲撞的真实身体顶点变形、双手低于肩膀及铁盆形态保留；三个移动动作各有 2,818 个身体顶点位移超过 2 厘米。404 个摆尾姿态重新采样六个真实蒙皮网格，通过穿地检查。Play Mode 已确认游戏实例加载六个有效蒙皮组件，并保存正面实际渲染画面 `Logs/Trial/FishSkinnedWalkA.png`、`FishSkinnedWalkB.png`，双臂已放下。两张手动姿态采样在同一次渲染调用内，不能作为跨帧 GPU 步态验证。

重建权重时，将上述 Git 历史的 LFS 二进制临时导入为 `Assets/Art/Models/DeepSeek/DeepSeekSkinSource.fbx`，再调用修复工具；输出已存在时工具会拒绝覆盖，需先人工核对资产引用。原始新版 FBX、贴图及材质不变。本次未重新打包独立版。

## 修改

- 大肥鱼运行时根据实际平面位移驱动步频与步幅，覆盖追赶、冲撞、折返冲撞及客户端插值；不再依赖技能期间归零的 MoveBlend。大于 2 米的单帧位移按传送处理，不推进步频。
- 加大交替迈腿、屈膝幅度，脚踝抵消腿部旋转；根据脚掌和脚趾的最低点修正模型高度。取消待机及施法的持续上浮，跳砸保留腾空。
- 药瓶在原有动作生效点恢复生命并补满耐力；满血但缺耐力也可以喝药。提前中断不恢复、不扣瓶。
- 完美闪避恢复 25 耐力，每次闪避最多奖励一次；成功弹反恢复 18，即返还 8 点消耗并净恢复 10。恢复均不超过上限，同时清除恢复延迟及疾跑耗尽状态。
- 耐力结算仍在原有权威模拟路径中，沿用已有快照同步字段。

## 已验证

- `Verify fish footwork (Edit Mode)`：29 项通过，覆盖三个朝向的八种地面姿态、追赶及两种冲撞交替迈腿、静止不迈腿和跳砸腾空。脚部支撑高度误差小于 0.002 米。
- `Verify tail sweep grounding (Edit Mode)`：404 个真实蒙皮网格姿态通过，未采集到低于地面的网格顶点。
- `Verify stamina rewards (Play Mode)`：12 项通过，覆盖补满、生命与耐力同时恢复、满血喝药、提前中断、药瓶只扣一次、完美闪避单次奖励、较晚无敌窗口无奖励、弹反及上限。
- `Verify displacement driven fish gait (Play Mode)`：3 项通过。MoveBlend 为零时，运行时实际位移仍驱动追赶、冲撞和折返冲撞腿部变化。
- 检查后的编辑器编译无错误；`git diff --check` 通过。证据位于 `Logs/Trial/FishFootworkValidation.txt`、`StaminaRewardsValidation.txt`、`FishDisplacementValidation.txt`，另保存了 `FishFootwork.png` 姿态画面。
- 开发中在 Play Mode 热重载验证脚本后，运行时集合丢失导致 HUD 空引用及特效集合越界；退出并重新进入 Play Mode 后重跑上述运行时检查，未再出现这些异常。本次没有修改项目的热重载生命周期。

## 手动复验与范围

从 AshenCourt 进入 Boss 战，拉开距离观察追赶、冲撞与二次折返，检查两腿交替、地面支撑及停步；消耗耐力后喝药，确认动作生效时补满，再分别尝试提前打断、一次翻滚躲过多个接触和成功弹反。

本次未重新打包独立版，未运行双端联机或手机实机验证；运行时位移测试使用编辑器内受控位移，尚需实际游玩评价步频和战斗资源平衡。
