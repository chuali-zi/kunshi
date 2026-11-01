using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Presentation.Animation;
using ConGame.Presentation.Audio;
using ConGame.Presentation.Vfx;
using ConGame.Presentation.Animation.Rigging;
using ConGame.Presentation.Choreography;
using ConGame.Presentation.M1;
using ConGame.Presentation.Camera;
using ConGame.Presentation.Mirror;
using ConGame.Presentation.Props;
using ConGame.Runtime.AI;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using ConGame.Runtime.Counterplay;
using UnityEngine;

namespace ConGame.Bootstrap
{
    /// <summary>
    /// Applies design-gap gameplay wiring to an already-built Room_M1 without
    /// requiring a full greybox rebuild.
    /// </summary>
    public static class RoomDesignGapInstaller
    {
        public static void Apply(RoomContext room)
        {
            if (room == null)
            {
                return;
            }

            GameplayFactService facts = room.GetComponent<GameplayFactService>() ??
                                       Object.FindAnyObjectByType<GameplayFactService>();
            GameplayEventHub events = Object.FindAnyObjectByType<GameplayEventHub>();
            PlayerInteractor player = Object.FindAnyObjectByType<PlayerInteractor>();
            KillerDirector killer = Object.FindAnyObjectByType<KillerDirector>(FindObjectsInactive.Include);
            InteractionScanner scanner = Object.FindAnyObjectByType<InteractionScanner>();
            Transform root = room.transform.root;
            if (facts == null)
            {
                return;
            }

            EnsureComponent<AmbushFactDriver>(facts.gameObject).Configure(facts);
            if (player != null && killer != null)
            {
                player.ConfigureKiller(killer.transform);
            }

            WireDresser(facts);
            WireDoorAndCorridor(facts, root);
            WireLamp(facts);
            WireVaseAndFragiles(events);
            WireBooks();
            WireRugAndBlanketTraps(facts, events);
            WireBookcase(facts, events);
            WireClock();
            WireSurfaces(facts, root);
            DisableAmbushProxy();
            WirePairedPerformance(player, killer, scanner);
            WireOpeningSeat(player, facts);
            WireLampBrightener(facts, killer);
            WireInspectFocus(scanner);
            WireRoomView();
            WireEndingObserver(root);
        }

        private static void WireDresser(GameplayFactService facts)
        {
            GameObject dresser = GameObject.Find("FUR_Dresser");
            if (dresser == null || dresser.transform.Find("Drawer_0") != null)
            {
                return;
            }

            Transform visual = dresser.transform.Find("FUR_Dresser_Visual");
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, 180f, 0f);

            CreateDrawer(dresser, "Drawer_0", new Vector3(0f, 0.22f, 0.28f), 1, true);
            CreateDrawer(dresser, "Drawer_1", new Vector3(0f, 0f, 0.28f), 1, false);
            CreateDrawer(dresser, "Drawer_2", new Vector3(0f, -0.22f, 0.28f), 2, false);
        }

