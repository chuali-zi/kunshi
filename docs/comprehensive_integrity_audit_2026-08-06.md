# 《困室》完整性、互动与联动静态审计

> 审计日期：2026-08-06  
> 基准：`docs/gameplay_logic_spec.md` 为运行时规则唯一真相，`docs/game_flow_script.md` 补充叙事演出。  
> 范围：场景与内容资产、D01–D22/D03′、E1–E3、23 件物品、输入、AI、循环重置、表现与美术接入。  
> 结论限定：本报告是代码/资产/场景序列化静态审计加自动化测试，不替代逐路线人工试玩和镜头验收。

## 1. 总结

当前版本属于“机制骨架可运行、内容尚未完成”，不能按 Release-ready 交付。

- **基础完整性：通过。** Unity 6000.4.6f1 可编译；项目验证器 0 error；EditMode 37/37、修复后 PlayMode 62/62（最终复验见本报告第 8 节）。Build Settings、URP、输入配置、23 个物品定义和 23 条反制定义均存在。
- **规则覆盖：基本齐。** D01–D22 加 D03′ 的选择规则、优先级与 E1–E3 顶层流程均有代码；D08、D21、D22 还具有部分 QTE/掉落/抢夺状态逻辑。
- **内容完整性：未通过。** 23 个死亡表现全部仍是 `PERF_*_Greybox.asset`；没有 `.playable`、`.timeline` 或 `.anim` 表现资产。灰盒 Playable 创建的是空 `Playable`，实际只插值神秘人位置和玩家旋转。
- **互动完整性：未通过。** 23 件物品中，拾取/搬运/投掷/基础命中等通用动词可用，但门、抽屉柜、书、窗板、墙损、花瓶、镜子、挂钟、被子多状态等专属状态机没有完成或没有接视觉。
- **验收口径存在假绿。** 全部种子和资产被标记为 `Implemented`，验证器却没有检查 Timeline 绑定、演出锚点、场景能力与状态变体，违反 `technical_architecture.md` 的完成定义。

建议将当前里程碑明确标为 **Alpha/greybox**，在第 6 节 P0 阻断项完成前不要使用“内容完成”或“Release validation passed”作为对外结论。

## 2. 本次已修复的问题

| ID | 问题 | 修复 |
|---|---|---|
| FIX-01 | 书柜直立时已经显示并允许 Hide，玩家可躲进尚不存在的倒地空间 | `HideCapability` 检测同物体 `TipBookcaseUseHandler.IsTipped`；新增回归测试 |
| FIX-02 | 墙/封死窗可按 E 直接写入 `IsStrikingWall/Window`，绕过球棒、椅子或投掷碰撞 | 禁止这两种通用 Use affordance；只允许 `StrikeSurfaceHitReceiver` 写事实；新增回归测试 |
| FIX-03 | D16 配成 `Entry + Reactive`，提前拉地毯后可在入场快照中错误触发 | 种子与 `CP_D16.asset` 改成只允许 `Reactive`，并更新目录选择测试 |
| FIX-04 | 全局 Throw 输入会把不带 `Throwable` 分类的抽屉柜也扔出去 | `PlayerInteractor.ThrowHeld` 对正式定义执行 `Throwable` 分类约束；新增回归测试 |

这些修复只收紧明显的机制绕过，没有更改叙事、关卡位置或演出节奏。

## 3. 死法与结局逐项检查

共同结论：下表所有死亡规则资产都存在并引用了表现资产，但引用对象均为灰盒。`M1GreyboxDeathStyle` 只有 `DefaultShot`、`SofaRearShot`、`ArmedStandoff` 三种；23 条路线最终都被压成“接近—横向抖动—旋转玩家”。所以“规则可选中”不等于“死法已实现”。

