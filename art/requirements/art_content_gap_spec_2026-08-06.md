# 《困室》美术、动画与表现缺口交付规格

> 日期：2026-08-06  
> 来源：完整性审计、`asset_manifest.md`、`animation_manifest.md`、Room_M1 场景与运行时代码。  
> 目的：区分“资产没做”“资产已有但没接”“运行时系统没做”，避免重复生产模型，也避免把未接入资产误报为完成。

## 1. 结论与边界

当前环境/家具/主要道具 FBX 大多已经存在；主要瓶颈不是继续批量建模，而是 **状态变体接入、角色表演、Timeline/IK/VFX 和音效触发**。

交付状态统一使用：

- `缺资产`：仓库内没有可用源资产，需要美术/动画新增。
- `待验收`：文件存在，但尚未通过骨架、比例、材质、镜头或动作验收。
- `待接入`：资源已经存在，Unity 状态机/Timeline/Prefab 没有引用它。
- `缺系统`：仅补资源无效，需要程序先提供状态、事件或绑定接口。

## 2. P0 角色与动画

### 2.1 已有但待验收/接入

| ID | 现有文件 | 当前状态 | 必须验收 |
|---|---|---|---|
| CH_Player | `Art/Characters/CH_Player_rigged.fbx` + 2 张贴图 | 待验收，场景已挂 | Humanoid Avatar、眼镜/发型轮廓、脚底接地、固定机位 LOD、E2 近景穿插 |
| CH_Killer | `Art/Characters/CH_Killer_rigged.fbx` + 2 张贴图 | 待验收，场景已挂 | Humanoid Avatar、风衣轮廓、枪/灯 socket、全黑下可读性、步行动作滑步 |
| AN_Player_Idle | `AN_Player_Idle.fbx` | 待验收，已进 Animator | 循环接缝、脚锁、上身呼吸幅度 |
| AN_Player_Walk | `AN_Player_Walk.fbx` | 待验收，已进 Animator | In-place、2.8/4.5m/s 速度映射、转向无滑步 |
| AN_Player_WakeUp | `AN_Player_WakeUp.fbx` | 待验收，已进 Animator | 与北墙木椅姿态对齐、可被首个有效输入打断 |
| AN_Player_Swing | `AN_Player_Swing.fbx` | 待验收，已进 Animator | 双手 IK、球棒/木椅复用、`HitStart/HitEnd` 事件与实际 Collider 帧对齐 |
| AN_Player_HitFall | `AN_Player_HitFall.fbx` | 待验收，已进 Animator | 命中反应 ≤0.5 秒、Ragdoll 接管无跳帧；目前普通死亡时序无法显示它 |
| AN_Killer_Walk | `AN_Killer_Walk.fbx` | 待验收，已进 Animator | In-place、NavMesh 速度、门内转身和搜索路径 |
| AN_Killer_AimFire | `AN_Killer_AimFire.fbx` | 待验收，已进 Animator/E3 显式触发 | 枪口/手 IK、Aim Rig 高度、开火事件、后坐和保持瞄准 |

注意：以上 7 条动画文件存在不代表 D01/D03/D07 已完成。普通死亡的 `M1GreyboxPerformancePlayer` 不驱动这些动作，`KillImpact` 又会立即重置；必须把动画事件接入正式表现时间轴。

### 2.2 缺失的 P1 动画资产

下列内容在 `animation_manifest.md` 中均为“未开始”，仓库中没有对应 FBX/clip：

