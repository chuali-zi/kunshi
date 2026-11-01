# 《困室》Gameplay Logic Spec

> 版本：1.0 · 本文是运行时规则的唯一真相。若与总纲或流程脚本的实现措辞冲突，以本文为准；剧情含义仍以上游文档为准。

> 参照边界：本规范只把 MV 的观看视点、循环氛围和身份转换作为改编方向；输入、阈值、状态机、距离和时间均为本作游戏设计值，不是 MV 逐镜证据。

## 1. 规范用语与目标

- **必须**：发布实现不可偏离。
- **应该**：默认实现；只有记录过的试玩结论才能调整。
- **可以**：不影响规则正确性的表现选择。
- 时间使用游戏时间；Pause、菜单与系统级演出暂停时不推进。
- 所有阈值放在配置资产中，本文给出首个默认值与边界。

游戏的顶层成功条件只有一个：电视承受两次有效重击并完成 E1–E3。神秘人不可被常规玩法杀死，所有非结局搏斗最终必须失败。

## 2. 顶层阶段

| 阶段 | 可输入 | 计时 | 允许转换 |
|---|:--:|:--:|---|
| `Awakening` | 是，可打断动画 | 是 | `FreeRoam`、`Breach` |
| `FreeRoam` | 是 | 是 | `Breach`、`EndingE1` |
| `Breach` | 门演出期间否；完成后是 | 门演出否；live hunt 是 | `Counterplay`、`EndingE1` |
| `Counterplay` | 默认否；仅声明的 QTE/抢夺窗口开放 | 否 | `KillImpact`、`EndingE1` |
| `KillImpact` | 否 | 否 | `LoopReset` |
| `LoopReset` | 否 | 否 | `Awakening` |
| `EndingE1` | 限定 | 否 | `EndingE2` |
| `EndingE2` | 是，第一人称 | 使用结局计时 | `EndingE3` |
| `EndingE3` | 只能转头 | 否 | `Finished` |
| `Finished` | 否 | 否 | 主菜单 |

`Awakening` 的第一个可读输入帧就是本轮 `t=0`。首次进入游戏播放 2 秒黑屏和电视开机声；普通循环重置不播放这段黑屏。玩家在北墙木椅上以坐姿开始；约 4 秒后苏醒阶段可自动结束并开放自由行动，但无有效操作时仍保持坐姿。第一次有效操作先让玩家起身，再执行该操作，不得吞掉首次交互。

`Breach` 不是整段锁定演出：门演出只做短暂输入锁；门演出完成后阶段仍为 `Breach`，玩家恢复 Move/Interact/Attack/Throw/Stance/Sprint，神秘人开始 live hunt。只有取得清晰射击解并提交攻击，或进入规则声明的配对分支时才转入 `Counterplay`。

苏醒中第一次有效操作既会接管玩家，也会让玩家从木椅起身。若两阶段的玩家控制权限相同，这次切换不得吞掉正在进行的短按、长按或物件交接；真正进入锁定阶段或循环重置才按取消契约清理。苏醒计时自动结束本身不令玩家起身。

## 3. 输入与玩家控制

| 动作 | 键鼠 | 手柄 | 第三人称 | 第一人称 |
|---|---|---|:--:|:--:|
| Move | WASD | 左摇杆 | 是 | 是 |
| Sprint | Left Shift | L3 | 是 | 是 |
| Look | 鼠标 | 右摇杆 | 否 | 是 |
| Interact | E | 南键 | 是 | 是 |
| Attack | 左键 | RT | 是 | 是 |
| Throw | 右键 | LT | 是 | 是 |
| Stance | C | 东键 | 是 | 否 |
| Pause | Esc | Start | 是 | 是 |

