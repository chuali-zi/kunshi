using System;
using System.Collections.Generic;
using System.IO;
using ConGame.Presentation.Animation;
using ConGame.Presentation.Audio;
using ConGame.Presentation.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConGame.Editor
{
    /// <summary>
    /// M4 白模替换:Room_M1 灰盒 → B08 成品资产。原则(asset_spec_3d §8):
    /// 保留玩法对象挂载点与组件,只换视觉网格;摆位按设计值修正
    /// (床/木椅/扶手椅/书柜/抽屉柜/镜/钟/毯/窗/门洞、新增电视柜与小圆几)。
    /// 苏醒点与结局按设计移到北墙木椅。批处理入口:
    /// -executeMethod ConGame.Editor.ConGameArtInstaller.Run
    /// </summary>
    public static class ConGameArtInstaller
    {
        const string ArtRoot = "Assets/_Game/Art";
        const string ScenePath = "Assets/_Game/Scenes/Room_M1.unity";
        const string NavMeshAssetPath = "Assets/_Game/Content/Navigation/NavMesh_Room_M1.asset";
        const string ShotDir = @"D:\game\con_game\art\previews\unity";

        [MenuItem("ConGame/Art/Install Room Art (Replace Greybox)")]
        public static void Run()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            InstallIntoCurrentScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("ARTINSTALL done, scene saved");
        }

        public static void InstallIntoCurrentScene()
        {
            var visuals = new Dictionary<string, GameObject>();

            InstallEnvironment(visuals);
            InstallFurniture(visuals);
            InstallProps(visuals);
            WireGameplayRepairs();
            MoveAwakeningToWoodChair();
            RebuildNavMesh();
            AudioCueAutoBinder.BindCues();
            MvRoomPresentationUpgrade.ApplyToCurrentScene();
        }

        // ---- 环境:壳体 / 门组 / 走廊 ----

        static void InstallEnvironment(Dictionary<string, GameObject> visuals)
        {
            Transform env = Find("Environment");

            // 成品壳体与灰盒同尺同位(地板 -0.05、墙 ±3.16/±2.66),原点即对齐
            AddStaticVisual(env, "Environment/ENV_RoomShell", Vector3.zero, 0f, visuals);

            // 灰盒环境体只留碰撞
            foreach (string n in new[]
                     {
                         "Floor", "North Wall", "South Wall", "West Wall", "East Wall Lower",
                         "East Wall Upper", "Door Lintel", "Ceiling", "Corridor Floor", "Black Corridor"
                     })
            {
                Transform shell = Find(n);
                HideRenderer(shell);
                Collider collider = shell.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }

            // 门洞按成品墙 E 的开口修正(z 0.10~1.00,原灰盒 1.00~1.95)
            SetBox("East Wall Lower", new Vector3(3.15f, 1.40f, -1.25f), new Vector3(0.10f, 2.80f, 2.70f));
            SetBox("East Wall Upper", new Vector3(3.15f, 1.40f, 1.825f), new Vector3(0.10f, 2.80f, 1.65f));
            SetBox("Door Lintel", new Vector3(3.15f, 2.48f, 0.55f), new Vector3(0.10f, 0.64f, 0.90f));

            // 铰链移到北门柱(成品门板枢轴 z=1.00),灰盒门板碰撞翻到 -z 侧
            Transform hinge = Find("DoorHinge");
            hinge.position = new Vector3(3.10f, 0f, 1.00f);
            Transform greyDoor = hinge.Find("Door");
            greyDoor.localPosition = new Vector3(0f, 1.03f, -0.45f);
            HideRenderer(greyDoor);

            // 门组:门框留在静态视觉里,门板+把手挂到铰链下随破门摆动
            Transform existingPanel = FindDeep(hinge, "Door_Panel");
            if (existingPanel != null)
            {
                UnityEngine.Object.DestroyImmediate(existingPanel.gameObject);
            }

            GameObject doorSet = AddStaticVisual(env, "Environment/ENV_DoorSet", Vector3.zero, 0f, visuals);
            Transform panel = FindDeep(doorSet.transform, "Door_Panel");
            Transform handle = FindDeep(doorSet.transform, "Door_Handle");
            handle.SetParent(panel, true);
            panel.SetParent(hinge, true);

            AddStaticVisual(env, "Environment/ENV_BlackCorridor", Vector3.zero, 0f, visuals);

            // 门侧玩法组整体南移到设计门位(Δz -0.9)
            Move("PRP_RoomDoor_Interact", new Vector3(2.70f, 1.05f, 0.55f));
            Move("PRP_DoorTrapSocket", new Vector3(2.55f, 0.08f, 0.20f));
            Move("PRP_AmbushPoint", new Vector3(2.55f, 0.55f, 0.95f));
            Move("Zone_DoorBlind", new Vector3(2.55f, 0.90f, 0.55f));
            Move("Corridor Floor", new Vector3(3.70f, -0.05f, 0.55f));
            Move("Black Corridor", new Vector3(4.28f, 1.30f, 0.55f));
            Move("Killer", new Vector3(3.62f, 0f, 0.55f));
            Debug.Log("ARTINSTALL environment ok");
        }

        // ---- 家具 ----

        static void InstallFurniture(Dictionary<string, GameObject> visuals)
        {
            Transform fur = Find("Furniture");

            // 床组:设计位 (1.3, 2.1),整组刚性平移
            NormalizeScale(Find("FUR_Bed").gameObject);
            Move("FUR_Bed", new Vector3(1.3f, 0.30f, 2.1f));
            Move("Zone_UnderBed", new Vector3(1.3f, 0.65f, 1.55f));
            Move("Zone_Bed", new Vector3(1.3f, 1.05f, 2.1f));
            NormalizeScale(Find("PRP_BedTopHide").gameObject);
            Move("PRP_BedTopHide", new Vector3(1.3f, 0.75f, 2.1f));
            HideRenderer(Find("PRP_BedTopHide"));
            AddChildVisual("FUR_Bed", "Environment/FUR_Bed", new Vector3(1.3f, 0f, 2.1f), 180f, visuals);

            // 木椅:北墙正中(苏醒/结局椅)
            GameObject woodChair = Find("FUR_WoodChair").gameObject;
            NormalizeScale(woodChair);
            MoveRot("FUR_WoodChair", new Vector3(0f, 0.475f, 2.35f), 180f);
            GameObject intactChair = AddChildVisual("FUR_WoodChair", "Environment/FUR_WoodChair",
                                                    new Vector3(0f, 0f, 2.35f), 180f, visuals);
            GameObject brokenChair = AddChildVisual("FUR_WoodChair", "Environment/FUR_WoodChair_Broken",
                                                    new Vector3(0f, 0f, 2.35f), 180f, visuals);
            ConfigureWorldItemVisuals(woodChair, intactChair, brokenChair);

            // 扶手椅:向电视和画面右侧靠拢,左后侧保留躲藏入口
            NormalizeScale(Find("FUR_Armchair").gameObject);
            MoveRot("FUR_Armchair", new Vector3(-1.8f, 0.575f, 1.5f), 160f);
            AddChildVisual("FUR_Armchair", "Environment/FUR_Armchair", new Vector3(-1.8f, 0f, 1.5f), 160f, visuals);
            MoveRot("Zone_SofaRear", new Vector3(-2.07f, 0.8f, 2.23f), 160f);

            // 高书柜:西墙北段,规格宽 1.0
            GameObject bookcase = Find("FUR_Bookcase").gameObject;
            NormalizeScale(bookcase);
            MoveRot("FUR_Bookcase", new Vector3(-2.925f, 1.05f, 1.8f), 90f);
            bookcase.GetComponent<BoxCollider>().size = new Vector3(1.0f, 2.10f, 0.35f);
            AddChildVisual("FUR_Bookcase", "Environment/FUR_Bookcase", new Vector3(-2.925f, 0f, 1.8f), 90f, visuals);

            // 抽屉柜:西墙(避开 StrikeWall 板 x≈-2.95,前移到 -2.62)
            NormalizeScale(Find("FUR_Dresser").gameObject);
            MoveRot("FUR_Dresser", new Vector3(-2.62f, 0.425f, 0.3f), 90f);
            AddChildVisual("FUR_Dresser", "Environment/FUR_Dresser", new Vector3(-2.62f, 0f, 0.3f), 90f, visuals);

            // 新增静态件:电视柜 / 小圆几 / 抽屉杂物
            GameObject cabinet = AddStaticVisual(fur, "Environment/FUR_TVCabinet", new Vector3(0f, 0f, -2.375f), 0f, visuals);
            AddBox(cabinet, new Vector3(0f, 0.325f, 0f), new Vector3(1.10f, 0.65f, 0.47f));
            GameObject sideTable = AddStaticVisual(fur, "Environment/FUR_SideTable", new Vector3(2.45f, 0f, 1.35f), 0f, visuals);
            AddBox(sideTable, new Vector3(0f, 0.275f, 0f), new Vector3(0.45f, 0.55f, 0.45f));
            AddStaticVisual(fur, "Props/PRP_DrawerClutter", new Vector3(-2.3f, 0f, -0.35f), -40f, visuals);
            Debug.Log("ARTINSTALL furniture ok");
        }

        // ---- 道具 ----

        static void InstallProps(Dictionary<string, GameObject> visuals)
        {
            GameplayEventHub events = UnityEngine.Object.FindFirstObjectByType<GameplayEventHub>(
                FindObjectsInactive.Include);
            GameplayFactService facts = UnityEngine.Object.FindFirstObjectByType<GameplayFactService>(
                FindObjectsInactive.Include);

            // 电视:留灰盒碰撞与相机锚点,成品屏幕中心须与固定机位重合(±2cm)
            NormalizeScale(Find("PRP_CRTTV").gameObject);
            AddChildVisual("PRP_CRTTV", "Props/PRP_CRTTV_Intact", new Vector3(0f, 0.65f, -2.438f), 0f, visuals);

            NormalizeScale(Find("PRP_CeilingLamp").gameObject);

            SphereCollider ceilingHit = Find("PRP_CeilingLamp").GetComponent<SphereCollider>();
            if (ceilingHit != null)
            {
                ceilingHit.center = new Vector3(0f, -0.43f, 0f);
                ceilingHit.radius = 0.28f;
            }
            AddChildVisual("PRP_CeilingLamp", "Props/PRP_CeilingLamp_Intact",
                           new Vector3(0f, 2.55f, 0f), 0f, visuals);

            // 台灯/遥控器 → 抽屉柜台面 SOCKET
            Vector3 lampTop = SocketWorld(visuals["FUR_Dresser"], "SOCKET_LampTop");
            NormalizeScale(Find("PRP_TableLamp").gameObject);
            MoveRot("PRP_TableLamp", lampTop + new Vector3(0f, 0.225f, 0f), 90f);
            AddChildVisual("PRP_TableLamp", "Props/PRP_TableLamp_Intact", lampTop, 90f, visuals);

            Vector3 remoteTop = SocketWorld(visuals["FUR_Dresser"], "SOCKET_RemoteTop");
            NormalizeScale(Find("PRP_Remote").gameObject);
            MoveRot("PRP_Remote", remoteTop + new Vector3(0f, 0.025f, 0f), 90f);
            AddChildVisual("PRP_Remote", "Props/PRP_Remote", remoteTop, 90f, visuals);

            // 书组 → 书柜第二层
            Vector3 shelf = SocketWorld(visuals["FUR_Bookcase"], "SOCKET_Shelf_02");
            NormalizeScale(Find("PRP_Books").gameObject);
            MoveRot("PRP_Books", shelf + new Vector3(0f, 0.06f, 0f), 90f);
            AddChildVisual("PRP_Books", "Props/PRP_Books_Set", shelf, 90f, visuals);

            // 花瓶 → 小圆几
            Vector3 vaseTop = SocketWorld(visuals["FUR_SideTable"], "SOCKET_Vase");
            GameObject vase = Find("PRP_Vase").gameObject;
            NormalizeScale(vase);
            MoveRot("PRP_Vase", vaseTop + new Vector3(0f, 0.175f, 0f), 0f);
            GameObject intactVase = AddChildVisual("PRP_Vase", "Props/PRP_Vase", vaseTop, 0f, visuals);
            GameObject brokenVase = AddChildVisual("PRP_Vase", "Props/PRP_Vase_Broken", vaseTop, 0f, visuals);
            ConfigureWorldItemVisuals(vase, intactVase, brokenVase);

            // 挂钟 → 北墙床上方
            GameObject wallClock = Find("PRP_WallClock").gameObject;
            NormalizeScale(wallClock);
            MoveRot("PRP_WallClock", new Vector3(1.3f, 1.85f, 2.51f), 180f);
            GameObject intactClock = AddChildVisual("PRP_WallClock", "Props/PRP_WallClock",
                                                    new Vector3(1.3f, 1.85f, 2.56f), 180f, visuals);
            GameObject brokenClock = AddChildVisual("PRP_WallClock", "Props/PRP_WallClock_Broken",
                                                    new Vector3(1.3f, 1.85f, 2.56f), 180f, visuals);
            ConfigureWorldItemVisuals(wallClock, intactClock, brokenClock);

            // 镜子 → 东墙南段,向房内偏 12°(镜中收进门+电视)
            GameObject mirror = Find("PRP_Mirror").gameObject;
            NormalizeScale(mirror);
            MoveRot("PRP_Mirror", new Vector3(3.02f, 1.13f, -1.1f), -78f);
            GameObject intactMirror = AddChildVisual("PRP_Mirror", "Props/PRP_Mirror",
                                                     new Vector3(3.0f, 0.5f, -1.1f), -78f, visuals);
            GameObject brokenMirror = AddChildVisual("PRP_Mirror", "Props/PRP_Mirror_Broken",
                                                     new Vector3(3.0f, 0.5f, -1.1f), -78f, visuals);
            StatefulWorldItem mirrorState = mirror.GetComponent<StatefulWorldItem>() ??
                                            mirror.AddComponent<StatefulWorldItem>();
            mirrorState.Configure(null);
            ConfigureWorldItemVisuals(mirror, intactMirror, brokenMirror);

            // 球棍:斜靠新书柜东面(成品沿 z 长,子节点转 -90°X 对齐灰盒 y 长胶囊)
            GameObject bat = Find("PRP_BaseballBat").gameObject;
            NormalizeScale(bat);
            bat.transform.position = new Vector3(-2.68f, 0.42f, 1.35f);
            bat.transform.rotation = Quaternion.Euler(0f, 0f, -20f);
            GameObject batVisual = AddChildVisual("PRP_BaseballBat", "Props/PRP_BaseballBat", bat.transform.position, 0f, visuals);
            batVisual.transform.localPosition = Vector3.zero;

            CapsuleCollider batCollider = bat.GetComponent<CapsuleCollider>();
            if (batCollider != null)
            {
                batCollider.direction = 1;
                batCollider.center = new Vector3(0f, 0.275f, 0f);
                batCollider.height = 0.85f;
                batCollider.radius = 0.045f;
            }
            batVisual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // 毯子:运行时由隐藏姿态、陷阱插槽和物件状态切换四套预制网格。
            GameObject blanket = Find("PRP_Blanket").gameObject;
            NormalizeScale(blanket);
            Move("PRP_Blanket", new Vector3(1.3f, 0.65f, 2.1f));
            GameObject crumpledBlanket = AddChildVisual("PRP_Blanket", "Props/PRP_Blanket_Crumpled",
                                                        new Vector3(1.3f, 0.525f, 2.1f), 0f, visuals);
            GameObject coveredBlanket = AddChildVisual("PRP_Blanket", "Props/PRP_Blanket_Covered",
                                                       new Vector3(1.3f, 0.525f, 2.1f), 0f, visuals);
            GameObject flatBlanket = AddChildVisual("PRP_Blanket", "Props/PRP_Blanket_FlatTrap",
                                                    blanket.transform.position, 0f, visuals);
            GameObject drapedBlanket = AddChildVisual("PRP_Blanket", "Props/PRP_Blanket_Draped",
                                                      blanket.transform.position, 0f, visuals);
            BlanketVisualPresenter blanketPresenter = blanket.GetComponent<BlanketVisualPresenter>() ??
                                                       blanket.AddComponent<BlanketVisualPresenter>();
            blanketPresenter.Configure(blanket.GetComponent<StatefulWorldItem>(), facts, crumpledBlanket,
                                       coveredBlanket, flatBlanket, drapedBlanket);

            // 地毯:3.5×0.9 斜铺,西端对齐扶手椅前脚
            GameObject rug = Find("PRP_Rug").gameObject;
            NormalizeScale(rug);
            MoveRot("PRP_Rug", new Vector3(0f, 0.03f, 0.85f), 16f);
            rug.GetComponent<BoxCollider>().size = new Vector3(3.5f, 0.06f, 0.9f);
            AddChildVisual("PRP_Rug", "Props/PRP_Rug", new Vector3(0f, 0f, 0.85f), 16f, visuals);

            // 封死窗 → 北墙偏西(书柜与木椅之间);隐藏断裂板变体
            NormalizeScale(Find("PRP_BoardedWindow").gameObject);
            Move("PRP_BoardedWindow", new Vector3(-1.2f, 1.47f, 2.55f));
            GameObject window = AddChildVisual("PRP_BoardedWindow", "Environment/ENV_BoardedWindow",
                                               new Vector3(-1.2f, 0.9f, 2.61f), 0f, visuals);
            ConfigureBoardedWindowVisuals(Find("PRP_BoardedWindow").gameObject, window, events);

            // 砸墙代理保留命中体，正式破损件按每次已解析命中累积至三档。
            GameObject strikeWall = Find("PRP_StrikeWall").gameObject;
            NormalizeScale(strikeWall);
            GameObject wallDamage = AddChildVisual("PRP_StrikeWall", "Environment/ENV_WallDamageKit",
                                                   new Vector3(-3.08f, 0.4f, 0f), 90f, visuals);
            ConfigureWallDamageVisuals(strikeWall, wallDamage, events);

            // 手电/双枪(初始未激活,入侵段生成)
            NormalizeScale(Find("PRP_Flashlight").gameObject);
            AddChildVisualCentered("PRP_Flashlight", "Props/PRP_Flashlight", visuals);
            NormalizeScale(Find("PRP_PistolMain").gameObject);
            AddChildVisualCentered("PRP_PistolMain", "Props/PRP_Pistol_Main", visuals);
            NormalizeScale(Find("PRP_PistolBackup").gameObject);
            AddChildVisualCentered("PRP_PistolBackup", "Props/PRP_Pistol_Backup", visuals);

            // 大衣视觉由 MV 展示安装器挂接，交互组件与命中体保留。
            MvRoomPresentationUpgrade.InstallCoat();
            foreach (string n in new[] { "PRP_StrikeWall", "PRP_AmbushPoint", "PRP_DoorTrapSocket", "PRP_RoomDoor_Interact" })
            {
                HideRenderer(Find(n));
            }

            Debug.Log("ARTINSTALL props ok");
        }

        // ---- 苏醒/结局移到北墙木椅(asset_spec_3d §8)----

        static void MoveAwakeningToWoodChair()
        {
            MoveRot("Player", new Vector3(0f, 0f, 1.8f), 180f);
            Move("Zone_Awakening", new Vector3(0f, 0.9f, 2.0f));
            MoveRot("EndingChairAnchor", new Vector3(0f, 0f, 2.35f), 180f);
            MoveRot("EndingKillerAnchor", new Vector3(0.15f, 0f, 1.15f), 0f);
            Debug.Log("ARTINSTALL awakening/ending anchors -> wood chair");
        }

        // ---- NavMesh ----

        internal static void RebuildNavMesh()
        {
            var surface = Find("Navigation").GetComponent<NavMeshSurface>();
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                throw new InvalidOperationException("navmesh bake produced no data");
            }

            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(NavMeshAssetPath);
            if (existing != null)
            {
                var baked = surface.navMeshData;
                EditorUtility.CopySerialized(baked, existing);
                surface.RemoveData();
                surface.navMeshData = existing;
                surface.AddData();
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(baked);
            }
            else AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            EditorUtility.SetDirty(surface);
            Debug.Log("ARTINSTALL navmesh rebaked -> " + NavMeshAssetPath);
        }

        [MenuItem("ConGame/Gameplay/Rebuild Room Navigation")]
        public static void RebuildNavigationOnly()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RebuildNavMesh();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [MenuItem("ConGame/Gameplay/Repair Room Wiring")]
        public static void RepairGameplayWiring()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            WireGameplayRepairs();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("ARTINSTALL gameplay wiring repaired");
        }

        static void WireGameplayRepairs()
        {
            Transform player = Find("Player");
            Transform handSocket = FindDeep(player, "HandSocket_R");
            if (handSocket == null)
            {
                handSocket = new GameObject("HandSocket_R").transform;
                handSocket.SetParent(player, false);
            }

            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            if (interactor != null)
            {
                SerializedObject serializedInteractor = new SerializedObject(interactor);
                serializedInteractor.FindProperty("handSocket").objectReferenceValue = handSocket;
                serializedInteractor.ApplyModifiedPropertiesWithoutUndo();
            }

            Animator playerAnimator = player.GetComponentInChildren<Animator>(true);
            HumanoidHandSocketBinder binder = player.GetComponent<HumanoidHandSocketBinder>() ??
                                              player.gameObject.AddComponent<HumanoidHandSocketBinder>();
            binder.Configure(playerAnimator, handSocket, Vector3.zero, Quaternion.identity);

            EnsureClimb(Find("FUR_WoodChair"), "ClimbStand_Chair", new Vector3(0f, 0.5f, 0f), 0.85f, true);
            EnsureClimb(Find("FUR_Bed"), "ClimbStand_Bed", new Vector3(0f, 0.52f, -0.05f), 0.95f, false);

            foreach (MeleeWeaponCapability melee in UnityEngine.Object.FindObjectsByType<MeleeWeaponCapability>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject serializedMelee = new SerializedObject(melee);
                serializedMelee.FindProperty("hitboxHalfHeight").floatValue = 0.75f;
                serializedMelee.ApplyModifiedPropertiesWithoutUndo();
            }

            KillerTacticalController tactical = Find("Killer").GetComponent<KillerTacticalController>();
            KillerHitReceiver hitReceiver = Find("Killer").GetComponent<KillerHitReceiver>();
            if (hitReceiver != null)
            {
                Transform feedback = FindDeep(Find("Killer"), "CH_Killer_Model");
                hitReceiver.Configure(feedback == null ? Find("Killer") : feedback, tactical);
            }

            SemanticAudioDirector audio = UnityEngine.Object.FindFirstObjectByType<SemanticAudioDirector>(
                FindObjectsInactive.Include);
            TelevisionController television = UnityEngine.Object.FindFirstObjectByType<TelevisionController>(
                FindObjectsInactive.Include);
            if (audio != null && television != null)
            {
                SerializedObject serializedAudio = new SerializedObject(audio);
                serializedAudio.FindProperty("television").objectReferenceValue = television;
                serializedAudio.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void EnsureClimb(Transform host, string anchorName, Vector3 localPosition, float duration,
                                bool requirePlacedState)
        {
            Transform anchor = FindDeep(host, anchorName);
            if (anchor == null)
            {
                anchor = new GameObject(anchorName).transform;
                anchor.SetParent(host, false);
            }

            anchor.localPosition = localPosition;
            anchor.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            ClimbCapability climb = host.GetComponent<ClimbCapability>() ??
                                    host.gameObject.AddComponent<ClimbCapability>();
            climb.Configure(anchor, duration, requirePlacedState);
            EditorUtility.SetDirty(climb);
        }

        static void ConfigureWorldItemVisuals(GameObject host, GameObject intact, GameObject broken)
        {
            StatefulWorldItem item = host.GetComponent<StatefulWorldItem>();
            if (item == null)
            {
                throw new InvalidOperationException("StatefulWorldItem missing on " + host.name);
            }

            WorldItemVisualPresenter presenter = host.GetComponent<WorldItemVisualPresenter>() ??
                                                 host.AddComponent<WorldItemVisualPresenter>();
            presenter.Configure(item, intact, broken);
            EditorUtility.SetDirty(presenter);
        }

        static void ConfigureBoardedWindowVisuals(GameObject host, GameObject visual, GameplayEventHub events)
        {
            const int boardCount = 5;
            var intact = new GameObject[boardCount];
            var broken = new GameObject[boardCount];
            for (int i = 0; i < boardCount; i++)
            {
                string suffix = (i + 1).ToString("00");
                intact[i] = RequireVisualChild(visual, "Window_Board_" + suffix).gameObject;
                broken[i] = RequireVisualChild(visual, "Window_Board_Broken_" + suffix).gameObject;
            }

            StrikeSurfaceHitReceiver receiver = host.GetComponent<StrikeSurfaceHitReceiver>();
            ImpactDamageVisualPresenter presenter = host.GetComponent<ImpactDamageVisualPresenter>() ??
                                                     host.AddComponent<ImpactDamageVisualPresenter>();
            presenter.Configure(events, receiver, boardCount, intact, broken);
            EditorUtility.SetDirty(presenter);
        }

        static void ConfigureWallDamageVisuals(GameObject host, GameObject visual, GameplayEventHub events)
        {
            GameObject[] damageVisuals =
            {
                RequireVisualChild(visual, "WallDamage_Decal_1").gameObject,
                RequireVisualChild(visual, "WallDamage_Decal_2").gameObject,
                RequireVisualChild(visual, "WallDamage_Decal_3").gameObject,
                RequireVisualChild(visual, "WallDamage_Flake_01").gameObject,
                RequireVisualChild(visual, "WallDamage_Flake_02").gameObject,
                RequireVisualChild(visual, "WallDamage_Flake_03").gameObject,
                RequireVisualChild(visual, "WallDamage_Flake_04").gameObject,
                RequireVisualChild(visual, "WallDamage_Flake_05").gameObject
            };
            int[] thresholds = { 1, 2, 3, 1, 2, 2, 3, 3 };
            StrikeSurfaceHitReceiver receiver = host.GetComponent<StrikeSurfaceHitReceiver>();
            ImpactDamageVisualPresenter presenter = host.GetComponent<ImpactDamageVisualPresenter>() ??
                                                     host.AddComponent<ImpactDamageVisualPresenter>();
            presenter.Configure(events, receiver, 3, stagedDamage: damageVisuals,
                                stagedThresholds: thresholds);
            EditorUtility.SetDirty(presenter);
        }

        static Transform RequireVisualChild(GameObject root, string name)
        {
            Transform child = FindDeep(root.transform, name);
            if (child == null)
            {
                throw new InvalidOperationException(name + " missing in " + root.name);
            }

            return child;
        }

        // ---- 通用工具 ----

        static Transform Find(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);
                if (found != null)
                {
                    return found;
                }
            }

            throw new InvalidOperationException("scene object missing: " + name);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        static void Move(string name, Vector3 pos) => Find(name).position = pos;

        static void MoveRot(string name, Vector3 pos, float rotY)
        {
            Transform t = Find(name);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, rotY, 0f);
        }

        static void HideRenderer(Transform t)
        {
            var renderer = t.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        static void SetBox(string name, Vector3 pos, Vector3 scale)
        {
            Transform t = Find(name);
            t.position = pos;
            t.localScale = scale;
        }

        static void AddBox(GameObject go, Vector3 center, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        /// <summary>
        /// 灰盒对象缩放归一:碰撞体尺寸吸收原缩放、子锚点偏移同步补偿,
        /// 之后可安全挂任意朝向的成品视觉子节点(避免非均匀缩放剪切)。
        /// </summary>
        static void NormalizeScale(GameObject go)
        {
            Vector3 s = go.transform.localScale;
            if (s == Vector3.one)
            {
                return;
            }

            var box = go.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size = Vector3.Scale(box.size, s);
                box.center = Vector3.Scale(box.center, s);
            }

            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.radius *= Mathf.Max(s.x, s.z);
                capsule.height *= s.y;
                capsule.center = Vector3.Scale(capsule.center, s);
            }

            var sphere = go.GetComponent<SphereCollider>();
            if (sphere != null)
            {
                sphere.radius *= Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                sphere.center = Vector3.Scale(sphere.center, s);
            }

            foreach (Transform child in go.transform)
            {
                child.localPosition = Vector3.Scale(child.localPosition, s);
                child.localScale = Vector3.Scale(child.localScale, s);
            }

            go.transform.localScale = Vector3.one;
        }

        static GameObject InstantiateVisual(string fbxRelPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/" + fbxRelPath + ".fbx");
            if (prefab == null)
            {
                throw new InvalidOperationException("fbx missing: " + fbxRelPath);
            }

            string instanceName = Path.GetFileName(fbxRelPath) + "_Visual";
            DestroySceneObjectsNamed(instanceName);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = instanceName;
            return instance;
        }

        static void DestroySceneObjectsNamed(string objectName)
        {
            var matches = new List<GameObject>();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == objectName)
                    {
                        matches.Add(candidate.gameObject);
                    }
                }
            }

            for (int i = 0; i < matches.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(matches[i]);
            }
        }

        static GameObject AddStaticVisual(Transform parent, string fbxRelPath, Vector3 pos, float rotY,
                                          Dictionary<string, GameObject> visuals)
        {
            GameObject instance = InstantiateVisual(fbxRelPath);
            instance.transform.SetParent(parent, false);
            instance.transform.position = pos;
            instance.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            visuals[Path.GetFileName(fbxRelPath)] = instance;
            return instance;
        }

        static GameObject AddChildVisual(string hostName, string fbxRelPath, Vector3 worldPos, float rotY,
                                         Dictionary<string, GameObject> visuals)
        {
            Transform host = Find(hostName);
            HideRenderer(host);
            GameObject instance = InstantiateVisual(fbxRelPath);
            instance.transform.SetParent(host, false);
            instance.transform.position = worldPos;
            instance.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            visuals[hostName] = instance;
            return instance;
        }

        static void AddChildVisualCentered(string hostName, string fbxRelPath, Dictionary<string, GameObject> visuals)
        {
            Transform host = Find(hostName);
            AddChildVisual(hostName, fbxRelPath, host.position, host.eulerAngles.y, visuals);
        }

        static Vector3 SocketWorld(GameObject visualInstance, string socketName)
        {
            Transform socket = FindDeep(visualInstance.transform, socketName);
            if (socket == null)
            {
                throw new InvalidOperationException(socketName + " missing in " + visualInstance.name);
            }

            return socket.position;
        }

        static void DeactivateByPrefix(GameObject root, string prefix)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        // ---- 验收截图 ----

        [MenuItem("ConGame/Art/Room Art Screenshots")]
        public static void Screenshot()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(ShotDir);

            Shoot("M4_fixed_camera.png", new Vector3(0f, 1.15f, -2.14f), new Vector3(0f, 1.0f, 0.4f), 62f, false, 0f);
            Shoot("M4_wake_chair.png", new Vector3(0f, 1.4f, 0.2f), new Vector3(0f, 0.9f, 2.35f), 55f, false, 0f);
            Shoot("M4_door_corner.png", new Vector3(0.8f, 1.5f, -1.2f), new Vector3(3.05f, 1.0f, 0.6f), 55f, false, 0f);
            Shoot("M4_west_side.png", new Vector3(1.5f, 1.5f, 0.6f), new Vector3(-2.8f, 0.9f, 1.4f), 55f, false, 0f);

            // 顶视:关掉成品天花再拍
            Transform ceiling = Find("ENV_RoomShell_Visual").Find("Shell_Ceiling");
            ceiling.gameObject.SetActive(false);
            Shoot("M4_topdown.png", new Vector3(0f, 12f, 0f), Vector3.zero, 0f, true, 4.5f);
            ceiling.gameObject.SetActive(true);
            Debug.Log("ARTINSTALL screenshots done");
        }

        static void Shoot(string file, Vector3 pos, Vector3 target, float fov, bool ortho, float orthoSize)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(target - pos, Vector3.up);
            if (ortho)
            {
                cam.orthographic = true;
                cam.orthographicSize = orthoSize;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                cam.fieldOfView = fov;
            }

            cam.nearClipPlane = 0.03f;
            var rt = new RenderTexture(960, 540, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, file), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("ARTINSTALL shot " + file);
        }
    }
}