| 优先顺序 | ID | 服务路线 | 交付要求 |
|---:|---|---|---|
| 1 | PAIR_D08_AmbushGrapple | D08 | 玩家/神秘人同步双轨；偷袭命中→拉扯/翻滚→膝顶脱身；提供双方起点、朝向、距离标记 |
| 2 | PAIR_D11_BedDrag | D11 | 抓脚踝后 2–3 秒同步拖拽；床边/床底锚点与手脚接触标记 |
| 3 | PAIR_D21_GroundStruggle | D21 | 8–12 秒同步动作，可循环中段；摔倒终点必须无缝接入，提供枪/灯 Detach 标记 |
| 4 | AN_Killer_DrawBackupFire | D21/D22/E3 | 风衣内拔备用枪→瞄准→开火；Attach、Muzzle、Fire 三标记 |
| 5 | AN_Killer_DodgeSide | D10/D22 | 一侧动作可镜像；需要明确起止位移和无敌/判定窗口 |
| 6 | AN_Killer_Fall | D21 | 向前摔倒，结束 Pose 对齐 PAIR_D21 |
| 7 | AN_Killer_Stumble | D08/D16 | 同一动作提供轻/重播放区段，D16 结束为单膝恢复 |
| 8 | AN_Killer_BlockLight | D17/D21 | 上半身蒙太奇；可叠加站立/单膝；挡光开始与硬直结束标记 |
| 9 | AN_Killer_KickDoor | D09 | 单脚踹击；接触帧对齐门板 Timeline |
| 10 | AN_Player_Pickup | 通用互动 | 正放拾取、倒放放下；Attach/Detach 标记和手部 IK |
| 11 | AN_Player_Throw | D10/通用互动 | 通用单手过肩投掷；Detach 帧必须驱动真实刚体 |
| 12 | CAM_E2_FirstPersonFall | E2 | Unity Timeline 相机曲线；约 1.2 秒倒向地面，不做晕动式旋转 |

`AN_Player_CrouchMove` 继续保持“待评估”：先完成程序化蹲姿和 IK 试玩，只有轮廓或滑步不可接受时再生产。

### 2.3 缺失的 Pose、Rig 和 Ragdoll 层

仓库没有可验收的独立 Pose/Rig 交付记录。至少需要：

- 玩家：SitChair、CrouchHide、Prone、KneelHandsUp、HoldSmall_R、HoldBat、CarryLarge、AimPistol。
- 神秘人：LowReady、Kneel。
- 通用：玩家/神秘人 Humanoid Avatar Mask；头/胸 Look IK；双手持物 IK；枪口 Aim Rig；脚底约束。
- 玩家 Ragdoll：头/胸/背三种命中只改目标与初始力，避免制作 22 条倒地动画。

验收必须在 Unity 的固定机位、全黑和 E2 第一人称三种条件下进行，不能只看 DCC 预览。

## 3. 缺资产与角色装备

| ID | 状态 | 缺口 | 验收标准 |
|---|---|---|---|
| CH_Killer_Coat | 缺资产 | 当前风衣烘进 CH_Killer；独立 `PRP_KillerCoat` 只是深色材质代理 | 独立蒙皮件、下摆 4–6 辅助骨、内侧枪套；D21/D22/E3 拉开衣襟和拔枪时不穿插 |
| InnerHolster | 缺资产/可并入 Coat | 主/备用枪没有可见枪套关系 | 固定机位可读出备用枪来源；提供 `SOCKET_BackupPistol` |

如果决定继续使用“风衣并入角色”的低成本方案，应正式删除独立风衣的可交互承诺，并为备用枪制作角色网格内的枪套/socket；不要同时保留两个互相矛盾的表现方案。

## 4. 已有模型变体但没有接入

这一组 **不需要重新建模**，需要程序/TA 建立 Visual State Presenter、Prefab 引用和 Reset：