- 初始步速 `2.8m/s`，冲刺 `4.5m/s`，无体力条；角色朝移动方向以最高 `720°/s` 转向。
- 搬椅子速度乘 `0.5`，搬抽屉柜乘 `0.33`；单手小物不减速。
- 固定机位移动使用相机投影到地面的前/右方向，不使用坦克式控制。
- 投掷方向为角色朝向，初速默认 `8m/s`；第一人称改用相机中心方向。
- `Interact` 长按默认阈值为 `0.35s`。空手面对同时支持 Use 与拾取的物品时，短按使用、长按拾取；仅支持拾取的小物仍可短按拾取。手持物短按 E 优先放入当前有效插槽（Place），没有有效插槽时执行手持物的 Use；长按 `0.35s` 放到地面。已铺好的被子在拉扯条件满足时短按拉角、长按重新拖拽；未满足拉扯条件时长按拖拽。木椅短按攀爬、长按搬运；书柜长按 `2s` 后进入 `0.35s` 受控倾倒。只有 Throw 输入会施加投掷速度。
- `Stance` 点击在有效 HideSpot 中进入/退出藏姿；再次按 C 或发生移动会退出藏匿。站立、空手、面对已入场神秘人时长按 `0.6s` 为投降；从蹲姿长按为俯卧装死。
- 任何 Move 超过死区 `0.15` 持续一帧，或 Interact/Attack/Throw/Stance/Sprint 按下，都算“有效行为”。第三人称无效的 Look 输入不算。

## 4. 神秘人入场计时

初始 `deadline=45s`，预警提前量 `0.5s`。

1. 玩家未产生有效行为时保持 45 秒。
2. 第一次有效行为发生在 `t<30s`：`deadline=30s`。
3. 第一次有效行为发生在 `t>=30s`：立刻发预警，`deadline=t+0.5s`。
4. 巨响发生时：`deadline=min(deadline, t+8s)`；巨响绝不能延后既定入场。
5. 当时间跨过 `deadline-0.5s` 时只发一次门外脚步预警。
6. 到达 deadline 后进入 `Breach`，入场 deadline 计时停止；门演出完成后玩家恢复自由行动，live hunt 计时正常推进。

巨响包括砸墙、砸窗、推倒书柜、花瓶碎裂、顶灯/台灯爆裂和重物撞击。普通脚步、拾取、开抽屉、遥控器和小书落地不是巨响。

门被椅子阻挡时 Breach 延长 3 秒；抽屉柜延长 5 秒；书柜堵门走 D13 专用门缝演出，不叠加普通延迟。

### 4.1 入场后的 live hunt

门演出完成后，神秘人按以下战术状态运行，顶层阶段继续保持 `Breach`：

```text
Observing → Investigating → Searching → Approaching → Aiming → Committing → Firing
                                  ↑          ↓          ↓
                                  └── 失去视线/受击 ─ Recovering
```

- 视觉默认使用 120° 视锥、胸/头双射线物理遮挡；Lit/非持久黑暗最大距离 `12m`，PersistentBlack 为 `1.5m`。`PlayerPose=Hidden` 不被普通远距视觉捕获，但 `Searching` 到达作者预挂的藏点锚点且水平距离不超过 `1.35m` 时会揭露玩家。
- 连续看见玩家 `0.3s` 后才完成检测；距离大于 `4.25m` 时走向有射线条件的预挂战术锚点，进入射程后连续瞄准 `0.7s` 才得到 clear firing solution。瞄准时失去视线或离开射程必须取消提交。
- 失去玩家后先调查最后已知位置，再在预挂锚点间搜索 `8s`；不得把玩家实时位置直接作为无约束追逐路径。调查到点默认停留/扫描 `1.2s`。
- 普通/巨响听觉半径默认 `12m/30m`。玩家移动时每 `0.42s` 发普通脚步；冲刺发 Loud，间隔为该值的 `0.72` 倍。巨响沿用第 4 节定义。`Aiming/Committing` 中的普通脚步不得使神秘人降级为 Investigating 或更换目标。
- 巡逻、追击和阻止电视的冲刺速度默认 `1.25/2.25/5.5m/s`。同一 `4s` 命中记忆窗口内，前两次非致命近战命中分别进入 `Recovering` `0.55s/0.275s`；后续命中不再打断，并使神秘人继续追查玩家。窗口到期后反应次数清零。
- 上述阈值必须来自配置资产。神秘人最终仍会杀死玩家，但不得在入场时锁定玩家或直接播放处决。

clear firing solution 产生后，神秘人进入 `Committing`，必须把当帧 outcome 原子复制为 committed outcome；此后 Continuous、Reactive 和事实变化都不得更换该次攻击结果。随后请求 `Counterplay`，请求获准后发出 `ShotFired`；默认 `0.18s` 后发出 `ShotResolved` 并提交 Death。`AimStarted` 与 `ShotFired` 分别驱动神秘人的 `AimFire` 和玩家的 `HitFall`；`AimCancelled` 必须清除 `AimFire` trigger，并使已经进入 AimFire 状态的神秘人回到 Idle。这些是表现事件，不自行修改顶层阶段。

