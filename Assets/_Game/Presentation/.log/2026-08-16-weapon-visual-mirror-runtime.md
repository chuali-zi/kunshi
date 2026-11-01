# 工作日志 2026-08-16：武器视觉所有权 + 镜面运行时

补齐非美术运行时缺口 #6（枪械视觉所有权与显隐闭环）与 #9（镜面运行时系统）。

## 缺口 6

- 新增 `Presentation/Props/WeaponVisualOwnershipModel.cs`：纯 C# 唯一所有权状态机
  （CharacterVisual / GameplayObject / Transitioning），两阶段转移 + 同帧权威切换 + Capture/Reset。
- 新增 `Presentation/Props/KillerWeaponVisualPresenter.cs`：管理主枪、枪轨手电、枪套、备用枪
  四个视觉副本的显隐；手电挂枪轨，主枪离手时随之隐藏；备用枪默认隐藏，仅演出绑定可见。
  实现 ILoopResettable（ResetOrder 96），LoopReset 恢复初始枪挂状态。
- 新增 `Presentation/Props/KillerWeaponVisualEventBridge.cs`：消费
  `CounterplayPerformanceRuntimeAdapter.PerformanceStarted/BranchResolved` 与掉落物
  `StatefulWorldItem.StateChanged`（D08 disarm_window、D21 掉枪/掉灯、D22、玩家拾取），
  转发给 Presenter 契约入口。未修改 Runtime/Interaction 任何文件。

## 缺口 9

- 新增 `Presentation/Mirror/MirrorUpdateStateMachine.cs`：纯 C# 更新决策状态机
  （Active / Hidden / Broken，破碎优先于可见性）。
- 新增 `Presentation/Mirror/MirrorReflectionController.cs`：独立低分辨率 Camera +
  RenderTexture（256² 按需创建一次、OnDestroy 释放，不每帧重建）；固定机位朝向 +
  视锥 AABB 可见性判断，仅可见帧手动 `Camera.Render()`（GL.invertCulling 修正镜像绕序）；
  消费 `StatefulWorldItem.StateChanged == Broken`（或 `NotifyBroken()`）停更并切破裂占位材质；
  ILoopResettable 恢复更新与完整材质。

## 测试与验证

- 新增 EditMode 测试 `WeaponVisualOwnershipModelTests`、`MirrorUpdateStateMachineTests`；
  `ConGame.Tests.EditMode.asmdef` 增加 `ConGame.Presentation` 引用。
- 未运行 Unity：使用 Unity 6000.4.6f1 自带 Roslyn + Library/ScriptAssemblies 引用对全部
  新增文件离线编译校验，零错误零警告。

## 遗留集成

- 场景装配（Bootstrap/场景生成器）需序列化/Configure：Killer 装备视觉副本 →
  KillerWeaponVisualPresenter；adapter 与掉落物 → KillerWeaponVisualEventBridge；
  固定机位/镜面 Renderer/破裂占位材质/mirror 的 StatefulWorldItem → MirrorReflectionController；
  三者注册进 LoopResetRegistry。
- StolenLootDropController 若要表达"过渡中"（掉落动画接触帧），可改用
  `Model.TryBeginTransfer/CompleteTransfer` 两阶段契约，当前桥接为同帧切换。
- 镜面 RenderTexture 镜像 UV 翻转依赖材质/shader 侧处理（占位材质可接受）；
  斜近裁剪面（oblique near-plane）可按需追加。
