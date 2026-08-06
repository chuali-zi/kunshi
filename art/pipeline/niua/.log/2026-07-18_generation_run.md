# 2026-07-18 首轮生成执行

修复三问题:CDN 拒 Python 默认 UA(403)、job completed 但 url 短暂为 null、单条失败拖垮整批。产出:18 条 SFX 全部通过 RIFF 校验;7 条 P0 动画(mixamo 65 骨);双角色立绘→mesh→rig 全通(玩家立绘重跑一次去底部文字)。rig 服务实测只出 raw 34 骨且剥贴图,新增 merge_rig_texture.py(权重回传原贴图网格)与 convert_all_fbx.py(GLB→FBX)。资产已入 Assets/_Game/Art/。总费用约 $4.4。待 Unity 内验收。