## 5. 交互判定

### 5.1 焦点

- 胸前半径 `1.2m` 内检测，先过滤不合法能力。
- 已布置陷阱的主动拉角单独按 `1.6m` 水平距离检测，并同时验证神秘人接触、玩家朝向和陷阱类型；胸前扫描球不能把贴地拉角的有效距离缩短。该例外不扩大拾取、布置陷阱或普通家具的交互范围。
- 目标分数依次考虑能力优先级、朝向点积、距离；同分时选择上次焦点，避免抖动。
- 焦点通过头部 IK 表达，不显示 UI。
- 进入焦点 `0.25s` 后可播放非锁定 Inspect 注视；不会阻止移动。

### 5.2 物品状态

| 状态 | 刚体 | 父级 | 碰撞 |
|---|---|---|---|
| Placed | 初始配置 | 场景/当前放置点 | 正常 |
| Held | Kinematic | 手部 socket | 忽略玩家 |
| Flying | Dynamic | 无 | 正常；首个 FixedUpdate 忽略玩家 |
| Scripted | 由 Timeline 决定 | 语义绑定点 | 禁止普通交互 |
| Broken | 按定义 | 原地/碎片池 | 只保留必要碰撞 |

玩家同一时间只能持有一个小物或搬运一个大物。进入配对演出前，导演明确决定手持物是锁到演出、掉落还是回收，禁止隐式消失。

### 5.3 23 件物品的运行状态

| ID | 初始状态 | 核心能力/状态机 |
|---|---|---|
| `sofa_awaken` | Intact | HideSpot；AwakeningAnchor；站姿在后区触发 D03 |
| `blanket` | Crumpled | Crumpled/Covered/Dragged/FlatTrap/Draped；门口+全黑+拉拽为 D21 |
| `bed` | Intact | UnderBed HideSpot；可低位攀爬到床面以够到顶灯，进入床底后标记 D11 |
| `wood_chair` | Placed | 短按攀爬、长按 Carry；HeavySwing/Throw/Place；门插槽阻挡 3 秒 |
| `bookcase` | Upright | 长按 2 秒推倒；Fallen 可作掩体；堵门为 D13 |
| `baseball_bat` | Placed | Pickup/Throw/Melee；HeavyImpact |
| `room_door` | Closed | Open/Closed/Blocked/Breached；门后盲区事实区域 |
| `ceiling_light` | IntactOn | Intact/Broken；破坏即巨响 |
| `table_lamp` | IntactOn | Off/Normal/Bright/Broken/HeldUnplugged；神秘人可设 Bright |
| `walls` | Intact | 每面 0–3 凹坑；永不穿透；每次重击为巨响 |
| `crt_tv` | Intact | Intact/Cracked/Destroyed；见第 10 节 |
| `remote` | Placed | Pickup/Use/Throw；使用触发 3 帧画面闪帧 |
| `pistol_main` | KillerHeld | ScriptedDrop/PlayerHeld/Empty；仅 1 发 |
| `pistol_backup` | Hidden | 只允许演出绑定，不进入普通交互 |
| `flashlight` | GunMounted | Mounted/Loose/PlayerHeld；光锥照射反应 |
| `killer_coat` | Worn | 只提供 Inspect/演出语义绑定 |
| `dresser` | Placed | 三个独立抽屉；Carry；门插槽阻挡 5 秒 |
| `books` | Shelved | Pickup/Inspect/Throw；轻物命中电视只脉冲裂纹 |
| `vase` | Intact | Pickup/Throw/Shatter；碎片区使玩家跛行 2 秒 |
| `boarded_window` | 5Boards | 木板逐条破坏，最后露砖墙；每击巨响 |
| `rug` | Flat | Pull；神秘人踏上时只 Stumble，不 Fall |
| `wall_clock` | Mounted | Pickup/Throw/Break；秒针固定抽搐 |
| `mirror` | Intact | Inspect/Break/FlashlightReactive；D17 反射目标 |

完整外观与演出要求继续引用 [items_interactions.md](items_interactions.md)，本文只裁定状态和规则。

## 6. 事实模型

持续事实只由各自权威系统写入：

