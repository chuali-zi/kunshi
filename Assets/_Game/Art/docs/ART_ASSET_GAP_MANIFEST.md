# 《困室》缺失美术资源清单

> 更新：2026-08-16。文件存在不等于已接入；允许灰盒不等于最终完成。美术生产的权威剩余清单位于 `art/requirements/remaining_art_deliverables_2026-08-16.md`，非美术运行时缺口位于 `docs/non_art_runtime_gaps_2026-08-16.md`；本文保留 Unity 侧快速索引。

## P0 动画与绑定

| 资源 | 当前灰盒/现状 | 需要补交 | 验收要点 |
|---|---|---|---|
| `AN_Player_PutDownLarge` | ~~物件直接稳定落地~~ **已交付 2026-08-16**(NIUA text2motion，待验收） | — | provisional `Detach` 事件已注入（1.75s），待验收校准 |
| Killer Aim 拆分 | ~~`AN_Killer_AimFire` 同时承担瞄准和开枪~~ **已交付 2026-08-16**:`AimEnter`(1.0s)、`AimHold`(3s 循环）、`Fire`(1.0s)，待验收 | — | AimEnter/Fire 时长受接口下限限制，可用播放速度压缩；`AimHold` 可稳定冻结待验收；`AN_Killer_AimFire` 过渡期 provisional `AimHold`/`Fire`/`Muzzle` 事件已注入 |
| Killer 搜索表现 | ~~Idle/Walk + 程序化转身~~ **已交付 2026-08-16**:`AN_Killer_SearchLoop`(4s 循环），待验收 | — | 上身叠加行走仍需程序 Avatar Mask/上身层（见下行动画 Rig） |
| Killer 跑动 | ~~加速播放 Walk~~ **已交付 2026-08-16**:`AN_Killer_Run`(1.2s 循环），待验收 | — | 5.5m/s 步频匹配与滑步待固定机位验收 |
| 装备切换语义 | 主枪与枪挂手电已作为纯视觉副本绑定右手和枪轨；D21/D22 掉落物保持独立 | ~~Attach/Detach/Muzzle/Fire 事件及显隐状态~~ provisional `Attach`/`Detach`/`Muzzle`/`Fire` 事件已注入（2026-08-16），显隐状态仍待程序接入 | Aim/Fire 不空手，D21 掉枪掉灯帧准确且无双份视觉 |
| 动画 Rig | 目前无 Avatar Mask/持物 IK 资产 | 上身持枪层、双手大物件约束、Look/Aim Rig | 行走、搜索、搬运时手臂不穿模 |

## P1 状态表现与 VFX

| 资源 | 当前灰盒/现状 | 需要补交 |
|---|---|---|
| 木椅破坏反馈 | 完整/破损 Presenter 与循环重置已接；玩法尚未提交 `MarkBroken()` | 木屑、落地碎件及权威破坏触发 |
| 被子状态反馈 | Crumpled/Covered/FlatTrap/Draped 四态 Presenter 已接 | Draped 放置插槽与状态过渡布料动作 |
| 花瓶/镜子/挂钟 | 完整/破坏 Presenter 与循环重置已接；玩法破坏状态尚未闭环 | 碎片、音画反馈及各自权威破坏触发 |
| 封窗/墙损 | 逐板破坏与 0–3 档墙损已监听权威命中事件并参与循环重置 | 木屑、灰尘和持久 Decal |
| 枪击 | 仅动画/程序化反馈 | 枪口火光、烟、命中火花/血迹和近距离枪响同步标记 |
| 顶灯/台灯破碎 | 完整/破碎模型已接 | 差异化玻璃碎屑、坠落粒子和闪灭瞬态 |
| 战术手电 | Spot Light 可工作 | 可见光锥网格/假体积光，黑暗扫描时不过曝 |
| 镜面 | 灰色表面/逻辑射线 | 低分辨率 RenderTexture 材质与破裂版本 |
| 电视结局 | 三态模型已接 | 屏幕炸裂碎片、白噪爆闪和 E2 手部玻璃残屑 |

## P1 正式演出

| 资源组 | 当前状态 | 必须后补 |
|---|---|---|
| D08 / D11 / D21 | 双人动画 FBX 已有，仍由 Greybox Performance 驱动 | 正式配对 Timeline、物件轨、相机/音频/Attach/Detach 标记 |
| D01–D22 | 多数结果复用少量动画和根位移 | 按 `docs/game_flow_script.md` 的差异化节拍补正式 Performance |
| E2 / E3 | 程序化倒地、入场和标题 | E2 第一人称倒地相机演出；E3 枪口对镜头与挂钟越格同步 |

## 2026-08-16 管线交付更新

- 新动画 6 条：`AN_Player_PutDownLarge`、`AN_Killer_AimEnter`/`AimHold`/`Fire`、`AN_Killer_SearchLoop`、`AN_Killer_Run`，Humanoid meta 已配，循环剪辑 Loop Time 已勾。交付记录:`art/.log/2026-08-16_remaining_animations.md`。
- 16 个剪辑注入 provisional Animation Events（共 46 个，含 PAIR 双边起点/接触/脱离与 D21 接续点），时间与理由:`art/.log/2026-08-16_animation_events_provisional.md`。**全部待动画/演出验收校准；代码侧尚无事件消费端，函数名清单待程序对齐。**
- 其余动画事件/验收项：`AN_Player_Swing` HitStart/HitEnd、`Pickup` Attach/Detach、`Throw` Detach、`KickDoor` KickContact、`Stumble` 轻/重区段+恢复、`BlockLight` BlockStart/HardStunEnd、`Fall` 落地+PAIR 起始均已注入。

## 已有，不要重复制作

- `CH_Player_rigged`、`CH_Killer_rigged`、房间壳体、床、木椅、书柜、抽屉柜、电视柜和主要道具模型。
- 主枪、备用枪、战术手电、枪套，以及 D08/D11/D21 双人动画文件。
- 电视三态、顶灯/台灯完整与破坏态、被子四态、花瓶/镜子/挂钟破坏态。
- `AN_Player_Climb` 已由攀爬玩法事件驱动并接入 `AC_Player`；主枪、枪轨手电、枪套和备用枪已完成骨骼/Socket 视觉绑定。
- 木椅、花瓶、镜子、挂钟的完整/破坏视觉 Presenter，以及封窗逐板和墙损分档 Presenter。

## 当前灰盒约束

- 攀爬逻辑和 `AN_Player_Climb` 已接入；仍需固定机位验收脚底接触与滑步。
- AI 搜索、接近、瞄准取消与开枪由代码状态机决定，美术事件只能表现，不得直接提交死亡。
- 所有临时粒子、Primitive 光锥和程序化受击都必须参与循环重置，不能跨循环残留。
