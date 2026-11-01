# 《困室》非美术运行时缺口

> 基线：2026-08-16。本文只记录程序、玩法状态、事件契约、运行时编排和验证缺口。需要美术生产的内容见 `art/requirements/remaining_art_deliverables_2026-08-16.md`。

## P0：阻塞正确玩法或演出

### 1. 易碎物权威状态未闭环

木椅、花瓶、镜子和挂钟已经能根据 `StatefulWorldItem.Broken` 切换模型，但当前正常玩法没有调用其 `MarkBroken()`。花瓶落地自碎、巨响、碎片减速区，镜子破碎后禁用 D17 反射，挂钟破坏后停止滴答也尚未实现。

对应规范：`gameplay_logic_spec.md` §5.3、`items_interactions.md` 物品 19/22/23。

验收：有效挥击或碰撞只提交一次破坏；状态、音效、碎片区和 LoopReset 同源恢复。

### 2. Pickup/PutDown/Throw 缺事务语义

`PlayerInteractor` 当前立即 Attach、Detach 或释放刚体，没有 `Started → Attach/DetachRequested → Completed/Cancelled` 契约。直接播放已有动画会出现物件先飞、手臂后挥，或动作被重置后物件遗留。

对应规范：`gameplay_logic_spec.md` §5.2、`technical_architecture.md` §6/§7。

验收：动画不存在时有定时回退；取消、死亡和 LoopReset 能原子恢复物件所有权。

### 3. live hunt 与正式 Performance 入口断开

正常 `LoopDirector` 进入 live hunt 后，Reactive/Continuous 结果主要更新战术意图；现有 `M1GreyboxPerformancePlayer` 的动画资源绑定并不等于正常路线会启动 Performance。必须区分“候选反制”和“已确认进入演出”。

对应规范：`gameplay_logic_spec.md` §4.1/§7/§8、`technical_architecture.md` §5。

验收：只有已确认接触、陷阱或攻击提交才能锁入 Performance；普通搜索和瞄准不得被错误吸附到演出锚点。

### 4. 踹门动画与门状态不同步

当前破门流程先把门打开，再开始神秘人入场；`AN_Killer_KickDoor` 即使播放，也不能驱动真实门接触帧。

对应规范：`gameplay_logic_spec.md` §2/§4。

验收：`KickWindup → KickContact → DoorOpen`，无动画时保留确定性的定时回退。

### 5. D08/D11/D21 缺双人编排契约

现有 PlayableGraph 能同步输出两条动画，但没有专用双方 Anchor、启动误差校验、同帧锁根、接触/脱离标记、取消清理和超差回退。D21 还缺 Fall、PAIR、挡光、掉枪/灯和备用枪反杀的阶段编排。

对应规范：`gameplay_logic_spec.md` §8/§12、`technical_architecture.md` §6/§10。

验收：启动误差超过 `0.15m/10°` 时禁止播放并回退 D01；中断和 LoopReset 无残留。

### 6. 枪械视觉所有权与显隐未闭环

黑衣人右手主枪和枪轨手电目前是始终显示的纯视觉副本；D21/D22 掉落由另一组玩法对象负责。尚需在权威掉落/拾取阶段切换角色视觉，避免掉落后手上与地上同时出现两份枪灯。

对应规范：`gameplay_logic_spec.md` §5.3/§8。

验收：角色视觉、玩法物件和事实状态始终只有一个权威持有者；LoopReset 恢复枪挂状态。

### 7. Aim/Fire 表现与权威射击时序不一致

同一个 `AN_Killer_AimFire` 同时承担抬枪、保持和开火，AimStart 即触发整段动作；实际 `ShotFired` 由独立状态机提交，E3 还存在重复调用表现入口的风险。

对应规范：`gameplay_logic_spec.md` §4.1、`technical_architecture.md` §5。

验收：Aim 可取消；只有权威 `ShotFired` 触发 Fire、枪口 VFX/SFX，`ShotResolved` 决定命中。

## P1：表现系统与完整性

### 8. VFX/音频语义、池化与重置

专用 VFX 到货后仍需统一消费 `ImpactResolved`、`ShotFired`、破坏状态和 Performance 语义轨；临时对象需要池化并纳入 `ILoopResettable`。已有多条 SFX 仍缺运行时语义触发。

对应规范：`technical_architecture.md` §7/§9、`gameplay_logic_spec.md` §11。

### 9. 镜面运行时系统

镜面目前有反射判定但没有独立低分辨率 Camera/RenderTexture 的可见性更新、破坏停更和循环恢复。

对应规范：`technical_architecture.md` §9、`items_interactions.md` 物品 23。

### 10. Animation Rigging 与 Ragdoll

仍缺上身持枪层、Look/Aim Rig、双手持物 IK、脚底约束和玩家 Ragdoll 接管。它们是 Unity 运行时系统，不应要求动画团队为每件家具复制整条动作。

对应规范：`technical_architecture.md` §9、`art/requirements/animation_manifest.md` §7。

### 11. 正式结局运行时编排

E2 第一人称控制、倒地、黑场与 E3 坐椅、秒针、备用枪、标题需要统一阶段时间线和取消边界；不能只靠分散 Transform 插值。

对应规范：`gameplay_logic_spec.md` §10、`technical_architecture.md` §5/§7。

### 12. 验证器与回归覆盖

需要验证 Animation Event/语义绑定、双人 Anchor、武器唯一所有权、可变视觉 Reset、镜面停更和正式 Performance 可达性。现有测试已覆盖基础 Presenter、房间集成和 D21/D22 玩法，但未覆盖上述完整演出闭环。

对应规范：`technical_architecture.md` §10。

## 实施顺序

1. 易碎物状态与交互事务。
2. 武器所有权、Aim/Fire 和踹门同步。
3. live hunt 到确认型 Performance 的入口。
4. D08、D11、D21 锚点和阶段编排。
5. 镜面、Rig、VFX/音频池化和 E2/E3。
6. 验证器与完整路线回归。
