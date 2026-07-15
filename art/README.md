# 《困室》美术资产工作区

`art/` 是美术生产资料的唯一入口，与玩法和剧情文档 `docs/` 分开维护。

## 当前文件

- [requirements/asset_manifest.md](requirements/asset_manifest.md)：模型、材质、破坏状态和制作批次。
- [requirements/animation_manifest.md](requirements/animation_manifest.md)：压缩后的最小动画集合，以及 22 种死法如何复用它们。

## 推荐目录

资产开始制作后按下面的结构存放；没有内容的目录不必提前创建。

```text
art/
├─ requirements/             # 需求、优先级和验收口径
├─ references/               # 按资产 ID 保存参考图，不放来源不明素材
│  ├─ environment/
│  ├─ characters/
│  └─ props/
├─ source/                   # DCC 源文件，不直接被 Unity 引用
│  └─ blender/
│     ├─ environment/
│     ├─ characters/
│     └─ props/
├─ exports/                  # 从 Blender 导出的 FBX/GLB 与贴图
│  ├─ environment/
│  ├─ characters/
│  ├─ props/
│  └─ animations/
├─ previews/                 # 转台图、尺寸检查图、破坏状态对照图
└─ .log/                     # 本模块工作记录
```

Unity 项目建立后，运行时资产建议放在：

```text
Assets/_Game/Art/
├─ Characters/
├─ Environment/
├─ Props/
├─ Materials/
├─ Textures/
├─ Animations/
└─ VFX/
```

`.blend` 源文件不要放进 Unity 的 `Assets/`。Unity 只接收 `art/exports/` 中经过确认的 FBX/GLB 和贴图，避免 Unity 隐式调用本机 Blender 导入器导致版本差异。

## 资产命名

| 类型 | 前缀 | 示例 |
|---|---|---|
| 角色 | `CH_` | `CH_Killer` |
| 场景结构 | `ENV_` | `ENV_RoomShell` |
| 家具 | `FUR_` | `FUR_Armchair` |
| 道具 | `PRP_` | `PRP_CRTTV` |
| 动画 | `AN_` | `AN_Player_Swing` |
| 材质 | `MAT_` | `MAT_Wood_Walnut` |
| 贴图 | `TEX_` | `TEX_Wood_Walnut_BC` |

统一状态后缀：`_Intact`、`_Damaged`、`_Broken`。左右侧使用 `_L`、`_R`，LOD 使用 `_LOD0`、`_LOD1`。不要用“最终版”“新建”“副本”等不可追踪名称。

## Blender 到 Unity 规范

- 单位：1 Blender Unit = 1 米；所有模型应用 Scale，导出前缩放为 `(1, 1, 1)`。
- Blender 中 Z 轴向上，物体正面朝 `-Y`；FBX 导出使用 `Forward -Z / Up Y`。
- 静态物体原点放在落地点中心；门、抽屉、钟针等原点放在真实转轴；手持物原点放在握持点。
- 可交互、可破坏、可掉落部分必须是独立对象，不能只在同一网格上做外观切分。
- 角色统一 Humanoid 骨架；玩家和神秘人共用骨骼命名与人体比例，便于动画复用。
- 默认贴图以 2K 为上限；小道具合图到 2K Atlas，角色各一套 2K，房间主体使用可平铺材质。只有电视和角色近景能证明需要时才升到 4K。
- 模型中不烘焙房间光照。灯光、镜面、CRT、裂屏、血迹、体积光和碎屑粒子在 Unity 中实现。

## 工作流

1. 从资产台账选择一个 Blender MCP 批次，只制作该批次列出的对象。
2. 源文件命名为 `<资产ID>_v001.blend`，大改递增版本，不覆盖已验收版本。
3. 在 Blender 中检查真实尺寸、原点、法线、应用变换和独立活动部件。
4. 导出到 `art/exports/`，在 Unity 固定机位中检查轮廓和遮挡；本游戏不以转台近景效果作为唯一验收标准。
5. 台账状态按 `未开始 → 制作中 → 待验收 → 已验收` 更新。