| 对象 | 已有资源 | 当前问题 | 目标状态与切换事件 |
|---|---|---|---|
| CRT 电视 | Intact/Cracked/Destroyed 三 FBX | 场景只挂 Intact；`TelevisionController` 只更新事实，`CrtStatePresenter` 主要处理全屏材质 | DamageChanged：Intact→Cracked→Destroyed；三态同原点；Reset 保留/恢复由 E1 preset 决定 |
| 顶灯 | Intact/Broken 两 FBX | `BreakableLight.intactVisual`、`brokenVisual` 均为空 | `BrokenChanged` 切网格、玻璃碎片/VFX、Light 关闭、LoopReset 恢复 |
| 台灯 | Intact/Broken 两 FBX | 两视觉引用为空；Bright/Unplugged 无表现 | Off/Normal/Bright/Broken/HeldUnplugged；电线和灯罩状态可读 |
| 被子 | Crumpled/Covered/FlatTrap/Draped 四 FBX | 场景只实例化 Crumpled | Placed-on-bed→Crumpled，Hide→Covered，door socket→FlatTrap，罩灯→Draped；Dragged 可用形变/短动画 |
| 木椅 | Intact/Broken FBX | Broken 未挂 | 重击/踹门散架时切换；碎件碰撞受控；Reset 回完整态 |
| 封死窗 | ENV_BoardedWindow 内含完整/断板节点 | 安装器仅按前缀隐藏断板，没有逐板控制器 | 5Boards→4→3→2→1→Brick；每击只切一板，生成少量碎件并 Reset |
| 花瓶 | Intact/Broken FBX | Broken 未挂，且缺 Shatter 系统 | 首次有效撞击切换、6–10 碎片池、巨响、地面 2 秒跛行 Trigger、Reset |
| 镜子 | Intact/Broken FBX | Broken 未挂，且没有破坏接收器 | 重击切 Broken、镜裂 VFX/SFX、关闭 RT；Reset 恢复；D17 光反射只在 Intact 生效 |
| 挂钟 | Intact/Broken FBX | Broken 未挂；正式三根钟针未被运行时绑定 | 平时秒针固定抽搐；E3 首次越格；撞击后破坏；Reset/Ending preset 明确 |
| 墙损 | ENV_WallDamageKit | 没有 Presenter/次数 | 每面独立 0–3 档 Decal/平面破片；不打穿；每击重置入池 |

每个 Presenter 必须实现或由其宿主实现 `ILoopResettable`，并加入 50 连续循环测试。状态切换时不能销毁后重新 Find；应预挂引用或使用池。

## 5. 缺失的 Unity 表现系统

这些工作不能通过补 FBX 独立解决：

| 系统 | 当前 | 交付 |
|---|---|---|
| 正式 Death Performance | 23 个空 Greybox Playable | 每条路线引用正式 Timeline/组合定义；至少含 actor、anchor、camera、animation、item policy、audio/VFX 语义轨 |
| 双人锚点 | 无自动验证 | D08/D11/D21 双方 anchor，启动误差 ≤0.15m/10°；超限回退 D01 并记录错误 |
| 镜面反射 | 仅射线事实 | 独立低分辨率 Camera+RenderTexture，可见时更新；镜面能同时读到门和电视；破碎时停更 |
| 头部 Inspect | 玩家根节点瞬间转向 | 0.25 秒稳定焦点后头/胸 IK，非锁定、不中断移动、无 HUD |
| 手电体积光 | 有 Light/射线机械 | 可见锥体/VFX、枪挂与手持两 socket、黑暗中边界一致，0.8 秒覆盖计时可读 |
| 破坏 VFX | 基本没有 | 灯玻璃、花瓶、镜裂、窗板、木椅、墙屑、电视爆裂；统一池化并参与 Reset |
| 命中反馈 | 事实写入为主 | 球棒/椅子 hit stop、火花/碎屑、轻重声音差异、电视轻击 3 帧脉冲 |
| E2 相机 | Transform 插值 | 正式第一人称倒地 Timeline；4 秒自由控制与入场、死亡、3 秒黑场帧序确定 |
| E3 秒针/备用枪 | 可动态生成 primitive | 绑定正式秒针和枪资产/socket；标题前 5 秒黑屏与声音时间线 |

## 6. 音频：文件已有但触发点缺失

仓库有 18 个 WAV。`SemanticAudioDirector` 目前只有 8 个语义槽，已覆盖：TelevisionPower、EntryWarning、DoorBreach、Gunshot、LoopReset、TelevisionCrack、TelevisionDestroyed、EndingSilence。