| 路线 | 规则/机械状态 | 当前缺失或不符合叙事的部分 | 判定 |
|---|---|---|---|
| D01 默认击杀 | Fallback 存在 | 无实际 AimFire、HitFall、Ragdoll 与枪击构图 | 灰盒 |
| D02 全程静止 | Entry 条件正确 | 缺 1 秒观察、注视和坐姿爆头节奏 | 灰盒 |
| D03 沙发后站立 | 区域+站姿条件存在 | 缺露头识别、头部瞄准与可见处决 | 灰盒 |
| D03′ 蹲藏搜索 | Entry/Continuous 条件存在 | 没有 8 秒搜索路径；一进入 PerformanceLocked 就关闭普通移动，无法实现“中途起身/出声立即被发现” | **逻辑/演出不完整** |
| D04 被子里 | Bed+Hidden 条件存在 | 缺 `Covered` 网格切换、1.5 秒凝视、连射三枪和布面渗血 | 灰盒 |
| D05 砸墙 | 重击接收器写事实；E 键绕过已修 | 墙面无 0–3 档凹坑/Decal，缺背后枪杀演出 | 机械部分完成 |
| D06 投降 | Kneeling 连续规则存在 | 当前无目标空手按 E 立即跪下，不要求面对神秘人或长按 0.6 秒；缺举手/额头处决 | **输入逻辑不符规格** |
| D07 持武器迎战 | 武器分类条件、挥击窗口存在 | 缺射程外先手、武器脱手、开火/中弹组合 | 灰盒 |
| D08 门后偷袭 | DoorBlind+Weapon+Swing、3 个 QTE 和成功后抢枪窗口存在 | 缺偷袭命中、踉跄、同步搏斗、膝顶脱身及失败分支画面；`PAIR_D08` 未制作 | **机械部分完成** |
| D09 家具顶门 | 椅 3 秒/柜 5 秒延迟存在 | 缺多次推门、静默、踹门、家具飞散；房门平时也不能开关 | 机械部分完成 |
| D10 投掷攻击 | `IncomingProjectile` Reactive 规则存在 | 缺侧闪动画、落脚位移与随后的反杀表现 | 机械部分完成 |
| D11 床底 | UnderBed 条件与藏点存在 | 缺径直到床边、下蹲、抓脚踝、拖出；`PAIR_D11` 未制作 | 灰盒 |
| D12 镜头死角 | CameraBlind 区域条件存在 | 缺画外锁定、声音方向与画外击杀构图 | 灰盒 |
| D13 书柜堵门 | Bookcase DoorBlock 规则存在 | 推倒书柜不校验是否位于门口，原地推倒也直接写 DoorBlock；缺门缝探枪扫射和盲区续接 | **联动错误** |
| D14 装死 | Prone 条件存在 | 姿态由 C 单击循环获得，不是规定的长按；缺鞋尖确认和补枪 | **输入逻辑不符规格** |
| D15 砸窗 | 重击接收器写事实；E 键绕过已修 | 没有 4–5 块木板逐条破坏、碎件、最终砖墙和逐击反馈 | 机械部分完成 |
| D16 拉地毯 | 已修为 Reactive-only | 当前一次 Use 同时“布置+拉拽”，没有神秘人脚部进入区域/远端抓角/时机窗口；缺 Stumble | **触发模型仍简化** |
| D17 镜面致盲 | 手电射线与镜面 Reactive 可写 Glare | 普通 Use 现已正确拒绝；缺真实镜面 RT、抬手挡光、1 秒硬直和后续枪杀 | 机械部分完成 |
| D18 可恢复黑暗 | 灯光状态与 Entry 规则存在 | 缺神秘人拧亮/调亮台灯动作、Bright 视觉档与反杀表现 | 机械部分完成 |
| D19 持续全黑 | 黑暗状态、经验与扫描判定存在 | 缺枪挂手电、搜索扫描动作/光柱演出和音画反馈 | 机械部分完成 |
| D20 全黑埋伏 | PersistentBlack+Ambushing 规则存在 | Ambush 是隐藏代理物体按 E 直接置真，不是姿态+区域组合；缺先扫盲区的路径演出 | **联动简化** |
| D21 完美计划 | 被子+全黑+拉拽规则、枪/手电二选一、镜反射分支、`d21Seen` 写入存在 | 表现资产仅 12 秒空灰盒且无 QTE；缺跌倒、掉枪掉灯、地面拉扯、挡光、备用枪全段；`PAIR_D21` 未制作 | **机械部分完成，核心内容缺失** |
| D22 夺枪开火 | 夺枪事实、1 发/空仓/备用枪状态链存在 | 缺玩家瞄准、神秘人闪避、主枪后坐、拔备用枪收尾；表现资产无窗口/分支 | **机械部分完成，核心内容缺失** |
| E1 电视二击 | 二次 HeavyHit、优先级胜过死亡、Destroyed 和黑屏流程存在 | 家具/碎片恢复主要靠通用 reset；神秘人阻止动作仍是移动插值；电视三态模型未接 | 机械部分完成 |
| E2 第一人称死亡 | 第一人称控制窗口、D01/D07/D10 过滤和无普通循环重置存在 | 倒地只是 Transform/相机插值；`CAM_E2_FirstPersonFall` 未制作 | 机械部分完成 |
| E3 终局 | 坐椅、限转头、入场、枪响、标题和存档存在 | 备用枪和秒针可动态用 primitive/Transform 代替；无正式 Timeline；规格写“中央木椅”，当前美术安装脚本把苏醒/结局锚点移至北墙木椅，需设计确认 | **可跑但表现未完成** |

