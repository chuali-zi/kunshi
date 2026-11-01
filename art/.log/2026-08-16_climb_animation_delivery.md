# 2026-08-16 AN_Player_Climb 攀爬动画交付记录

来源：2026-08-16 `docs/items_interactions.md`（床沿短按攀爬到床面打顶灯）与
`docs/gameplay_logic_spec.md`（可攀爬木椅短按攀爬、长按搬运）新增“攀爬”交互，
代码侧 `ClimbCapability`/`PlayerMotor.TryClimb` 已就位但**清单中没有对应动画**，
角色只能平滑滑上台面的占位表现。本次补齐该美术缺口。

## 交付物

### 动画 AN_Player_Climb（NIUA text2motion，21¢）

- 清单登记：`art/pipeline/niua/manifests/animations.json` 新增条目；
  `art/requirements/animation_manifest.md` §3 新增 P1 行（低位攀爬，木椅座面/床面共用，
  不复制两条——高度差由脚部 IK 吸收，位移由控制器提供）。
- 生成：`gen_animations.py --only AN_Player_Climb`，2.0s / 48 帧@24fps / 非循环 /
  In-Place / mixamo 骨架。
- 产物：`art/exports/animations/AN_Player_Climb.glb + .fbx`（Blender 5.1 无头转换，
  bake_anim=True，单文件定向转换，未触碰既有角色 FBX）。
- 机检：新增 `art/pipeline/niua/inspect_animation.py`（纯标准库 GLB 解析 +
  numpy/matplotlib 棍图，替代已散佚的 verify/analyze 脚本）。
  结果：67/67 骨、mixamorig 命名、时长 2.000s（±20% 内）、髋部水平漂移 0.000m。
- 语义抽查（脚部高度轨迹）：t=0.75 左脚抬至 0.63m（跨上膝高台面）、髋部下蹲蓄力
  至 0.71m → t=1.5 右脚跟抬 0.42m → t=2.0 恢复直立。符合“踏上台阶”语义。
- 棍图预览：`art/previews/animations/AN_Player_Climb.png`（全帧叠加正视/侧视）。
- Unity 导入：`import_to_unity.py` 已扩展条目，复制到
  `Assets/_Game/Art/Animations/` 并生成 Humanoid meta（lastFrame=47、非循环，
  模板取 AN_Player_Swing）。

## Unity 接入（本次一并完成）

- `ConGameCharacterInstaller.BuildController`：AC_Player 新增 `Climb` trigger +
  `ClimbRate` float + Climb 一次性状态（AnyState 进入、exit 0.95 回 Idle），
  状态 speedParameter=ClimbRate，让 2s 剪辑适配实际攀爬时长。
- `PlayerMotor.TryClimb`：新增 `ClimbStarted(duration)` 事件（纯表现信号，
  不改移动逻辑）；`CharacterAnimatorDriver` 订阅并按 `2.0/duration` 设 ClimbRate
  后 SetTrigger(Climb)；ResetTriggers 一并复位。
- 攀爬时长从占位值调到可读区间：木椅 0.4→0.85s、床 0.45→0.95s
  （`ConGameScaffoldBuilder` + `ConGameArtInstaller`），对应播放速率约 2.2–2.35x。
- 批处理 `ConGameCharacterInstaller.Run` 重建控制器并保存场景（INSTALL done，
  日志 `Logs/character_install_climb_2026-08-16.log`；Climb 状态 motion 已确认
  指向 `AN_Player_Climb.fbx` 剪辑 GUID）。

## 测试

- PlayMode `InteractionRepairPlayModeTests` 12/12 通过（含
  `ClimbMovesToStandAnchorThenRestoresFreeMovement`；
  `Logs/playmode_climb_2026-08-16.xml`）。注意：Unity 6000.4 批跑测试的可用参数为
  `-batchmode -nographics -runTests -testPlatform ... -testResults ...`，**不要加
  `-quit`**，否则初始化后直接退出、测试不执行。
- EditMode 全量回归 39/39 通过（`Logs/editmode_climb_2026-08-16.xml`）。

## 验收注意事项

1. 状态为**待验收**：需在 Unity 固定机位确认踏上动作与 `ClimbTo` 位移同步、
   脚底不打滑（必要时按 animation_manifest §7 用 Animation Rigging 修脚接触）。
2. 动画事件无需添加（无 Attach/Hit 窗口）；若后续要脚步声，
   在剪辑上打 Footstep 标记即可。
3. 剪辑 In-Place，垂直位移完全由 `PlayerMotor.ClimbTo` 提供；若改攀爬时长，
   ClimbRate 会自动等比缩放，无需重导资产。
