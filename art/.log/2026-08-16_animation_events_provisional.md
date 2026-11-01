# 2026-08-16 动画语义标记(provisional Animation Events)

> 按 `art/requirements/remaining_art_deliverables_2026-08-16.md` §2 给现有 FBX 的
> ModelImporter `clipAnimations[].events` 注入语义事件。**全部为 provisional:
> 时间取剪辑内合理归一化位置 × 剪辑时长,待动画/演出验收校准。**
> 注入脚本:`art/pipeline/add_animation_events.py`(幂等,可重跑)。

## 命名约定(供程序对齐)

已 Grep `Assets/_Game` 全部代码:目前**没有任何 Animation Event 消费端**
(无 `OnAnimationEvent`/`HitStart`/`Attach` 等字符串),事件名以
`animation_manifest.md` §7 与 `ART_ASSET_GAP_MANIFEST.md` 的语义约定为准。
程序接入时请对齐以下函数名(Animator 所在 GameObject 需有对应方法):

`HitStart`, `HitEnd`, `Attach`, `Detach`, `AimHold`, `Fire`, `Muzzle`,
`KickContact`, `StumbleLightEnd`, `StumbleHeavyEnd`, `RecoverEnd`,
`BlockStart`, `HardStunEnd`, `FallContact`, `PairStart`, `PairContact`,
`PairRelease`, `PairLoopStart`, `PairLoopEnd`, `DrawStart`

## 标记明细

| 剪辑(时长) | 事件 | time(秒) | 归一化 | 理由 |
|---|---|---|---|---|
| `AN_Player_Swing`(3.0s) | `HitStart` | 1.35 | 0.45 | 挥击中段,挥击进入命中窗口(接触类取 0.45–0.55 起点) |
| | `HitEnd` | 1.80 | 0.60 | 命中窗口结束,之后为随挥收势 |
| `AN_Player_Pickup`(2.5s) | `Attach` | 1.25 | 0.50 | 弯腰中点,手接触物体帧 |
| | `Detach` | 1.25 | 0.50 | 与 Attach 同一物理帧:剪辑反放作放下时同一帧为离手 |
| `AN_Player_Throw`(2.5s) | `Detach` | 1.25 | 0.50 | 过肩投掷出手点(挥臂中点) |
| `AN_Player_PutDownLarge`(2.5s) | `Detach` | 1.75 | 0.70 | 物件放稳落地后松手,晚于 Pickup 的接触帧,保证"不呈投掷感" |
| `AN_Killer_AimFire`(4.0s) | `AimHold` | 1.00 | 0.25 | 抬枪动作约前 1/4 完成,进入瞄准保持 |
| | `Muzzle` | 2.00 | 0.50 | 枪口表现帧,与 Fire 同帧驱动枪口 VFX |
| | `Fire` | 2.00 | 0.50 | 描述"raise→aim→fire→keep aiming"中开火居中 |
| `AN_Killer_KickDoor`(2.5s) | `KickContact` | 1.25 | 0.50 | 抬膝→前蹬的中点为脚触门帧 |
| `AN_Killer_Stumble`(3.0s) | `StumbleLightEnd` | 1.20 | 0.40 | 轻档区段结束:此前取为 D16 轻度趔趄 |
| | `StumbleHeavyEnd` | 2.10 | 0.70 | 重档区段结束:含"drop toward one knee"最低点 |
| | `RecoverEnd` | 2.70 | 0.90 | "heavily regains footing"完成,结束类取 0.9 |
| `AN_Killer_BlockLight`(3.0s) | `BlockStart` | 0.45 | 0.15 | 抬前臂挡脸动作快,0.15 处到位 |
| | `HardStunEnd` | 2.70 | 0.90 | 硬直结束,结束类取 0.9 |
| `AN_Killer_DrawBackupFire`(4.0s) | `Attach` | 1.40 | 0.35 | 探入风衣拔枪后备用枪入手帧 |
| | `Muzzle` | 2.50 | 0.625 | 伸臂瞄准后开火枪口表现帧 |
| | `Fire` | 2.50 | 0.625 | 拔枪(0–0.35)→伸臂(0.35–0.625)→开火 |
| `AN_Killer_Fall`(3.0s) | `FallContact` | 1.50 | 0.50 | 前扑落地接触帧 |
| | `PairStart` | 2.70 | 0.90 | 落地后进入地面搏斗起始 Pose(D21 PAIR 接续) |
| `PAIR_D08_PlayerSide`/`KillerSide`(6.0s) | `PairStart` | 0.00 | 0.00 | 双方起点(朝向/距离由演出锚点保证) |
| | `PairContact` | 0.60 | 0.10 | 背后熊抱抓住帧,偷袭命中后紧随 |
| | `PairRelease` | 5.10 | 0.85 | 膝顶后脱离帧,收尾枪杀由演出接续 |
| `PAIR_D11_PlayerSide`/`KillerSide`(3.0s) | `PairStart` | 0.00 | 0.00 | 双方起点(床边/床底锚点由 Timeline 对位) |
| | `PairContact` | 0.45 | 0.15 | 抓脚帧:剪辑"抓住后拖拽"起步即抓,留 0.15 起手 |
| | `PairRelease` | 2.70 | 0.90 | 拖拽结束释放帧 |
| `PAIR_D21_PlayerSide`/`KillerSide`(8.0s) | `PairStart` | 0.00 | 0.00 | `AN_Killer_Fall` 的 `PairStart` 接续点 |
| | `PairContact` | 0.80 | 0.10 | 地面纠缠接触开始 |
| | `PairLoopStart` | 2.00 | 0.25 | 中段可重复循环段开始(manifest §3:循环中段可重复一次) |
| | `PairLoopEnd` | 4.80 | 0.60 | 中段可重复循环段结束 |
| | `Detach` | 5.60 | 0.70 | 枪/手电脱手帧(掉枪掉灯由 Timeline + Rigidbody 表现) |
| | `BlockStart` | 6.40 | 0.80 | `AN_Killer_BlockLight` 接续点 |
| | `DrawStart` | 7.20 | 0.90 | `AN_Killer_DrawBackupFire` 接续点 |

## 备注

- `AN_Player_Pickup` 的手部 IK 对位属运行时 Animation Rigging 工作,不在事件标记范围。
- 新剪辑 `AN_Killer_AimEnter`/`AimHold`/`Fire`/`SearchLoop`/`Run` 暂未加事件
  (Enter/Hold 由状态机切换驱动,Fire 单发剪辑的开火帧待验收后与程序约定再补)。
- 校准流程:验收时按轮廓/接触帧调整 `time` 值后,可直接改 meta 或更新
  `add_animation_events.py` 重跑,同步更新本日志。
