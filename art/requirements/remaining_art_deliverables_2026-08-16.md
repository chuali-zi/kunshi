# 《困室》剩余美术交付清单

> 基线：2026-08-16。这里只记录需要美术、动画、TA 或演出制作交付的内容。程序状态、事件契约和运行时编排缺口见 `docs/non_art_runtime_gaps_2026-08-16.md`。

## 1. 仍缺的新资产

| 优先级 | 交付 | 当前状态 | 验收要求 |
|---|---|---|---|
| ~~P0~~ | `AN_Player_PutDownLarge` | **已交付 2026-08-16**(NIUA text2motion，入 `Assets/_Game/Art/Animations/`，待验收) | 大物件稳定落地，不呈投掷感；provisional `Detach` 标记已注入(1.75s)，待验收校准 |
| ~~P0~~ | Killer Aim 拆分 | **已交付 2026-08-16**:`AimEnter`/`AimHold`/`Fire` 三条已生成，待验收 | AimEnter/Fire 因 NIUA 接口时长下限取 1.0s(需求 0.8s/0.6s)，用播放速度压缩；`AimHold` 为 3s 循环、可稳定冻结待验收 |
| ~~P1~~ | Killer 搜索/扫视循环 | **已交付 2026-08-16**:`AN_Killer_SearchLoop` 4s 循环已生成，待验收 | 低戒备扫描可循环；上身叠加行走依赖程序 Avatar Mask/上身层(非美术项) |
| ~~P1~~ | `AN_Killer_Run` | **已交付 2026-08-16**(1.2s 循环，待验收) | 适配电视重击阻止冲刺的 `5.5m/s`，步频匹配与滑步待固定机位验收 |
| P1 | `CAM_E2_FirstPersonFall` | 未制作(纯演出制作项，text2motion 做不了相机曲线) | 约 `1.2s` 第一人称倒地曲线，避免剧烈滚转和晕动 |

`AN_Player_CrouchMove` 仍是条件项：只有程序化蹲姿和 IK 试玩不合格时才生产，不纳入当前必交。

## 2. 已有动画仍缺交付标记

> **2026-08-16 更新：下表全部标记已以 provisional 状态注入各 FBX 的 ModelImporter `clipAnimations[].events`(时间与理由见 `art/.log/2026-08-16_animation_events_provisional.md`，注入脚本 `art/pipeline/add_animation_events.py`)。标记时间已由管线按归一化位置预填，仍待动画/演出验收校准；程序只消费语义。代码侧尚无事件消费端，函数名清单已在日志列出供程序对齐。**

| 动画 | 必须补充的标记或对位数据 |
|---|---|
| `AN_Player_Swing` | ~~`HitStart`、`HitEnd`~~ 已注入(provisional 1.35s/1.80s) |
| `AN_Player_Pickup` | ~~`Attach`、反放使用的 `Detach`~~ 已注入(provisional 均 1.25s)；手部 IK 对位仍待运行时 Animation Rigging |
| `AN_Player_Throw` | ~~`Detach`~~ 已注入(provisional 1.25s) |
| `AN_Killer_AimFire` | ~~过渡方案使用期间至少提供 `AimHold`、`Fire/Muzzle`~~ 已注入(provisional 1.00s/2.00s/2.00s)；正式拆分剪辑已交付 |
| `AN_Killer_KickDoor` | ~~`KickContact`~~ 已注入(provisional 1.25s) |
| `AN_Killer_Stumble` | ~~轻/重区段、恢复结束~~ 已注入 `StumbleLightEnd`/`StumbleHeavyEnd`/`RecoverEnd`(provisional 1.20s/2.10s/2.70s) |
| `AN_Killer_BlockLight` | ~~`BlockStart`、`HardStunEnd`~~ 已注入(provisional 0.45s/2.70s) |
| `AN_Killer_DrawBackupFire` | ~~`Attach`、`Muzzle`、`Fire`~~ 已注入(provisional 1.40s/2.50s/2.50s) |
| `AN_Killer_Fall` | ~~落地接触、PAIR 起始 Pose~~ 已注入 `FallContact`/`PairStart`(provisional 1.50s/2.70s) |
| `PAIR_D08` | ~~双方起点、朝向、距离、接触和脱离标记~~ `PairStart`/`PairContact`/`PairRelease` 已注入双边(provisional 0s/0.60s/5.10s)；朝向/距离仍由演出锚点保证 |
| `PAIR_D11` | ~~床边/床底锚点、抓脚与释放标记~~ `PairStart`/`PairContact`/`PairRelease` 已注入双边(provisional 0s/0.45s/2.70s)；锚点仍由 Timeline 对位 |
| `PAIR_D21` | ~~Fall 接续点、循环段、枪/灯 `Detach`、Block/Draw 接续点~~ `PairStart`/`PairContact`/`PairLoopStart`/`PairLoopEnd`/`Detach`/`BlockStart`/`DrawStart` 已注入双边(provisional) |

