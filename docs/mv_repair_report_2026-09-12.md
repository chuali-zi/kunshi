# 2026-09-12 MV 设计与游戏接线修复记录

本文记录 M1 可玩链、房间表现和结局生产入口的修复。依据是项目设计文档、源代码及 Unity 实际运行。浏览器桥接服务与扩展已统一到 2.0.9，导航、读取和截图正常；YouTube 页面要求登录验证，B 站视频持续加载，当前尚未取得真实 MV 帧，因此不把这轮工作称为逐镜还原。

## 交互输入与物品状态

`gameplay_logic_spec.md` §4、`game_outline.md` §3.2 规定 Interact 长按阈值为 0.35 秒。空手面对同时支持 Use 和拾取的物品时，短按优先执行 Use，长按才拾取；只有拾取能力的小物仍可短按拾取。持物时短按优先放入当前有效 Place 插槽，没有可用插槽才执行手持物 Use；长按把物品放到地面。木椅短按攀爬、长按搬运；书柜长按 2 秒后进入 0.35 秒受控倾倒。Throw 仍是单独的投掷输入，不由 Interact 长按代替。

当前明确实现的是地面拖被；没有把地毯描述成可搬运的地面拖拽物，也不声称已经实现 Cloth 物理。被子可在放置后进入 `FlatTrap`，也可重新拖动；地毯保留主动拉扯的较重失衡教学。被子拉角必须满足门口布置、距离约 1.6m、神秘人进入接触区以及玩家主动提交拉拽，单纯踩入触发区不会自动绊倒。地毯只产生较短的趔趄/恢复窗口，不应被解释为真正倒地。当前设计中的真正倒地仍由轻而近的被子拉扯进入 D21。

交互扫描先过滤不可执行能力，再按 XZ 水平朝向和三维距离评分，能力优先级只作很小的平分项。已用成品布局核对台灯与柜体、被子与门、床面被子与床的攀爬能力。抢夺窗口另外按 PickUp/Carry 扫描，防止旁边只能 Use 的门吃掉抢枪输入。

陷阱触发器的物理接线已按实际角色脚部高度修正：陷阱使用 kinematic Rigidbody，trigger 高度扩到约 0.4m，并保持地面底部和 XZ 位置不变。原先 trigger 约在 y=0.05–0.11，而神秘人脚部胶囊底部约 y=0.18，角色实际踩入时无法进入触发体。当前 MCP 验证观察到 `Occupied=true`、`TrapPulled=false`，说明进入占位和等待玩家主动拉扯已经分开。

## 光照、失明与反击窗口

顶灯和台灯的损坏、断线、关闭、被子遮光是不同状态。两盏灯具都损坏才进入 `PersistentBlack`；被子遮台灯只改变暗红氛围，不写入永久黑暗；断电或断线不能简单等同于永久黑暗，也不应自动触发同一条 D18。神秘人的枪挂手电是黑暗中的动态威胁光源，光锥照到玩家的击杀规则与玩家手电照到神秘人的短暂挡光是两条不对称规则。设计文档明确玩家手电照中神秘人不会永久致盲，也不会直接把普通 D17 规则改成可杀死神秘人。

Room_M1 的环境光从 Skybox 改为 Flat，沿用场景原有的低环境色，避免双灯已经损坏但天空探针仍将房间照亮。台灯、顶灯的已有 Broken FBX 已接入 BreakableLightPresenter；损坏会切换破损外形，循环重置恢复完整外形。

## D08 与 D21 掉落窗口

D08 的三个拉扯窗口全部成功后，`D08.LootWindow.Open` 由 paired semantic event 发出；生产 loot controller 消费该事件，显示主枪并打开短暂 Grab 窗口。拾枪后转入 D22，超时或失败仍回到 D08 的处决结算。D08 loot 等待期间 paired 的动画图保持当前姿态并暂停，避免等待窗口让神秘人恢复站立。

