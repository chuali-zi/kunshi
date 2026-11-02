# 《困室》 / ConGame

Unity 固定机位死亡循环解谜游戏。玩法与剧情设计位于 `docs/`，美术生产要求位于 `art/`，Unity 工程内容位于 `Assets/_Game/`。

## 打开工程

1. 使用 Unity `6000.4.6f1` 打开仓库根目录。
2. 等待 Package Manager 和脚本编译完成。
3. 执行菜单 `ConGame > Scaffold > Build M1 Greybox`。
4. 如果脚手架提示刚启用了 Input System，重启一次 Unity。
5. 打开 `Assets/_Game/Scenes/Bootstrap.unity` 并进入 Play Mode。
6. 使用 `Window > General > Test Runner` 运行 EditMode 与 PlayMode 测试。

场景生成命令是幂等的：重新执行会重建由脚手架管理的场景与定义资产，不会操作 `art/` 中的 DCC 源文件。首次由 Unity 打开工程后，应将 Unity 生成的 `.meta` 和 `Packages/packages-lock.json` 一并纳入版本控制。

## M1 控制

- 移动：`WASD` / 左摇杆
- 冲刺：`Shift` / L3
- 交互：`E` / 手柄南键
- 挥击：鼠标左键 / RT
- 投掷：鼠标右键 / LT
- 姿态/藏身：`C` / 手柄东键
- 暂停：`Esc` / Start

游戏内没有交互提示。当前灰盒用于验证固定机位、拾取/挥击/藏身、神秘人入场、D01/D03/D07 选择与无缝重置，不代表最终美术和动画品质。

## 文档权威顺序

1. `docs/story_outline.md`：原始构思，保持原样。
2. `docs/game_outline.md`、`docs/items_interactions.md`、`docs/game_flow_script.md`：体验与内容设计。
3. `docs/gameplay_logic_spec.md`：可执行规则的唯一真相。
4. ScriptableObject 数据与代码。

架构说明见 `docs/technical_architecture.md`。

## MV 主镜头（2026-11-01）

`Room_M1.unity` 已按 `art/reference/mv_frames/mv_t0006.0s.jpg` 调整固定正面机位、家具与灯光，可直接从 Bootstrap 进入。菜单 `ConGame > Art > Build Reference Main Shot (Round 2)` 在现有玩法接线之上重建这次的视觉细节，并重烘焙导航；程序生成资产保存在 `Assets/_Game/Art/MainShot_20261101/`。本轮以真实帧为参照，早期未看 MV 的文档不作为外观依据。

可重复的批处理入口（均保留图形设备，不使用 `-nographics`）：

```bash
# 重建场景外观并输出编辑状态的相机图。
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -logFile /workspace/ref/mv_build.log \
  -executeMethod ConGame.Editor.MvMainShotBuilder.Build -quit

# 捕获实际运行中第 3 秒的苏醒坐姿，完成后自动退出；此入口不加 -quit。
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -logFile /workspace/ref/mv_capture.log \
  -executeMethod ConGame.Editor.MvMainShotBuilder.CaptureGameplay
```

默认输出 `/workspace/ref/progress.png`（1920×1080），可用 `-mvScreenshot <path>` 覆盖。`ConGame.Editor.MvMainShotBuilder.Capture` 则直接捕获已保存场景的主相机，不推进玩法。捕获包含 URP 调色、暗角和颗粒。

Round 1 验证：Unity `6000.4.6f1` 编译零错误；EditMode `169/173`，所选 PlayMode `10/14`（导航、主场景 smoke、灯光跟随与循环重置通过）。四个 EditMode 失败项涉及 aim fallback 默认值与 SemanticVfx 事件，四个 PlayMode 失败项涉及输入注入后的起身、地板移动、被子拾取与台灯开关；这八项均在原始场景/配置基线复测中复现。批处理日志与 NUnit XML 位于 `/workspace/ref/mv_*`。

Round 2：相机后移至 `(0, 1.55, -5.2)`，垂直 FOV `36°`，俯角 `3°`，让左侧书柜、完整门框、天花边缘与更多地板入镜。固定机位使用独立的观看锚点；前墙与电视等前景仅在固定视角剖开，第一人称恢复可见，结局的电视前出生点仍留在房间内。角色默认在木椅上面向镜头坐着；模型偏移补偿 CharacterController 的 `0.08m` skin width，坐姿再单独校准鞋底与椅面。墙纸每组玫瑰间隔由 `0.35m` 缩为 `0.225m`；被褥采用更少、更宽的褶皱，并加强暖琥珀色、暗角与颗粒，压暗天花。

截图现在使用 `RenderPipeline.StandardRequest` 更新 Volume 后捕获，避免 `SingleCameraRequest` 跳过 Volume 更新造成 PNG 缺失调色、暗角和颗粒。可在上述运行捕获命令末尾加 `-mvStanding true -mvScreenshot /workspace/ref/round2_standing.png`，直接触发坐姿 presenter 的起身方法并检查站姿接地；这不代替真实设备输入测试。

Round 2 验证：Unity `6000.4.6f1` 编译零错误，导航重烘焙后全部战术锚点可达。所选 PlayMode `8/10`：新增的无输入坐姿保持、坐/站实际鞋底接地、固定镜头构图与第一人称剖面恢复测试通过，主场景 smoke、导航、灯光跟随、结局接管测试通过。剩余两项仍是 Round 1 已复现的键盘注入起身/地板移动失败，本轮没有修改输入系统。最终场景再次运行主场景 smoke、坐/站与镜头、导航三项，`3/3` 通过。日志与 XML：`/workspace/ref/mv_round2_playmode_verified.*`、`/workspace/ref/mv_round2_final_smoke.*`；最终游戏相机图：`/workspace/ref/progress.png`。

Round 3：加入灯灭后的灰蓝近单色调色、随既有神秘人灯光启闭的可见光锥、实际照亮房间的枪口点光；死亡的 0.8 秒保留内叠加白色「困室」标题，重置直接切回坐姿。D12 镜头盲区枪击与 E2 观看者中枪可触发屏幕玻璃裂纹；首次重击电视触发短漫画领悟插画，期间玩法照常推进。所有暂态表现参与循环重置，不增加存档字段。完整接线、素材来源、批处理入口和验证记录见 [Round 3 说明](docs/mv_signature_beats_round3_2026-11-01.md)。
