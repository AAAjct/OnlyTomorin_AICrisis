# 只鹅 · 智械危机

以凑企鹅 Tomorin 为主角的第三人称剑术动作游戏。支持单人挑战、多人合作和竞技场对战。

## 开始游玩

用 Unity **6000.6.8f1** 打开项目，等待资源导入完成。打开 [InkStart](Assets/Scenes/InkStart.unity)，点击 Play，从主菜单选择「单人游玩」或「多人联机」。

项目使用 URP、Input System 和 Mirror。Android 打包需要为该编辑器安装 Android Build Support、SDK、NDK 和 OpenJDK。

## 游戏内容

- **Boss 挑战** · 从水墨庭院的大肥鱼到断羽终庭的凑企鹅残躯，共七个关卡，可选简单、正常或困难。
- **无尽雾潮** · 按波次生存，击败敌人积累经验并选择强化。
- **百战之庭** · 连续挑战八位 Boss，规划有限的药瓶。
- **多人联机** · 合作挑战关卡，或进入竞技场对战。

键鼠默认用 WASD 移动、鼠标控制镜头，左键轻击、右键格挡、Q 重击、空格闪避。支持手柄和手机横屏触控，可在「设置」中调整键鼠与手柄绑定。[完整操作与战斗规则](Docs/PLAY.md)

## 多人联机

在「多人联机」中填写昵称，创建或加入房间。各端使用同一游戏版本。

| 连接方式 | 加入方法 |
| --- | --- |
| 房间码 | 输入房主分享的 Unity Relay 房间码。 |
| IP 直连 | 输入 `IP:端口` 或 `域名:端口`，默认 TCP 端口为 7777。 |
| 公共房间 | 从房间列表选择。公开直连房间需提供玩家可访问的公网入口。 |

房间码与公共目录需要可用的 Unity Services 配置。[联机流程与验证](Docs/NETWORK_VALIDATION.md)

## 开发文档

[关卡与 Boss](Docs/CHAMPION_STAGES.md) · [移动端触控](Docs/MOBILE_TOUCH_UI.md) · [资源目录](Docs/ASSET_ORGANIZATION.md)

编辑器中的 `Tools > Tomorin` 提供场景构建和 Play Mode 验证入口。验证报告写入本地 `Logs/Trial/`。
