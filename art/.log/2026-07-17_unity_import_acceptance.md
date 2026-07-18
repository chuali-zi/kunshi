# 2026-07-17 Unity 导入与验收记录（B08 收尾 + 终验）

执行：FBX 38 件 + 贴图 23 张拷入 `Assets/_Game/Art/{Environment,Props,Textures}`；
`Assets/_Game/Editor/ArtAcceptance.cs`（批处理入口 `ConGame.Editor.ArtAcceptance.Run`）完成：
贴图导入设置（_N 转 NormalMap、MRA 关 sRGB）→ 22 个 URP Lit 材质（`_Game/Art/Materials`）→
38 个 FBX 全部 External 重映射 → 搭 `ArtAcceptance.unity` 验收场景 → 5 机位截图（`art/previews/unity/`）。

## 验收结论（§1.9 终验：固定机位截图对比）

- 顶视/镜面机位/花瓶特写：布局、轮廓、遮挡、材质贴图全部正确（暗绿地、胡桃木、酒红皮、碎花墙纸、玫瑰红焦点）✓
- 挂钟特写：白面黑针指 12 点 ✓；远机位钟面暖色反光是点光在玻璃上的高光，非贴图错误
- 固定机位全景：所有资产就位；画面整体偏亮是验收场景临时灯光（单方向光+单点光+平环境光）所致，非资产问题，游戏正式场景有自己的灯光体系

## 决策与遗留

- FBX 材质重映射用 `AddRemap(SourceAssetIdentifier)` 逐名映射（SearchAndRemapMaterials 参数签名踩坑放弃）
- 验收场景灯光仅用于核验资产，不接进正式场景；灰盒→成品的场景替换（保挂载点换网格，spec §8 差异表）属下一步集成工作
- Atlas UV 内缩 0.02 防深 mip 渗色；书本页块同色问题记录在 B05 日志
- `Assets/_Game/Scenes/ArtAcceptance.unity` 保留作美术回归基准；批处理命令可重复跑

## 台账

B01~B08 全部批次（角色除外）交付完毕：38 FBX + 8 组贴图 + 22 URP 材质。
美术资产状态全部 → 已验收（美术口径）。角色 CH_*、风衣、动画待后续立项。
