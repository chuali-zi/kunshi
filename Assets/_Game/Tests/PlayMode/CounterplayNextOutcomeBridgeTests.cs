using System.Collections;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.AI;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConGame.Tests.PlayMode
{
    public sealed class CounterplayNextOutcomeBridgeTests
    {
        [UnityTest]
        public IEnumerator StaleEpochNextOutcomeIsIgnoredWhenLoopIsBound()
        {
            BridgeFixture fixture = new BridgeFixture("stale_epoch_bridge", "D08", "D22");
            try
            {
                Assert.That(fixture.Killer.TryStartOutcome("D08"), Is.True);
                Assert.That(fixture.Killer.SelectedOutcomeId, Is.EqualTo("D08"));

                fixture.Adapter.RequestNextOutcome("D08", fixture.Loop.LoopEpoch - 1, "D22");

                Assert.That(fixture.Killer.SelectedOutcomeId, Is.EqualTo("D08"));
                yield return null;
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator CurrentEpochD08AndD21SourcesAdvanceTheirOwnOutcomes()
        {
            BridgeFixture fixture = new BridgeFixture(
                "current_epoch_bridge", "D08", "D22", "D21", "D17");
            try
            {
                Assert.That(fixture.Killer.TryStartOutcome("D08"), Is.True);
                fixture.Adapter.RequestNextOutcome("D08", fixture.Loop.LoopEpoch, "D22");
                Assert.That(fixture.Killer.SelectedOutcomeId, Is.EqualTo("D22"));

                Assert.That(fixture.Killer.TryStartOutcome("D21"), Is.True);
                fixture.Adapter.RequestNextOutcome("D21", fixture.Loop.LoopEpoch, "D17");
                Assert.That(fixture.Killer.SelectedOutcomeId, Is.EqualTo("D17"));

                yield return null;
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private sealed class BridgeFixture
        {
            private readonly CounterplayDefinitionAsset[] _definitions;

            public BridgeFixture(string name, params string[] outcomeIds)
            {
                Root = new GameObject(name);
                Loop = Root.AddComponent<LoopDirector>();

                CounterplayCatalogAsset catalog = ScriptableObject.CreateInstance<CounterplayCatalogAsset>();
                _definitions = new CounterplayDefinitionAsset[outcomeIds.Length];
                for (int i = 0; i < outcomeIds.Length; i++)
                {
                    CounterplayDefinitionAsset definition =
                        ScriptableObject.CreateInstance<CounterplayDefinitionAsset>();
                    definition.Configure(
                        outcomeIds[i],
                        i + 1,
                        CounterplayTriggerMode.Reactive,
                        new FactCondition[0],
                        ContentStatus.Implemented,
                        null);
                    _definitions[i] = definition;
                }

                catalog.Configure(_definitions);
                Catalog = catalog;

                Killer = Root.AddComponent<KillerDirector>();
                Killer.Configure(null, null, null, Catalog, null);
                Killer.PrepareEntry(Loop.LoopEpoch, _ => { });

                Adapter = Root.AddComponent<CounterplayPerformanceRuntimeAdapter>();
                Adapter.Configure(null, Loop, null);

                Bridge = Root.AddComponent<CounterplayNextOutcomeBridge>();
                Bridge.Configure(Adapter, Killer);
            }

            public GameObject Root { get; }
            public LoopDirector Loop { get; }
            public CounterplayCatalogAsset Catalog { get; }
            public KillerDirector Killer { get; }
            public CounterplayPerformanceRuntimeAdapter Adapter { get; }
            public CounterplayNextOutcomeBridge Bridge { get; }

            public void Dispose()
            {
                Object.Destroy(Root);
                Object.Destroy(Catalog);
                for (int i = 0; i < _definitions.Length; i++)
                {
                    Object.Destroy(_definitions[i]);
                }
            }
        }
    }
}