D21 的生产顺序是：`PersistentBlack` 前置成立、被子拉扯 → 神秘人真正倒地约 1.2 秒 → 主枪和战术手电在当前神秘人附近掉落 → paired 演出暂停 → 玩家自由选择抢枪、照镜或拾取手电。单纯把灯盖住形成的 `Draped` 氛围状态不满足 D21，也不应在文中当成全黑前置。D21 开始时 Counterplay 输入窗口为 None，掉落 semantic event 到达后才打开 Grab。掉落位置按本次 killer 的位置计算，不依赖场景固定初始点。抢到主枪转 D22；照镜转 D17；手电直照神秘人后，paired 从 `block-light` 继续到备用枪约 1.6 秒和枪击结算约 0.5 秒，最后自然完成并标记 `d21Seen`。旧 loop epoch 或其他 outcome 的 semantic event 不得打开当前窗口。

玩家在主动拉扯后，即使神秘人仍处于刚进门、尚未进入普通 live-hunt 的阶段，也会进入已确认的 paired 启动路径；这保证陷阱提交本身不会只改变标签而没有演出。

源 D21 动作保留站立髋部高度，原来播放时横躺在空中。现在仅对 D21 的视觉子层做贴地修正，保持玩法碰撞根和陷阱位置不变；取消和重置恢复视觉原位。选择窗口暂停神秘人的演出，同时断开玩家的配对输出、恢复 Idle/行走，避免玩家横躺着移动；选择手电直照后再接回配对动作。

## E2 观察者分离与 E3 木椅

生产结局入口保持 `Bootstrap.EndingDirector`；`EndingSequenceDirector` 只保留为非生产参考实现。`EndingDirector.ConfigurePresentation(settingsAsset, screenAnchor, originalActorVisual, exitAnchor)` 接收 `CFG_EndingSequence` 和表现锚点。E1 黑屏前保存原主角 pose。配置完整时，`EndingObserverHandoff` 只复制 `CH_Player_Model` 的视觉层，移除克隆体上的 gameplay `MonoBehaviour`，让原视觉克隆独立向出口移动；真实 `PlayerMotor` 移到 TV 前的 screen anchor，成为 E2 的观察者。这样 E2 不再用同一个玩家根节点原地冒充交接。克隆体不加入 loop reset registry，结局结束时清理；旧 `Configure` 无 presentation 锚点时保留兼容路径。

E2 倒地使用 `EndingFallMotion` 采样，只降低第一人称 view 的高度并施加受限 pitch/roll，不旋转整个玩家根节点。进入 E3 后，玩家按生产木椅现场位置坐到 `FUR_WoodChair`，电视保持损坏，沙发保持空置；`FirstPersonLook.ResetLook()` 在设置最终朝向前调用，避免重置顺序把 E3 目标视线覆盖。E3 的 killer approach、站定、瞄准、时钟 tick、静默枪响、标题保持和 yaw 限制读取 `CFG_EndingSequence` 对应字段，仍由 `EndingDirector` 驱动。

场景空间接线也随成品门洞统一：新成品门洞使用 z=0.55；额外墙开口、`blackcorridor` 和 ending exit 不再沿用旧的 1.45 坐标，ScaffoldBuilder 源与实际接线保持同一位置。盖灯的 `Draped` 几何 bounds 顶部对齐灯具 socket，修掉遮灯物体漂浮在灯上方的问题。

实时平面反射已通过真实 GameView 截图检查，反射中可以看到对面柜子、台灯和沙发。该结果只说明当前反射表现可见，不延伸为所有材质、镜头或全场景表现均已验收。

## 当前验证边界

本轮 14 项 EditMode 和 28 项 PlayMode 定向回归通过。最后的 D21 贴地和抢夺目标修复，又单独重跑 3 项生产房间集成测试，全部通过，其中检查了实际髋部落地高度以及拾枪进入 D22。结果保存在 `Logs/mv_repair_20260912/editmode_final.json`、`playmode_final.xml` 和 `d21_final.xml`。这些是本轮相关检查，不代表项目全部测试或 D01–D22 全路线自然通关。