| FactKey | 值域 | 写入者 |
|---|---|---|
| PlayerZone | Open/SofaRear/DoorBlind/CameraBlind/UnderBed/Bed/Other | 区域系统 |
| PlayerPose | Sitting/Standing/Crouched/Hidden/Prone/Kneeling/Carrying/Scripted | PlayerInteractor/演出 |
| HeldItemCategories | Flags | PlayerInteractor |
| RoomLight | Lit/Dim/RecoverableDark/PersistentBlack | LightCoordinator；`RecoverableDark` 只表示台灯完整、接线且可被神秘人近距离打开 |
| DoorBlock | None/Furniture/Bookcase | DoorController |
| Trap | None/RugArmed/BlanketArmed | TripTrap |
| TrapPulled | bool | TripTrap |
| HasStolenPistol | bool | 抢夺/拾取系统 |
| IsAmbushing | bool | 玩家姿态与区域组合 |
| IsMoving | bool | PlayerMotor |
| IsGlareActive | bool | Flashlight/Mirror |
| IsStrikingWall/Window | bool | Melee 命中窗口 |
| HasMeaningfulInput | bool | LoopDirector |
| TelevisionDamage | Intact/Cracked/Destroyed | TelevisionController |

瞬时 `KillerStimulus` 包括 `Entry`、`IncomingProjectile`、`TrapPull`、`Glare`、`TelevisionHeavyHit`、`PlayerExposed`。刺激消费一次后清除。

## 7. 反制目录与仲裁

排序键越小优先级越高。下表完整覆盖 D01–D22 和 D03′；E1 由 EndingDirector 在反制目录之前仲裁。

| 排序 | ID | 触发模式 | 条件摘要 |
|---:|---|---|---|
| 200 | D21 | Reactive | BlanketArmed + TrapPull + PersistentBlack |
| 300 | D22 | Reactive/Continuous | HasStolenPistol |
| 400 | D08 | Reactive | DoorBlind + Weapon + Swing |
| 500 | D20 | Entry/Continuous | PersistentBlack + Ambushing |
| 600 | D19 | Entry/Continuous | PersistentBlack；D20 未匹配 |
| 700 | D18 | Entry | RecoverableDark |
| 800 | D16 | Reactive | RugArmed + TrapPull |
| 900 | D17 | Reactive | GlareActive |
| 1000 | D09 | Entry | DoorBlock=Furniture |
| 1100 | D13 | Entry | DoorBlock=Bookcase |
| 1200 | D11 | Entry | PlayerZone=UnderBed |
| 1300 | D04 | Entry | PlayerPose=Hidden + PlayerZone=Bed |
| 1400 | D03P | Entry/Continuous | SofaRear + Crouched/Hidden |
| 1500 | D03 | Entry | SofaRear + Standing |
| 1600 | D12 | Entry | CameraBlind |
| 1700 | D14 | Entry | Prone |
| 1800 | D10 | Reactive | IncomingProjectile |
| 1900 | D06 | Continuous | Kneeling/Surrender |
| 2000 | D07 | Entry/Continuous | 持 Weapon 正面暴露；D08 未匹配 |
| 2100 | D05 | Entry/Continuous | IsStrikingWall |
| 2101 | D15 | Entry/Continuous | IsStrikingWindow |
| 2200 | D02 | Entry | HasMeaningfulInput=false 且仍在苏醒区 |
| 2300 | D01 | Fallback | 无条件 |

仲裁规则：

- Entry 在门进入可感知位置时捕获一次快照。
- Reactive 刺激到达时重新捕获快照。
- Continuous 在 Entering 及 live hunt 的 Observing/Investigating/Searching/Approaching/Aiming 中重评；进入 Committing/Firing 或 PerformanceLocked 后不重评。
- 当前连续意图的条件不再成立时必须立即重新选择，不能保留陈旧结果；例如玩家离开 UnderBed 后不得继续保留 D11。没有其他匹配时回退 D01。
- Reactive 赢得仲裁后锁存 `2.5s`，期间 Continuous 不得覆盖；新的更高优先级 Reactive 可以替换。进入 Committing 后按第 4.1 节原子冻结结果。
- 同一刺激只启动一条反制。
- D01 必须存在且无条件，目录缺失时构建失败。

## 8. 搏斗与分支