### 死亡动画的联动缺陷

`CharacterAnimatorDriver` 在 `KillImpact` 才触发 `AimFire/HitFall`，而 `LoopDirector` 在同一阶段回调中立刻递归提交 `LoopReset → Awakening`。订阅顺序不同会产生“触发后立刻 Reset”或“Reset 后又收到旧 KillImpact”的不确定结果；无论哪一种，正常死亡中都没有可见的中弹动画时长。正确做法是把开火/中弹放进各表现的 impact beat，并只在表现完成后提交 `KillImpact`。

## 4. 23 件物品与互动检查

“可用”表示当前场景有可执行组件，不代表已有正式动作或视觉反馈。

| 物品 | 当前可用 | 缺失/错误 |
|---|---|---|
| sofa_awaken | Inspect、Hide、苏醒/后区区域 | 无头部 IK；D03′ 搜索不成立 |
| blanket | Pickup、Throw、Place、Hide、拉拽事实 | 四态只有 Crumpled 接场景；Covered/FlatTrap/Draped/Dragged 无状态呈现；没有布料音/动作 |
| bed | 床底和床上两个 HideSpot | D11 拖拽和床上覆盖演出缺失 |
| wood_chair | Carry、Swing、Throw、门插槽 | 无长按搬运、搬运/挥砸 IK、散架态切换；Use 定义与实际组件不一致 |
| bookcase | 推倒、倒后 Hide、Inspect | 本次修复直立 Hide；仍为瞬时推倒而非长按 2 秒；不校验门口位置就写 D13 |
| baseball_bat | Pickup、Swing、Throw、命中 | 挥击动画已有 FBX 但需正式事件/IK 验收；缺命中音和墙损反馈 |
| room_door | Inspect、放置家具 | **缺 Open/Close Use**，玩家不能主动开门或观察黑走廊；Closed/Blocked/Breached 视觉状态不完整 |
| ceiling_light | 受重击、关灯、重置 | 完整/破碎 FBX 均有，但 `BreakableLight.intactVisual/brokenVisual` 在场景均为空 |
| table_lamp | Pickup、Throw、开关、破坏、灯光事实 | Normal/Bright 没有明确视觉差；完整/破碎引用为空；拿起后断电线/Unplugged 状态缺失 |
| walls | Inspect、物理重击事实 | 本次禁用按 E 伪造；缺每面独立 0–3 损伤、Decal、碎屑和次数状态 |
| crt_tv | Inspect、轻/重命中、两击结局、全屏 CRT | Item 定义有 Use 但场景无 Use；三态机身只挂 Intact，Cracked/Destroyed 未随控制器切换 |
| remote | Pickup、Use、Throw | 电视闪帧逻辑存在；缺按键/手部动作和专属音效 |
| pistol_main | D08/D21 动态掉落、Pickup、Use、1 发/空仓 | 仅窗口中动态加 FirearmUse；无持枪/后坐/弹药反馈表现 |
| pistol_backup | 仅脚本掉落/收尾 | 作为普通物品无交互正确，但枪套、拔枪绑定和动画缺失 |
| flashlight | 动态掉落、Pickup、Use、射线/光锥 | 缺枪轨挂载视觉、拿取/脱离动作、体积光美术验收 |
| killer_coat | 独立静态代理 Inspect | 当前角色风衣烘进角色网格，独立代理是深色占位；内枪套和可绑定独立蒙皮件缺失 |
| dresser | Carry、门插槽；本次禁止误投掷 | **三个独立抽屉和内容物完全没有互动**；定义中的 Use 无场景能力；无长按搬运 |
| books | Pickup、Throw、Inspect | 定义声明 Use，但场景无 Use；不能翻/打开；当前是一组而不是至少 8 本独立实例 |
| vase | Pickup、Throw、碰撞 | 没有 Shatter、碎片替换、巨响和 2 秒跛行区域 |
| boarded_window | Inspect、物理重击事实 | 本次禁用按 E 伪造；没有逐板生命/切换/碎件/最终砖墙状态 |
| rug | Inspect、一次 Use 写陷阱+拉拽 | D16 已修 Reactive；仍缺“布置/脚踩/拉拽”三方时序与可抓取角形变 |
| wall_clock | Pickup、Throw | 没有 Break 状态；普通秒针抽搐与 E3 首次越格没有绑定到正式模型（可退化为动态 primitive） |
| mirror | Inspect、手电反射 | 没有 Break；无镜面相机/RenderTexture；完整/破碎态未切；普通 Use 代理已被禁用 |