以下 10 个 WAV 已存在但不在 `AudioCueId`/运行时触发中，不要重做音频文件，应先扩展语义事件：

| 文件 | 应绑定事件 | 空间/播放要求 |
|---|---|---|
| SFX_Bat_Impact | Melee 命中墙/窗/物品/神秘人 | 3D，一次命中一次，按材质可调音量 |
| SFX_Clock_Tick | 普通抽搐、E3 越格 | 挂钟 3D；E3 需在枪响前清楚可闻 |
| SFX_Cloth_Rustle | 被子拾取、铺门、拉拽、藏入 | 3D；Dragged 可循环短片段但退出必须停止 |
| SFX_Footstep_Bare_Wood | 玩家脚步 | Animation Event/距离节流；Walk/Sprint 不重叠 |
| SFX_Footstep_Shoes_Wood | 神秘人脚步/入场预警 | 3D；预警可用门外位置，不与 EntryWarning 双播失真 |
| SFX_Glass_Shatter | 顶灯/台灯/花瓶玻璃 | 3D；每个对象首次破坏一次 |
| SFX_Mirror_Crack | 镜面破坏 | 3D，配合镜裂/RT 停止 |
| SFX_TV_Static_Cut | 遥控闪帧、D21 后噪点变化或 E1 断讯 | 2D/电视源按镜头阶段切换，避免与 Destroyed 重复 |
| SFX_Window_Board_Break | 每条窗板断裂 | 3D，一击一板 |
| SFX_Wood_Splinter | 门踹、书柜/木椅/墙木件 | 3D，按事件来源节流 |

旧接入日志记录“11 个 SFX 触发点缺失”，比未绑定文件数多 1 是合理的：同一文件可服务多个不同事件。验收以触发事件覆盖而不是文件数量为准。

## 7. 路线到表现资产的最低交付映射

无需为 22 条死亡各制作独立倒地动画，但每条必须有可区分的构图/节拍：

| 复用组 | 路线 | 必须独有的内容 |
|---|---|---|
| 通用枪杀 | D01/D02/D03/D05/D06/D07/D12/D14/D15 | 目标高度、玩家 Pose、神秘人站位、停顿、命中方向与持物掉落 |
| 环境处决 | D04/D09/D13/D18 | 被子三枪、踹门/家具、门缝枪、调亮台灯四套环境时间线 |
| 反应闪避 | D10/D16/D17 | Dodge、Stumble、BlockLight 三个独有反应动作 |
| 黑暗搜索 | D19/D20 | 两套手电扫描路径：循声与优先盲区 |
| 配对演出 | D08/D11/D21 | 三组同步双人动画与各自道具绑定 |
| 夺枪 | D22 | 玩家 Aim/Fire、神秘人 Dodge、空仓、备用枪 |
| 结局 | E1/E2/E3 | 电视爆裂、第一人称倒地、坐椅+秒针+备用枪 |

完成一条路线的最低标准：正确规则可触发、进入锚点无跳变、动作/道具/音效/VFX 同帧、持物策略明确、结束后无残留、下一循环恢复、固定机位读得懂死法差异。

## 8. 交付与验收顺序

1. **技术基座**：正式 Performance 数据结构、语义轨道、锚点验证、Visual State Presenter、音频枚举和 Reset 接口。
2. **垂直切片**：D01 + D03/D03′ + D07，确认现有 P0 动画真正能在死亡链路播放。
3. **环境互动组**：电视/灯/被子/窗/墙/花瓶/镜/钟/椅全部状态接入。
4. **三组配对**：D08 → D11 → D21；每组完成后先做对位和重置压力测试。
5. **黑暗与夺枪**：D16–D22，补手电、镜面和备用枪层。
6. **结局**：E1–E3 正式镜头、音频和标题节奏。
7. **整体验收**：23 死法逐条录屏，50 循环压力测试，固定机位/黑暗/第一人称三种画质档验证。

不建议先继续增加 P2 小道具或材质细节；现阶段最能提高完整度的是把已有资产接到权威状态和表现链路中。
