using System.Collections;
using ConGame.Domain.Facts;
using ConGame.Runtime.AI;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using ConGame.Domain.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class KillerLampBrightenerPlayModeTests
    {
        [UnityTest]
        public IEnumerator DrapingTableLampTemporarilyBlocksD18AndResetRestoresItsInitialState()
        {
            GameObject root = new GameObject("draped_lamp_coordinator_test");
            try
            {
                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                Light ceilingUnityLight = CreateLight(root.transform, "ceiling_light", Vector3.zero);
                BreakableLight ceiling = ceilingUnityLight.gameObject.AddComponent<BreakableLight>();
                ceiling.Configure(ceilingUnityLight);
                ceiling.CaptureInitialState();

                Light tableUnityLight = CreateLight(root.transform, "table_lamp", new Vector3(0f, 1.08f, 1.4f));
                BoxCollider lampCollider = tableUnityLight.gameObject.AddComponent<BoxCollider>();
                lampCollider.size = new Vector3(0.35f, 1f, 0.35f);
                BreakableLight table = tableUnityLight.gameObject.AddComponent<BreakableLight>();
                table.Configure(tableUnityLight);
                table.CaptureInitialState();

                RoomLightCoordinator coordinator = root.AddComponent<RoomLightCoordinator>();
                coordinator.Configure(ceiling, table, facts);
                coordinator.CaptureInitialState();
                facts.CaptureInitialState();

                ceiling.ReceiveHit(new HitInfo(ImpactKind.Heavy, "test", Vector3.zero, Vector3.forward, 1f));
                table.SetSwitchOn(false);
                Assert.That(coordinator.CurrentState, Is.EqualTo(RoomLightState.RecoverableDark));

                int drapeChanges = 0;
                table.BrokenChanged += (_, __) => drapeChanges++;
                table.SetDraped(true);
                Assert.That(coordinator.CurrentState, Is.EqualTo(RoomLightState.Dim));
                table.SetDraped(true);
                Assert.That(drapeChanges, Is.EqualTo(1), "Repeated presenter updates must not rebroadcast unchanged drape state.");

                GameObject killer = new GameObject("killer");
                killer.transform.SetParent(root.transform);
                KillerLampBrightener brightener = killer.AddComponent<KillerLampBrightener>();
                brightener.Configure(facts, table);
                brightener.TryBrightenForRecoverableDark();
                Assert.That(table.IsSwitchOn, Is.False, "A draped lamp must not be restored through the D18 path.");

                table.SetDraped(false);
                Assert.That(coordinator.CurrentState, Is.EqualTo(RoomLightState.RecoverableDark));
                brightener.TryBrightenForRecoverableDark();
                Assert.That(table.IsSwitchOn, Is.True);

                LoopResetContext context = new LoopResetContext(1, 2);
                table.Restore(context);
                ceiling.Restore(context);
                coordinator.Resume(context);
                Assert.That(table.IsDraped, Is.False);
                Assert.That(tableUnityLight.color, Is.EqualTo(Color.white));
                Assert.That(coordinator.CurrentState, Is.EqualTo(RoomLightState.Lit));
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator RecoverableDarkLampCannotBeTurnedOnThroughAWall()
        {
            GameObject root = new GameObject("lamp_brightener_test");
            try
            {
                GameplayFactService facts = root.AddComponent<GameplayFactService>();
                facts.CaptureInitialState();
                facts.SetRoomLight(RoomLightState.RecoverableDark);

                GameObject killer = new GameObject("killer");
                killer.transform.SetParent(root.transform);
                GameObject lampObject = new GameObject("table_lamp");
                lampObject.transform.SetParent(root.transform);
                lampObject.transform.position = new Vector3(0f, 1.08f, 1.4f);
                BoxCollider lampCollider = lampObject.AddComponent<BoxCollider>();
                lampCollider.size = new Vector3(0.35f, 1f, 0.35f);
                BreakableLight lamp = lampObject.AddComponent<BreakableLight>();
                Light light = lampObject.AddComponent<Light>();
                lamp.Configure(light);
                lamp.SetSwitchOn(false);

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "wall";
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector3(0f, 0.75f, 0.7f);
                wall.transform.localScale = new Vector3(1f, 0.8f, 0.2f);

                KillerLampBrightener brightener = killer.AddComponent<KillerLampBrightener>();
                brightener.Configure(facts, lamp);
                Physics.SyncTransforms();
                brightener.TryBrightenForRecoverableDark();
                Assert.That(lamp.IsSwitchOn, Is.False, "A low obstacle blocks the killer's reach to the lamp body.");

                wall.SetActive(false);
                Physics.SyncTransforms();
                brightener.TryBrightenForRecoverableDark();
                Assert.That(lamp.IsSwitchOn, Is.True, "The lamp can be restored after the killer reaches it.");
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        private static Light CreateLight(Transform parent, string name, Vector3 position)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent);
            lightObject.transform.position = position;
            return lightObject.AddComponent<Light>();
        }
    }
}
