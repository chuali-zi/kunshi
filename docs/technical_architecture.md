# 《困室》Unity 技术架构

> 本文定义代码边界、运行时所有权和扩展规则。玩法行为以 [gameplay_logic_spec.md](gameplay_logic_spec.md) 为准；演出内容以 [game_flow_script.md](game_flow_script.md) 为准。

## 1. 技术基线

| 项目 | 决策 |
|---|---|
| Unity | `6000.4.6f1`，与当前工作站 Editor 对齐并锁定补丁版本 |
| 目标平台 | Windows x64，DX11，开发版 Mono，发布版 IL2CPP |
| 渲染 | URP 17.4，Forward，HDR，固定 1080p/60 FPS 基线 |
| 输入 | Input System 1.19，键鼠与 Xbox 布局手柄 |
| 相机 | Cinemachine 2.10；常规固定机位与结局第一人称硬切 |
| 动画 | Animator、Timeline、Animation Rigging；配对演出使用统一锚点 |
| 寻路 | AI Navigation 2.0；神秘人只在演出锚点间使用 NavMeshAgent |
| 测试 | Unity Test Framework；Domain 用 EditMode，场景契约用 PlayMode |
| 存档 | 版本化 JSON，单存档，临时文件替换与 `.bak` 回退 |

不使用行为树、ECS、Addressables、运行时资源下载、第三方 DI、Tween 或异步框架。游戏只有一个小房间，复杂框架不能带来相称收益。

## 2. 工程与程序集边界

```text
Assets/_Game/
├─ Domain/          # 纯 C#，不引用 UnityEngine
├─ Runtime/         # 玩家、交互、循环、事实、AI 与存档适配
├─ Presentation/    # Camera、Timeline、动画、音频、VFX、后处理
├─ Bootstrap/       # 场景入口与依赖装配
├─ Editor/          # 灰盒生成、配置生成、项目验证
├─ Content/         # ScriptableObject、Timeline、输入与渲染配置
├─ Scenes/          # Bootstrap、MainMenu、Room_M1
├─ Art/             # 经 art/exports 验收后导入的运行时资产
└─ Tests/           # EditMode / PlayMode
```

依赖方向固定为：

```text
Domain ← Runtime ← Presentation
   ↑         ↑           ↑
   └──────── Bootstrap ──┘
```

- `Domain` 不出现 `MonoBehaviour`、`Vector3`、`Transform`、`ScriptableObject`。
- `Runtime` 不直接依赖具体 Timeline 或正式美术资产。
- `Presentation` 通过事件和语义角色绑定消费 Runtime 状态，不反向决定玩法结果。
- `Bootstrap` 是唯一允许同时认识多个子系统并执行装配的层。
- Editor 和 Tests 可以引用运行时程序集，发布构建不包含它们。

## 3. 场景生命周期与所有权

### 3.1 场景

| 场景 | 生命周期 | 职责 |
|---|---|---|
| `Bootstrap` | 进程级 | 创建持久根节点、加载设置/存档、切到菜单 |
| `MainMenu` | 可卸载 | 新游戏、继续、设置、退出；UI 只存在于此类非玩法场景 |
| `Room_M1` / `Room` | 一次加载，多次循环 | 房间、角色、LoopDirector、事实、反制、演出与结局 |

死亡不得重载 `Room`。E1–E3 也在同一 Room 场景中切换专用预设，以维持“打破观看层”的连续性。

### 3.2 单一状态权威

`GameFlowController` 是唯一可以提交顶层阶段变更的对象：

```text
Boot → Menu → Awakening → FreeRoam → Breach → Counterplay
                                  ↘ EndingE1 → EndingE2 → EndingE3 → Finished
Counterplay → KillImpact → LoopReset → Awakening
```

所有系统只能提交 `FlowTransitionRequest`，不能自行修改阶段。请求带优先级：

1. `EndingCommitted`：电视第二次有效重击；最高。
2. `Death`：枪击/处决命中。
3. `Normal`：苏醒、入场、演出结束等普通推进。

