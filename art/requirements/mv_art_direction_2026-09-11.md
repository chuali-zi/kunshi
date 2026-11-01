# 《困室》：《あぶく》MV 美术更新

## 参考与观察范围

- 官方 MV：https://www.youtube.com/watch?v=OHAjc-ayhus
- 同一 MV 的转载页面：https://www.bilibili.com/video/BV1RwojBLEhr/
- 本轮直接观察了转载页面的房间封面与项目中 `art/reference/character/` 的人物画面。官方播放器在当前浏览器无法播放，未声称逐帧审阅完整 MV。
- 参考的是房间布景与角色造型。墙纸、木纹等交付纹理由本项目程序绘制，没有把视频帧直接当作游戏贴图。

## 画面标准

房间保持现有 6.2 × 5.2 × 2.8 米游戏空间和固定电视机视点。主要视觉识别是奶油黄底的玫瑰碎花墙纸、暗绿地面、深胡桃木、酒红软包椅、暖白床品、深色吊灯外罩。墙纸花簇约每 30 厘米重复一次，木纹降低反差，布料和地面避免金属反光。

高背扶手椅是左侧色彩焦点，菱格软包、侧翼、座垫分层和细滚边需要在固定机位仍可读。书柜增加上下木线、侧边立柱和不同书脊颜色，装饰书并入柜体；第二层原有交互书组与全部 Socket 保留。木椅增加竖向靠背细杆。时钟有刻度、独立三针，完整与破损状态共用外观语言。

顶灯提供主要暖光，台灯形成低位暖区，四角逐渐压暗。Blender lookdev 的摄影补光仅用于观察模型；它不代表游戏内关灯、手电和黑暗适应已经验证。

## 交付

- `art/source/blender/environment/*_v003.blend` 和 `props/*_v003.blend`：38 项原资产的新版本。
- `art/exports/environment/`、`props/` 与 Unity 对应目录：同名 FBX，原 `.meta` 保留。
- 7 套材质图案：墙纸、两类木材、皮革、地面、床品、地毯；贴图上限 2048，包含底色、平坦切线法线、粗糙度/金属度 Mask。
- `art/source/blender/environment/ROOM_MV_Lookdev_v003.blend`：带摄影机和灯光的房间合成源文件。
- `art/previews/mv_20260911/`：房间及家具渲染、结构检查报告、Unity 执行日志。
- `Assets/_Game/Editor/MvArtUpgrade.cs`：Unity 菜单 `ConGame > Art > Apply Abuku MV Art`。导入资源、创建/重映射材质、恢复模型源材质、调整可破坏灯光、保存场景并拍摄房间。

备份在 `art/source/pre_mv_20260911/`；旧 v001/v002 源文件继续保留。本轮没有重新生成玩法脚手架。

## 验证与尚未完成的内容

38 项模型的对象名检查没有发现原对象丢失。新增细节并入相应家具/钟体，坐垫、钟针、窗板、抽屉、Socket 等保留原有层级。

软包高背椅最终为 7,684 三角面，书柜为 3,436 三角面；装饰书使用低面数硬边书脊，避免为远景小物件保留多段倒角。38 个资源（含互斥的破损状态）合计 84,769 三角面，这不是实际同屏三角面统计。Python 生产脚本通过语法检查，修改过的已跟踪文本通过 `git diff --check`。这些检查不替代实际 Unity 导入和游戏内测试。

Unity 6000.4.6f1 在执行导入前报告 `No valid Unity Editor license found`，退出码 198。因此目前不能把游戏内材质映射、光照、导航、交互或动画表现标记为通过；本目录的 `blender_*.png` 明确是 Blender 渲染图。

角色脸部精修、完整 MV 动作逐帧对齐、D01–D22 正式演出及全部 VFX 不属于这 38 个环境/道具导出的完成范围，仍按原剩余交付清单验收。现有角色与动作资产没有被这次批量环境导出覆盖。

## 再生产

1. `python art/pipeline/mv_palette_textures.py`
2. 启动本机 Blender MCP，然后执行 `python tools/blender_bridge.py art/source/blender/_shared/upgrade_mv_room.py`。
3. `python tools/blender_bridge.py art/source/blender/_shared/render_mv_room.py`。
4. Unity 许可证恢复后，在编辑器执行上述菜单，检查实际游戏固定机位、关灯路线和破损状态。
