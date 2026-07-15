# 《困室》Gameplay Logic Spec

> 版本：1.0 · 本文是运行时规则的唯一真相。若与总纲或流程脚本的实现措辞冲突，以本文为准；剧情含义仍以上游文档为准。

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
| `Breach` | 视门演出而定 | 否 | `Counterplay`、`EndingE1` |
| `Counterplay` | 默认否；仅声明的 QTE/抢夺窗口开放 | 否 | `KillImpact`、`EndingE1` |
| `KillImpact` | 否 | 否 | `LoopReset` |
| `LoopReset` | 否 | 否 | `Awakening` |
| `EndingE1` | 限定 | 否 | `EndingE2` |
| `EndingE2` | 是，第一人称 | 使用结局计时 | `EndingE3` |
| `EndingE3` | 只能转头 | 否 | `Finished` |
| `Finished` | 否 | 否 | 主菜单 |

`Awakening` 的第一个可读输入帧就是本轮 `t=0`。首次进入游戏播放 2 秒黑屏和电视开机声；普通循环重置不播放这段黑屏。

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
- `Interact` 的点击/长按由物品 Interaction Profile 决定，长按默认阈值 `0.35s`。
- `Stance` 点击在有效 HideSpot 中进入/退出藏姿；站立、空手、面对已入场神秘人时长按 `0.6s` 为投降；从蹲姿长按为俯卧装死。
- 任何 Move 超过死区 `0.15` 持续一帧，或 Interact/Attack/Throw/Stance/Sprint 按下，都算“有效行为”。第三人称无效的 Look 输入不算。

## 4. 神秘人入场计时

初始 `deadline=45s`，预警提前量 `0.5s`。

1. 玩家未产生有效行为时保持 45 秒。
2. 第一次有效行为发生在 `t<30s`：`deadline=30s`。
3. 第一次有效行为发生在 `t>=30s`：立刻发预警，`deadline=t+0.5s`。
4. 巨响发生时：`deadline=min(deadline, t+8s)`；巨响绝不能延后既定入场。
5. 当时间跨过 `deadline-0.5s` 时只发一次门外脚步预警。
6. 到达 deadline 后进入 `Breach`，自由行动计时停止。

巨响包括砸墙、砸窗、推倒书柜、花瓶碎裂、顶灯/台灯爆裂和重物撞击。普通脚步、拾取、开抽屉、遥控器和小书落地不是巨响。

门被椅子阻挡时 Breach 延长 3 秒；抽屉柜延长 5 秒；书柜堵门走 D13 专用门缝演出，不叠加普通延迟。

## 5. 交互判定

### 5.1 焦点

- 胸前半径 `1.2m` 内检测，先过滤不合法能力。
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
| `bed` | Intact | UnderBed HideSpot，进入后标记 D11 |
| `wood_chair` | Placed | Carry/HeavySwing/Throw/Place；门插槽阻挡 3 秒 |
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
| RoomLight | Lit/Dim/RecoverableDark/PersistentBlack | LightCoordinator |
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
- Continuous 只在 Entering/Perceiving/Navigating/Search 中重评；PerformanceLocked 不重评。
- 同一刺激只启动一条反制。
- D01 必须存在且无条件，目录缺失时构建失败。

## 8. 搏斗与分支

- 对抗窗口默认 `1.0s`，统计 Interact 的新按下次数；按键长按不重复计数。
- 每个窗口的阈值由 PerformanceDefinition 配置；无障碍设置可以降低阈值，但不能改变剧情结局。
- D08 三个窗口任一失败：继续 D08 贴身枪杀。
- D08 三个窗口全胜：主枪掉落，开放短暂抢夺窗口；玩家拾枪进入 D22，未拾到仍以 D08 结束。
- D21 入场即让主枪与手电分离：
  - 先拾主枪：D22。
  - 先拾手电并直照神秘人：完成 D21，设置 `d21Seen=true`，备用枪反杀。
  - 先拾手电并照镜子：反射致盲 1 秒，进入 D17 后被杀；不设置 d21Seen。