        private static void CreateDrawer(GameObject dresser, string name, Vector3 localPos, int pulls, bool withClutter)
        {
            GameObject drawer = new GameObject(name);
            drawer.transform.SetParent(dresser.transform, false);
            drawer.transform.localPosition = localPos;
            BoxCollider trigger = drawer.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.05f, 0.24f, 0.48f);
            string modelName = name == "Drawer_0" ? "Dresser_Drawer_Top" :
                               name == "Drawer_1" ? "Dresser_Drawer_Mid" : "Dresser_Drawer_Bot";
            Transform model = dresser.transform.Find("FUR_Dresser_Visual/" + modelName);
            if (model != null)
            {
                drawer.transform.position = model.position;
                model.SetParent(drawer.transform, true);
            }
            InteractableEntity entity = drawer.AddComponent<InteractableEntity>();
            entity.Configure(null, "dresser_drawer");
            OpenableDrawer openable = drawer.AddComponent<OpenableDrawer>();
            GameObject[] contents = null;
            if (withClutter)
            {
                GameObject clutterModel = GameObject.Find("PRP_DrawerClutter_Visual");
                GameObject clutter = clutterModel == null
                    ? GameObject.CreatePrimitive(PrimitiveType.Cube) : Object.Instantiate(clutterModel);
                clutter.name = "PRP_DrawerClutter_Runtime";
                clutter.transform.SetParent(drawer.transform, false);
                clutter.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                clutter.transform.localScale = clutterModel == null ? new Vector3(0.2f, 0.08f, 0.2f) : Vector3.one;
                if (clutter.GetComponent<Collider>() == null)
                    clutter.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.08f, 0.24f);
                clutter.SetActive(false);
                Rigidbody body = clutter.AddComponent<Rigidbody>();
                body.mass = 0.2f;
                body.isKinematic = true;
                InteractableEntity clutterEntity = clutter.AddComponent<InteractableEntity>();
                clutterEntity.Configure(null, "drawer_clutter");
                StatefulWorldItem stateful = clutter.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
                clutter.AddComponent<PickupCapability>();
                contents = new[] { clutter };
            }

            openable.Configure(pulls, drawer.transform, 0.22f, contents);
            UseCapability use = drawer.AddComponent<UseCapability>();
            use.Configure(openable);
            drawer.AddComponent<InspectCapability>();
        }

        private static void WireDoorAndCorridor(GameplayFactService facts, Transform root)
        {
            GameObject doorProxy = GameObject.Find("PRP_RoomDoor_Interact");
            Transform hinge = GameObject.Find("DoorHinge")?.transform;
            if (doorProxy != null && doorProxy.GetComponent<RoomDoorUseHandler>() == null)
            {
                RoomDoorUseHandler doorUse = doorProxy.AddComponent<RoomDoorUseHandler>();
                doorUse.Configure(hinge, facts);
                UseCapability use = doorProxy.GetComponent<UseCapability>() ?? doorProxy.AddComponent<UseCapability>();
                use.Configure(doorUse);
            }

            if (GameObject.Find("Zone_BlackCorridor") != null)
            {
                return;
            }

            GameObject corridor = new GameObject("Zone_BlackCorridor");
            corridor.transform.SetParent(root, false);
            corridor.transform.position = new Vector3(3.6f, 0.9f, 0.55f);
            BoxCollider box = corridor.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.6f, 2.2f, 1.4f);
            CorridorResistanceVolume volume = corridor.AddComponent<CorridorResistanceVolume>();
            volume.Configure(Vector3.right, 0f, 1.6f);
        }

        private static void WireLamp(GameplayFactService facts)
        {
            GameObject lamp = GameObject.Find("PRP_TableLamp");
            if (lamp == null)
            {
                return;
            }

            BreakableLight breakable = lamp.GetComponent<BreakableLight>();
            StatefulWorldItem item = lamp.GetComponent<StatefulWorldItem>();
            if (breakable != null && item != null && lamp.GetComponent<LampUnplugOnHold>() == null)
            {
                lamp.AddComponent<LampUnplugOnHold>().Configure(breakable, item);
            }

            if (lamp.transform.Find("LampDrapeSocket") != null)
            {
                return;
            }

            GameObject socketObject = new GameObject("LampDrapeSocket");
            socketObject.transform.SetParent(lamp.transform, false);
            socketObject.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            BoxCollider drapeTarget = socketObject.AddComponent<BoxCollider>();
            drapeTarget.isTrigger = true;
            drapeTarget.size = new Vector3(0.55f, 0.65f, 0.55f);
            InteractableEntity entity = socketObject.AddComponent<InteractableEntity>();
            entity.Configure(null, "lamp_drape_socket");
            PlacementSocket socket = socketObject.AddComponent<PlacementSocket>();
            socket.Configure("lamp", ItemCategory.TripTrap, socketObject.transform, facts);
            PlaceCapability place = socketObject.AddComponent<PlaceCapability>();
            place.Configure(socket);
        }

        private static void WireVaseAndFragiles(GameplayEventHub events)
        {
            WireFragile("PRP_Vase", events, true, true, false);
            WireFragile("FUR_WoodChair", events, false, false, false);
            WireFragile("PRP_WallClock", events, false, false, true);
            WireFragile("PRP_Mirror", events, false, false, false);
        }

        private static void WireFragile(string objectName, GameplayEventHub events, bool shatterOnLand, bool shards,
                                        bool silence)
        {
            GameObject item = GameObject.Find(objectName);
            if (item == null || item.GetComponent<BreakableItemBehaviour>() != null)
            {
                return;
            }

            StatefulWorldItem stateful = item.GetComponent<StatefulWorldItem>();
            if (stateful == null)
            {
                Rigidbody body = item.GetComponent<Rigidbody>() ?? item.AddComponent<Rigidbody>();
                body.isKinematic = true;
                stateful = item.AddComponent<StatefulWorldItem>();
                stateful.Configure(body);
            }

            BreakableItemBehaviour breakage = item.AddComponent<BreakableItemBehaviour>();
            breakage.Configure(stateful, events, null, shatterOnLand, shards, silence);
        }

        private static void WireBooks()
        {
            GameObject books = GameObject.Find("PRP_Books");
            if (books == null || books.GetComponent<BookFlipUseHandler>() != null)
            {
                return;
            }

            BookFlipUseHandler flip = books.AddComponent<BookFlipUseHandler>();
            UseCapability use = books.GetComponent<UseCapability>() ?? books.AddComponent<UseCapability>();
            use.Configure(flip);
        }

        private static void WireRugAndBlanketTraps(GameplayFactService facts, GameplayEventHub events)
        {
            GameObject blanket = GameObject.Find("PRP_Blanket");
            if (blanket != null)
            {
                EnsureComponent<CarryCapability>(blanket).ConfigureDrag(0.5f);
            }

            GameObject rug = GameObject.Find("PRP_Rug");
            if (rug != null && rug.GetComponent<TripTrapController>() == null)
            {
                GameplayAffordanceHandler old = rug.GetComponent<GameplayAffordanceHandler>();
                if (old != null)
                {
                    Object.Destroy(old);
                }

                Collider collider = rug.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.isTrigger = true;
                }

                TripTrapController trap = rug.AddComponent<TripTrapController>();
                trap.Configure(facts, events, TrapState.RugArmed, true);
                UseCapability use = rug.GetComponent<UseCapability>() ?? rug.AddComponent<UseCapability>();
                use.Configure(trap);
            }

            GameObject socket = GameObject.Find("PRP_DoorTrapSocket");
            if (socket != null && socket.GetComponent<TripTrapController>() == null)
            {
                Collider collider = socket.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.isTrigger = true;
                }

                TripTrapController contact = socket.AddComponent<TripTrapController>();
                contact.Configure(facts, events, TrapState.BlanketArmed, false);
                contact.ConfigureSocket(socket.GetComponent<PlacementSocket>());
            }
            if (socket != null)
            {
                TripTrapController contact = socket.GetComponent<TripTrapController>();
                contact?.ConfigureSocket(socket.GetComponent<PlacementSocket>());
                if (contact != null)
                    EnsureComponent<UseCapability>(socket).Configure(contact);
            }
        }

        private static void WireBookcase(GameplayFactService facts, GameplayEventHub events)
        {
            GameObject bookcase = GameObject.Find("FUR_Bookcase");
            Transform door = GameObject.Find("PRP_RoomDoor_Interact")?.transform;
            TipBookcaseUseHandler tip = bookcase == null ? null : bookcase.GetComponent<TipBookcaseUseHandler>();
            tip?.Configure(facts, events, new Vector3(0f, 0f, 85f), door, Vector3.right);
        }

        private static void WireClock()
        {
            GameObject clock = GameObject.Find("PRP_WallClock");
            if (clock == null || clock.GetComponent<ClockTwitchBehaviour>() != null)
            {
                return;
            }

            Transform hand = clock.transform.Find("SecondHand");
            if (hand == null)
            {
                GameObject handObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handObject.name = "SecondHand";
                handObject.transform.SetParent(clock.transform, false);
                handObject.transform.localScale = new Vector3(0.02f, 0.16f, 0.02f);
                handObject.transform.localPosition = new Vector3(0f, 0.06f, 0.04f);
                Object.Destroy(handObject.GetComponent<Collider>());
                hand = handObject.transform;
            }

            ClockTwitchBehaviour twitch = clock.AddComponent<ClockTwitchBehaviour>();
            twitch.Configure(hand, clock.GetComponent<StatefulWorldItem>());
        }

        private static void WireSurfaces(GameplayFactService facts, Transform root)
        {
            GameObject window = GameObject.Find("PRP_BoardedWindow");
            StrikeSurfaceHitReceiver windowHit = window == null ? null : window.GetComponent<StrikeSurfaceHitReceiver>();
            if (windowHit != null)
            {
                GameObject brick = window.transform.Find("BrickReveal")?.gameObject;
                if (brick == null)
                {
                    brick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    brick.name = "BrickReveal";
                    brick.transform.SetParent(window.transform, false);
                    brick.transform.localPosition = Vector3.zero;
                    brick.transform.localScale = new Vector3(1.05f, 1.05f, 0.4f);
                    brick.SetActive(false);
                    Object.Destroy(brick.GetComponent<Collider>());
                }

                windowHit.Configure(facts, false, 5, brick);
            }

            GameObject wall = GameObject.Find("PRP_StrikeWall");
            wall?.GetComponent<StrikeSurfaceHitReceiver>()?.Configure(facts, true, 3);
            EnsureWall(root, "PRP_StrikeWall_North", new Vector3(0f, 1.4f, 2.64f), new Vector3(6.2f, 2.8f, 0.08f), facts);
            EnsureWall(root, "PRP_StrikeWall_South", new Vector3(0f, 1.4f, -2.64f), new Vector3(6.2f, 2.8f, 0.08f), facts);
            // Match the installed art and door collider opening (z 0.10 .. 1.00).
            EnsureWall(root, "PRP_StrikeWall_East", new Vector3(3.17f, 1.4f, -1.25f), new Vector3(0.08f, 2.8f, 2.70f), facts);
            EnsureWall(root, "PRP_StrikeWall_EastRear", new Vector3(3.17f, 1.4f, 1.825f), new Vector3(0.08f, 2.8f, 1.65f), facts);
        }

        private static void EnsureWall(Transform root, string name, Vector3 position, Vector3 scale, GameplayFactService facts)
        {
            if (GameObject.Find(name) != null)
            {
                return;
            }

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(root, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().enabled = false;
            InteractableEntity entity = wall.AddComponent<InteractableEntity>();
            entity.Configure(null, "walls");
            StrikeSurfaceHitReceiver hit = wall.AddComponent<StrikeSurfaceHitReceiver>();
            hit.Configure(facts, true, 3);
            wall.AddComponent<InspectCapability>();
        }

        private static void DisableAmbushProxy()
        {
            GameObject ambush = GameObject.Find("PRP_AmbushPoint");
            if (ambush == null)
            {
                return;
            }

            UseCapability use = ambush.GetComponent<UseCapability>();
            if (use != null)
            {
                use.enabled = false;
            }

            GameplayAffordanceHandler affordance = ambush.GetComponent<GameplayAffordanceHandler>();
            if (affordance != null)
            {
                affordance.enabled = false;
            }

            Collider collider = ambush.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static void WirePairedPerformance(PlayerInteractor player, KillerDirector killer,
                                                  InteractionScanner scanner)
        {
            if (killer == null || player == null)
            {
                return;
            }

            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            PairedPerformancePlayer paired = killer.GetComponent<PairedPerformancePlayer>();
            if (paired == null)
            {
                paired = killer.gameObject.AddComponent<PairedPerformancePlayer>();
            }

            paired.Configure(killer.transform, motor, player);
            Animator playerAnimator = player.GetComponentInChildren<Animator>(true);
            Animator killerAnimator = killer.GetComponentInChildren<Animator>(true);
            if (playerAnimator != null)
                EnsureComponent<PairedAnimationMarkerReceiver>(playerAnimator.gameObject);
            if (killerAnimator != null)
                EnsureComponent<PairedAnimationMarkerReceiver>(killerAnimator.gameObject);
            foreach (KillerWeaponVisualEventBridge bridge in
                     Object.FindObjectsByType<KillerWeaponVisualEventBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                bridge.ConfigurePairedPlayer(paired);
            CounterplayPerformanceRuntimeAdapter adapter = Object.FindAnyObjectByType<CounterplayPerformanceRuntimeAdapter>();
            adapter?.ConfigurePerformanceHooks(paired);
            if (killer.Catalog != null)
            {
                foreach (CounterplayDefinitionAsset definition in killer.Catalog.Definitions)
                {
                    if (definition != null && definition.Performance is M1GreyboxPlayableAsset performance)
                        paired.ConfigureAnimations(definition.OutcomeId, performance.PlayerAnimation, performance.KillerAnimation);
                }
            }
            M1GreyboxPerformancePlayer greybox = killer.GetComponent<M1GreyboxPerformancePlayer>();
            if (greybox != null)
            {
                paired.ConfigureD01Fallback(greybox, null);
            }

            WireCounterplayProps(player, killer, paired, greybox, adapter);

            killer.ConfigurePerformanceLauncher(paired);
        }

        private static void WireCounterplayProps(PlayerInteractor player, KillerDirector killer,
            PairedPerformancePlayer paired, M1GreyboxPerformancePlayer greybox,
            CounterplayPerformanceRuntimeAdapter adapter)
        {
            StolenLootDropController loot = Object.FindAnyObjectByType<StolenLootDropController>();
            if (loot == null) return;
            loot.ConfigureD21PairedPerformance(paired, player);
            loot.ConfigureD22FinalePresentation(greybox);

            Animator animator = killer.GetComponentInChildren<Animator>(true);
            Transform main = FindNamedChild(killer.transform, "PRP_PistolMain_EquippedVisual");
            Transform rail = FindNamedChild(killer.transform, "PRP_Flashlight_RailMountedVisual");
            Transform holster = FindNamedChild(killer.transform, "PRP_KillerHolster_Mounted");
            Transform backup = FindNamedChild(killer.transform, "PRP_PistolBackup_Holstered");
            loot.ConfigureD21DropOrigins(main, rail);
            WireDroppedFlashlight(player);
            if (animator != null && animator.isHuman && main != null && backup != null)
            {
                var presenter = EnsureComponent<KillerWeaponVisualPresenter>(killer.gameObject);
                presenter.Configure(main.gameObject, rail?.gameObject, holster?.gameObject,
                    backup.gameObject, animator.GetBoneTransform(HumanBodyBones.RightHand));
                presenter.ConfigureAimTarget(player.transform);
                var bridge = EnsureComponent<KillerWeaponVisualEventBridge>(killer.gameObject);
                bridge.Configure(presenter, adapter, FindWorldItem("PRP_PistolMain"),
                    FindWorldItem("PRP_Flashlight"), paired);
                bridge.ConfigureD22Loot(loot);
                loot.ConfigureD22ShotCueSource(backup);
            }

            SemanticAudioDirector audio = Object.FindAnyObjectByType<SemanticAudioDirector>();
            if (audio != null)
            {
                loot.D22ShotCueRequested -= audio.OnD22ShotCueRequested;
                loot.D22ShotCueRequested += audio.OnD22ShotCueRequested;
            }
            SemanticVfxDirector vfx = Object.FindAnyObjectByType<SemanticVfxDirector>();
            if (vfx != null)
            {
                loot.D22ShotCueRequested -= vfx.OnD22ShotCueRequested;
                loot.D22ShotCueRequested += vfx.OnD22ShotCueRequested;
            }
            killer.GetComponent<KillerWeaponVisualEventBridge>()?.ConfigureShotFeedback(
                audio, vfx, killer.GetComponent<CharacterAnimatorDriver>());
        }

        private static StatefulWorldItem FindWorldItem(string name)
        {
            foreach (var item in Object.FindObjectsByType<StatefulWorldItem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.name == name) return item;
            return null;
        }

        private static void WireDroppedFlashlight(PlayerInteractor player)
        {
            StatefulWorldItem flashlight = FindWorldItem("PRP_Flashlight");
            if (flashlight == null) return;
            Transform child = flashlight.transform.Find("DroppedFlashlightBeam");
            if (child == null)
            {
                child = new GameObject("DroppedFlashlightBeam").transform;
                child.SetParent(flashlight.transform, false);
            }
            Light beam = EnsureComponent<Light>(child.gameObject);
            beam.type = LightType.Spot;
            beam.range = 7f;
            beam.spotAngle = 32f;
            beam.innerSpotAngle = 18f;
            beam.intensity = 4f;
            beam.color = new Color(0.82f, 0.9f, 1f);
            beam.shadows = LightShadows.Soft;
            beam.shadowBias = 0.02f;
            beam.shadowNormalBias = 0.05f;
            beam.enabled = false;
            EnsureComponent<DroppedFlashlightPresenter>(flashlight.gameObject).Configure(flashlight, player, beam);
        }

        private static Transform FindNamedChild(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static void WireLampBrightener(GameplayFactService facts, KillerDirector killer)
        {
            if (killer == null)
            {
                return;
            }

            GameObject lamp = GameObject.Find("PRP_TableLamp");
            BreakableLight table = lamp == null ? null : lamp.GetComponent<BreakableLight>();
            KillerLampBrightener brightener = killer.GetComponent<KillerLampBrightener>() ??
                                              killer.gameObject.AddComponent<KillerLampBrightener>();
            brightener.Configure(facts, table);
        }

        private static void WireOpeningSeat(PlayerInteractor player, GameplayFactService facts)
        {
            if (player == null) return;
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            Animator animator = player.GetComponentInChildren<Animator>(true);
            Transform chair = GameObject.Find("FUR_WoodChair")?.transform;
            LoopDirector loop = Object.FindAnyObjectByType<LoopDirector>();
            if (motor == null || animator == null || chair == null || loop == null) return;
            EnsureComponent<PlayerSeatedPosePresenter>(player.gameObject).Configure(
                loop, motor.Input, motor, facts, animator, chair);
        }

        private static void WireInspectFocus(InteractionScanner scanner)
        {
            if (scanner == null)
            {
                return;
            }

            LookAimRigController look = scanner.GetComponent<LookAimRigController>() ??
                                        Object.FindAnyObjectByType<LookAimRigController>();
            InspectFocusLookDriver driver = scanner.GetComponent<InspectFocusLookDriver>() ??
                                            scanner.gameObject.AddComponent<InspectFocusLookDriver>();
            driver.Configure(scanner, look);
        }

        private static void WireRoomView()
        {
            Transform screen = GameObject.Find("PRP_CRTTV")?.transform.Find("CameraScreenAnchor");
            Object.FindAnyObjectByType<CameraModeController>()?.AlignFixedView(screen);

            GameObject mirror = GameObject.Find("PRP_Mirror");
            if (mirror == null) return;
            Renderer surface = null;
            foreach (Renderer candidate in mirror.GetComponentsInChildren<Renderer>(true))
            {
                if (candidate.name == "Mirror_Plane") { surface = candidate; break; }
            }
            if (surface != null)
            {
                EnsureComponent<MirrorReflectionController>(mirror).Configure(
                    UnityEngine.Camera.main, null, surface, surface.transform, null,
                    mirror.GetComponent<StatefulWorldItem>());
            }
        }

        private static void WireEndingObserver(Transform roomRoot)
        {
            EndingDirector ending = Object.FindAnyObjectByType<EndingDirector>();
            Transform screen = GameObject.Find("PRP_CRTTV")?.transform.Find("CameraScreenAnchor");
            Transform actor = GameObject.Find("Player")?.transform.Find("CH_Player_Model");
            if (ending == null || screen == null || actor == null) return;
            Transform entry = roomRoot.Find("EndingObserverEntry");
            if (entry == null)
            {
                entry = new GameObject("EndingObserverEntry").transform;
                entry.SetParent(roomRoot, false);
            }
            Vector3 position = screen.position + screen.forward * 0.7f;
            position.y = 0.08f;
            entry.SetPositionAndRotation(position, screen.rotation);
            Transform exit = roomRoot.Find("EndingOriginalActorExit");
            if (exit == null)
            {
                exit = new GameObject("EndingOriginalActorExit").transform;
                exit.SetParent(roomRoot, false);
            }
            exit.SetPositionAndRotation(new Vector3(3.75f, 0.08f, 0.55f), Quaternion.Euler(0f, 90f, 0f));
            ending.ConfigurePresentation(ending.SettingsAsset, entry, actor, exit);
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component == null ? target.AddComponent<T>() : component;
        }
    }
}
