using System;
using System.Collections.Generic;
using ConGame.Bootstrap;
using ConGame.Domain.Facts;
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
using ConGame.Presentation.Camera;
using ConGame.Presentation.CRT;
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
            CounterplayCatalogAsset counterplays = BuildCounterplayDefinitions();
            Dictionary<string, Material> materials = BuildMaterials();

            BuildBootstrapScene();
            BuildMenuScene();
            BuildRoomScene(config, counterplays, items, materials);
            SetBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ConGameProjectValidator.ValidateProject(true);
            EditorUtility.DisplayDialog(
                "ConGame M1",
                inputBackendChanged
                    ? "M1 greybox rebuilt and the new Input System backend was enabled. Restart Unity once, then open Assets/_Game/Scenes/Bootstrap.unity."
                    : "M1 greybox rebuilt. Open Assets/_Game/Scenes/Bootstrap.unity and enter Play Mode.",
                "OK");
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
            EnsureFolder(MaterialRoot);
            EnsureFolder(ContentRoot + "/Timelines");
            EnsureFolder(Root + "/Art");
        }

        private static Dictionary<string, ItemDefinitionAsset> BuildItemDefinitions()
        {
            Dictionary<string, ItemDefinitionAsset> result = new Dictionary<string, ItemDefinitionAsset>(StringComparer.Ordinal);
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

        private static CounterplayCatalogAsset BuildCounterplayDefinitions()
        {
            List<CounterplayDefinitionAsset> definitions = new List<CounterplayDefinitionAsset>();
            foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
            {
                string path = $"{CounterplayRoot}/CP_{seed.Id}.asset";
                CounterplayDefinitionAsset asset = EnsureAsset<CounterplayDefinitionAsset>(path);
                asset.Configure(seed.Id, seed.SortOrder, seed.TriggerMode, seed.Conditions, seed.Status);
                EditorUtility.SetDirty(asset);
                definitions.Add(asset);
            }

            CounterplayCatalogAsset catalog = EnsureAsset<CounterplayCatalogAsset>(CounterplayRoot + "/CAT_Counterplays.asset");
            catalog.Configure(definitions.ToArray());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static Dictionary<string, Material> BuildMaterials()
        {
            return new Dictionary<string, Material>(StringComparer.Ordinal)
            {
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
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
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
            UnityEngine.Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets == null || settingsAssets.Length == 0)
            {
                Debug.LogWarning("Could not inspect PlayerSettings; set Active Input Handling to Input System Package (New) before playtesting.");
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

        private static void BuildRoomScene(
            GameConfigAsset config,
            CounterplayCatalogAsset counterplays,
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

            BuildRoomShell(roomRoot.transform, materials);

            Transform fixedCameraRig = BuildFixedCamera(roomRoot.transform, out Camera outputCamera);
            BuildPlayer(roomRoot.transform, fixedCameraRig, config, facts, events, items, materials,
                out PlayerMotor playerMotor,
                out PlayerInputRouter input,
                out PlayerInteractor interactor,
                out GameObject firstPersonRig);

            CameraModeController cameraMode = systems.AddComponent<CameraModeController>();
            cameraMode.Configure(fixedCameraRig.gameObject, firstPersonRig);

            BuildFurnitureAndZones(roomRoot.transform, facts, items, materials);
            BuildBat(roomRoot.transform, items["baseball_bat"], materials["interactable"]);
            BuildTelevision(roomRoot.transform, facts, events, flow, items["crt_tv"], materials["tv"]);
            BuildLights(roomRoot.transform, facts, materials, out BreakableLight ceiling, out BreakableLight table);
            lightCoordinator.Configure(ceiling, table, facts);

            KillerDirector killer = BuildKiller(roomRoot.transform, playerMotor.transform, facts, counterplays, config, materials["killer"]);
            registry.Configure(roomRoot.transform);
            loop.Configure(config, flow, registry, facts, events, input, playerMotor, killer);
            RoomContext context = systems.AddComponent<RoomContext>();
            context.Configure(config, flow, facts, events, registry, loop, input, playerMotor, killer);

            BuildNavigation(roomRoot.transform);
            killer.gameObject.SetActive(false);

            outputCamera.transform.SetPositionAndRotation(fixedCameraRig.position, fixedCameraRig.rotation);
            Selection.activeObject = systems;
            EditorSceneManager.SaveScene(scene, RoomScenePath);
        }

        private static void BuildRoomShell(Transform parent, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject shell = new GameObject("Environment");
            shell.transform.SetParent(parent);

            CreateCube("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(6.2f, 0.1f, 5.2f), shell.transform, materials["floor"]);
            CreateCube("North Wall", new Vector3(0f, 1.4f, 2.65f), new Vector3(6.4f, 2.8f, 0.1f), shell.transform, materials["wall"]);
            CreateCube("South Wall", new Vector3(0f, 1.4f, -2.65f), new Vector3(6.4f, 2.8f, 0.1f), shell.transform, materials["wall"]);
            CreateCube("West Wall", new Vector3(-3.15f, 1.4f, 0f), new Vector3(0.1f, 2.8f, 5.2f), shell.transform, materials["wall"]);
            CreateCube("East Wall", new Vector3(3.15f, 1.4f, 0f), new Vector3(0.1f, 2.8f, 5.2f), shell.transform, materials["wall"]);
            CreateCube("Ceiling", new Vector3(0f, 2.85f, 0f), new Vector3(6.4f, 0.1f, 5.4f), shell.transform, materials["wall"]);

            CreateCube("Door", new Vector3(3.05f, 1.025f, 1.45f), new Vector3(0.12f, 2.05f, 0.9f), shell.transform, materials["wood"]);
            CreateCube("Black Corridor", new Vector3(3.7f, 1.3f, 1.45f), new Vector3(1.2f, 2.6f, 1.0f), shell.transform, materials["killer"]);
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

        private static void BuildPlayer(
            Transform parent,
            Transform fixedCamera,
            GameConfigAsset config,
            GameplayFactService facts,
            GameplayEventHub events,
            IReadOnlyDictionary<string, ItemDefinitionAsset> items,
            IReadOnlyDictionary<string, Material> materials,
            out PlayerMotor motor,
            out PlayerInputRouter input,
            out PlayerInteractor interactor,
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

        private static void BuildFurnitureAndZones(
            Transform parent,
            GameplayFactService facts,
            IReadOnlyDictionary<string, ItemDefinitionAsset> items,
            IReadOnlyDictionary<string, Material> materials)
        {
            GameObject furniture = new GameObject("Furniture");
            furniture.transform.SetParent(parent);

            GameObject armchair = CreateCube("FUR_Armchair", new Vector3(-0.7f, 0.575f, -0.55f), new Vector3(0.95f, 1.15f, 0.75f), furniture.transform, materials["chair"]);
            InteractableEntity chairEntity = armchair.AddComponent<InteractableEntity>();
            chairEntity.Configure(items["sofa_awaken"]);
            HideCapability hide = armchair.AddComponent<HideCapability>();
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

            CreateCube("FUR_Bed", new Vector3(1.7f, 0.3f, 1.85f), new Vector3(2.0f, 0.6f, 1.0f), furniture.transform, materials["wood"]);
            CreateCube("FUR_Bookcase", new Vector3(-2.65f, 1.05f, 1.65f), new Vector3(0.8f, 2.1f, 0.35f), furniture.transform, materials["wood"]);
            CreateCube("FUR_Dresser", new Vector3(-1.45f, 0.425f, 2.1f), new Vector3(1.1f, 0.85f, 0.48f), furniture.transform, materials["wood"]);
            CreateCube("FUR_WoodChair", new Vector3(-0.1f, 0.48f, 1.7f), new Vector3(0.45f, 0.95f, 0.5f), furniture.transform, materials["interactable"]);
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
            bat.AddComponent<PickupCapability>();
            bat.AddComponent<MeleeWeaponCapability>();
            bat.AddComponent<RigidbodyResettable>();
        }

        private static void BuildTelevision(
            Transform parent,
            GameplayFactService facts,
            GameplayEventHub events,
            GameFlowController flow,
            ItemDefinitionAsset definition,
            Material material)
        {
            GameObject tv = CreateCube("PRP_CRTTV", new Vector3(0f, 0.9f, -2.38f), new Vector3(1.2f, 0.8f, 0.45f), parent, material);
            InteractableEntity entity = tv.AddComponent<InteractableEntity>();
            entity.Configure(definition);
            TelevisionController controller = tv.AddComponent<TelevisionController>();
            controller.Configure(facts, events, flow);
            tv.AddComponent<CrtStatePresenter>().Configure(controller);
            tv.AddComponent<TransformResettable>();

            GameObject screenAnchor = new GameObject("CameraScreenAnchor");
            screenAnchor.transform.SetParent(tv.transform, false);
            screenAnchor.transform.localPosition = new Vector3(0f, 0.31f, 0.51f);
        }

        private static void BuildLights(
            Transform parent,
            GameplayFactService facts,
            IReadOnlyDictionary<string, Material> materials,
            out BreakableLight ceilingBreakable,
            out BreakableLight tableBreakable)
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
        }

        private static KillerDirector BuildKiller(
            Transform parent,
            Transform player,
            GameplayFactService facts,
            CounterplayCatalogAsset counterplays,
            GameConfigAsset config,
            Material material)
        {
            GameObject killer = new GameObject("Killer");
            killer.transform.SetParent(parent);
            killer.transform.position = new Vector3(2.45f, 0f, 1.45f);

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
            TimelineSequenceRunner sequenceRunner = killer.AddComponent<TimelineSequenceRunner>();
            KillerDirector killerDirector = killer.AddComponent<KillerDirector>();
            killerDirector.Configure(agent, player, facts, counterplays, config, sequenceRunner);
            return killerDirector;
        }

        private static void BuildNavigation(Transform parent)
        {
            GameObject navigation = new GameObject("Navigation");
            navigation.transform.SetParent(parent);
            NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        private static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(RoomScenePath, true)
            };
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Transform parent, Material material)
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

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
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

            Debug.LogWarning($"Optional component {string.Join(" or ", typeNames)} was not found. The scene remains usable with the physical Camera fallback.");
            return null;
        }
    }
}
