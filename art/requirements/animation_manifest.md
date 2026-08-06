# 《困室》最小动画需求

## 1. 压缩原则

本项目不为 D01–D22 各做一套死亡动画。动画预算按以下规则控制：

- 通用枪杀统一为“神秘人瞄准开火 + 玩家一次中弹反应 + Ragdoll”。命中头、胸、背只调整瞄准目标和 Ragdoll 初始力。
- 检查物品主要用头部/胸部 IK 注视；只有拾取、挥击、投掷等影响碰撞窗口的动作制作动画。
- 坐、跪、俯卧、双手持物优先做 Pose 或短过渡，不做每种姿态的完整移动集。
- 门、抽屉、钟针、电视破坏、被子形态切换全部由 Unity Transform、Timeline 或预制体切换完成。
- 只有 D08、D11、D21 需要角色接触对位，保留三组双人动画；其余演出由通用动作组合。
- 风衣下摆使用辅助骨骼 + Spring Bone/简化物理，不为每个动作单独烘焙布料。

## 2. P0 通用动画

这些动画足够完成垂直切片 D01、D03、D07。可优先使用同骨架的免费动作库，只有 `WakeUp` 需要定制。

| ID | 角色 | 循环 | Root Motion | 用途 | 状态 |
|---|---|:--:|:--:|---|---|
| `AN_Player_Idle` | 玩家 | 是 | 否 | 默认站立、轻微呼吸 | 待验收 |
| `AN_Player_Walk` | 玩家 | 是 | 否 | 全部常规移动；速度由代码控制 | 待验收 |
| `AN_Player_WakeUp` | 玩家 | 否 | 否 | 扶手椅瘫坐 → 抬头环顾 → 起身；允许输入提前打断 | 待验收 |
| `AN_Player_Swing` | 玩家 | 否 | 否 | 球棍、木椅统一横向挥击；命中窗口由动画事件标记 | 待验收 |
| `AN_Player_HitFall` | 玩家 | 否 | 否 | 通用中弹反应，约 0.35 秒后切 Ragdoll | 待验收 |
| `AN_Killer_Walk` | 神秘人 | 是 | 否 | 入场、搜索、逼近；不制作常规跑步 | 待验收 |
| `AN_Killer_AimFire` | 神秘人 | 否 | 否 | 从低持枪抬枪、开火、保持瞄准；Aim Rig 调整目标高度 | 待验收 |

P0 只需要 7 条单人动作。蹲藏使用 `AN_Player_Walk` 的低速移动配合静态蹲姿/程序化骨盆下压，等验证手感后再决定是否补蹲走。

## 3. P1 完整玩法补充

| ID | 角色 | 用途 | 最省成本实现 | 状态 |
|---|---|---|---|---|
| `AN_Player_Pickup` | 玩家 | 地面小物拾取/放下 | 一条动作正放为拾取、倒放为放下；手部 IK 接管物体 | 未开始 |
| `AN_Player_Throw` | 玩家 | 投掷所有单手物品 | 一条通用过肩投掷；椅子不做投掷动画，直接复用挥砸 | 未开始 |
| `AN_Player_CrouchMove` | 玩家 | 沙发后、门后低姿移动 | 只在程序化蹲姿效果不足时制作 | 待评估 |
| `AN_Killer_DodgeSide` | 神秘人 | 闪避投掷物与 D22 子弹 | 一条侧闪，左右镜像复用 | 未开始 |
| `AN_Killer_KickDoor` | 神秘人 | D09 踹门 | 单脚踹击，门板运动由 Timeline | 未开始 |
| `AN_Killer_Stumble` | 神秘人 | D08 命中、D16 趔趄 | 一条失衡动作，通过播放区段得到轻/重两档 | 未开始 |
| `AN_Killer_Fall` | 神秘人 | D21 绊倒 | 向前摔倒并进入地面搏斗起始 Pose | 未开始 |
| `AN_Killer_BlockLight` | 神秘人 | D17/D21 抬手挡光 | 上半身蒙太奇，可叠加站立或单膝 Pose | 未开始 |
| `AN_Killer_DrawBackupFire` | 神秘人 | D21/D22/E3 | 风衣内慢速拔备用枪并开火 | 未开始 |
| `PAIR_D08_AmbushGrapple` | 双人 | 偷袭后的站立搏斗 | 偷袭命中 → 拉扯/翻滚 → 膝顶脱身；双方各 1 条同步轨 | 未开始 |
| `PAIR_D11_BedDrag` | 双人 | 床底抓脚踝拖出 | 只做抓住后的 2–3 秒拖拽；靠近、下蹲由走路和 IK 完成 | 未开始 |
| `PAIR_D21_GroundStruggle` | 双人 | 黑暗地面拉扯 | 一组约 8–12 秒同步动作，循环中段可重复一次，不制作 40 秒连续动画 | 未开始 |
| `CAM_E2_FirstPersonFall` | 相机 | 结局第一人称倒地 | Unity Timeline 相机曲线，不做角色全身动画 | 未开始 |

## 4. 静态 Pose 与程序化层

以下不是完整动画，不应扩张为独立移动套装：

| Pose/层 | 用途 |
|---|---|
| `POSE_Player_SitChair` | 循环重置首帧、E3 坐椅终局 |
| `POSE_Player_CrouchHide` | 沙发后、门后盲区 |
| `POSE_Player_Prone` | 床底躲藏、装死 |
| `POSE_Player_KneelHandsUp` | D06 投降 |
| `POSE_Player_HoldSmall_R` | 遥控器、书、花瓶、手电 |
| `POSE_Player_HoldBat` | 球棍持握；双手 IK 校正 |
| `POSE_Player_CarryLarge` | 木椅/抽屉柜双手搬运；下半身复用 Walk |
| `POSE_Player_AimPistol` | D22；Aim Rig 对准神秘人 |
| `POSE_Killer_LowReady` | 常规持枪行走 |
| `POSE_Killer_Kneel` | D16 恢复、D21 挡光阶段 |