实际 Input System 输入验证包括：短 E 关台灯、长 E 拿灯断线、长 E 放下；长 E 从床上拖被，S 移动时布料跟随地面、速度倍率为 0.5；短 E 放入盖灯插槽，灯色变暗红且不写入 PersistentBlack；黑暗和接触条件满足时，短 E 主动拉被进入 D21，倒地结束后开放 Grab 窗口。为稳定核对，部分步骤使用编辑器定位角色和冻结循环时间，不冒充一次连续自然游玩。

最后一次键盘输入实测中，D21 倒地时神秘人 Hips.y≈0.301，玩家恢复站立；短 E 拿起 `PRP_PistolMain`，loot 状态进入 `D22AwaitMainShot`。布本体和门口接触插槽都能提交拉拽，避免扫描到插槽时丢失同一操作。

当前人物皮肤、服装材质和部分道具仍能看出粗糙之处；实时镜面和可交互接线已经修复，美术与 MV 的逐帧匹配尚未验收。

收尾实测确认台灯和顶灯受到 Heavy 命中后均停止发光，完整模型隐藏、破损模型显示。最后一次控制台读取没有 error/warning。已退出 Play Mode，Room_M1 保存完成，并恢复测试前的 save.json 和 save.json.bak；测试添加的虚拟键盘已移除。

## 实际游戏截图

以下均来自 Unity GameView。房间图使用最终环境光设置，正常开局唤醒流程结束后拍摄；盖灯图展示通过实际 E 键交互得到的状态。

- [最终房间全景](../Logs/mv_repair_20260912/13_room_final.png)
- [拖被盖灯后的房间](../Logs/mv_repair_20260912/14_room_draped_final.png)
- [第一人称镜面反射](../Logs/mv_repair_20260912/15_mirror_final.png)
- [D21 倒地与选择窗口](../Logs/mv_repair_20260912/11_d21_grounded_final.png)（角色贴地验证；拍摄时间早于最后的环境光调整）

## 新建模引用复核与人物材质修正

用户追问实机是否使用新版模型后，直接读取 Room_M1 的 Renderer/Mesh 引用，并核对 art/exports 与 Assets/_Game/Art 的对应 FBX。房间、家具、被子、灯具等主要资源已使用当前导出的新版模型。人物引用 CH_Player_rigged.fbx 和 CH_Killer_rigged.fbx，但人物本体不属于 v003 的 38 项环境/道具更新。PRP_KillerCoat 仍是可见默认网格占位，不能据此宣称全部资源已替换完。

这次还定位了人物斑驳明暗的具体导入错误：两份源角色 GLB 的第 1 号纹理均声明为 metallicRoughnessTexture，没有 normalTexture；ConGameCharacterInstaller 却将其设置为 NormalMap 并绑定到 _BumpMap。现已将该纹理恢复为线性普通数据纹理，移除两个角色材质中的错误法线绑定，并同步修正导入脚本以防下次安装复现。现有非金属材质与粗糙度常量继续使用，未新增纹理重建流程。

再次进入正常开局后核对，原有脸部和服装黑白斑驳已经消除；运行中两个人物的 Mesh 仍指向上述 FBX，错误法线绑定为空，控制台无错误。当前效果见 [人物材质修正后的实机房间](../Logs/model_reference_audit_20260912/room_character_material_fixed.png)。本次验证结束后已退出 Play Mode 并恢复本次检查前的存档。

随后根据用户提供的 Blender 房间图进行了完整表现核查，确认此前的模型引用检查不足以说明美术迁移完成。主要新版网格已接入，但软包面的背面剔除、旧地毯材质、缺失阴影及不同相机参数会造成明显差异。具体结论见 [美术迁移核查](art_migration_verification_2026-09-12.md)。
