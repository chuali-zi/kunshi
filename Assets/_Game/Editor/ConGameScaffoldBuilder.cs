using System;
using System.Collections.Generic;
using ConGame.Bootstrap;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Config;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Data;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using ConGame.Presentation.Audio;
using ConGame.Presentation.Camera;
using ConGame.Presentation.CRT;

using ConGame.Presentation.M1;
using ConGame.Presentation.Timeline;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ConGame.Editor
{

    public static class ConGameScaffoldBuilder
    {
        private const string Root = "Assets/_Game";
        private const string ContentRoot = Root + "/Content";
        private const string SceneRoot = Root + "/Scenes";
        private const string ItemRoot = ContentRoot + "/Items";
        private const string CounterplayRoot = ContentRoot + "/Counterplays";
        private const string ConfigRoot = ContentRoot + "/Config";

        private const string PerformanceRoot = ContentRoot + "/Performances";
        private const string NavigationRoot = ContentRoot + "/Navigation";
        private const string NavMeshPath = NavigationRoot + "/NavMesh_Room_M1.asset";
        private const string MaterialRoot = ContentRoot + "/Materials";
        private const string PipelinePath = ConfigRoot + "/RP_ConGame.asset";
        private const string RendererPath = ConfigRoot + "/RP_ConGame_Renderer.asset";

        private const string BootstrapScenePath = SceneRoot + "/Bootstrap.unity";
        private const string MenuScenePath = SceneRoot + "/MainMenu.unity";
        private const string RoomScenePath = SceneRoot + "/Room_M1.unity";

        [MenuItem("ConGame/Scaffold/Build M1 Greybox", priority = 1)]
        public static void BuildM1Greybox()
        {
            EnsureFolders();
            bool inputBackendChanged = EnsureInputSystemBackend();
            EnsureRenderPipeline();
            GameConfigAsset config = EnsureAsset<GameConfigAsset>(ConfigRoot + "/CFG_Game.asset");
            EnsureAsset<DarkAdaptationConfigAsset>(ConfigRoot + "/CFG_DarkAdaptation.asset");
            Dictionary<string, ItemDefinitionAsset> items = BuildItemDefinitions();
            Dictionary<string, M1GreyboxPlayableAsset> performances = BuildM1Performances();
            CounterplayCatalogAsset counterplays = BuildCounterplayDefinitions(performances);
            Dictionary<string, Material> materials = BuildMaterials();

            BuildBootstrapScene();
            BuildMenuScene();
            BuildRoomScene(config, counterplays, items, materials);
            SetBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ConGameProjectValidator.ValidateProject(true);
            Debug.Log(inputBackendChanged ? "[ConGame] M1 greybox rebuilt. Restart Unity once because the new Input " +
                                                "System backend was enabled."
                                          : "[ConGame] M1 greybox rebuilt and ready for playtesting.");
        }

        [MenuItem("ConGame/Scaffold/Open M1 Room", priority = 2)]
        public static void OpenM1Room()
        {
            if (!System.IO.File.Exists(RoomScenePath))
            {
                BuildM1Greybox();
            }

            EditorSceneManager.OpenScene(RoomScenePath);
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(ContentRoot);
            EnsureFolder(SceneRoot);
            EnsureFolder(ItemRoot);
            EnsureFolder(CounterplayRoot);
            EnsureFolder(ConfigRoot);

            EnsureFolder(PerformanceRoot);
            EnsureFolder(NavigationRoot);
            EnsureFolder(MaterialRoot);
            EnsureFolder(ContentRoot + "/Timelines");
            EnsureFolder(Root + "/Art");
        }

        private static Dictionary<string, ItemDefinitionAsset> BuildItemDefinitions()
        {
            Dictionary<string, ItemDefinitionAsset> result =
                new Dictionary<string, ItemDefinitionAsset>(StringComparer.Ordinal);
            foreach (ItemSeed seed in ItemSeedLibrary.All)
            {
                string path = $"{ItemRoot}/ITEM_{Sanitize(seed.Id)}.asset";
                ItemDefinitionAsset asset = EnsureAsset<ItemDefinitionAsset>(path);
                asset.Configure(seed.Id, seed.DisplayName, seed.Verbs, seed.Categories, seed.Status);
                EditorUtility.SetDirty(asset);
                result.Add(seed.Id, asset);
            }

            return result;
        }

        private static CounterplayCatalogAsset BuildCounterplayDefinitions(
            IReadOnlyDictionary<string, M1GreyboxPlayableAsset> performances)
        {
            List<CounterplayDefinitionAsset> definitions = new List<CounterplayDefinitionAsset>();
            foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
            {
                string path = $"{CounterplayRoot}/CP_{seed.Id}.asset";
                CounterplayDefinitionAsset asset = EnsureAsset<CounterplayDefinitionAsset>(path);
                performances.TryGetValue(seed.Id, out M1GreyboxPlayableAsset performance);
                asset.Configure(seed.Id, seed.SortOrder, seed.TriggerMode, seed.Conditions, seed.Status, performance);
                EditorUtility.SetDirty(asset);
                definitions.Add(asset);
            }

            CounterplayCatalogAsset catalog =
                EnsureAsset<CounterplayCatalogAsset>(CounterplayRoot + "/CAT_Counterplays.asset");
            catalog.Configure(definitions.ToArray());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static Dictionary<string, M1GreyboxPlayableAsset> BuildM1Performances()
        {
            Dictionary<string, M1GreyboxPlayableAsset> result =
                new Dictionary<string, M1GreyboxPlayableAsset>(StringComparer.Ordinal);

            foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
            {
                M1GreyboxDeathStyle style =
                    seed.Id == "D03" || seed.Id == "D03P"
                        ? M1GreyboxDeathStyle.SofaRearShot
                        : seed.Id == "D07" || seed.Id == "D08" || seed.Id == "D21" || seed.Id == "D22"
                            ? M1GreyboxDeathStyle.ArmedStandoff
                            : M1GreyboxDeathStyle.DefaultShot;
                float duration = seed.Id == "D21" ? 12f : seed.Id == "D03P" ? 8f : seed.Id == "D08" ? 5f : 1.8f;
                QteWindowDefinition[] windows = Array.Empty<QteWindowDefinition>();
                PerformanceBranchDefinition[] branches = Array.Empty<PerformanceBranchDefinition>();
                PerformanceHeldItemPolicy policy = PerformanceHeldItemPolicy.Preserve;

                if (seed.Id == "D08")
                {
                    windows = new[] { new QteWindowDefinition(1.1f, 4), new QteWindowDefinition(1f, 5),
                                      new QteWindowDefinition(0.9f, 6) };
                    branches = new[] { new PerformanceBranchDefinition("disarm_window", true, "D22"),
                                       new PerformanceBranchDefinition("close_shot", false, "D08") };
                    policy = PerformanceHeldItemPolicy.DropAtStart;
                }
                else if (seed.Id == "D21")
                {
                    windows = new[] { new QteWindowDefinition(1.5f, 5), new QteWindowDefinition(1.3f, 6),
                                      new QteWindowDefinition(1.2f, 7) };
                    branches = new[] { new PerformanceBranchDefinition("flashlight_victory", true, "D21", true),
                                       new PerformanceBranchDefinition("backup_pistol", false, "D21") };
                    policy = PerformanceHeldItemPolicy.DropAtStart;
                }
                else if (seed.Id == "D22")
                {
                    branches = new[] { new PerformanceBranchDefinition("single_round_dodged", true, "D22") };
                    policy = PerformanceHeldItemPolicy.LockToPerformance;
                }

                M1GreyboxPlayableAsset asset =
                    EnsureAsset<M1GreyboxPlayableAsset>($"{PerformanceRoot}/PERF_{seed.Id}_Greybox.asset");
                asset.Configure(seed.Id, style, duration, policy, windows, branches);
                EditorUtility.SetDirty(asset);
                result.Add(seed.Id, asset);
            }

            return result;
        }

        private static Dictionary<string, Material> BuildMaterials()
        {
            return new Dictionary<string, Material>(StringComparer.Ordinal) {
                ["wall"] = EnsureMaterial("MAT_Greybox_Wall", new Color(0.55f, 0.49f, 0.39f)),
                ["floor"] = EnsureMaterial("MAT_Greybox_Floor", new Color(0.10f, 0.20f, 0.14f)),
                ["wood"] = EnsureMaterial("MAT_Greybox_Wood", new Color(0.25f, 0.12f, 0.06f)),
                ["chair"] = EnsureMaterial("MAT_Greybox_Armchair", new Color(0.32f, 0.04f, 0.06f)),
                ["player"] = EnsureMaterial("MAT_Greybox_Player", new Color(0.76f, 0.68f, 0.58f)),
                ["killer"] = EnsureMaterial("MAT_Greybox_Killer", new Color(0.035f, 0.035f, 0.045f)),
                ["interactable"] = EnsureMaterial("MAT_Greybox_Interactable", new Color(0.67f, 0.42f, 0.12f)),
                ["tv"] = EnsureMaterial("MAT_Greybox_TV", new Color(0.04f, 0.04f, 0.04f))
            };
        }

        private static void EnsureRenderPipeline()
        {
            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                UniversalRendererData renderer = EnsureAsset<UniversalRendererData>(RendererPath);
                renderer.name = "RP_ConGame_Renderer";
                EditorUtility.SetDirty(renderer);

                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "RP_ConGame";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            pipeline.supportsHDR = true;
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 2;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }

        private static bool EnsureInputSystemBackend()
        {
            UnityEngine.Object[] settingsAssets =
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets == null || settingsAssets.Length == 0)
            {
                Debug.LogWarning(
                    "Could not inspect PlayerSettings; set Active Input Handling to Input System Package " +
                    "(New) before playtesting.");
                return false;
            }

            SerializedObject settings = new SerializedObject(settingsAssets[0]);
            SerializedProperty activeInputHandler = settings.FindProperty("activeInputHandler");
            if (activeInputHandler == null || activeInputHandler.intValue == 1)
            {
                return false;
            }

            activeInputHandler.intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Debug.LogWarning("Enabled the new Input System backend. Restart Unity before playtesting.");
            return true;
        }

        private static void BuildBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("Bootstrap");
            BootstrapSceneController controller = root.AddComponent<BootstrapSceneController>();
            controller.Configure("MainMenu");
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void BuildMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("MainMenu");
            SimpleMainMenu menu = root.AddComponent<SimpleMainMenu>();
            menu.Configure("Room_M1");

            GameObject cameraObject = new GameObject("Menu Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

private static void BuildRoomScene(GameConfigAsset config, CounterplayCatalogAsset counterplays,
                                           IReadOnlyDictionary<string, ItemDefinitionAsset> items,
                                           IReadOnlyDictionary<string, Material> materials)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject roomRoot = new GameObject("Room_M1");
            GameObject systems = new GameObject("Systems");
            systems.transform.SetParent(roomRoot.transform);

            GameFlowController flow = systems.AddComponent<GameFlowController>();
            GameplayFactService facts = systems.AddComponent<GameplayFactService>();
            GameplayEventHub events = systems.AddComponent<GameplayEventHub>();
            LoopResetRegistry registry = systems.AddComponent<LoopResetRegistry>();
            LoopDirector loop = systems.AddComponent<LoopDirector>();
            RoomLightCoordinator lightCoordinator = systems.AddComponent<RoomLightCoordinator>();

            BuildRoomShell(roomRoot.transform, materials, out Transform doorHinge);
            Transform fixedCameraRig = BuildFixedCamera(roomRoot.transform, out Camera outputCamera);
            BuildPlayer(roomRoot.transform, fixedCameraRig, config, facts, events, items, materials,
                        out PlayerMotor playerMotor, out PlayerInputRouter input, out PlayerInteractor interactor,
                        out GameObject firstPersonRig);

            CameraModeController cameraMode = systems.AddComponent<CameraModeController>();
            cameraMode.Configure(fixedCameraRig.gameObject, firstPersonRig);

            BuildFurnitureAndZones(roomRoot.transform, facts, events, items, materials);
            BuildBat(roomRoot.transform, items["baseball_bat"], materials["interactable"]);
            TelevisionController television =
                BuildTelevision(roomRoot.transform, facts, events, flow, items["crt_tv"], materials["tv"]);
            BuildLights(roomRoot.transform, facts, items, materials, out BreakableLight ceiling, out BreakableLight table);
            lightCoordinator.Configure(ceiling, table, facts);

            DarkAdaptationConfigAsset darkConfig =
                AssetDatabase.LoadAssetAtPath<DarkAdaptationConfigAsset>(ConfigRoot + "/CFG_DarkAdaptation.asset");

            Material crtMaterial = EnsureCrtMaterial();
            CrtSignalController signal = systems.AddComponent<CrtSignalController>();
            CrtFullscreenPass crtPass = systems.AddComponent<CrtFullscreenPass>();
            crtPass.Configure(signal, crtMaterial, outputCamera);
            signal.enabled = false;
            signal.Configure(television, loop);
            signal.enabled = true;

            CrtStatePresenter tvPresenter = television.GetComponent<CrtStatePresenter>();
            tvPresenter?.Configure(television, signal, crtPass, crtMaterial);

            BuildExtendedInteractables(roomRoot.transform, doorHinge, facts, events, items, materials, television,
                                       out GameObject pistolDrop, out GameObject flashlightDrop,
                                       out GameObject backupPistolDrop);

            KillerDirector killer = BuildKiller(roomRoot.transform, playerMotor, interactor, facts, counterplays,
                                                config, materials["killer"]);
            RoomDoorBreachController breach = systems.AddComponent<RoomDoorBreachController>();
            breach.Configure(doorHinge, config.BreachDuration, config.BreachOpenAngle);

            registry.Configure(roomRoot.transform);
            loop.Configure(config, flow, registry, facts, events, input, playerMotor, killer, breach, darkConfig);

            M1GreyboxPerformancePlayer performancePlayer =
                killer.GetComponent<M1GreyboxPerformancePlayer>();
            CounterplayPerformanceRuntimeAdapter performanceAdapter =
                systems.AddComponent<CounterplayPerformanceRuntimeAdapter>();
            performanceAdapter.enabled = false;
            performanceAdapter.Configure(input, loop, performancePlayer);
            performanceAdapter.enabled = true;

            CounterplayNextOutcomeBridge nextOutcomeBridge = systems.AddComponent<CounterplayNextOutcomeBridge>();
            nextOutcomeBridge.enabled = false;
            nextOutcomeBridge.Configure(performanceAdapter, killer);
            nextOutcomeBridge.enabled = true;

            StolenLootDropController lootDrop = systems.AddComponent<StolenLootDropController>();
            lootDrop.Configure(pistolDrop, flashlightDrop, performanceAdapter, backupPistolDrop);

            FirstPersonLookController fpLook = playerMotor.gameObject.AddComponent<FirstPersonLookController>();
            fpLook.Configure(input, playerMotor.transform, firstPersonRig.transform);
            fpLook.LookEnabled = false;

            PauseMenuController pause = playerMotor.gameObject.AddComponent<PauseMenuController>();
            pause.Configure(input, "MainMenu");

            AwakeningPresenter awakening = systems.AddComponent<AwakeningPresenter>();
            awakening.enabled = false;
            awakening.Configure(loop, crtPass);
            awakening.enabled = true;

            SemanticAudioDirector audio = systems.AddComponent<SemanticAudioDirector>();
            audio.enabled = false;
            audio.Configure(flow, loop, television);
            audio.EnsureProceduralBindings();
            audio.enabled = true;

            RemoteUseHandler remoteUse = roomRoot.GetComponentInChildren<RemoteUseHandler>(true);
            remoteUse?.Configure(television, crtPass, loop);

            Transform chairAnchor = new GameObject("EndingChairAnchor").transform;
            chairAnchor.SetParent(roomRoot.transform);
            chairAnchor.SetPositionAndRotation(new Vector3(-0.7f, 0f, -1.35f), Quaternion.identity);
            Transform killerAnchor = new GameObject("EndingKillerAnchor").transform;
            killerAnchor.SetParent(roomRoot.transform);
            killerAnchor.SetPositionAndRotation(new Vector3(0.2f, 0f, -0.3f), Quaternion.LookRotation(Vector3.back));

            EndingDirector ending = systems.AddComponent<EndingDirector>();
            ending.enabled = false;
            ending.Configure(flow, loop, registry, input, playerMotor, killer, television, cameraMode,
                             chairAnchor, killerAnchor, "MainMenu", fpLook, crtPass);
            ending.enabled = true;

            RoomContext context = systems.AddComponent<RoomContext>();
            context.Configure(config, flow, facts, events, registry, loop, input, playerMotor, killer);

            BuildNavigation(roomRoot.transform);
            killer.gameObject.SetActive(false);
            outputCamera.transform.SetPositionAndRotation(fixedCameraRig.position, fixedCameraRig.rotation);
            Selection.activeObject = systems;
            EditorSceneManager.SaveScene(scene, RoomScenePath);
        }

        private static void BuildRoomShell(Transform parent, IReadOnlyDictionary<string, Material> materials,
                                           out Transform doorHinge)
        {
            GameObject shell = new GameObject("Environment");
            shell.transform.SetParent(parent);

            CreateCube("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(6.2f, 0.1f, 5.2f), shell.transform,
                       materials["floor"]);
            CreateCube("North Wall", new Vector3(0f, 1.4f, 2.65f), new Vector3(6.4f, 2.8f, 0.1f), shell.transform,
                       materials["wall"]);
            CreateCube("South Wall", new Vector3(0f, 1.4f, -2.65f), new Vector3(6.4f, 2.8f, 0.1f), shell.transform,
                       materials["wall"]);
            CreateCube("West Wall", new Vector3(-3.15f, 1.4f, 0f), new Vector3(0.1f, 2.8f, 5.2f), shell.transform,
                       materials["wall"]);

            CreateCube("East Wall Lower", new Vector3(3.15f, 1.4f, -0.6f), new Vector3(0.1f, 2.8f, 3.2f),
                       shell.transform, materials["wall"]);
            CreateCube("East Wall Upper", new Vector3(3.15f, 1.4f, 2.3f), new Vector3(0.1f, 2.8f, 0.7f),
                       shell.transform, materials["wall"]);
            CreateCube("Door Lintel", new Vector3(3.15f, 2.48f, 1.45f), new Vector3(0.1f, 0.64f, 1.0f), shell.transform,
                       materials["wall"]);
            CreateCube("Ceiling", new Vector3(0f, 2.85f, 0f), new Vector3(6.4f, 0.1f, 5.4f), shell.transform,
                       materials["wall"]);

            GameObject hinge = new GameObject("DoorHinge");
            hinge.transform.SetParent(shell.transform);
            hinge.transform.position = new Vector3(3.05f, 0f, 1f);
            GameObject door = CreateCube("Door", new Vector3(3.05f, 1.025f, 1.45f), new Vector3(0.12f, 2.05f, 0.9f),
                                         hinge.transform, materials["wood"]);
            doorHinge = hinge.transform;

            CreateCube("Corridor Floor", new Vector3(3.7f, -0.05f, 1.45f), new Vector3(1.2f, 0.1f, 1.2f),
                       shell.transform, materials["floor"]);
            CreateCube("Black Corridor", new Vector3(4.28f, 1.3f, 1.45f), new Vector3(0.1f, 2.6f, 1.2f),
                       shell.transform, materials["killer"]);
        }

        private static Transform BuildFixedCamera(Transform parent, out Camera outputCamera)
        {
            Vector3 position = new Vector3(0f, 1.15f, -2.14f);
            Quaternion rotation = Quaternion.LookRotation(new Vector3(0f, 1.0f, 0.4f) - position, Vector3.up);

            GameObject output = new GameObject("Main Camera");
            output.tag = "MainCamera";
            output.transform.SetParent(parent);
            output.transform.SetPositionAndRotation(position, rotation);
            outputCamera = output.AddComponent<Camera>();
            outputCamera.fieldOfView = 62f;
            outputCamera.nearClipPlane = 0.03f;
            outputCamera.farClipPlane = 30f;
            output.AddComponent<AudioListener>();
            AddOptionalMonoBehaviour(output, "CinemachineBrain");

            GameObject rig = new GameObject("CM Fixed Television Camera");
            rig.transform.SetParent(parent);
            rig.transform.SetPositionAndRotation(position, rotation);
            AddOptionalMonoBehaviour(rig, "CinemachineCamera", "CinemachineVirtualCamera");
            return rig.transform;
        }

        private static void BuildPlayer(Transform parent, Transform fixedCamera, GameConfigAsset config,
                                        GameplayFactService facts, GameplayEventHub events,
                                        IReadOnlyDictionary<string, ItemDefinitionAsset> items,
                                        IReadOnlyDictionary<string, Material> materials, out PlayerMotor motor,
                                        out PlayerInputRouter input, out PlayerInteractor interactor,
                                        out GameObject firstPersonRig)
        {
            GameObject player = new GameObject("Player");
            player.transform.SetParent(parent);
            // Keep the awakening pose visually aligned with the chair without spawning
            // the CharacterController inside the greybox chair collider.
            player.transform.SetPositionAndRotation(new Vector3(-0.7f, 0f, -1.35f), Quaternion.identity);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "CH_Player_Greybox";
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = Vector3.up;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = materials["player"];

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = Vector3.up;
            input = player.AddComponent<PlayerInputRouter>();
            motor = player.AddComponent<PlayerMotor>();
            interactor = player.AddComponent<PlayerInteractor>();
            InteractionScanner scanner = player.AddComponent<InteractionScanner>();

            GameObject hand = new GameObject("HandSocket_R");
            hand.transform.SetParent(player.transform, false);
            hand.transform.localPosition = new Vector3(0.35f, 1.15f, 0.45f);

            firstPersonRig = new GameObject("CM First Person Camera");
            firstPersonRig.transform.SetParent(player.transform, false);
            firstPersonRig.transform.localPosition = new Vector3(0f, 1.65f, 0.05f);
            AddOptionalMonoBehaviour(firstPersonRig, "CinemachineCamera", "CinemachineVirtualCamera");
            firstPersonRig.SetActive(false);

            motor.Configure(controller, input, fixedCamera, facts, config);
            scanner.Configure(interactor, config.InteractionRadius, ~0);
            interactor.Configure(input, scanner, hand.transform, motor, facts, events, config);
        }

        private static void BuildFurnitureAndZones(Transform parent, GameplayFactService facts,
                                                   GameplayEventHub events,
                                                   IReadOnlyDictionary<string, ItemDefinitionAsset> items,
                                                   IReadOnlyDictionary<string, Material> materials)
        {
            GameObject furniture = new GameObject("Furniture");
            furniture.transform.SetParent(parent);

            GameObject armchair = CreateCube("FUR_Armchair", new Vector3(-0.7f, 0.575f, -0.55f),
                                             new Vector3(0.95f, 1.15f, 0.75f), furniture.transform, materials["chair"]);
            InteractableEntity chairEntity = armchair.AddComponent<InteractableEntity>();
            chairEntity.Configure(items["sofa_awaken"]);
            HideCapability hide = armchair.AddComponent<HideCapability>();
            armchair.AddComponent<InspectCapability>();
            armchair.AddComponent<TransformResettable>();

            GameObject hideAnchor = new GameObject("HideAnchor_SofaRear");
            hideAnchor.transform.SetParent(armchair.transform, false);
            hideAnchor.transform.localPosition = new Vector3(0f, -0.5f, 1.05f);
            hideAnchor.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            hide.Configure(hideAnchor.transform, PlayerZone.SofaRear);

            GameObject sofaZone = new GameObject("Zone_SofaRear");
            sofaZone.transform.SetParent(furniture.transform);
            sofaZone.transform.position = new Vector3(-0.7f, 0.8f, 0.15f);
            BoxCollider sofaTrigger = sofaZone.AddComponent<BoxCollider>();
            sofaTrigger.size = new Vector3(1.25f, 1.6f, 0.7f);
            PlayerZoneTrigger zone = sofaZone.AddComponent<PlayerZoneTrigger>();
            zone.Configure(PlayerZone.SofaRear, facts);

            GameObject awakeningZone = new GameObject("Zone_Awakening");
            awakeningZone.transform.SetParent(furniture.transform);
            awakeningZone.transform.position = new Vector3(-0.7f, 0.9f, -0.55f);
            BoxCollider awakeningTrigger = awakeningZone.AddComponent<BoxCollider>();
            awakeningTrigger.size = new Vector3(1.1f, 1.8f, 1.0f);
            PlayerZoneTrigger awakening = awakeningZone.AddComponent<PlayerZoneTrigger>();
            awakening.Configure(PlayerZone.Awakening, facts);

            GameObject bed = CreateCube("FUR_Bed", new Vector3(1.7f, 0.3f, 1.85f), new Vector3(2.0f, 0.6f, 1.0f),
                                        furniture.transform, materials["wood"]);
            WireBed(bed, items["bed"], facts);

            GameObject bookcase = CreateCube("FUR_Bookcase", new Vector3(-2.65f, 1.05f, 1.65f),
                                             new Vector3(0.8f, 2.1f, 0.35f), furniture.transform, materials["wood"]);
            WireBookcase(bookcase, items["bookcase"], facts, events);

            GameObject dresser = CreateCube("FUR_Dresser", new Vector3(-1.45f, 0.425f, 2.1f),
                                            new Vector3(1.1f, 0.85f, 0.48f), furniture.transform, materials["wood"]);
            WireCarryableFurniture(dresser, items["dresser"], 0.33f, false);

            GameObject woodChair = CreateCube("FUR_WoodChair", new Vector3(-0.1f, 0.48f, 1.7f),
                                              new Vector3(0.45f, 0.95f, 0.5f), furniture.transform,
                                              materials["interactable"]);
            WireCarryableFurniture(woodChair, items["wood_chair"], 0.5f, true);
        }

        private static void WireBookcase(GameObject bookcase, ItemDefinitionAsset definition,
                                         GameplayFactService facts, GameplayEventHub events)
        {
            InteractableEntity entity = bookcase.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            TipBookcaseUseHandler tip = bookcase.AddComponent<TipBookcaseUseHandler>();
            tip.Configure(facts, events, new Vector3(0f, 0f, 85f));
            UseCapability use = bookcase.AddComponent<UseCapability>();
            use.Configure(tip);
            HideCapability hide = bookcase.AddComponent<HideCapability>();
            bookcase.AddComponent<InspectCapability>();
            bookcase.AddComponent<TransformResettable>();

            GameObject hideAnchor = new GameObject("HideAnchor_Bookcase");
            hideAnchor.transform.SetParent(bookcase.transform, false);
            hideAnchor.transform.localPosition = new Vector3(0f, -0.7f, 0.55f);
            hideAnchor.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            hide.Configure(hideAnchor.transform, PlayerZone.Other);

            GameObject cameraBlind = new GameObject("Zone_CameraBlind");
            cameraBlind.transform.SetParent(bookcase.transform.parent, false);
            cameraBlind.transform.position = bookcase.transform.position + new Vector3(-0.55f, 0f, 0.15f);
            BoxCollider blindTrigger = cameraBlind.AddComponent<BoxCollider>();
            blindTrigger.size = new Vector3(0.7f, 2.0f, 1.0f);
            PlayerZoneTrigger blindZone = cameraBlind.AddComponent<PlayerZoneTrigger>();
            blindZone.Configure(PlayerZone.CameraBlind, facts);
        }

        private static void WireBed(GameObject bed, ItemDefinitionAsset definition, GameplayFactService facts)
        {
            InteractableEntity entity = bed.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            bed.AddComponent<InspectCapability>();
            HideCapability underHide = bed.AddComponent<HideCapability>();
            bed.AddComponent<TransformResettable>();

            GameObject underAnchor = new GameObject("HideAnchor_UnderBed");
            underAnchor.transform.SetParent(bed.transform, false);
            underAnchor.transform.localPosition = new Vector3(0f, -0.15f, -0.55f);
            underAnchor.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            underHide.Configure(underAnchor.transform, PlayerZone.UnderBed);

            GameObject underBedZone = new GameObject("Zone_UnderBed");
            underBedZone.transform.SetParent(bed.transform.parent, false);
            underBedZone.transform.position = bed.transform.position + new Vector3(0f, 0.35f, -0.55f);
            BoxCollider trigger = underBedZone.AddComponent<BoxCollider>();
            trigger.size = new Vector3(1.8f, 0.9f, 0.7f);
            PlayerZoneTrigger zone = underBedZone.AddComponent<PlayerZoneTrigger>();
            zone.Configure(PlayerZone.UnderBed, facts);

            // Separate top-of-bed hide for D04 (Bed + Hidden). HideCapability is single-instance.
            GameObject bedTop = CreateCube("PRP_BedTopHide", bed.transform.position + new Vector3(0f, 0.45f, 0f),
                                           new Vector3(1.6f, 0.08f, 0.85f), bed.transform.parent,
                                           bed.GetComponent<Renderer>().sharedMaterial);
            InteractableEntity topEntity = bedTop.AddComponent<InteractableEntity>();
            topEntity.Configure(definition);
            HideCapability topHide = bedTop.AddComponent<HideCapability>();
            bedTop.AddComponent<InspectCapability>();
            bedTop.AddComponent<TransformResettable>();
            GameObject topAnchor = new GameObject("HideAnchor_Bed");
            topAnchor.transform.SetParent(bedTop.transform, false);
            topAnchor.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            topHide.Configure(topAnchor.transform, PlayerZone.Bed);

            GameObject bedZone = new GameObject("Zone_Bed");
            bedZone.transform.SetParent(bed.transform.parent, false);
            bedZone.transform.position = bed.transform.position + new Vector3(0f, 0.75f, 0f);
            BoxCollider bedTrigger = bedZone.AddComponent<BoxCollider>();
            bedTrigger.size = new Vector3(1.8f, 0.8f, 1.0f);
            PlayerZoneTrigger bedZoneTrigger = bedZone.AddComponent<PlayerZoneTrigger>();
            bedZoneTrigger.Configure(PlayerZone.Bed, facts);
        }

        private static void WireCarryableFurniture(GameObject furniture, ItemDefinitionAsset definition,
                                                   float carrySpeedMultiplier, bool meleeWeapon)
        {
            InteractableEntity entity = furniture.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            Rigidbody body = furniture.AddComponent<Rigidbody>();
            body.mass = meleeWeapon ? 8f : 18f;
            body.isKinematic = true;
            body.useGravity = true;
            StatefulWorldItem stateful = furniture.AddComponent<StatefulWorldItem>();
            stateful.Configure(body);
            CarryCapability carry = furniture.AddComponent<CarryCapability>();
            carry.Configure(carrySpeedMultiplier);
            furniture.AddComponent<InspectCapability>();
            if (meleeWeapon)
            {
                furniture.AddComponent<MeleeWeaponCapability>();
            }

            furniture.AddComponent<RigidbodyResettable>();
        }

        private static void BuildBat(Transform parent, ItemDefinitionAsset definition, Material material)
        {
            GameObject bat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bat.name = "PRP_BaseballBat";
            bat.transform.SetParent(parent);
            bat.transform.SetPositionAndRotation(new Vector3(-2.15f, 1.05f, 1.45f), Quaternion.Euler(0f, 0f, 90f));
            bat.transform.localScale = new Vector3(0.055f, 0.425f, 0.055f);
            bat.GetComponent<Renderer>().sharedMaterial = material;
            Rigidbody body = bat.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            InteractableEntity entity = bat.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            StatefulWorldItem stateful = bat.AddComponent<StatefulWorldItem>();
            stateful.Configure(body);
            bat.AddComponent<PickupCapability>();
            bat.AddComponent<MeleeWeaponCapability>();
            bat.AddComponent<InspectCapability>();
            bat.AddComponent<RigidbodyResettable>();
        }

        private static TelevisionController BuildTelevision(Transform parent, GameplayFactService facts, GameplayEventHub events,
                                            GameFlowController flow, ItemDefinitionAsset definition, Material material)
        {
            GameObject tv = CreateCube("PRP_CRTTV", new Vector3(0f, 0.9f, -2.38f), new Vector3(1.2f, 0.8f, 0.45f),
                                       parent, material);
            InteractableEntity entity = tv.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            TelevisionController controller = tv.AddComponent<TelevisionController>();
            controller.Configure(facts, events, flow);
            tv.AddComponent<CrtStatePresenter>().Configure(controller);
            tv.AddComponent<InspectCapability>();
            tv.AddComponent<TransformResettable>();

            GameObject screenAnchor = new GameObject("CameraScreenAnchor");
            screenAnchor.transform.SetParent(tv.transform, false);
            screenAnchor.transform.localPosition = new Vector3(0f, 0.31f, 0.51f);
            return controller;
        }

        private static void BuildLights(Transform parent, GameplayFactService facts,
                                        IReadOnlyDictionary<string, ItemDefinitionAsset> items,
                                        IReadOnlyDictionary<string, Material> materials,
                                        out BreakableLight ceilingBreakable, out BreakableLight tableBreakable)
        {
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ceiling.name = "PRP_CeilingLamp";
            ceiling.transform.SetParent(parent);
            ceiling.transform.position = new Vector3(0f, 2.55f, 0f);
            ceiling.transform.localScale = Vector3.one * 0.25f;
            ceiling.GetComponent<Renderer>().sharedMaterial = materials["interactable"];
            Light ceilingLight = ceiling.AddComponent<Light>();
            ceilingLight.type = LightType.Point;
            ceilingLight.range = 7f;
            ceilingLight.intensity = 5f;
            ceilingBreakable = ceiling.AddComponent<BreakableLight>();
            ceilingBreakable.Configure(ceilingLight);
            InteractableEntity ceilingEntity = ceiling.AddComponent<InteractableEntity>();
            ceilingEntity.Configure(items["ceiling_light"]);
            ceiling.AddComponent<InspectCapability>();
            ceiling.AddComponent<TransformResettable>();

            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "PRP_TableLamp";
            table.transform.SetParent(parent);
            table.transform.position = new Vector3(-1.45f, 1.05f, 2.1f);
            table.transform.localScale = new Vector3(0.25f, 0.45f, 0.25f);
            table.GetComponent<Renderer>().sharedMaterial = materials["interactable"];
            Light tableLight = table.AddComponent<Light>();
            tableLight.type = LightType.Point;
            tableLight.range = 4f;
            tableLight.color = new Color(1f, 0.65f, 0.35f);
            tableLight.intensity = 2f;
            tableBreakable = table.AddComponent<BreakableLight>();
            tableBreakable.Configure(tableLight);
            InteractableEntity tableEntity = table.AddComponent<InteractableEntity>();
            tableEntity.Configure(items["table_lamp"]);
            Rigidbody tableBody = table.AddComponent<Rigidbody>();
            tableBody.mass = 1.4f;
            tableBody.isKinematic = true;
            StatefulWorldItem tableStateful = table.AddComponent<StatefulWorldItem>();
            tableStateful.Configure(tableBody);
            table.AddComponent<PickupCapability>();
            ToggleLightUseHandler toggle = table.AddComponent<ToggleLightUseHandler>();
            toggle.Configure(tableBreakable);
            UseCapability tableUse = table.AddComponent<UseCapability>();
            tableUse.Configure(toggle);
            table.AddComponent<InspectCapability>();
            table.AddComponent<RigidbodyResettable>();
        }

        private static void BuildExtendedInteractables(Transform parent, Transform doorHinge,
                                                       GameplayFactService facts, GameplayEventHub events,
                                                       IReadOnlyDictionary<string, ItemDefinitionAsset> items,
                                                       IReadOnlyDictionary<string, Material> materials,
                                                       TelevisionController television,
                                                       out GameObject pistolDrop, out GameObject flashlightDrop,
                                                       out GameObject backupPistolDrop)
        {
            Material interactable = materials["interactable"];
            Material wood = materials["wood"];

            BuildDoorInteractable(parent, doorHinge, facts, items["room_door"], wood);
            BuildDoorTrapSocket(parent, facts, wood);
            BuildDoorBlindZone(parent, facts);
            BuildAmbushPoint(parent, facts, wood);
            BuildBlanket(parent, facts, events, items["blanket"], interactable);
            BuildRug(parent, facts, items["rug"], materials["floor"]);
            BuildStrikeWall(parent, facts, items["walls"], materials["wall"]);
            BuildBoardedWindow(parent, facts, items["boarded_window"], materials["wall"]);
            BuildThrowable(parent, items["books"], interactable, "PRP_Books",
                           new Vector3(-2.45f, 1.55f, 1.55f), new Vector3(0.28f, 0.12f, 0.2f), 0.6f);
            BuildThrowable(parent, items["vase"], interactable, "PRP_Vase",
                           new Vector3(-1.9f, 1.15f, 2.05f), new Vector3(0.18f, 0.35f, 0.18f), 0.9f);
            BuildThrowable(parent, items["wall_clock"], interactable, "PRP_WallClock",
                           new Vector3(1.55f, 1.85f, -2.45f), new Vector3(0.35f, 0.35f, 0.08f), 0.7f);
            BuildRemote(parent, television, items["remote"], interactable);
            BuildKillerCoat(parent, items["killer_coat"], materials["killer"]);

            pistolDrop = BuildPickupProp(parent, items["pistol_main"], interactable, "PRP_PistolMain",
                                         new Vector3(2.35f, 0.35f, 1.85f), new Vector3(0.22f, 0.08f, 0.12f), 0.8f);
            pistolDrop.SetActive(false);
            flashlightDrop = BuildFlashlight(parent, facts, events, items["flashlight"], interactable);
            flashlightDrop.SetActive(false);
            backupPistolDrop = BuildScriptedProp(parent, items["pistol_backup"], interactable, "PRP_PistolBackup",
                                                 new Vector3(2.55f, 0.95f, -0.35f), new Vector3(0.18f, 0.06f, 0.1f));
            backupPistolDrop.SetActive(false);
            BuildMirror(parent, facts, events, items["mirror"], materials["wall"]);
        }

        private static void BuildAmbushPoint(Transform parent, GameplayFactService facts, Material material)
        {
            GameObject ambush = CreateCube("PRP_AmbushPoint", new Vector3(2.55f, 0.55f, 1.85f),
                                           new Vector3(0.35f, 1.0f, 0.35f), parent, material);
            InteractableEntity entity = ambush.AddComponent<InteractableEntity>();
            entity.Configure(null, "ambush_point");
            GameplayAffordanceHandler affordance = ambush.AddComponent<GameplayAffordanceHandler>();
            affordance.Configure(GameplayAffordanceKind.StartAmbush, facts);
            UseCapability use = ambush.AddComponent<UseCapability>();
            use.Configure(affordance);
            ambush.AddComponent<InspectCapability>();
            ambush.AddComponent<TransformResettable>();
        }

        private static void BuildRemote(Transform parent, TelevisionController television,
                                        ItemDefinitionAsset definition, Material material)
        {
            GameObject remote = CreateCube("PRP_Remote", new Vector3(0.45f, 0.55f, -2.0f),
                                           new Vector3(0.18f, 0.05f, 0.08f), parent, material);
            InteractableEntity entity = remote.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            Rigidbody body = remote.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.isKinematic = true;
            StatefulWorldItem stateful = remote.AddComponent<StatefulWorldItem>();
            stateful.Configure(body);
            remote.AddComponent<PickupCapability>();
            RemoteUseHandler remoteUse = remote.AddComponent<RemoteUseHandler>();
            remoteUse.Configure(television);
            UseCapability use = remote.AddComponent<UseCapability>();
            use.Configure(remoteUse);
            remote.AddComponent<InspectCapability>();
            remote.AddComponent<RigidbodyResettable>();
        }

        private static void BuildKillerCoat(Transform parent, ItemDefinitionAsset definition, Material material)
        {
            GameObject coat = CreateCube("PRP_KillerCoat", new Vector3(2.55f, 1.15f, -0.55f),
                                         new Vector3(0.45f, 1.3f, 0.2f), parent, material);
            InteractableEntity entity = coat.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            coat.AddComponent<InspectCapability>();
            coat.AddComponent<TransformResettable>();
        }

        private static GameObject BuildScriptedProp(Transform parent, ItemDefinitionAsset definition, Material material,
                                                    string objectName, Vector3 position, Vector3 scale)
        {
            GameObject prop = CreateCube(objectName, position, scale, parent, material);
            InteractableEntity entity = prop.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            Rigidbody body = prop.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            StatefulWorldItem stateful = prop.AddComponent<StatefulWorldItem>();
            stateful.Configure(body, ItemPhysicalState.Scripted);
            prop.AddComponent<TransformResettable>();
            return prop;
        }

        private static void BuildDoorTrapSocket(Transform parent, GameplayFactService facts, Material material)
        {
            GameObject trapSocket = CreateCube("PRP_DoorTrapSocket", new Vector3(2.55f, 0.08f, 1.1f),
                                               new Vector3(0.9f, 0.06f, 0.55f), parent, material);
            Collider collider = trapSocket.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            InteractableEntity entity = trapSocket.AddComponent<InteractableEntity>();
            entity.Configure(null, "door_trap_socket");
            GameObject anchor = new GameObject("TrapAnchor");
            anchor.transform.SetParent(trapSocket.transform, false);
            PlacementSocket socket = trapSocket.AddComponent<PlacementSocket>();
            socket.Configure("door_trap", ItemCategory.TripTrap, anchor.transform, facts, DoorBlockState.None,
                             TrapState.BlanketArmed);
            PlaceCapability place = trapSocket.AddComponent<PlaceCapability>();
            place.Configure(socket);
            trapSocket.AddComponent<InspectCapability>();
            trapSocket.AddComponent<TransformResettable>();
        }

        private static void BuildBlanket(Transform parent, GameplayFactService facts, GameplayEventHub events,
                                         ItemDefinitionAsset definition, Material material)
        {
            GameObject blanket = CreateCube("PRP_Blanket", new Vector3(1.7f, 0.65f, 1.85f),
                                            new Vector3(1.2f, 0.12f, 0.85f), parent, material);
            InteractableEntity entity = blanket.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            Rigidbody body = blanket.AddComponent<Rigidbody>();
            body.mass = 1.2f;
            body.isKinematic = true;
            StatefulWorldItem stateful = blanket.AddComponent<StatefulWorldItem>();
            stateful.Configure(body);
            blanket.AddComponent<PickupCapability>();
            BlanketTrapUseHandler trap = blanket.AddComponent<BlanketTrapUseHandler>();
            trap.Configure(facts, events);
            UseCapability use = blanket.AddComponent<UseCapability>();
            use.Configure(trap);
            blanket.AddComponent<InspectCapability>();
            blanket.AddComponent<RigidbodyResettable>();
        }

        private static GameObject BuildPickupProp(Transform parent, ItemDefinitionAsset definition, Material material,
                                                  string objectName, Vector3 position, Vector3 scale, float mass)
        {
            GameObject item = CreateCube(objectName, position, scale, parent, material);
            InteractableEntity entity = item.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            StatefulWorldItem stateful = item.AddComponent<StatefulWorldItem>();
            stateful.Configure(body);
            item.AddComponent<PickupCapability>();
            item.AddComponent<InspectCapability>();
            item.AddComponent<RigidbodyResettable>();
            return item;
        }

        private static GameObject BuildFlashlight(Transform parent, GameplayFactService facts, GameplayEventHub events,
                                                  ItemDefinitionAsset definition, Material material)
        {
            GameObject flashlight = BuildPickupProp(parent, definition, material, "PRP_Flashlight",
                                                    new Vector3(2.2f, 0.35f, 1.55f), new Vector3(0.12f, 0.12f, 0.35f),
                                                    0.5f);
            FlashlightUseHandler useHandler = flashlight.AddComponent<FlashlightUseHandler>();
            useHandler.Configure(facts, events);
            UseCapability use = flashlight.AddComponent<UseCapability>();
            use.Configure(useHandler);
            return flashlight;
        }

        private static void BuildMirror(Transform parent, GameplayFactService facts, GameplayEventHub events,
                                        ItemDefinitionAsset definition, Material material)
        {
            GameObject mirror = CreateCube("PRP_Mirror", new Vector3(-3.05f, 1.55f, -1.1f),
                                           new Vector3(0.08f, 0.9f, 0.55f), parent, material);
            InteractableEntity entity = mirror.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            MirrorFlashlightReactive reactive = mirror.AddComponent<MirrorFlashlightReactive>();
            reactive.Configure(facts, events);
            GameplayAffordanceHandler affordance = mirror.AddComponent<GameplayAffordanceHandler>();
            affordance.Configure(GameplayAffordanceKind.ActivateGlare, facts);
            UseCapability use = mirror.AddComponent<UseCapability>();
            use.Configure(affordance);
            mirror.AddComponent<InspectCapability>();
            mirror.AddComponent<TransformResettable>();
        }

        private static void BuildDoorInteractable(Transform parent, Transform doorHinge, GameplayFactService facts,
                                                  ItemDefinitionAsset definition, Material material)
        {
            GameObject doorProxy = CreateCube("PRP_RoomDoor_Interact", new Vector3(2.7f, 1.05f, 1.45f),
                                              new Vector3(0.35f, 1.9f, 0.85f), parent, material);
            Collider proxyCollider = doorProxy.GetComponent<Collider>();
            if (proxyCollider != null)
            {
                proxyCollider.isTrigger = true;
            }

            InteractableEntity entity = doorProxy.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            doorProxy.AddComponent<InspectCapability>();

            GameObject socketAnchor = new GameObject("DoorBlockAnchor");
            socketAnchor.transform.SetParent(doorProxy.transform, false);
            socketAnchor.transform.localPosition = new Vector3(-0.2f, -0.55f, 0f);

            PlacementSocket socket = doorProxy.AddComponent<PlacementSocket>();
            socket.Configure("door_block", ItemCategory.DoorBlocker, socketAnchor.transform, facts,
                             DoorBlockState.Furniture);
            PlaceCapability place = doorProxy.AddComponent<PlaceCapability>();
            place.Configure(socket);
            doorProxy.AddComponent<TransformResettable>();
        }

        private static void BuildDoorBlindZone(Transform parent, GameplayFactService facts)
        {
            GameObject zoneObject = new GameObject("Zone_DoorBlind");
            zoneObject.transform.SetParent(parent);
            zoneObject.transform.position = new Vector3(2.55f, 0.9f, 1.45f);
            BoxCollider trigger = zoneObject.AddComponent<BoxCollider>();
            trigger.size = new Vector3(0.7f, 1.8f, 1.0f);
            PlayerZoneTrigger zone = zoneObject.AddComponent<PlayerZoneTrigger>();
            zone.Configure(PlayerZone.DoorBlind, facts);
        }

        private static void BuildRug(Transform parent, GameplayFactService facts, ItemDefinitionAsset definition,
                                     Material material)
        {
            GameObject rug = CreateCube("PRP_Rug", new Vector3(0.3f, 0.03f, 0.6f), new Vector3(1.6f, 0.04f, 1.1f),
                                        parent, material);
            InteractableEntity entity = rug.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            GameplayAffordanceHandler affordance = rug.AddComponent<GameplayAffordanceHandler>();
            affordance.Configure(GameplayAffordanceKind.PullRugTrap, facts);
            UseCapability use = rug.AddComponent<UseCapability>();
            use.Configure(affordance);
            rug.AddComponent<InspectCapability>();
            rug.AddComponent<TransformResettable>();
        }

        private static void BuildStrikeWall(Transform parent, GameplayFactService facts,
                                            ItemDefinitionAsset definition, Material material)
        {
            GameObject wall = CreateCube("PRP_StrikeWall", new Vector3(-2.95f, 1.4f, 0f),
                                         new Vector3(0.12f, 2.2f, 2.4f), parent, material);
            InteractableEntity entity = wall.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            StrikeSurfaceHitReceiver hitReceiver = wall.AddComponent<StrikeSurfaceHitReceiver>();
            hitReceiver.Configure(facts, true);
            GameplayAffordanceHandler affordance = wall.AddComponent<GameplayAffordanceHandler>();
            affordance.Configure(GameplayAffordanceKind.StrikeWall, facts);
            UseCapability use = wall.AddComponent<UseCapability>();
            use.Configure(affordance);
            wall.AddComponent<InspectCapability>();
            wall.AddComponent<TransformResettable>();
        }

        private static void BuildBoardedWindow(Transform parent, GameplayFactService facts,
                                               ItemDefinitionAsset definition, Material material)
        {
            GameObject window = CreateCube("PRP_BoardedWindow", new Vector3(0f, 1.55f, 2.55f),
                                           new Vector3(1.4f, 1.1f, 0.12f), parent, material);
            InteractableEntity entity = window.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            StrikeSurfaceHitReceiver hitReceiver = window.AddComponent<StrikeSurfaceHitReceiver>();
            hitReceiver.Configure(facts, false);
            GameplayAffordanceHandler affordance = window.AddComponent<GameplayAffordanceHandler>();
            affordance.Configure(GameplayAffordanceKind.StrikeWindow, facts);
            UseCapability use = window.AddComponent<UseCapability>();
            use.Configure(affordance);
            window.AddComponent<InspectCapability>();
            window.AddComponent<TransformResettable>();
        }

        private static void BuildThrowable(Transform parent, ItemDefinitionAsset definition, Material material,
                                           string objectName, Vector3 position, Vector3 scale, float mass)
        {
            GameObject item = CreateCube(objectName, position, scale, parent, material);
            InteractableEntity entity = item.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            StatefulWorldItem stateful = item.AddComponent<StatefulWorldItem>();
            stateful.Configure(body);
            item.AddComponent<PickupCapability>();
            item.AddComponent<InspectCapability>();
            item.AddComponent<RigidbodyResettable>();
        }

        private static KillerDirector BuildKiller(Transform parent, PlayerMotor player, PlayerInteractor interactor,
                                                  GameplayFactService facts, CounterplayCatalogAsset counterplays,
                                                  GameConfigAsset config, Material material)
        {
            GameObject killer = new GameObject("Killer");
            killer.transform.SetParent(parent);
            killer.transform.position = new Vector3(3.62f, 0f, 1.45f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "CH_Killer_Greybox";
            visual.transform.SetParent(killer.transform, false);
            visual.transform.localPosition = Vector3.up;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;

            NavMeshAgent agent = killer.AddComponent<NavMeshAgent>();
            agent.speed = 2.4f;
            agent.angularSpeed = 540f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 2.6f;

            PlayableDirector director = killer.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            killer.AddComponent<TimelineSequenceRunner>();

            M1GreyboxPerformancePlayer performance = killer.AddComponent<M1GreyboxPerformancePlayer>();
            performance.Configure(killer.transform, player, interactor);
            KillerDirector killerDirector = killer.AddComponent<KillerDirector>();
            killerDirector.Configure(agent, player.transform, facts, counterplays, config, performance);

            GameObject lightObject = new GameObject("KillerFlashlight");
            lightObject.transform.SetParent(killer.transform, false);
            lightObject.transform.localPosition = new Vector3(0.2f, 1.35f, 0.35f);
            lightObject.transform.localRotation = Quaternion.identity;
            Light spot = lightObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 8f;
            spot.spotAngle = 42f;
            spot.intensity = 3.5f;
            spot.color = new Color(0.85f, 0.92f, 1f);
            spot.enabled = false;
            KillerFlashlightCone cone = killer.AddComponent<KillerFlashlightCone>();
            cone.Configure(player.transform, facts, killerDirector, spot);
            return killerDirector;
        }

        private static Material EnsureCrtMaterial()
        {
            const string path = MaterialRoot + "/MAT_CRT_Fullscreen.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("ConGame/CRT/FullscreenOverlay");
            Material material = shader == null
                                    ? new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                                    : new Material(shader);
            material.name = "MAT_CRT_Fullscreen";
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void BuildNavigation(Transform parent)
        {
            GameObject navigation = new GameObject("Navigation");
            navigation.transform.SetParent(parent);
            NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) != null)
            {
                AssetDatabase.DeleteAsset(NavMeshPath);
            }

            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
                EditorUtility.SetDirty(surface);
            }
        }

        private static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapScenePath, true),
                                                 new EditorBuildSettingsScene(MenuScenePath, true),
                                                 new EditorBuildSettingsScene(RoomScenePath, true) };
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Transform parent,
                                             Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return gameObject;
        }

        private static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{MaterialRoot}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T EnsureAsset<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string Sanitize(string value)
        {
            return value.Replace(' ', '_').Replace('/', '_');
        }

        private static Component AddOptionalMonoBehaviour(GameObject target, params string[] typeNames)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                foreach (string typeName in typeNames)
                {
                    if (string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    {
                        return target.AddComponent(type);
                    }
                }
            }

            Debug.LogWarning(
                $"Optional component {string.Join(" or ", typeNames)} was not found. The scene remains usable with the physical Camera fallback.");
            return null;
        }
    }
}
