using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Counterplay;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class EndingCatalogFilterTests
    {
        [Test]
        public void EntryWithWeaponSelectsD07ForEndingFightPool()
        {
            var created = new System.Collections.Generic.List<Object>();
            try
            {
                var definitions = new System.Collections.Generic.List<CounterplayDefinitionAsset>();
                foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
                {
                    CounterplayDefinitionAsset definition = ScriptableObject.CreateInstance<CounterplayDefinitionAsset>();
                    definition.Configure(seed.Id, seed.SortOrder, seed.TriggerMode, seed.Conditions, seed.Status);
                    definitions.Add(definition);
                    created.Add(definition);
                }

                CounterplayCatalogAsset catalog = ScriptableObject.CreateInstance<CounterplayCatalogAsset>();
                catalog.Configure(definitions.ToArray());
                created.Add(catalog);

                FactSnapshot snapshot = FactSnapshot.CreateBaseline();
                snapshot.HasMeaningfulInput = true;
                snapshot.IsAtAwakeningPoint = false;
                snapshot.PlayerZone = PlayerZone.Open;
                snapshot.HeldItemCategories = ItemCategory.Weapon;

                CounterplayDefinitionAsset selected = catalog.Select(CounterplayTriggerMode.Entry, snapshot);
                Assert.That(selected, Is.Not.Null);
                Assert.That(selected.OutcomeId, Is.EqualTo("D07"));
            }
            finally
            {
                for (int i = created.Count - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }
        }
    }
}
