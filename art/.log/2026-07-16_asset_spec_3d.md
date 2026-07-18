# 2026-07-16 新增 3D 资产制作规格

新建 `requirements/asset_spec_3d.md`:面向 AI 辅助 DCC(Blender MCP)的逐件制作规格。内容:全局硬规范(单位/原点/拆件/面数三档/UV密度/材质/挂点/验收流程)、场景基准(相机 (0,1.15,-2.14) FOV62)、23 个运行时 itemId ↔ 资产 ID ↔ 灰盒对象名对照表、ENV×5 / FUR×7 / PRP×15 规格卡、共享 Atlas 细则、灰盒摆位与设计布局差异表(镜子/挂钟/花瓶/地毯/封窗/苏醒点等 8 处)。

角色与动画不在本次范围。规格来源:items_interactions.md 布局 + asset_manifest.md 尺寸 + ConGameScaffoldBuilder 灰盒实测坐标。README 索引已更新。