- D08/D11/D21 等声明的配对分支仍使用 `PerformanceLocked`、配对锚点和各自窗口；普通 live D01 不把玩家吸附到处决锚点。进入配对演出后保持既有“不再重选反制”规则。
- 对抗窗口默认 `1.0s`，统计 Interact 的新按下次数；按键长按不重复计数。
- 每个窗口的阈值由 PerformanceDefinition 配置；无障碍设置可以降低阈值，但不能改变剧情结局。
- D08 三个窗口任一失败：继续 D08 贴身枪杀。
- D08 三个窗口全胜：主枪掉落，开放短暂抢夺窗口；玩家拾枪进入 D22，未拾到仍以 D08 结束。
- D21 触发倒地后先保持约 `1.2s` 的倒地阶段，随后主枪与手电掉落，玩家可以主动选择拾取；不强制自动拾取。第一次实际拾起手电启动黑暗拉扯 QTE，连续三个 `1s` 对抗窗口全部完成后手电脱手落地，玩家恢复自由操作。
  - 拾取主枪：直接进入 D22，等待玩家主动开火。
  - QTE 后再次手动拾起手电并照镜：进入 D17。
  - QTE 后再次手动拾起手电并照向神秘人：他挡光约 `3s`，随后进入备用枪终结演出；结算后设置 `d21Seen=true`。
- D22 枪内只有 1 发。第一次开火时神秘人闪避；再次扣动扳机只播放空仓声。自进入 D22 起，玩家有 `12s` 开火窗口，超时也触发备用枪终结。备用枪演出持续 `2s`：前 `0.7s` 展露备用枪，随后 `1.3s` 开火；开火后再保留 `2s` 结算并进入死亡重置。等待窗口期间不得提前让玩家倒地或播放处决。以上是本作的可玩分支规则，不是 MV 时间码。

## 9. 光照与黑暗经验

| 状态 | 条件 | 神秘人行为 | 可累计经验 |
|---|---|---|:--:|
| Lit | 顶灯正常 | 常规 | 否 |
| Dim | 顶灯或台灯损坏，但仍有稳定光源 | 常规/D18 变体 | 否 |
| RecoverableDark | 顶灯损坏，台灯完整、接线、未遮盖且关闭 | 神秘人接近至 `1.8m` 内且无遮挡后开启台灯，D18 | 否 |
| PersistentBlack | 顶灯和台灯都损坏 | 开枪挂手电，D19–D21 | 是 |

- PersistentBlack 连续每满 `5s` 增加 1 XP，上限 5。
- 非 PersistentBlack 或死亡时未满 5 秒的部分归零。
- 默认环境强度曲线为 `[0.002, 0.0045, 0.0075, 0.011, 0.015, 0.02]`；最终由截图直方图和试玩校准。
- 神秘人光锥连续覆盖玩家 `0.8s` 后，只向战术感知器提供短暂可见脉冲；仍必须经过检测、接近、瞄准和攻击提交，不得直接提交击杀。离开光锥后覆盖累计逐步衰减。

## 10. 电视与结局

### 10.1 有效命中

- `LightImpact`（书、遥控器等）只播放一次裂纹脉冲，3 帧内自愈，不改变状态。
- `HeavyImpact`（棒球棍挥击、木椅/重物挥砸）改变电视状态。
- 第一次 HeavyImpact：`Intact → Cracked`，显示全屏裂纹并播放电流啸叫。
- Cracked 在本轮持续到第二击或死亡，没有任意秒数超时。
- 神秘人在场时第一击发出最高优先级 `TelevisionHeavyHit`，让其全场唯一一次跑动阻止玩家。
- 第一击后被杀：设置 `tvCrackEchoPending=true`；下一轮 Awakening 显示一次短暂裂纹后消费该标记。
- 第二次 HeavyImpact：立即提交最高优先级 `EndingCommitted`。若同帧有 Death，请求结局获胜。

### 10.2 E1

1. 禁止普通反制和循环重置。
2. 电视切 Destroyed，全屏炸裂，白噪声后黑屏 `1.5s`。
3. 应用 `EndingPass1Preset`：家具恢复初始位置、临时碎片清除、电视仍损坏。
4. 相机硬切到第一人称，进入 E2。

### 10.3 E2

- 电视破坏后由屏幕外的观看者接管第一人称；玩家先从屏幕前看到独立可见的原主角离开固定观看位置，随后获得第一人称 Move/Look/Interact/Attack/Throw 控制 `4s`，可以自由环顾房间，之后神秘人入场。
- 只实现 D01、D07、D10 的第一人称变体；其他状态落入 D01 变体。
- 玩家必死，但不执行普通循环重置。
- 视角在约 `1.2s` 内倒向地面，随后黑屏 `3s`，进入 E3。

