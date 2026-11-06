# 资源目录分类

项目自有资源按用途归类。角色模型附带的材质、贴图继续放在各角色的 Materials、Textures 子目录中，便于整体维护。

| 目录 | 内容 |
| --- | --- |
| `Assets/Audio/Resources/Trial/Audio/` | 139 个战斗音效，含 76 条连招与技能专属变体；后续普通音乐可放在 `Assets/Audio/Music/` |
| `Assets/Art/UI/` | 界面图片、图标、书法图片和字体 |
| `Assets/Art/Startup/` | 启动标识与作者头像 |
| `Assets/Art/Models/` | 角色、武器、特效模型和网格 |
| `Assets/Art/Materials/Resources/Trial/VFX/` | 运行时加载的特效材质 |
| `Assets/Art/Champions/` | 角色生成网格及配套材质 |
| `Assets/Anims/` | 动画片段和 Animator Controller |
| `Assets/Prefabs/` | 游戏预制体 |
| `Assets/Prefabs/AnimationPreviews/` | 六个角色动画预览预制体 |
| `Assets/Shaders/` | 着色器 |
| `Assets/Scenes/` | 游戏场景 |
| `Assets/Scripts/`、`Assets/Editor/` | 运行时代码、编辑器工具 |
| `Assets/Settings/` | 渲染与输入配置 |
| `Assets/ThirdParty/`、`Assets/Plugins/` | 第三方资源和插件，保持各自包结构 |
| `ArtSource/`、`Tools/`、`Docs/` | 美术源文件、生成工具、项目文档 |

## Resources 路径规则

运行时加载的资源放在对应类型目录下的 `Resources` 子目录。Unity 使用 `Resources` 后的相对路径，因此现有加载名称保持不变。例如：

- 文件：`Assets/Audio/Resources/Trial/Audio/hit_0.wav`
- 加载：`Resources.Load<AudioClip>("Trial/Audio/hit_0")`

材质和着色器可能共享相同的加载名称（例如 `Trial/VFX/TideSigil`），加载时保持原有的明确类型。新增同类型资源时避免重复的 Resources 加载名称。

`Assets/Resources/DOTweenSettings.asset` 保留为插件设置。移动资源应通过 Unity Project 窗口或 `AssetDatabase.MoveAsset`，保留 `.meta` 和 GUID；资源生成工具的输出路径也应同步更新。

本次通过编辑器移动 95 个资源，移动前后的资源和 `.meta` 内容逐字节一致，运行时 Resources 相对路径保持不变。
