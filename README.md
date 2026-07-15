# 《困室》 / ConGame

Unity 固定机位死亡循环解谜游戏。玩法与剧情设计位于 `docs/`，美术生产要求位于 `art/`，Unity 工程内容位于 `Assets/_Game/`。

## 打开工程

1. 使用 Unity `6000.4.6f1` 打开仓库根目录。
2. 等待 Package Manager 和脚本编译完成。
3. 执行菜单 `ConGame > Scaffold > Build M1 Greybox`。
4. 如果脚手架提示刚启用了 Input System，重启一次 Unity。
5. 打开 `Assets/_Game/Scenes/Bootstrap.unity` 并进入 Play Mode。
6. 使用 `Window > General > Test Runner` 运行 EditMode 与 PlayMode 测试。

场景生成命令是幂等的：重新执行会重建由脚手架管理的场景与定义资产，不会操作 `art/` 中的 DCC 源文件。首次由 Unity 打开工程后，应将 Unity 生成的 `.meta` 和 `Packages/packages-lock.json` 一并纳入版本控制。

## M1 控制

- 移动：`WASD` / 左摇杆
- 冲刺：`Shift` / L3
- 交互：`E` / 手柄南键
- 挥击：鼠标左键 / RT
- 投掷：鼠标右键 / LT
- 姿态/藏身：`C` / 手柄东键
- 暂停：`Esc` / Start

游戏内没有交互提示。当前灰盒用于验证固定机位、拾取/挥击/藏身、神秘人入场、D01/D03/D07 选择与无缝重置，不代表最终美术和动画品质。

## 文档权威顺序

1. `docs/story_outline.md`：原始构思，保持原样。
2. `docs/game_outline.md`、`docs/items_interactions.md`、`docs/game_flow_script.md`：体验与内容设计。
3. `docs/gameplay_logic_spec.md`：可执行规则的唯一真相。
4. ScriptableObject 数据与代码。

架构说明见 `docs/technical_architecture.md`。