同一帧只提交最高优先级请求；同级使用最早请求。这样电视第二击与枪击同帧发生时，结局稳定胜出。

### 3.3 服务装配

`RoomContext` 在场景启动时显式装配：

- `GameFlowController`
- `GameplayFactService`
- `GameplayEventHub`
- `LoopResetRegistry`
- `LoopDirector`
- `PlayerInputRouter` / `PlayerMotor` / `PlayerInteractor`
- `KillerDirector`
- `CameraModeController`
- `JsonSaveStore`

禁止运行时使用 `FindObjectOfType` 或全局 Service Locator。场景生成器负责序列化引用；测试可以调用公开的 `Configure/Initialize` 方法装配最小对象图。

## 4. 公共契约

以下名称与语义是稳定 API；具体表示可以在不改变语义的前提下扩展。

### 4.1 循环重置

```csharp
public interface ILoopResettable
{
    int ResetOrder { get; }
    void CaptureInitialState();
    void Quiesce(in LoopResetContext context);
    void Restore(in LoopResetContext context);
    void Resume(in LoopResetContext context);
}
```

- `CaptureInitialState` 每次 Room 加载只调用一次。
- `Quiesce` 停止导航、Timeline、协程、刚体和输入副作用。
- `Restore` 恢复父子关系、Transform、组件状态和本地数据。
- `Resume` 在 `Physics.SyncTransforms` 后恢复可运行状态。
- `ResetOrder` 从系统到物体再到角色排序；相同值以注册顺序稳定执行。

### 4.2 交互与命中

```csharp
public interface IInteractionCapability
{
    InteractionVerb Verb { get; }
    int Priority { get; }
    bool CanExecute(in InteractionContext context);
    InteractionResult Execute(in InteractionContext context);
}

public interface IHitReceiver
{
    HitResult ReceiveHit(in HitInfo hit);
}

public interface IFlashlightReactive
{
    void React(in FlashlightHit hit);
}
```

`InteractableEntity` 只保存稳定 `ItemId`、类别和能力集合。能力之间不得持有具体物品类引用。组合交互通过 `ItemCategory`、`GameplayTagId` 与 `PlacementSocket` 的接受条件建立。

### 4.3 事实与反制

```csharp
public interface IGameplayFacts
{
    FactSnapshot CaptureSnapshot();
}

public interface ISequenceRunner
{
    SequenceHandle Play(PerformanceDefinition definition, SequenceBindings bindings);
    void Cancel(SequenceHandle handle);
    void CancelAllForLoop(int loopEpoch);
}
```

- `FactSnapshot` 是某一时刻的值拷贝；反制选择期间不可被中途修改。
- `KillerStimulus` 表达只存在一帧或一次调用的事件，例如投掷物飞来、拉动陷阱、光线照脸、攻击电视。
- `CounterplayDefinition` 包含稳定 ID、排序键、触发模式、条件数组、演出引用、结果 ID 与内容状态。
- 新死法使用已有事实时只添加数据和演出；只有出现新的世界语义时才允许扩展 `FactKey`。

## 5. 交互架构

### 5.1 目标选择

`InteractionScanner` 使用胸前 `OverlapSphereNonAlloc`，默认半径 `1.2m`：

1. 排除禁用对象、玩家自身和不支持当前上下文的能力。
2. 计算距离、玩家朝向点积和能力优先级。
3. 保留分数最高的一个 `InteractableEntity`。
4. 焦点只驱动角色头部 IK，不产生高亮、文字或图标。

第三人称没有鼠标准星；投掷、挥击和交互都使用角色朝向。第一人称结局才使用相机中心射线。

### 5.2 物品物理状态

```text
Placed ↔ Held → Flying → Placed
```

- Held：挂到手部/双手 socket，刚体 kinematic，忽略玩家碰撞。
- Flying：解除父级，恢复碰撞并施加速度；至少等一个 FixedUpdate 才能重新与玩家碰撞。
- Reset：先禁用模拟，再恢复父级和初始姿态，速度归零，最后恢复初始刚体模式。
- 破碎物、Decal 和临时 VFX 使用本轮池，不无限实例化。

