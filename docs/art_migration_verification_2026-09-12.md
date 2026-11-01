# Blender 到 Unity 的美术迁移核查

核查对象是用户提供的房间图片，以及当前 Room_M1 编辑场景和正常开局后的 Unity GameView。用户图片对应 [Blender 房间预览](../art/previews/mv_20260911/blender_room.png)，不是 Unity 实机图。

**结论：主要新版网格已经接入；材质、网格面的显示、光照和相机构图没有完整迁移。当前不能把“FBX 已引用”视作“Blender 美术效果已落地”。**

## 已确认的差异

| 项目 | Blender 参考 | Unity 当前状态 | 判断 |
| --- | --- | --- | --- |
| 主要模型 | v003 环境、家具及道具 | 现场床、椅、地毯、窗等 Mesh 指向导出的对应 FBX，宿主旧网格 Renderer 已禁用 | 网格接入已发生，未发现这些物体退回灰盒 |
| 软包椅背 | 可见菱格起伏 | 新软包几何在网格内，但默认背面剔除时不可见 | 面朝向存在问题，构成显示缺陷 |
| 地毯 | 棕色带边框织物 | Rug_Body 的材质实际绑定 TEX_MAT_Floor_DarkGreen_BC.png，而代码要求 TEX_MAT_Fabric_Rug | 材质仍是旧映射，不能只看 FBX 路径判断已迁完 |
| 房间光照 | Cycles 反弹、两盏实际灯及两盏 Area 补光 | 两盏房间点光源的 shadows 均为 None；URP 的额外光源阴影与柔阴影支持均关闭 | 缺少参考图的遮挡、软阴影与空间层次 |
| 曝光及色调 | AgX，曝光 -0.65 | Main Camera 后处理关闭，场景没有 Volume | Blender 的色调处理没有对应到当前游戏画面 |
| 相机 | 1600×900 投影矩阵换算垂直 FOV 57.383538° | Camera.fieldOfView 72° | 实机广角更宽，不能视作同机位构图 |
| 扶手椅摆位 | 换算到 Unity 坐标约 (-1.8, 0, 1.5) | 视觉根约 (-1.25, 0, 1.05) | 构图确有偏移 |
| 封板窗状态 | Lookdev 中正常与破损两套窗板都参与渲染 | 游戏初始状态只启用五块正常板、关闭破损件 | 两边展示状态不一致；黑缝的具体成因尚未完全定位，不能据黑缝直接断言是旧模型 |

## 现场验证方式

- 读取 Unity 现场 Renderer、MeshFilter、共享材质、模型源引用、主相机、Light 和 URP 资产设置。
- 主动进入 Play Mode，等待正常唤醒结束后截取 [当前 Unity 实机房间](../Logs/atmosphere_migration_audit_20260912/unity_actual_room.png)。
- 仅为诊断，临时把软包材质的剔除模式从 Back 改为 Off；软包几何立即出现，但由于面朝向错误，明暗仍不正确。见 [软包剔除诊断图](../Logs/atmosphere_migration_audit_20260912/unity_chair_culling_probe.png)。该图不是修复后的交付效果。
- 诊断后已恢复原材质剔除模式，退出 Play Mode，恢复检查前的存档；本轮没有正式修改模型、材质或场景设置。
- 使用独立 Blender 后台进程只读打开 ROOM_MV_Lookdev_v003.blend，读取对象状态与相机投影；没有保存或重渲染该文件。

## 对应源位置

- [Blender 房间渲染脚本](../art/source/blender/_shared/render_mv_room.py)：生成 blender_room.png，设置四盏灯、AgX 与曝光。
- [软包几何生成脚本](../art/source/blender/_shared/upgrade_mv_room.py)：armchair 的网格面序朝向需要修正。
- [Unity 材质准备](../Assets/_Game/Editor/ArtAcceptance.cs)：地毯目标贴图已经写为 Fabric_Rug，但当前材质资产仍绑定 Floor_DarkGreen。
- [Unity 美术更新入口](../Assets/_Game/Editor/MvArtUpgrade.cs)：存在灯光与材质更新逻辑，但现场参数未体现该入口预期的完整结果。

后续修复应先纠正软包面朝向与地毯映射，再校准游戏相机、摆位、实时阴影和色调。Blender 的固定补光需要结合游戏开关灯、破坏灯具和黑暗规则重新设计，直接照搬会与可玩状态冲突。