## 5. 死法与结局复用表

| 流程 | 动画组合 | 不新增动画的处理 |
|---|---|---|
| D01 默认击杀 | Killer Walk + AimFire；Player HitFall | 命中后 Ragdoll |
| D02 静止被杀 | Killer Walk + AimFire；Player HitFall | 低头注视用头部 Aim IK，静止 1 秒 |
| D03 掩体露头 | Killer AimFire；Player HitFall | 枪口 Aim Rig 指向头部 |
| D03' 搜索 | Killer Walk + LowReady；Player Crouch Pose | 搜索方向由头部 IK 和路径点控制 |
| D04 被子中枪 | Killer Walk + AimFire | 玩家保持蜷缩 Pose；被子切 `Covered` 网格，渗色用 Decal |
| D05 砸墙 | Player Swing；Killer AimFire；Player HitFall | 木椅通过双手 IK 绑定 |
| D06 投降 | Player Kneel Pose；Killer Walk + AimFire | 跪地过渡可先用 0.3 秒 Pose Blend |
| D07 持棍迎战 | Player Walk/Swing；Killer AimFire；Player HitFall | 球棍脱手后转 Rigidbody |
| D08 偷袭搏斗 | Player Swing + `PAIR_D08` | 收尾枪杀包含在配对演出，之后切 Ragdoll |
| D09 顶门 | Killer KickDoor + AimFire；Player HitFall | 门和椅子用 Timeline/物理 |
| D10 投掷攻击 | Player Throw；Killer DodgeSide + AimFire | 左右闪避镜像复用 |
| D11 床底 | `PAIR_D11` + Killer AimFire；Player HitFall | 接近和下蹲用 Walk + IK |
| D12 画外击杀 | Killer AimFire | 玩家在镜头外，无需播放可见死亡动作 |
| D13 门缝射击 | Killer AimFire；Player HitFall | 神秘人身体用站位点约束，手枪用 Aim Rig 探入门缝 |
| D14 装死 | Player Prone Pose；Killer Walk + AimFire | 鞋尖确认用脚部 IK 做一次前推，不单做动画 |
| D15 砸窗 | Player Swing；Killer AimFire；Player HitFall | 木板逐件物理脱落 |
| D16 拉地毯 | Killer Stumble + AimFire；Player HitFall | 取 Stumble 前半段形成单膝点地 |
| D17 镜面晃眼 | Killer BlockLight + AimFire；Player HitFall | 光斑和硬直计时在 Unity |
| D18 单灯埋伏 | Killer Walk + AimFire；Player HitFall | 拧亮台灯用手部 IK + 灯开关 Transform |
| D19 全黑乱撞 | Killer Walk + AimFire；Player HitFall | 手电扫描由上半身 Aim Rig |
| D20 黑暗埋伏 | Killer Walk + AimFire；Player HitFall | 同 D19，只换扫描路径 |
| D21 完美计划 | Killer Fall + `PAIR_D21` + BlockLight + DrawBackupFire；Player HitFall | 掉枪、掉手电和扫动光柱由 Timeline + Rigidbody |
| D22 夺枪开火 | Player Aim Pose；Killer DodgeSide + DrawBackupFire；Player HitFall | 玩家后坐用 Animation Rig 权重脉冲 |
| E1 砸电视 | Player Swing；Killer Walk 加速播放 | 首版不做专用跑步；只有效果不够急迫时再补 Run |
| E2 第一人称死亡 | AimFire + `CAM_E2_FirstPersonFall` | 玩家身体倒地用相机曲线与手臂下落表达 |
| E3 终局 | Player Sit Pose；Killer Walk + DrawBackupFire | 钟针和黑屏由 Timeline |

## 6. 明确不做或延后

- 不做 22 条独立处决和 22 条独立死亡倒地。
- 不做面部口型、台词动画和复杂表情系统；神秘人的情绪靠停顿、朝向和步速表达。
- 不做被子实时钻入、覆盖、平铺的长动画；使用短黑场/遮挡、角色 Pose 和 4 个网格状态切换。
- 不做角色搬运每件家具的专属动画；统一 Carry Pose + 手部 IK。
- 不做第一人称全套手臂交互动画；结局 E2 只复用必要的持棍、投掷和倒地表现。
- `AN_Killer_Run`、精细蹲走、多个中弹方向和风衣烘焙布料全部列为 P2，只有试玩明确暴露问题才补。

## 7. 动画技术验收

- 所有单人动画使用同一 Humanoid 参考姿势，默认 In-Place；实际位移由角色控制器或 Timeline 提供。
- D08、D11、D21 每组配对动画都提供双方起点、朝向、距离和地面对齐标记。
- 手、脚接触点允许用 Animation Rigging 修正，禁止为不同家具尺寸复制整条动画。
- 挥击动作必须有 `HitStart`、`HitEnd` 事件；拾取/丢枪必须有 `Attach`、`Detach` 事件。
- 中弹动画在进入 Ragdoll 前不能超过 0.5 秒，确保死亡后可在同一帧重置。
- 固定机位距离下优先验收轮廓、节奏和不滑步；不为镜头不可见的手指细节增加制作量。