## 6. 神秘人导演与演出

`KillerDirector` 只有以下高层状态：

```text
Dormant → Entering → Perceiving → Navigating → PerformanceLocked → Dormant
```

- `NavMeshAgent` 只负责走到演出锚点；开枪、闪避、踹门、搏斗和处决由 Timeline/Animator 完成。
- 进入 `PerformanceLocked` 后停止 Agent 并把双方对齐到 `PerformanceAnchor`。
- Timeline 轨道不按名称猜测绑定，使用 `BindingRole` 显式映射 Player、Killer、MainPistol、Flashlight、Door、Camera 等角色。
- `KillImpact`、`Attach`、`Detach`、`HitStart`、`HitEnd` 和输入对抗窗口使用标记/信号，不依赖动画长度硬编码。
- 普通演出锁定后不再重选反制；`EndingCommitted` 可取消一切普通演出。

## 7. 循环重置与异步安全

每轮拥有递增 `loopEpoch`。协程、延时调用、Timeline 信号和物理回调在执行前必须检查创建时 epoch；不匹配立即丢弃。

击杀命中后的同帧重置顺序：

1. `GameFlowController` 接受 Death，请求锁输入。
2. Registry 按序执行所有 `Quiesce`。
3. 取消本轮 Timeline、导航路径、协程与延时动作。
4. 回收本轮生成物并清空瞬时刺激。
5. Registry 执行 `Restore`，所有刚体速度归零。
6. 调用 `Physics.SyncTransforms`。
7. Registry 执行 `Resume`，重置事实、计时、相机和角色。
8. 循环号加一并保存跨循环状态。
9. 提交 `Awakening`，恢复输入；苏醒动画可被任何有效输入打断。

目标是从 `KillImpact` 到下一轮第一次可读输入不超过 `0.5s`，正常机器上为一个渲染帧。

## 8. 存档边界

`PersistentProgressV1` 仅包含：

- `schemaVersion`
- `loopIndex`
- `darkAdaptationXp`（0–5）
- `d21Seen`
- `tvCrackEchoPending`
- `endingCompleted`

位置、灯光、门、物品、玩家姿态、当前反制、QTE、剩余子弹等都不持久化。每次获得黑暗经验、完成 D21、设置裂纹回声、循环递增和完成结局时保存。

写入使用 `save.tmp → save.json` 替换；替换前保留 `save.bak`。主文件解析失败时读取备份，两者都失败则创建新存档并记录错误，不阻塞进入菜单。

## 9. 表现层约束

- 常规输出相机固定在电视屏幕位置，常规阶段位置和旋转不得由任何运行时系统修改。
- Cinemachine 默认 Blend 为 Cut；E1 第二击后切第一人称也是零时长切换。
- CRT 扫描线、暗角、雪花和全屏裂纹使用 URP Full Screen Pass；正式 Shader 未完成前允许透明材质占位。
- 电视当前画面闪帧读取上一帧捕获，避免读写同一 RenderTexture 造成反馈未定义行为。
- 镜子使用独立低分辨率相机/RenderTexture；只在可见时更新。
- 战术手电使用 Spot Light、可见光锥网格与碰撞锥；命中判断不做逐像素光学模拟。
- 开发事实查看器仅在 Editor 或 `DEVELOPMENT_BUILD` 中存在，Release 玩法场景不得有 HUD Canvas。

## 10. 内容验证与完成定义

Editor 验证器必须报告：

- 重复或空的 Item/Outcome/Counterplay ID。
- 反制排序键冲突、缺失兜底 D01、已标记 Implemented 却缺演出引用。
- Timeline 缺语义绑定、配对演出缺锚点。
- 可变物体未实现 `ILoopResettable`。
- 固定相机与电视屏幕锚点偏差超过 `0.02m`。
- Build Settings 场景顺序错误。
- `Specified` 或 `Stub` 内容进入 Release 内容清单。

一项内容只有同时满足“规则测试通过、场景可稳定触发、重置无残留、演出锚点验收”才能标记 `Implemented`。