## 3. 仍缺 VFX 与技术美术

| 优先级 | 交付 | 最低范围 |
|---|---|---|
| P0 | 枪击反馈 | 枪口火光、烟、弹着火花/血迹；由权威 `ShotFired/ShotResolved` 驱动 |
| P0 | 破坏反馈 | 木椅木屑、花瓶陶瓷碎片、镜裂、窗板木屑、墙灰、灯玻璃、电视爆裂 |
| P1 | 战术手电可见光锥 | 枪挂和手持共用；暗处可读且不过曝，视觉边界与机械锥一致 |
| P1 | 镜面材质 | 低分辨率 RenderTexture 表面、破裂版本和停更状态 |
| P1 | 被子过渡 | 四态切换所需短形变/遮挡；不制作长布料模拟 |
| P1 | E2/E3 近景残屑 | E2 手部玻璃残屑、E3 枪口对镜头和黑屏前轮廓 |

所有临时碎片、粒子和 Decal 必须提供可池化 Prefab，禁止以销毁重建作为循环重置方案。

## 4. 正式演出资产

| 组 | 当前状态 | 仍需交付 |
|---|---|---|
| D01-D22 | 多数仍复用 Greybox Performance | 差异化站位、节拍、相机、动画、道具、音频/VFX 语义轨 |
| D08/D11/D21 | 双边 FBX 已有 | 正式双人 Timeline、专用锚点和接触标记 |
| D21 | 单段资源均已有 | `Fall → PAIR → BlockLight → 掉枪/掉灯 → DrawBackupFire` 完整编排 |
| E1/E2/E3 | 程序化演出 | 电视爆裂、第一人称倒地、坐椅/秒针/备用枪正式时间线 |

## 5. 已完成，不要重复制作

- 2026-08-16:`AN_Player_PutDownLarge`、`AN_Killer_AimEnter`/`AimHold`/`Fire`、`AN_Killer_SearchLoop`、`AN_Killer_Run` 已由 NIUA text2motion 交付并入库(均待固定机位验收；记录 `art/.log/2026-08-16_remaining_animations.md`)。
- 2026-08-16:§2 全部语义标记已 provisional 注入(待验收校准；记录 `art/.log/2026-08-16_animation_events_provisional.md`)。
- `AN_Player_Climb` 已接入 `PlayerMotor → CharacterAnimatorDriver → AC_Player`，仅待固定机位脚底验收。
- 主枪、枪轨手电、枪套和备用枪模型及 Socket 已接入角色视觉。
- 木椅、花瓶、镜子、挂钟完整/破坏模型已接 Presenter。
- 被子 Crumpled/Covered/FlatTrap/Draped 四态已接 Presenter。
- 封窗五段和墙损三档已接命中事件与循环重置。
- 电视三态、顶灯/台灯完整与破坏态已有并已接入。

## 6. 统一验收

- 固定机位、持续全黑和 E2 第一人称三种条件都必须验收。
- 双人动作启动误差目标不超过 `0.15m/10°`。
- 动作、道具 Attach/Detach、碰撞、音效和 VFX 必须同帧可读。
- 所有视觉临时对象必须能被循环重置回收，不跨循环残留。