- 枪在 D22 只有 1 发，射击由演出保证闪避；随后只播放空仓声，备用枪收尾。

## 9. 光照与黑暗经验

| 状态 | 条件 | 神秘人行为 | 可累计经验 |
|---|---|---|:--:|
| Lit | 顶灯正常 | 常规 | 否 |
| Dim | 顶灯或台灯损坏，但仍有稳定光源 | 常规/D18 变体 | 否 |
| RecoverableDark | 顶灯损坏，台灯完整但关闭 | 开启/调亮台灯，D18 | 否 |
| PersistentBlack | 顶灯和台灯都损坏 | 开枪挂手电，D19–D21 | 是 |

- PersistentBlack 连续每满 `5s` 增加 1 XP，上限 5。
- 非 PersistentBlack 或死亡时未满 5 秒的部分归零。
- 默认环境强度曲线为 `[0.002, 0.0045, 0.0075, 0.011, 0.015, 0.02]`；最终由截图直方图和试玩校准。
- 神秘人光锥连续覆盖玩家 `0.8s` 即提交击杀；离开光锥后覆盖计时归零。

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

- 玩家获得第一人称 Move/Look/Interact/Attack/Throw 控制 `4s`，随后神秘人入场。
- 只实现 D01、D07、D10 的第一人称变体；其他状态落入 D01 变体。
- 玩家必死，但不执行普通循环重置。
- 视角在约 `1.2s` 内倒向地面，随后黑屏 `3s`，进入 E3。

### 10.4 E3

- 应用 `EndingPass2Preset`：玩家坐在中央木椅，电视保持损坏，沙发空置。
- 玩家不能起身，只能有限转头。
- 神秘人进门、站定、瞄准；挂钟秒针首次越过原格。
- 枪响、黑屏 5 秒、显示游戏名，设置 `endingCompleted=true` 并保存。
- 返回主菜单后 Continue 禁用；没有 New Game+。New Game 必须明确删除旧存档。

## 11. 循环重置与持久状态

死亡命中后必须在同一帧开始重置：

1. 锁输入，阶段进入 LoopReset。
2. 取消 Timeline、导航、协程、延时回调和本轮刺激。
3. 停止物理，回收碎片/VFX/Decal。
4. 恢复物品父级、位置、旋转、缩放、完整度、灯光和门状态。
5. 恢复玩家苏醒点、神秘人 Dormant、固定相机和事实基线。
6. 增加 `loopIndex`，清除本轮行为与黑暗部分计时，原子存档。
7. 播放极短电流声，进入 Awakening，并在下一次 Update 接受输入。

跨循环保留项只有：loopIndex、0–5 黑暗 XP、d21Seen、一次性电视裂纹回声、结局完成标记。D21 完成后电视噪声增强只由 d21Seen 派生，不另存表现参数。

## 12. 失败保护

- 任何反制缺演出资产时，开发构建记录错误并使用 D01 占位演出；Release 验证直接失败。
- NavMesh 路径无效时，神秘人使用已烘焙的最近演出锚点；不得原地发呆。
- 配对锚点失配超过 `0.15m` 或 `10°` 时禁止启动演出并回退 D01。
- 存档损坏回退备份；不能因此阻塞新游戏。
- 重置对象抛异常时继续重置其余对象，并在开发构建汇总错误；Release 测试必须做到零异常。

## 13. 验收基线

- 入场计时在一帧误差内满足 45/30/噪声 8 秒规则。
- 反制表每一行都有自动规则测试；未覆盖状态总是 D01。
- 连续死亡 50 次无状态残留、无持续内存增长，恢复可输入不超过 0.5 秒。
- 常规阶段固定相机 Transform 零变化，结局切换无 Blend。
- 三名全新测试者中至少两名能在 2 小时内无提示发现电视结局。
- 正式玩法场景无 HUD、交互图标、任务文字和高亮材质。