### 10.4 E3

- 生产入口使用 `Bootstrap.EndingDirector` 与 `CFG_EndingSequence`。应用 `EndingPass2Preset`：玩家坐在场景中的 `FUR_WoodChair` 木椅上；当前生产接线取该木椅的现场位置，默认仍为北墙位置，不再同时规定为房间中央。电视保持损坏，沙发空置。`EndingSequenceDirector` 仅保留为非生产参考实现。
- 玩家不能起身，只能有限转头。
- 神秘人进门、站定、瞄准；挂钟秒针首次越过原格。
- 枪响、黑屏 5 秒、显示游戏名，设置 `endingCompleted=true` 并保存。
- 返回主菜单后 Continue 禁用；没有 New Game+。New Game 必须明确删除旧存档。

## 11. 循环重置与持久状态

live 枪击在 `ShotFired` 时立即播放枪击/倒地表现，默认 `0.18s` 后结算命中。命中同帧进入 `KillImpact` 并锁输入，默认保留 `0.8s`；保留结束后开始重置：

1. 阶段进入 LoopReset，并保持输入锁定。
2. 取消 Timeline、导航、协程、延时回调和本轮刺激。
3. 停止物理，回收碎片/VFX/Decal。
4. 恢复物品父级、位置、旋转、缩放、完整度、灯光和门状态。
5. 恢复玩家苏醒点、神秘人 Dormant、固定相机和事实基线；下一次 Awakening 将玩家放回木椅坐姿。坐姿持续到第一次有效操作，此时玩家起身并执行该操作。
6. 增加 `loopIndex`，清除本轮行为与黑暗部分计时，原子存档。
7. 播放极短电流声，进入 Awakening，并在下一次 Update 接受输入。

跨循环保留项只有：loopIndex、0–5 黑暗 XP、d21Seen、一次性电视裂纹回声、结局完成标记。D21 完成后电视噪声增强只由 d21Seen 派生，不另存表现参数。

`KillerTacticalController` 与 `PlayerNoiseEmitter` 必须参与 `ILoopResettable`：Quiesce 清除导航、噪声记忆、最后已知位置、瞄准/射击进度和本轮回调。瞄准、枪击、延时命中及重置回调执行前必须校验 `loopEpoch`，旧循环事件不得影响新循环。

## 12. 失败保护

- 任何反制缺演出资产时，开发构建记录错误并使用 D01 占位演出；Release 验证直接失败。
- NavMesh 路径无效时，神秘人使用已烘焙的最近演出锚点；不得原地发呆。
- 配对锚点失配超过 `0.15m` 或 `10°` 时禁止启动演出并回退 D01。
- 存档损坏回退备份；不能因此阻塞新游戏。
- 重置对象抛异常时继续重置其余对象，并在开发构建汇总错误；Release 测试必须做到零异常。

## 13. 验收基线

- 入场计时在一帧误差内满足 45/30/噪声 8 秒规则。
- 反制表每一行都有自动规则测试；未覆盖状态总是 D01。
- 连续死亡 50 次无状态残留、无持续内存增长；`KillImpact` 的 `0.8s` 可读保留结束后，恢复可输入不超过 0.5 秒。
- 常规阶段固定相机 Transform 零变化，结局切换无 Blend。
- 三名全新测试者中至少两名能在 2 小时内无提示发现电视结局。
- 正式玩法场景无 HUD、交互图标、任务文字和高亮材质。
- live hunt 只代表技术基座与 D01 垂直切片完成，不据此标记环境 Visual State Presenter、D08/D11/D21 配对组或 D01–D22 全路线完成；各组仍须独立通过规则、场景触发、重置和锚点/表现验收。

## 14. 当前非美术实现缺口

2026-08-16 的差距清单保留为历史基线：[non_art_runtime_gaps_2026-08-16.md](non_art_runtime_gaps_2026-08-16.md)，不能作为当前仍缺失的证据。后续修复与验证分别记录于 [2026-09-12 修复记录](mv_repair_report_2026-09-12.md) 和 [2026-09-22 优化记录](mv_optimization_2026-09-22.md)。当前进度须以生产场景接线、源码和最新验证共同核对；组件存在或历史测试通过，不代表 D01–D22 已全部自然通关，也不代表与 MV 的逐段动作已完全对齐。
