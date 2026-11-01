# 2026-08-16 剩余动画补齐(NIUA text2motion ×6)

> 范围:`remaining_art_deliverables_2026-08-16.md` §1 中可由管线完成的动画缺口。
> 实际扣费:6 条 × 21¢ = 126¢(≈ $1.26,与授权预算一致)。

## 交付清单

| 动画 | 时长 | 循环 | 状态 | 说明 |
|---|---|---|---|---|
| `AN_Player_PutDownLarge` | 2.5s | 否 | 已生成/已转 FBX/已入 Unity | 双手放低大物件稳定落地再松手;含 provisional `Detach` 事件(见 events 日志) |
| `AN_Killer_AimEnter` | 1.0s | 否 | 已生成/已转 FBX/已入 Unity | **时长调整为 1.0s**:NIUA text2motion 接口限制 duration ∈ [1,10]s,需求 0.8s 不可达;Unity 侧可用播放速度压回 ~0.8s |
| `AN_Killer_AimHold` | 3.0s | 是 | 已生成/已转 FBX/已入 Unity | 提示词强调"almost perfectly still",可稳定冻结;meta `loopTime: 1` |
| `AN_Killer_Fire` | 1.0s | 否 | 已生成/已转 FBX/已入 Unity | **时长调整为 1.0s**:同 AimEnter(需求 0.6s 低于接口下限);开火事件点见 `AN_Killer_AimFire`/`DrawBackupFire` 的 provisional `Fire`/`Muzzle`,本条剪辑事件待演出验收后补 |
| `AN_Killer_SearchLoop` | 4.0s | 是 | 已生成/已转 FBX/已入 Unity | 低戒备持枪扫描,上身扫视;叠加行走需程序配 Avatar Mask/上身层(见 GAP manifest"动画 Rig"行);meta `loopTime: 1` |
| `AN_Killer_Run` | 1.2s | 是 | 已生成/已转 FBX/已入 Unity | 首次下载网络中断(IncompleteRead),`--force` 同幂等键重提交未重复扣费,第二次成功;步频/滑步待固定机位验收(5.5m/s);meta `loopTime: 1` |

## 管线动作

1. `manifests/animations.json` 追加 6 条(mixamo 骨架、In-Place)。
2. `gen_animations.py --dry-run` 确认 6 条 ~126¢ 后逐条 `--only` 生成,全部一次通过(Run 第一次仅下载失败)。
3. `blender -b -P convert_all_fbx.py` 转 FBX(Blender 5.1,增量转换)。
4. `import_to_unity.py`(ANIMATIONS 表同步追加 6 条)复制 FBX 到 `Assets/_Game/Art/Animations/` 并生成 Humanoid meta(`animationType: 3`);循环剪辑 AimHold/SearchLoop/Run 已勾 `loopTime: 1`,`lastFrame` 按时长 × 24fps - 1 设置。

## Blocked 项

无。NIUA 服务与余额正常。

## 遗留

- `CAM_E2_FirstPersonFall` 不属于 text2motion 能力(相机 Timeline 曲线),仍为纯演出制作项,保留在剩余清单。
- 全部 6 条为 text2motion 直出,仍需固定机位验收(轮廓、不滑步、Run 步频匹配 5.5m/s、AimHold 冻结稳定性)。