另外，Inspect 目前只让玩家根节点瞬间朝向目标并广播事件；未实现规格要求的 0.25 秒焦点停留、头/胸 IK 注视。Interact 长按阈值 0.35 秒也未进入输入仲裁，所有搬运/推倒/投降动作实质上都是单次按下。

## 5. 主要联动风险

### P0：会让“内容完成”判断失真的问题

1. **全死亡目录是假完成。** `ContentStatus.Implemented` 只保证非空引用，无法区分空灰盒与正式演出。
2. **Timeline 通道未使用。** 场景 `PlayableDirector.m_PlayableAsset` 为空，`TimelineSequenceRunner.director` 为空；`KillerDirector` 实际绑定 `M1GreyboxPerformancePlayer`。
3. **验证器覆盖不足。** 架构要求的 Timeline 语义绑定、双人锚点、可变物体 Resettable、相机—电视 2cm 误差都未真正验证。
4. **普通死亡动画时序不确定。** `KillImpact` 同回调立即重置，角色动画触发无法形成可见演出。

### P1：玩家可感知或会改变路线成立性的错误

1. 书柜在任何位置推倒都会成为门阻挡，D13 缺空间条件。
2. D20 的 Ambush 由隐藏功能代理直接置真，不是玩家姿态与盲区组合。
3. D06/D14 的长按与朝向条件缺失；姿态循环可直接制造 Kneeling/Prone。
4. 门不能主动开关；抽屉柜无抽屉；这两件关键环境物的 Use 定义是假能力。
5. D03′ 进入演出后输入被锁，无法执行“搜索期间保持/暴露”的设计。
6. 灯、电视、被子、窗板、花瓶、镜子、挂钟等状态逻辑与已有模型变体脱节。
7. `GameplayAffordanceHandler` 与 `StolenLootDropController` 使用 `FindAnyObjectByType`/Resources 兜底，违反架构的依赖注入边界，并会在多实例或重载场景中产生隐式绑定风险。

