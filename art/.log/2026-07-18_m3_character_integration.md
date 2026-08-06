# 2026-07-18 M3 角色接入(Unity 批处理)

新增 CharacterAnimatorDriver(速度/GamePhase/IsSwinging→Animator 参数)与 ConGameCharacterInstaller(Humanoid 导入、AC_Player/AC_Killer 控制器、Room_M1 替换胶囊、绑 8 音效)。踩坑:raw 骨架需按拓扑重命名为 mixamo 名才能 Humanoid 映射;FBX 内嵌贴图 Unity 不认,改外置 PNG;固定机位 rig 在 CRT 模型内部导致截图花屏(运行时无碍)。PlayMode 54/54 通过。验收图 art/previews/unity/M3_*.png。
