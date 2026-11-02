# 角色手感与大肥鱼 Boss 验证

验证日期：2026-11-02。Unity 6000.3.27f2 / Windows。

## 已实现

- 普通移动 4.5 m/s、疾跑 7.2 m/s；快速加减速、反向制动、锁定横移、转向侧倾、速度驱动步态和镜头视野过渡。
- 0.28 秒攻击 / 闪避输入缓冲、提早衔接三连斩、收招闪避取消、缩短翻滚及受击保护。
- 使用项目 DeepSeek 模型实现大肥鱼 Boss：摆尾断浪、大肥鱼冲撞、鱼跃压顶、深海泡泡阵、涡流三重奏；低于 55% 生命进入深海觉醒。
- Boss 起手预警、技能名、红色不可格挡提示、命中窗口、弹反硬直、32 槽弹幕池及重生 / 死亡清场。
- Tomorin 原有 13 个动画已重新导出；DeepSeek 新增 10 个动画、预览控制器和骨骼预制体。

## Play Mode

最终日志：`Logs/Trial/Validation.txt`，2026-11-02 15:23:35（UTC+8），**44 项通过**。

检查以 Input System 虚拟键盘驱动真实游戏帧，覆盖原有 21 项玩法回归，以及奔跑骨骼变化、急停、反向转身、锁定横移、连招输入缓冲、闪避取消、DeepSeek 模型引用、五种 Boss 技能、真实按键弹反、觉醒、暂停、重生及死亡清场。

检查过程中修正了弹反触发硬直后又被技能结束逻辑覆盖的问题。奔跑检查连续采样步态，避免两个采样点恰好落在相同腿部角度。

画面记录：`Logs/Trial/DeepSeekEncounter.png`、`Logs/Trial/DeepSeekBarrage.png`。两张均来自运行中的游戏相机。

## Windows 构建

最终构建于 2026-11-02 15:25:22（UTC+8）成功，0 错误、1 警告，约 188 MB。警告来自 Unity URP 包内 `Hidden/Core/DebugOccluder` 的 D3D11 向量截断，不在本次角色或 Boss 代码中。

包含当前启用的 InkStart、AshenCourt、SampleScene；返回独立菜单所需场景已打包。构建摘要见 `Logs/Trial/BuildSummary.txt`。

入口：`Builds/AshenCourt/Tomorin_AshenCourt.exe`。独立版无图形启动检查完成，日志 `Logs/Trial/PlayerStartup.log` 中未出现异常；画面与战斗交互由上述编辑器实测验证。

## 重跑

打开 `Assets/Scenes/AshenCourt.unity`，进入 Play Mode，选择 `Tools > Tomorin > Verify movement and DeepSeek boss (Play Mode)`。检查会接管输入并重置进度。

仅想直接试 Boss，可在该场景的标题界面点击「直接挑战大肥鱼」。
