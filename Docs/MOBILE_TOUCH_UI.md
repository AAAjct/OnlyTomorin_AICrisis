# 手机战斗操作区优化（2026-11-03）

## 手机输入框与软键盘（2026-11-12）

- 联机菜单的昵称、房间名、连接地址、端口和公网入口统一接入 `TrialMobileKeyboardAvoidance`。手机键盘可见时将当前输入框移到屏幕顶部安全区域，键盘隐藏或结束编辑后恢复；设置 `shouldHideMobileInput = true`，在游戏中显示文字和光标。
- 最终修正将 Android Application Entry 从 GameActivity 改为 Unity 标准 Activity，使用标准原生输入框通路。此前用于 GameActivity 的自定义 Java Activity、manifest 构建回调、IME 标记及菜单点按隔离均已移除。
- 原始截图显示输入法全屏提取文本界面覆盖游戏。后续真机诊断确认：GameActivity 方案下，键盘绘制在屏幕 y=517..1260，InputMethod 窗口的可点区域却为 y=0..743。上排部分键可输入，下排按键落到游戏窗口，导致输入框取消焦点或无法输入。系统证据：`Logs/Trial/Keyboard_WrongTouchRegion.txt`。
- 失焦调用栈为 `InputSystemUIInputModule.ProcessPointerButton → EventSystem.SetSelectedGameObject → InputField.OnDeselect → DeactivateInputField`，此时键盘仍可见。证据：`Logs/Trial/KeyboardFocusProbe.txt`；临时日志已移除。仅隔离游戏点按虽能保住焦点，却不能修复输入法可点区域，因此未保留该处理。
- 编辑器布局验证入口：`Tools/Tomorin/Verify mobile keyboard avoidance`，覆盖两种画布缩放、安全区、连续更新、恢复、禁用，以及实际创建/加入房间的全部输入框。曾通过的 28 项包含已移除的点按隔离检查；当前测试保留 22 项布局及原生预览设置检查。该结果不能代替真机输入法验证。
- Android APK 构建、安装成功：`Builds/Android/OnlyTomorin-KeyboardFix.apk`。已在 V2361A 手机、搜狗 vivo 输入法上完成真机回归：昵称中文候选确认及下排 A/Z 字母输入、地址/房间名/公网入口下排字母输入、端口数字输入均实际写入且保持焦点；中英文切换、删除、完成和系统返回收起正常，布局恢复，测试追加字符已删除。
- 修正后的 InputMethod 可点区域为 y=517..1260，与键盘显示位置一致：`Logs/Trial/Keyboard_Activity_TouchRegion.txt`。各字段输入后的截图为 `Logs/Trial/Keyboard_Activity_*Passed.png`。原有 22 项编辑器布局检查已扩展为下述 32 项，无当前 Console 编译/运行错误；未验证其他型号或输入法。
- 真机回归：依次编辑所有字段，输入英文字母、中文并确认候选字、数字和标点，切换输入法模式，按完成或系统返回收起，再次编辑；检查字符实际写入、按键不导致意外失焦、关闭后布局恢复。
- Unity 官方入口说明：https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/developing/application-entries 。入口决定原生输入与软键盘的实现；未更新 Unity 或第三方包。

### 输入法倒三角收起后的归位

- 真机重现：输入房间名后点搜狗右上角的倒三角，系统 `mInputShown=false`，但输入框仍保持编辑状态。原布局仅判断 `InputField.isFocused`，因此还会上移，需要额外点按屏幕结束编辑才恢复。
- 真机进一步发现该路径下 Unity 的 `TouchScreenKeyboard.visible` 也可滞后，因此不能单独用它恢复布局。Android 11/API 30 以上改为读取 Activity 根视图的 `WindowInsets.isVisible(Type.ime())`，每 50ms 采样一次；其他平台及旧 Android 保留 Unity 可见状态回退。根视图 JNI 引用随组件销毁释放。
- `UpdateKeyboardLayout` 每帧先恢复已有偏移，仅在键盘实际可见时上移。倒三角隐藏键盘后直接归位，并结束之前已显示键盘的字段编辑，保留输入内容；这样下一次点同一字段能够正常重新弹出键盘。没有观察到键盘显示的新编辑不会被提前取消。官方 API 说明：https://developer.android.com/develop/ui/views/layout/sw-keyboard 。
- Play Mode 32 项检查通过，覆盖键盘隐藏后的归位与文字保留，以及新编辑等待键盘弹出时的行为。最终包已在搜狗 vivo 输入法上验证，点击倒三角后界面自动归位，再次点击同一房间名输入框可正常弹出键盘，原文字保留。

## 战斗触控布局（2026-11-03）

- 轻击从 90×80 放大到 176×176，闪避为 156×156，重击和格挡为 148×148（900 高参考画布单位）。四个战斗键独立排布，增大文字和可点击区域，保留图标与中文名称，加入按下变色反馈。
- 药瓶移到左侧，锁定独立放在右侧战斗键上方。取消独立奔跑按钮：移动摇杆拖到最大行程的 95% 时奔跑，回收至 85% 以下或松手时停止；手机摇杆不受键盘/手柄长按/切换设置影响。
- 休息键只在非竞技场、靠近休息点时出现；手机操作提示不再显示键盘按键。手机隐藏重复的桌面技能条。
- 保留安全区域适配、左半屏浮动摇杆和右半屏拖动视角。Boss 血条放在屏幕底部安全区域内，目标文字与暂停键错开。暂停、死亡、选择入庭增益时隐藏操作区并清除触控输入。
- 沿用运行时生成的 uGUI，无需重新配置场景或 Inspector。

## 空白触屏误触轻击修复

- 删除 `Player/Attack` 的 `<Touchscreen>/primaryTouch/tap` 全屏点按绑定。该绑定独立于 UI 接收触控，点按空白区域或格挡时也可能触发轻击。
- 输入层忽略原始触屏轻击和手机端鼠标轻击事件，再合并专用轻击按钮输入。手机触控轻击仅由“轻击”按钮触发，键盘和手柄攻击继续保留。
- 本次修改后 Unity 编译完成，未出现编译错误；尚未重新打包进行真机回归。真机检查：连续点按空白区域、快速反复按下/松开格挡、移动时连续格挡均不应出轻击；单按轻击及移动中按轻击应正常攻击。

## 验证

Unity 6000.3.27f2 编译成功，无编译错误。Play Mode 中强制启用手机控件，确认帧数持续推进。

在 1005×776 和 1920×1080 Game View 各通过 85 项检查：按钮位于安全区域内、尺寸、中心与左右边缘真实 UI 射线命中、所有可见按钮互不重叠、隐藏键鼠技能条、双指移动/视角隔离、格挡保持和释放、轻击输入及按下反馈、休息点显隐、暂停清理与恢复。另通过增益选择期间隐藏和选择后恢复的 2 项检查。

当前结果：`Logs/Trial/MobileTouchValidation.txt`；最终预览：`Logs/Trial/MobileTouchLayout_After.png`。验证后没有新增运行时 Console 错误。未连接手机实测拇指触感，也未重新打包 APK。

## 摇杆奔跑与底部 Boss 血条回归

Unity 编译通过，0 错误、0 警告。在战斗场景 Play Mode 强制开启手机控件，22 项检查通过：半程移动、拖满奔跑、外圈防抖、回收停止、超范围拖动、双指释放隔离、松手/禁用/暂停清理、两种奔跑设置、移除奔跑键、血条底部安全区域位置。结果见 Logs/Trial/JoystickSprintValidation.txt。战斗场景验证期间没有新增 Console 错误；尚未打包 APK 或真机验证。
