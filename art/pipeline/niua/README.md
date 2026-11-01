# NIUA 生成管线(角色 / 动画 / 音效)

用 [NIUA API](https://api.niua.ohao.tech) 生成《困室》缺失的美术资产:
`CH_Player`、`CH_Killer` 两个角色的建模+绑骨、P0 七条动画、全游戏音效。
零第三方依赖,Python 3.10+ 直接运行。

## 一次性配置

1. 在 https://niua.ohao.tech/settings?tab=developer 创建 API key(`niua_live_` 开头),钱包充值(建议 ≥$10)。
2. `cp .env.example .env`,填入 `NIUA_API_KEY=niua_live_xxx`(`.env` 已被 .gitignore 排除)。

## 三条管线

### 1) 音效(最先跑,验证链路)

```bash
python gen_sfx.py --dry-run   # 看清单和费用
python gen_sfx.py             # 全量 18 条,~90¢
```

产物:`art/exports/audio/SFX_*.wav`。前 8 条文件名与 `AudioCueId` 一一对应。
单条不满意:`python gen_sfx.py --only SFX_Gunshot --force --seed 7`(5¢/次)。

### 2) 动画(P0 七条 + P1/Pose/PAIR 组件)

```bash
python gen_animations.py --dry-run
python gen_animations.py      # P0 七条 ~$1.5; 全量 32 条 ~$6.7
```

产物:`art/exports/animations/{AN,POSE,PAIR}_*.glb`(mixamo 骨架、In-Place)。
双人配合演出(PAIR_*)API 做不了,交付为 `PAIR_Dxx_PlayerSide`/`PAIR_Dxx_KillerSide`
单边组件,在 Timeline 按锚点对位拼接。贴地类动作(摔倒/俯卧/跪/坐)的 In-Place
剪辑髋部保持站立高度,Unity 接入时需用根锚点下移贴地(见
`art/.log/2026-08-06_p1_animation_holster_delivery.md`)。

GLB→FBX 转换后一键入 Unity(含 Humanoid meta 模板生成):

```bash
blender -b -P convert_all_fbx.py   # 增量: 已转换且未更新的自动跳过
python import_to_unity.py          # 复制 FBX 到 Assets/_Game/Art 并生成 meta
```

### 3) 角色(分阶段,含人工确认闸门)

先把参考图放到 `art/references/characters/CH_Player_ref.png`、`CH_Killer_ref.png`(png/jpg 均可)。

```bash
# ① 标准化立绘(9¢):参考图 → 干净背景全身 A-pose
python gen_characters.py --char CH_Player --stage standardize
# 打开 art/exports/characters/CH_Player_sheet.png 人工检查;
# 不满意换 seed 重跑:--stage standardize --force --seed 42

# ② 满意后建模+绑骨(49¢+49¢,共约 15-20 分钟)
python gen_characters.py --char CH_Player --stage all
# CH_Killer 同理
```

产物:`CH_*.glb`(带 PBR 贴图)、`CH_*_rigged.glb`(绑骨)。
每阶段结果记录在 `state/characters.json`,重跑自动跳过已完成阶段,不会重复扣费。

**rig 服务实测(2026-07-18)**:虽然请求了 `mixamo-humanoid`,当前服务只返回
raw 骨架(34 根 `bone_N` 通用命名,拓扑是标准人形),且会剥掉材质/UV。
两个后处理脚本解决(Blender 5.x 无头运行,零费用):

```bash
# 把 rig 产物的蒙皮权重传回原始带贴图 mesh,绑骨+贴图合并,导出 FBX + 预览图
blender -b -P merge_rig_texture.py
# 动画 GLB → FBX(Unity 原生不导入 GLB)
blender -b -P convert_all_fbx.py
```

**绑骨兜底**(如果 raw 骨架在 Unity 里 Automap 失败):用
https://www.mixamo.com 免费网页绑定 —— 上传 `CH_*.glb` 转出的 FBX
(`blender -b -P convert_glb_fbx.py -- in.glb out.fbx`),Auto-Rigger 完成后
下载 T-pose FBX 放回 `art/exports/characters/CH_*_rigged.fbx`。

## Unity 导入

1. 音效:Unity 菜单 `ConGame > Art > Import SFX From art-exports` 复制 wav,
   再在主场景 `ConGame > Art > Bind SFX Audio Cues In Open Scene` 自动填充
   `SemanticAudioDirector.bindings`(缺失槽位会在 Console 列出)。
2. 角色:`CH_*_rigged.fbx` 复制到 `Assets/_Game/Art/Characters/`,
   Import Settings → Rig → Animation Type = **Humanoid**,Avatar Definition =
   Create From This Model。骨骼是 `bone_N` 通用命名,自动映射失败时进
   Configure → Mapping → Automap(按骨骼位置映射,拓扑标准,基本一键成功),
   确认全绿后 Apply。
3. 动画:`AN_*.fbx` 复制到 `Assets/_Game/Art/Animations/`(mixamo 65 骨命名,
   Humanoid 自动识别),Rig = Humanoid;循环动画(Idle/Walk/Killer_Walk)
   勾选 Loop Time。之后任意 Humanoid Avatar(两个角色)都能复用这些剪辑。
4. 验收按 `art/README.md` 工作流:固定机位看轮廓、不滑步;
   挥击/拾取动画事件(HitStart 等)在 Unity 剪辑上手动添加(animation_manifest §7)。

## 费用与故障排查

| 接口 | 单价 | 说明 |
|---|---|---|
| image | 9¢ | 立绘标准化(img2img) |
| mesh | 49¢ | 图生 3D,同步但需数分钟 |
| rig | 49¢ | 每次尝试都计费,成功率 ~60% |
| text2motion | 21¢ | 异步,1-3 分钟 |
| sfx | 5¢ | 异步 |

- **402**:钱包余额不足,去 https://niua.ohao.tech 充值。
- **失败重试**:所有脚本支持 `--only <name> --force --seed N` 单条重跑。
- **断点续跑**:中断后直接重新运行同一命令,已完成条目自动跳过(`state/*.json`)。
- 模型实时健康/价格:`curl https://api.niua.ohao.tech/api/v1/models`(免费)。
