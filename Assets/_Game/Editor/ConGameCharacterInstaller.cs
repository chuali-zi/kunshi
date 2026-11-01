using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConGame.Presentation.Animation;
using ConGame.Presentation.Audio;
using ConGame.Runtime.AI;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ConGame.Editor
{
    /// <summary>
    /// M3 角色接入:NIUA 生成的角色/动画 FBX → Humanoid 导入设置 →
    /// AnimatorController → Room_M1 场景替换灰盒胶囊 + 挂 CharacterAnimatorDriver
    /// + 绑定音效。批处理入口:
    /// -executeMethod ConGame.Editor.ConGameCharacterInstaller.Run
    /// </summary>
    public static class ConGameCharacterInstaller
    {
        const string CharDir = "Assets/_Game/Art/Characters";
        const string AnimDir = "Assets/_Game/Art/Animations";
        const string CtrlDir = "Assets/_Game/Art/Animations/Controllers";
        const string PropDir = "Assets/_Game/Art/Props";
        const string ScenePath = "Assets/_Game/Scenes/Room_M1.unity";
        const string ShotDir = @"D:\game\con_game\art\previews\unity";

        static readonly string[] LoopClips = { "AN_Player_Idle", "AN_Player_Walk", "POSE_Player_SitChair", "AN_Killer_Walk" };

        [MenuItem("ConGame/Characters/Install All (Import + Controllers + Scene)")]
        public static void Run()
        {
            ConfigureModelImports();
            AnimatorController playerController = BuildController("AC_Player", killer: false);
            AnimatorController killerController = BuildController("AC_Killer", killer: true);
            ConGameScaffoldBuilder.InstallDeliveredPerformanceAssets();
            WireScene(playerController, killerController);
            Debug.Log("INSTALL done");
        }

        [MenuItem("ConGame/Characters/Install Killer Visual Equipment")]
        public static void InstallKillerVisualEquipment()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform characterModel = FindInScene("CH_Killer_Model");
            if (characterModel == null)
            {
                throw new InvalidOperationException("scene object missing: CH_Killer_Model");
            }

            InstallKillerEquipment(characterModel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("INSTALL killer visual equipment saved");
        }

        [MenuItem("ConGame/Characters/Update Player SitChair State")]
        public static void EnsurePlayerSitChairControllerState()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                $"{CtrlDir}/AC_Player.controller");
            if (controller == null)
            {
                throw new InvalidOperationException("AC_Player.controller is missing; install the player controller first.");
            }

            if (EnsurePlayerSitChairState(controller))
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
            }
        }

        // ---- 1) 导入设置:全部 Humanoid,循环动画勾 Loop Time ----

        static void ConfigureModelImports()
        {
            foreach (string path in Directory.GetFiles(CharDir, "CH_*_rigged.fbx")
                                             .Select(p => p.Replace('\\', '/')))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer == null)
                {
                    throw new InvalidOperationException($"importer missing: {path}");
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.SaveAndReimport();
                ConfigureCharacterTextures(Path.GetFileNameWithoutExtension(path).Replace("_rigged", string.Empty));
                Debug.Log($"INSTALL import {path} humanoid ok");
            }

            foreach (string path in Directory.GetFiles(AnimDir, "AN_*.fbx")
                                             .Concat(Directory.GetFiles(AnimDir, "POSE_*.fbx"))
                                             .Concat(Directory.GetFiles(AnimDir, "PAIR_*.fbx"))
                                             .Select(p => p.Replace('\\', '/')))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer == null)
                {
                    throw new InvalidOperationException($"importer missing: {path}");
                }

                string baseName = Path.GetFileNameWithoutExtension(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                // A changed Avatar mode must be imported before Unity exposes the default take list.
                importer.SaveAndReimport();
                importer = (ModelImporter)AssetImporter.GetAtPath(path);
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips.Length == 0)
                {
                    throw new InvalidOperationException($"animation take missing: {path}");
                }

                foreach (ModelImporterClipAnimation clip in clips)
                {
                    clip.name = baseName;
                    clip.loopTime = baseName.StartsWith("POSE_", StringComparison.Ordinal) ||
                                    Array.IndexOf(LoopClips, baseName) >= 0;
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                Debug.Log($"INSTALL import {path} humanoid ok");
            }
        }

        static void ConfigureCharacterTextures(string characterName)
        {
            // NIUA's source GLB uses image 1 for metallic/roughness, not normals.
            string packedPath = $"{CharDir}/TEX_{characterName}_1.png";
            if (AssetImporter.GetAtPath(packedPath) is TextureImporter packedImporter)
            {
                packedImporter.textureType = TextureImporterType.Default;
                packedImporter.sRGBTexture = false;
                packedImporter.SaveAndReimport();
            }
        }

        // ---- 2) AnimatorController ----

        static AnimationClip LoadClip(string baseName)
        {
            string path = $"{AnimDir}/{baseName}.fbx";
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null)
            {
                throw new InvalidOperationException($"clip missing in {path}");
            }

            return clip;
        }

        static AnimatorController BuildController(string name, bool killer)
        {
            Directory.CreateDirectory(CtrlDir);
            string path = $"{CtrlDir}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("LocomotionRate", AnimatorControllerParameterType.Float);
            foreach (string trigger in killer ? new[] { "AimFire", "Dodge" } : new[] { "WakeUp", "Swing", "HitFall", "Climb" })
            {
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            }

            if (!killer)
            {
                controller.AddParameter("ClimbRate", AnimatorControllerParameterType.Float);
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // 待机/行走:杀手没有专用待机片段,复用玩家 Idle(Humanoid 重定向)
            AnimatorState idle = sm.AddState("Idle", new Vector3(240, 0, 0));
            idle.motion = LoadClip("AN_Player_Idle");
            AnimatorState walk = sm.AddState("Walk", new Vector3(240, 120, 0));
            walk.motion = LoadClip(killer ? "AN_Killer_Walk" : "AN_Player_Walk");
            walk.speedParameter = "LocomotionRate";
            walk.speedParameterActive = true;
            walk.iKOnFeet = true;
            sm.defaultState = idle;

            if (!killer) EnsurePlayerSitChairState(controller);

            AnimatorStateTransition toWalk = idle.AddTransition(walk);
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            AnimatorStateTransition toIdle = walk.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.2f;

            if (killer)
            {
                AddOneShot(sm, idle, "AimFire", LoadClip("AN_Killer_AimFire"), returnToIdle: true, new Vector3(480, 0, 0));
                AddOneShot(sm, idle, "Dodge", LoadClip("AN_Killer_DodgeSide"), returnToIdle: true, new Vector3(480, 120, 0));
            }
            else
            {
                AddOneShot(sm, idle, "WakeUp", LoadClip("AN_Player_WakeUp"), returnToIdle: true, new Vector3(480, -120, 0));
                AddOneShot(sm, idle, "Swing", LoadClip("AN_Player_Swing"), returnToIdle: true, new Vector3(480, 0, 0));
                // 中弹倒地停在末帧,等 LoopReset 由驱动强制回 Idle
                AddOneShot(sm, idle, "HitFall", LoadClip("AN_Player_HitFall"), returnToIdle: false, new Vector3(480, 120, 0));
                // 攀爬位移由 PlayerMotor.ClimbTo 提供;ClimbRate 让 2s 剪辑适配实际攀爬时长
                AnimatorState climb = AddOneShot(sm, idle, "Climb", LoadClip("AN_Player_Climb"), returnToIdle: true, new Vector3(480, 240, 0));
                climb.speedParameter = "ClimbRate";
                climb.speedParameterActive = true;
            }

            EditorUtility.SetDirty(controller);
            Debug.Log($"INSTALL controller {name} ok");
            return controller;
        }

        private static bool EnsurePlayerSitChairState(AnimatorController controller)
        {
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState sitChair = sm.states
                .Select(child => child.state)
                .FirstOrDefault(state => state != null && state.name == "SitChair");
            bool changed = false;
            if (sitChair == null)
            {
                sitChair = sm.AddState("SitChair", new Vector3(240, 240, 0));
                changed = true;
            }

            AnimationClip clip = LoadClip("POSE_Player_SitChair");
            if (sitChair.motion != clip)
            {
                sitChair.motion = clip;
                changed = true;
            }

            return changed;
        }

        static AnimatorState AddOneShot(AnimatorStateMachine sm, AnimatorState idle, string trigger, AnimationClip clip,
                               bool returnToIdle, Vector3 position)
        {
            AnimatorState state = sm.AddState(trigger, position);
            state.motion = clip;
            AnimatorStateTransition entry = sm.AddAnyStateTransition(state);
            entry.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            entry.hasExitTime = false;
            entry.duration = 0.08f;
            entry.canTransitionToSelf = false;
            if (returnToIdle)
            {
                AnimatorStateTransition exit = state.AddTransition(idle);
                exit.hasExitTime = true;
                exit.exitTime = 0.95f;
                exit.duration = 0.15f;
            }

            return state;
        }

        // ---- 3) 场景接线 ----

        static void WireScene(AnimatorController playerController, AnimatorController killerController)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameFlowController flow = UnityEngine.Object.FindFirstObjectByType<GameFlowController>(FindObjectsInactive.Include);
            GameplayFactService facts = UnityEngine.Object.FindFirstObjectByType<GameplayFactService>(FindObjectsInactive.Include);

            InstallCharacter("Player", "CH_Player_rigged", playerController, flow, facts,
                             CharacterAnimatorDriver.DriverRole.Player, scale: 1.89f,
                             greyboxNames: new[] { "CH_Player_Greybox" });
            InstallCharacter("Killer", "CH_Killer_rigged", killerController, flow, facts,
                             CharacterAnimatorDriver.DriverRole.Killer, scale: 2.06f,
                             greyboxNames: new[] { "CH_Killer_Greybox", "PRP_KillerCoat" });

            BindAudio();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("INSTALL scene saved");
        }

        static void InstallCharacter(string rootName, string modelName, AnimatorController controller,
                                     GameFlowController flow, GameplayFactService facts,
                                     CharacterAnimatorDriver.DriverRole role, float scale, string[] greyboxNames)
        {
            // GameObject.Find 找不到未激活对象(Killer 初始为 inactive),遍历场景根搜索
            GameObject root = null;
            foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (sceneRoot.name == rootName)
                {
                    root = sceneRoot;
                    break;
                }

                Transform found = FindRecursive(sceneRoot.transform, rootName);
                if (found != null)
                {
                    root = found.gameObject;
                    break;
                }
            }

            if (root == null)
            {
                throw new InvalidOperationException($"scene object missing: {rootName}");
            }

            foreach (string greybox in greyboxNames)
            {
                Transform old = root.transform.Find(greybox);
                if (old != null)
                {
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
            }

            string instanceName = modelName.Replace("_rigged", "_Model");
            Transform existing = root.transform.Find(instanceName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharDir}/{modelName}.fbx");
            if (prefab == null)
            {
                throw new InvalidOperationException($"model missing: {CharDir}/{modelName}.fbx");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = instanceName;
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * scale;

            Material characterMaterial = BuildCharacterMaterial(role);
            foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterial = characterMaterial;
            }

            Animator animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            CharacterAnimatorDriver driver = root.GetComponent<CharacterAnimatorDriver>() ??
                                             root.AddComponent<CharacterAnimatorDriver>();
            driver.Configure(role, animator, root.GetComponent<CharacterController>(),
                             root.GetComponent<NavMeshAgent>(), flow, facts);
            if (role == CharacterAnimatorDriver.DriverRole.Killer)
            {
                InstallKillerEquipment(instance.transform);
                KillerHitReceiver hitReceiver = root.GetComponent<KillerHitReceiver>() ??
                                                 root.AddComponent<KillerHitReceiver>();
                hitReceiver.Configure(instance.transform, root.GetComponent<KillerTacticalController>());
                CapsuleCollider hurtbox = root.GetComponent<CapsuleCollider>() ?? root.AddComponent<CapsuleCollider>();
                hurtbox.center = Vector3.up;
                hurtbox.radius = 0.42f;
                hurtbox.height = 1.8f;
                EditorUtility.SetDirty(hitReceiver);
                EditorUtility.SetDirty(hurtbox);
            }
            else
            {
                Transform handSocket = FindRecursive(root.transform, "HandSocket_R");
                if (handSocket == null)
                {
                    handSocket = new GameObject("HandSocket_R").transform;
                    handSocket.SetParent(root.transform, false);
                }

                PlayerInteractor interactor = root.GetComponent<PlayerInteractor>();
                if (interactor != null)
                {
                    SerializedObject serializedInteractor = new SerializedObject(interactor);
                    serializedInteractor.FindProperty("handSocket").objectReferenceValue = handSocket;
                    serializedInteractor.ApplyModifiedPropertiesWithoutUndo();
                }

                HumanoidHandSocketBinder binder = root.GetComponent<HumanoidHandSocketBinder>() ??
                                                  root.AddComponent<HumanoidHandSocketBinder>();
                binder.Configure(animator, handSocket, Vector3.zero, Quaternion.identity);
                EditorUtility.SetDirty(binder);
            }
            EditorUtility.SetDirty(driver);
            Debug.Log($"INSTALL character {rootName} <- {instanceName} ok");
        }

        static Material BuildCharacterMaterial(CharacterAnimatorDriver.DriverRole role)
        {
            string characterName = role == CharacterAnimatorDriver.DriverRole.Player ? "CH_Player" : "CH_Killer";
            string path = $"{CharDir}/MAT_{characterName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{CharDir}/TEX_{characterName}_0.png"));
            material.SetTexture("_BumpMap", null);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", role == CharacterAnimatorDriver.DriverRole.Player ? 0.16f : 0.24f);
            material.SetFloat("_BumpScale", 1f);
            material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        static void InstallKillerEquipment(Transform characterModel)
        {
            Transform spine = FindRecursive(characterModel, "mixamorig:Spine1");
            if (spine == null)
            {
                throw new InvalidOperationException("killer bone missing: mixamorig:Spine1");
            }

            Animator animator = characterModel.GetComponent<Animator>();
            Transform rightHand = animator != null && animator.isHuman && animator.avatar != null &&
                                  animator.avatar.isValid
                                      ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                                      : null;
            rightHand ??= FindRecursive(characterModel, "mixamorig:RightHand");
            if (rightHand == null)
            {
                throw new InvalidOperationException("killer bone missing: mixamorig:RightHand");
            }

            foreach (string mountedName in new[]
                     {
                         "PRP_KillerHolster_Mounted",
                         "PRP_PistolMain_EquippedVisual"
                     })
            {
                Transform existing = FindRecursive(characterModel, mountedName);
                if (existing != null)
                {
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                }
            }

            GameObject holsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PropDir}/PRP_KillerHolster.fbx");
            GameObject backupPistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PropDir}/PRP_Pistol_Backup.fbx");
            GameObject mainPistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PropDir}/PRP_Pistol_Main.fbx");
            GameObject flashlightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PropDir}/PRP_Flashlight.fbx");
            if (holsterPrefab == null || backupPistolPrefab == null ||
                mainPistolPrefab == null || flashlightPrefab == null)
            {
                throw new InvalidOperationException("killer visual equipment asset missing");
            }

            GameObject holster = (GameObject)PrefabUtility.InstantiatePrefab(holsterPrefab);
            holster.name = "PRP_KillerHolster_Mounted";
            holster.transform.SetParent(characterModel, false);
            holster.transform.localPosition = Vector3.zero;
            holster.transform.localRotation = Quaternion.identity;
            holster.transform.localScale = Vector3.one;
            holster.transform.SetParent(spine, true);

            Transform socket = FindRecursive(holster.transform, "SOCKET_BackupPistol");
            if (socket == null)
            {
                UnityEngine.Object.DestroyImmediate(holster);
                throw new InvalidOperationException("holster socket missing: SOCKET_BackupPistol");
            }

            GameObject backupPistol = (GameObject)PrefabUtility.InstantiatePrefab(backupPistolPrefab);
            backupPistol.name = "PRP_PistolBackup_Holstered";
            backupPistol.transform.SetParent(socket, false);
            backupPistol.transform.localPosition = Vector3.zero;
            backupPistol.transform.localRotation = Quaternion.identity;
            backupPistol.transform.localScale = Vector3.one;

            GameObject mainPistol = (GameObject)PrefabUtility.InstantiatePrefab(mainPistolPrefab);
            mainPistol.name = "PRP_PistolMain_EquippedVisual";
            mainPistol.transform.SetParent(rightHand, false);
            mainPistol.transform.localPosition = Vector3.zero;
            mainPistol.transform.localRotation = Quaternion.identity;
            mainPistol.transform.localScale = InverseScale(rightHand.lossyScale);

            Transform railSocket = FindRecursive(mainPistol.transform, "SOCKET_RailLight");
            if (railSocket == null)
            {
                UnityEngine.Object.DestroyImmediate(mainPistol);
                throw new InvalidOperationException("main pistol socket missing: SOCKET_RailLight");
            }

            GameObject flashlight = (GameObject)PrefabUtility.InstantiatePrefab(flashlightPrefab);
            flashlight.name = "PRP_Flashlight_RailMountedVisual";
            flashlight.transform.SetParent(railSocket, false);
            flashlight.transform.localPosition = Vector3.zero;
            flashlight.transform.localRotation = Quaternion.identity;
            flashlight.transform.localScale = Vector3.one;

            Transform railMount = FindRecursive(flashlight.transform, "SOCKET_RailMount");
            if (railMount == null)
            {
                UnityEngine.Object.DestroyImmediate(flashlight);
                UnityEngine.Object.DestroyImmediate(mainPistol);
                throw new InvalidOperationException("flashlight socket missing: SOCKET_RailMount");
            }

            AlignSocket(flashlight.transform, railMount, railSocket);
            float railPositionError = Vector3.Distance(railMount.position, railSocket.position);
            float railAngleError = Quaternion.Angle(railMount.rotation, railSocket.rotation);
            if (railPositionError > 0.0001f || railAngleError > 0.01f)
            {
                UnityEngine.Object.DestroyImmediate(flashlight);
                UnityEngine.Object.DestroyImmediate(mainPistol);
                throw new InvalidOperationException(
                    $"rail flashlight alignment failed: position={railPositionError}, angle={railAngleError}");
            }

            Debug.Log($"INSTALL killer visual equipment mounted: rightHand={rightHand.name}, " +
                      $"mainWorldScale={mainPistol.transform.lossyScale}, " +
                      $"railPositionError={railPositionError:F6}, railAngleError={railAngleError:F4}");
        }

        static void AlignSocket(Transform mountedRoot, Transform sourceSocket, Transform targetSocket)
        {
            mountedRoot.rotation = targetSocket.rotation * Quaternion.Inverse(sourceSocket.rotation) *
                                   mountedRoot.rotation;
            mountedRoot.position += targetSocket.position - sourceSocket.position;
        }

        static Vector3 InverseScale(Vector3 scale)
        {
            return new Vector3(SafeReciprocal(scale.x), SafeReciprocal(scale.y), SafeReciprocal(scale.z));
        }

        static float SafeReciprocal(float value)
        {
            return Mathf.Abs(value) < 0.0001f ? 1f : 1f / value;
        }

        static Transform FindRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                Transform found = FindRecursive(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        static void BindAudio()
        {
            AudioCueAutoBinder.BindCues();
        }

        // ---- 4) 固定机位验收截图(批处理入口)----

        public static void Screenshot()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(ShotDir);

            // 真实固定机位:场景里的电视机位 rig
            Transform fixedCam = FindInScene("CM Fixed Television Camera");
            if (fixedCam != null)
            {
                // 对照:原位 / 前移出屏幕玻璃 / 杀手未激活
                Shoot("M3_fixed_a_asis.png",
                      fixedCam.position, fixedCam.position + fixedCam.forward * 4f, 58f);
                Shoot("M3_fixed_b_forward.png",
                      fixedCam.position + fixedCam.forward * 0.35f,
                      fixedCam.position + fixedCam.forward * 4f, 58f);
                Transform killerProbe = FindInScene("Killer");
                if (killerProbe != null)
                {
                    killerProbe.gameObject.SetActive(false);
                    Shoot("M3_fixed_c_nokiller.png",
                          fixedCam.position + fixedCam.forward * 0.35f,
                          fixedCam.position + fixedCam.forward * 4f, 58f);
                    killerProbe.gameObject.SetActive(true);
                }
            }

            // 杀手默认未激活(破门时才出场),验收截图临时激活,不保存场景
            Transform killerRoot = FindInScene("Killer");
            if (killerRoot != null)
            {
                killerRoot.gameObject.SetActive(true);
            }

            // 实测可用机位:东侧看玩家(避开床),房间中央看杀手
            Shoot("M3_player_front.png", new Vector3(1.6f, 1.5f, -1.3f), new Vector3(-0.7f, 1.0f, -1.35f), 50f);
            Shoot("M3_killer_front.png", new Vector3(0.5f, 1.5f, 0.0f), new Vector3(3.62f, 1.1f, 1.45f), 50f);
            Shoot("M3_killer_side.png", new Vector3(3.6f, 1.5f, -0.6f), new Vector3(3.62f, 1.1f, 1.45f), 50f);

            Debug.Log("INSTALL screenshots done");
        }

        static Transform FindInScene(string name)
        {
            foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (sceneRoot.name == name)
                {
                    return sceneRoot.transform;
                }

                Transform found = FindRecursive(sceneRoot.transform, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        static void Shoot(string file, Vector3 pos, Vector3 target, float fov)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(target - pos, Vector3.up);
            cam.fieldOfView = fov;
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
            Debug.Log("INSTALL shot " + file);
        }
    }
}
