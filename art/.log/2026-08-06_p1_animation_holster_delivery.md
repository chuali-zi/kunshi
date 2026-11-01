# 2026-08-06 P1 动画/Pose/PAIR 组件 + 神秘人枪套交付记录

依据 `art/requirements/art_content_gap_spec_2026-08-06.md` 的"缺资产"清单补齐。

## 交付物

### 动画 24 条（NIUA text2motion，mixamo 骨架，In-Place，~$5.3）

| 组 | 条目 | 说明 |
|---|---|---|
| P1 单人 ×8 | AN_Player_Pickup / AN_Player_Throw / AN_Killer_DodgeSide / AN_Killer_KickDoor / AN_Killer_Stumble / AN_Killer_Fall / AN_Killer_BlockLight / AN_Killer_DrawBackupFire | animation_manifest §3 全部补齐（除 CrouchMove 维持待评估、CAM_E2 属 Unity Timeline） |
| 静态 Pose ×10 | POSE_Player_SitChair / CrouchHide / Prone / KneelHandsUp / HoldSmall_R / HoldBat / CarryLarge / AimPistol、POSE_Killer_LowReady / Kneel | 2 秒循环静姿剪辑，Unity 取静帧或短 Blend 使用 |
| PAIR 单边组件 ×6 | PAIR_D08 / D11 / D21 各 PlayerSide + KillerSide | API 不支持双人同步；按 README 既定路线在 Timeline 按锚点对位拼接 |

- 产物：`art/exports/animations/*.glb + .fbx`（Blender 5.1 无头转换，`convert_all_fbx.py` 已扩展 POSE_/PAIR_ 通配与增量跳过）。
- 已复制到 `Assets/_Game/Art/Animations/` 并生成 Humanoid meta（`import_to_unity.py`：模板取 P0 meta，loop 取 TARGET|clip-loop / 非循环 TARGET|clip，lastFrame = 时长×24−1）。
- 机检：`verify_p1_animations.py` 24/24 通过（67 骨、帧数 = 时长×24fps ±20%）。
- 关节轨迹抽查：`analyze_p1_animations.py` 首/中/末帧关节世界坐标。Pickup 下探、Throw 过肩、BlockLight 抬前臂遮面、Stumble 前扑恢复、KneelHandsUp 举手、AimPistol/CarryLarge/LowReady 手部位置均符合语义。

### 道具 PRP_KillerHolster（Blender 脚本建模，免费）

- `art/source/blender/props/build_killer_holster.py` → `art/exports/props/PRP_KillerHolster.fbx` + `previews/PRP_KillerHolster_v001.png`。
- 决策（gap spec §3 低成本方案）：风衣保持并入 CH_Killer 本体，**删除独立蒙皮风衣承诺**；内侧枪套（口袋+斜挎带+胸围带+按扣，1548 tris）射线贴合风衣胸前建模，含 `SOCKET_BackupPistol`。
- Unity 接入：bone-parent 到 `mixamorig:Spine1`；备用枪 Attach/Detach 走 socket；D21/D22/E3 可见性策略由 Presenter 控制。
- 注意：CH_Killer_rigged.fbx 内混有 NIUA 管线的 1m Icosphere 残留网格（构建脚本已规避）；角色空间约半尺寸（全高 0.9m），Unity 侧 useFileScale 正常。

## 验收注意事项（重要）

1. **贴地类剪辑的"悬浮"约定**：text2motion In-Place 锁定髋部 XY 但保留站立高度基准，Fall/Prone/Kneel/CrouchHide/SitChair/PAIR 等贴地内容的身体是水平的但悬在站立髋高。Unity 接入时须用 Timeline 根锚点下移（或 Humanoid Root Y 设置）把髋部放回地面，不能直接在原根位置播放。
2. `AN_Killer_KickDoor` 经 3 版迭代（v1 抬腿过低、v2 过头顶劈踢、v3 seed 23 低踹伸腿 0.48m——采用 v3，低踹踹锁读法符合 D09）；`AN_Killer_DrawBackupFire` seed 11 版保留（有开衣-分手-前指节拍，拔枪精细轨迹靠 socket+Timeline 补足）。再换：`--only <name> --force --seed N`。
3. 动画事件（HitStart/HitEnd/Attach/Detach/Muzzle/Fire）仍未添加，按 animation_manifest §7 在 Unity 剪辑上手动打；Pickup 实际下探手为**左手**（Attach 事件挂 LeftHand）。
4. 全部 24 条状态为**待验收**：需在 Unity 固定机位/全黑/E2 第一人称三条件下过验收后才算完成；PAIR 三组另需双人锚点对位验证（误差 ≤0.15m/10°）。