### P2：规格或工程口径不一致

1. `gameplay_logic_spec.md` 写 E3 在中央木椅；当前布局文档和 ArtInstaller 把木椅、苏醒点、结局锚点移至北墙。需明确以哪一版构图为准。
2. README 仍描述只验证 D01/D03/D07 的灰盒，与目录里所有内容均标 `Implemented` 相互矛盾。
3. `game_flow_script.md` 的 D09 描述允许利用延迟转入其他死法；权威规则表却让 `DoorBlock=Furniture` 在 Entry 高优先选中 D09。需确认 D09 是“完整死亡”还是“破门前奏”。

## 6. 发布阻断清单与建议顺序

### P0：必须先完成

1. 建立正式 Performance/Timeline 基类与语义轨道，把 D01 做成可见的 Walk/AimFire/HitFall/Ragdoll 闭环。
2. 完成三条独特双人路线：D08、D11、D21；再完成 D22 与 E2/E3。
3. 把已有视觉变体接入权威状态机和 LoopReset；至少电视、两灯、被子、窗、花瓶、镜、木椅、挂钟。
4. 扩展 Release Validator：拒绝 `M1GreyboxPlayableAsset`、检查空 PlayableDirector/绑定/锚点/Resettable/相机误差，并把实际交互组件与 ItemDefinition 动词对表。
5. 修正死亡 impact 时序，保证表现完成、命中帧、持有物策略、音效和重置顺序确定。

### P1：随后完成

1. 补门开关、三抽屉、书本 Use、墙损、逐板窗、花瓶碎片/跛行、镜子破坏与真实反射。
2. 实现长按仲裁：Carry 0.35 秒、Bookcase 2 秒、Surrender 0.6 秒，并加入面对/空手/神秘人已入场条件。
3. 用明确的 Trigger/Zone 组合替换 D13、D16、D20 的隐藏代理捷径。
4. 接齐动作、IK、VFX 和音效，逐条执行第 7 节验收。

## 7. 必需的人工内容验收

自动化测试不能验证镜头可读性。至少录制或逐条执行以下清单：

- 23 条 D 路线各从新循环触发一次，确认实际画面与 `game_flow_script.md` 对应，且 OutcomeId 正确。
- D08 三窗全败、部分胜、全胜未抢枪、全胜抢枪四条；D21 拿枪、拿灯照人、拿灯照镜三条；D22 一发+空仓。
- 每条死亡前分别持小物/搬大物/物体飞行，确认 Preserve/Drop/Recover 不会隐式消失或跨循环残留。
- 所有可变物体至少经历一次改变→死亡→下一轮；再做 50 连续循环，无残留 Collider、协程、VFX、Decal、音源或事实。
- E1 第二击与同帧死亡竞争；E2 三种允许变体；E3 只能转头、枪响、保存、Continue 禁用、New Game 清档。
- 固定镜头、全黑 0–5 XP、手电锥 0.8 秒、镜面反射和电视裂纹回声做逐帧/直方图验收。

## 8. 自动化证据

| 检查 | 结果 | 说明 |
|---|---:|---|
| `ConGameProjectValidator.ValidateBatch` | 通过，0 error | 只能证明当前有限检查项通过，不代表内容完成 |
| EditMode | 37/37 通过 | 规则、优先级、持久数据等纯逻辑 |
| PlayMode | 62/62 通过 | 包含本次书柜 Hide、物理重击、防误投掷与 D16 触发回归 |
| 源码 TODO/FIXME | 无实质代码 TODO | 未完成内容主要以 Greybox、空绑定或错误的 `Implemented` 状态存在，而不是 TODO 注释 |
| 正式 Timeline/动画资产 | 0 | 未发现 `.playable`、`.timeline`、`.anim`；FBX 动画不等于演出接入 |

详细的美术、动画、VFX、音频缺口和交付验收标准见 `art/requirements/art_content_gap_spec_2026-08-06.md`。
