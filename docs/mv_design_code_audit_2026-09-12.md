# 《泡沫》参考、设计与实现对照审查

日期：2026-09-12。范围：现有设计文档、当前工作区 C#、Room_M1 场景序列化接线及 RoomDesignGapInstaller 的运行时补装。三个子 agent 分别审查交互、AI/灯光/反制、文档一致性，主 agent 汇总并复核关键结论。

本轮没有替换资源、修改玩法代码或运行测试。下文“已确认”指静态代码/文档证据成立，不等于已在真实操作中复现。此前的 7 项测试通过不代表这些组合链已完成。

## 1. MV 参考证据与边界

- 官方视频：[ヨルシカ「あぶく」](https://www.youtube.com/watch?v=OHAjc-ayhus)，官方站也指向该视频：[官方发布页](https://yorushika.com/news/detail/11782)。
- 本次官方页面抓取受限；Kimi WebBridge daemon 已启动，但返回 `no extension connected`。没有完成逐镜观看，因此不提供伪造时间码，不把项目原始构思或网上观后感当成逐镜证据。
- 已读制作团队本人访谈：[Billboard JAPAN／擬態するメタ](https://www.billboard-japan.com/special/detail/5358)。访谈明确谈到“短循环”、熄灯后的反击、用鞋击破画面，以及把观看的人拉进作品。这支持将“策略造成可见变化”和“观看者身份转换”作为对照重点；不支持凭空推定某个精确拖拽距离、灯光阈值或死法表来自 MV。
- 门缝射杀、镜面眩光、地毯与被子的差别、黑暗 XP、两次重击、秒针最终前进等，必须分别标注为“视频逐镜已证实”“用户原构思”“游戏新增”。目前这些文件没有完成这样的来源标记。

**当前判断：房间循环、利用环境反抗、电视与视点的关系方向一致；设计和实现尚不能称为与 MV 全面对准。最明确的偏移发生在身份转换、交互输入、空间触发和演出时序。**

## 2. 最先处理的设计问题

### D1：结局把“观看者被拉入”改写成“进入原主角眼睛”

`story_outline.md` 最后写的是主角打破电视、把玩家拉入；[game_flow_script.md:200](D:/game/con_game/docs/game_flow_script.md:200) 的 E2 却明确让玩家进入主角眼睛。代码 [EndingDirector.cs:191](D:/game/con_game/Assets/_Game/Bootstrap/EndingDirector.cs:191) 记录原 Player 的位置，恢复后把同一个 Player 传送回原位，再切第一人称。场景第一人称机位也在原 Player 下：[Room_M1.unity:14444](D:/game/con_game/Assets/_Game/Scenes/Room_M1.unity:14444)。

这实现了视角变化，却没有实现两个身份之间的交接。建议先决定：E2 操控的是原主角，还是从屏幕外进入的观看者？如果追求参考作品的身份转换，需要明确原主角此时在哪里、做什么，谁继承循环；不能只以相机切换作为完成标准。原主角离开的具体动作和先后顺序仍需视频逐镜核对。

### D2：固定机位与“电视就是玩家眼睛”的空间关系不成立

总纲要求机位在电视屏幕位置：[game_outline.md:65](D:/game/con_game/docs/game_outline.md:65)。当前场景固定相机在 `(0, 2.75, -2.58)`，俯角约 45°：[Room_M1.unity:13863](D:/game/con_game/Assets/_Game/Scenes/Room_M1.unity:13863)。电视根在 `(0, 0.9, -2.38)`，屏幕锚点局部位置 `(0, 0.248, 0.2295)`，即约 `(0, 1.148, -2.1505)`。二者高度相差约 1.60m。

这不只是构图问题：玩家走向电视、砸向屏幕时，动作目标与观看者的位置不同。应将可见性、电视交互距离、砸向镜头的动作和机位一起设计，再决定允许多少镜头偏移。临时截图相机的漂亮构图不能代替正式游戏机位。

### D3：“最终无法杀死神秘人”可以保留，但不应消灭局部成功

[gameplay_logic_spec.md:13](D:/game/con_game/docs/gameplay_logic_spec.md:13) 明确所有非结局搏斗最终失败，这是当前设计决定，不是程序 bug。D08 掉枪、D21 获取枪灯也表明设计并非完全没有中间收益。

问题在于未把“绊倒有效多久、掉枪是否解除主枪射击、何时能行动、玩家怎样看出已成功”写成统一契约。建议每种策略定义一个可观察的中间结果，再由具体反制收束：倒地→不能立即瞄准；掉枪→主枪不可用；亮灯恢复→真实接触灯具或明确的超自然规则。最终失败也必须让玩家能解释失败原因。

### D4：零 HUD 与复杂操作缺少一致的发现方式

普通 E 同时承担拿取、使用、放置、短按攀爬和长按搬运；C 又承担藏身、投降和俯卧。文档依靠注视、动作、声音引导，但没有给这些关键选择统一的成功/失败反馈标准。零 HUD 可以成立；问题是玩家必须能区分“按键没用途”“姿势不对”“太远”“目标被别的能力抢走”。需要先修输入语义，再补手部预动作、抓角姿态、脚步/布料音、明确的成功反应，不必增加满屏提示。

### D5：上游文档与实现规范长期分叉

- 苏醒点：原构思和流程开场是沙发，总纲和美术规范是北墙木椅。见 [story_outline.md:1](D:/game/con_game/docs/story_outline.md:1)、[game_outline.md:36](D:/game/con_game/docs/game_outline.md:36)、[game_flow_script.md:19](D:/game/con_game/docs/game_flow_script.md:19)。原始构思可作为历史保留，但执行用文档必须同步。
- 躲藏：物品文档说移动退出，运行规范使用 C 切换；当前代码也使用 C 退出。应选择一种，而非两套同时作为验收依据。
- D18：流程描述“台灯还亮，神秘人调亮”；规则是“台灯完整但关闭，神秘人开启”。二者对应不同反馈。
- 计时：总纲“20–45 秒”和“默认 30 秒”不够精确，运行规范实际使用 45/首次行为改30/巨响取更早截止时间。应复用后者的决策表，删除无来源的20秒说法。
- 总纲只列经验和循环数跨轮保留，运行规范另有 d21Seen、一次性裂纹回声和结束标记。裂纹回声不是上一轮伤害延续，必须明确。

## 3. 已确认的代码与接线问题

### C1｜高：陷阱类型可被另一触发区覆盖

[TripTrapController.cs:129](D:/game/con_game/Assets/_Game/Runtime/Interaction/TripTrapController.cs:129) 将全局 `snapshot.Trap` 送入判定，未验证它等于本控制器的 `armedState`。[TripTrapTiming.cs:7](D:/game/con_game/Assets/_Game/Domain/Interaction/TripTrapTiming.cs:7) 只检查“任一种陷阱已布置”。[CommitPull:141](D:/game/con_game/Assets/_Game/Runtime/Interaction/TripTrapController.cs:141) 随后反而写入当前触发区自己的类型。

结果：只布置地毯，神秘人进入空的门口被子插槽，也可能把状态改成 BlanketArmed 并发出 blanket 拉扯事件；全黑时进而满足 D21。反向覆盖也可能发生。两个控制器都由 [RoomDesignGapInstaller.cs:214](D:/game/con_game/Assets/_Game/Bootstrap/RoomDesignGapInstaller.cs:214) 安装。

建议：把“当前触发体有对应物品/已布置”“脚进入此陷阱”“玩家拉此陷阱”绑定到同一个实例。最低限度应验证类型和插槽占用，不能仅依赖一个全局枚举。

### C2｜高：主动拉扯被自动踩踏触发替代

[TripTrapController.cs:101](D:/game/con_game/Assets/_Game/Runtime/Interaction/TripTrapController.cs:101) 的 `OnTriggerEnter` 自动调用 `TryAutoPull`，而 ShouldAutoPull 与 CanPull 是同一个判断。玩家提前布置之后，即使不抓角、不再按键，也会触发。被子 Use Handler 虽存在，却不是启动拉扯所必需。

与 [items_interactions.md:87](D:/game/con_game/docs/items_interactions.md:87) 的“抓被角，在神秘人踏上瞬间猛拉”不一致；地毯也有同类偏移。建议接触只代表“允许拉”，实际拉由玩家输入提交。过早、过晚和亮灯跨过陷阱要有各自可读反馈。是否强制蹲姿可以设计决定，但必须保留明确的主动时机。

### C3｜中高：被子运输是普通拿取，没有拖拽过程

[PickupCapability.cs:38](D:/game/con_game/Assets/_Game/Runtime/Interaction/PickupCapability.cs:38) 调用普通 TryHold；[PlayerInteractor.cs:207](D:/game/con_game/Assets/_Game/Runtime/Player/PlayerInteractor.cs:207) 使用普通持物路径。外观只有 Crumpled/Covered/FlatTrap/Draped，没有可操作的 Dragged 过程：[BlanketVisualPresenter.cs:159](D:/game/con_game/Assets/_Game/Presentation/Interaction/BlanketVisualPresenter.cs:159)。

已有“拿被子→放入门口插槽”，不能据此声称“拖下床→沿地面拖至门口”已实现。无需复杂布料模拟，低成本拖拽姿态、布料尾端约束、地面贴合和布料声音即可服务玩法；速度是否降低应另作设计，不是判定拖拽成立的唯一条件。

### C4｜高：普通 Use 被拾取/放置仲裁遮蔽

[PlayerInteractor.cs:399](D:/game/con_game/Assets/_Game/Runtime/Player/PlayerInteractor.cs:399) 在自由状态持物时只尝试 Place，否则放地上；不会执行手持物 Use。未持物时普通交互选最高优先级：Pickup=100，Use=40，见 [InteractableEntity.cs:40](D:/game/con_game/Assets/_Game/Runtime/Interaction/InteractableEntity.cs:40)。

台灯确实存在 ToggleLightUseHandler，书本确实存在翻阅 Handler；问题不是没写组件，而是默认 E 先拿取，再按 E 放下。遥控器等普通持物使用也受这一通用分支影响。Counterplay 中有专用 Use/Grab 路径，所以不能据此说 D21 手电、D22 主枪全部不可用。

需要统一动作表：同一物品什么时候使用、什么时候拿取、什么时候放下，长短按或其他既有输入如何区分。先解决仲裁，避免为每件物品再加特判。

### C5｜高：盖台灯组合有两处断点

[RoomDesignGapInstaller.cs:160](D:/game/con_game/Assets/_Game/Bootstrap/RoomDesignGapInstaller.cs:160) 创建 LampDrapeSocket 时没有 Collider；[InteractionScanner.cs:45](D:/game/con_game/Assets/_Game/Runtime/Interaction/InteractionScanner.cs:45) 只从物理 Overlap 返回的 Collider 向父级寻找交互对象，无法发现这个子插槽。持被子按 E 会退回放地上。

即便直接调用 Place，Draped Presenter 也只切换模型：[BlanketVisualPresenter.cs:101](D:/game/con_game/Assets/_Game/Presentation/Interaction/BlanketVisualPresenter.cs:101)。未找到盖上/移走时调暗台灯、变红和恢复的联动。文档称其为氛围交互，不应擅自把盖布升级成 PersistentBlack 或 D21 条件。

### C6｜高：被拔走的台灯仍可能被神秘人远程重新点亮

[LampUnplugOnHold.cs:48](D:/game/con_game/Assets/_Game/Runtime/Interaction/LampUnplugOnHold.cs:48) 只在 Held 时 SetPowered(false)，没有独立“插电/电线已断”状态。顶灯坏、完整台灯不亮会被归为 RecoverableDark：[RoomLightCoordinator.cs:66](D:/game/con_game/Assets/_Game/Runtime/Lighting/RoomLightCoordinator.cs:66)。开始追杀直接调用 [KillerLampBrightener:19](D:/game/con_game/Assets/_Game/Runtime/Lighting/KillerLampBrightener.cs:19)，检查该标签后 SetPowered(true)，没有灯的位置、距离、持有人或电源连接检查。

所以“拿走且电线扯断”的物理叙述，与“关了但仍可开”的状态被混为一谈。应先区分关灯、断电、破损、遮光，再决定神秘人如何恢复照明；否则玩家移走灯具也没有对应的因果收益。

### C7｜高：正常追杀中的眩光没有接上预期反应

[KillerDirector.cs:449](D:/game/con_game/Assets/_Game/Runtime/AI/KillerDirector.cs:449) 会为陷阱/确认偷袭启动演出，但 Glare 走选择候选意图与 AlertToPosition 分支。暗处 D19/D20 排序高于 D17，D17 往往不能替换意图；已经 Aiming 时 AlertToPosition 也被忽略：[KillerTacticalController.cs:217](D:/game/con_game/Assets/_Game/Runtime/AI/KillerTacticalController.cs:217)。

因此“有 Glare 事件、有 D17 资源”不能证明正常追杀里照镜子/照人就有挡光、停顿或取消瞄准。需要定义一次眩光确认后的具体行为以及恢复时间；与已提交攻击不可取消的既定规则分开处理。D21 专用窗口通往 D17 的桥接路径另行存在，不应混为一条通路。

### C8｜高：D21 两套演出时序没有统一，掉枪窗口提前开启

[KillerDirector.cs:449](D:/game/con_game/Assets/_Game/Runtime/AI/KillerDirector.cs:449) 先 StartCounterplay 启动灰盒播放器，再 LaunchConfirmedPerformance 启动 paired 播放器。灰盒开始即通知 adapter：[M1GreyboxPerformancePlayer.cs:106](D:/game/con_game/Assets/_Game/Presentation/M1/M1GreyboxPerformancePlayer.cs:106)。Loot Controller 收到 D21 开始，马上显示枪灯并启动 12 秒选择窗口：[StolenLootDropController.cs:196](D:/game/con_game/Assets/_Game/Runtime/Interaction/StolenLootDropController.cs:196)。

但 paired 定义先 Fall 1.2 秒，再发布枪灯掉落事件：[PairedPerformanceCatalog.cs:104](D:/game/con_game/Assets/_Game/Domain/Choreography/PairedPerformanceCatalog.cs:104)。当前 [WirePairedPerformance:357](D:/game/con_game/Assets/_Game/Bootstrap/RoomDesignGapInstaller.cs:357) 没有为 paired 注入 ConfigureHooks；未找到枪灯控制器订阅这些 paired 掉落事件的生产接线。

静态确认的是：物件显示/选择窗口由旧的开始回调驱动，不能保证在真正摔倒和掉落后才开放。不是“整条 D21 没做”，也不能声称已完成逐拍交互。应该由同一个事件源驱动摔倒接触、掉枪、拾取窗口、手电所有权及备用枪收尾；精确的并行表现后果留待后续动态验证。

### C9｜中：推倒书柜只有瞬间旋转

[TipBookcaseUseHandler.cs:52](D:/game/con_game/Assets/_Game/Runtime/Interaction/TipBookcaseUseHandler.cs:52) 在长按成功后直接写 localRotation，再按空间门槛写堵门事实和发噪声。文档要求的物理施力、倒下过程、书本散落没有由这条代码实现。不能推断旋转后原 Collider 消失——它仍可能形成遮挡；真正的问题是倒下过程和接触反馈缺失。

建议用短动画或受控物理倒下均可，接触时再确认堵门/噪声。无需为了“物理”字样强行引入复杂模拟。

### C10｜中高：镜面和新版结局系统有代码但未接入当前正式房间

当前 Room_M1 有 MirrorFlashlightReactive，却未找到 MirrorReflectionController 的场景/生产安装接线；不应把镜面脚本编译通过当成镜子可见反射已完成。

当前结局实际挂 [Bootstrap.EndingDirector](D:/game/con_game/Assets/_Game/Scenes/Room_M1.unity:6929)。新的 EndingSequenceDirector、Settings 和对应 Presenter 有代码与测试，但未找到其生产场景接入；RoomDesignGapInstaller 也未安装它们。调整 CFG_EndingSequence 因而不能证明会改变正式房间结局，当前旧入口仍使用类内固定时间参数：[EndingDirector.cs:32](D:/game/con_game/Assets/_Game/Bootstrap/EndingDirector.cs:32)。

建议选定一条生产入口，并以场景实际引用为准；不要再把新旧两套都算作已完成。

## 4. 全系列覆盖概览

下表是静态审查范围，不是通过/失败的试玩记录。

| 对象/链 | 已找到的实现 | 本轮主要结论 |
|---|---|---|
| 苏醒椅、扶手椅掩体 | 起点、Hide、姿势事实 | 起点文档冲突，移动退出与C切换规则不一致 |
| 被子、床、门口伏击 | 拿取、藏身、插槽、四态模型、D21选择 | 拖拽缺失，自动拉扯，陷阱串扰 |
| 地毯 | TripTrap、D16 Stumble 资源/演出 | 是自动接触触发，不能等同主动拉角 |
| 木椅 | 短按攀爬、长按搬运、重击、放置 | 已有能力，后续查搬动后的攀爬/挡门组合 |
| 书柜 | 长按推倒、堵门门槛、噪声 | 瞬间旋转，散书/倒下接触未闭合 |
| 抽屉柜 | 运行时三个抽屉、杂物、搬运/门插槽 | 不是完全没有实现；运行时灰块与美术接入需单独处理 |
| 棒球棍、主枪、备用枪 | 近战、夺枪窗口、主枪开火、备用枪分支 | 普通近战不能杀死是设计；枪灯逐拍所有权接线有风险 |
| 顶灯、台灯 | 破坏、供电、全局明暗、神秘人开灯 | 普通Use遮蔽，拔线与关灯混淆，远程开灯 |
| 被子盖台灯 | 插槽、Draped 模型 | 插槽不可扫描，红暗灯光联动缺失 |
| 手电、镜子 | 光锥/反射判定、D17、窗口Use | live hunt反应入口断点；镜面画面系统未接入 |
| 电视、遥控器 | 两次重击、轻击脉冲、结局、遥控Use | Use仲裁、机位位置和观看者身份需重审 |
| 书本 | 拿取、投掷、翻阅Handler | 翻阅被拾取优先级与放置分支遮蔽 |
| 花瓶、挂钟 | Fragile/碎片/时钟运行时补装 | 旧缺口文档不能证明仍缺；效果和复位留待动态检查 |
| 墙、封窗、房門/走廊 | 命中、破坏级数、堵门、入场/区域机制 | 查两种阻挡、黑暗与伏击同时成立时的行为衔接 |
| 风衣/枪套 | 角色视觉、装备挂点 | 视觉存在不等于发现备用枪有可读反馈 |
| 死亡、循环、最终第一人称 | Loop、旧结局入口、另有新Sequence代码 | 生产入口与文档/测试资产不统一，身份转移被视角切换替代 |

## 5. 不应误报成缺失的内容

- 正常追杀已有视觉检测、遮挡、调查、瞄准、受击恢复；不是入场就无条件执行 D01。
- 前两次受击短暂恢复、第三次抗连击，以及攻击提交后不再取消，均有明确规范；是否好玩需要后续体验评估，不是本轮静态缺陷。
- 杀手手电让杀手看见玩家，本来就不要求玩家失明；不能用“没有玩家失明状态”判错。
- D16 有 Stumble 演出，D21 有 Fall/配对定义和枪灯窗口；问题是触发、接线与时序，而非名称不存在。
- 运行时安装器补了抽屉、易碎物、陷阱、书本等。不能只看 YAML 中没有组件便判缺失，也不能只看组件已创建便判完整。

## 6. 建议的下一步顺序

1. 先定文档：观看者/原主角身份，正式机位，主动陷阱操作，物品使用/拿取/放置输入，以及断电/关灯/破损/遮光的含义。
2. 再修通用代码：陷阱实例隔离与主动触发、Use 仲裁、灯罩插槽与照明状态。它们影响多条路线，应先于新增死法。
3. 合并 D08/D11/D21 的演出与交互事件源，明确倒地、掉落、抢夺、眩光和备用枪各自的权威时刻；选定唯一结局生产入口。
4. 浏览器扩展连接后逐镜补 MV 事件表，确认哪些必须保留、哪些是游戏新增；再恢复美术接入。
5. 之后才做真实操作验收：被子/地毯分别与同时布置、早拉/晚拉、明亮/关灯/拔灯/破灯/盖灯、持物Use、绊倒后攻击/抢枪/拿灯、镜面反光、电视第一击与敌人反应，以及重置。

本报告没有宣称全部组合已穷举，也没有用自动测试替代视频对照。逐镜 MV 校对仍待浏览器连接；静态确认的设计/代码问题已足以指导下一轮修订。
