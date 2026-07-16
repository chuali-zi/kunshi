using System.Collections.Generic;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Counterplay;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class CounterplayCatalogM1Tests
    {
        private readonly List<Object> _createdObjects = new List<Object>();
        private CounterplayCatalogAsset _catalog;

        [SetUp]
        public void SetUp()
        {
            List<CounterplayDefinitionAsset> definitions = new List<CounterplayDefinitionAsset>();
            foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
            {
                CounterplayDefinitionAsset definition = ScriptableObject.CreateInstance<CounterplayDefinitionAsset>();
                definition.Configure(seed.Id, seed.SortOrder, seed.TriggerMode, seed.Conditions, seed.Status);
                definitions.Add(definition);
                _createdObjects.Add(definition);
            }

            _catalog = ScriptableObject.CreateInstance<CounterplayCatalogAsset>();
            _catalog.Configure(definitions.ToArray());
            _createdObjects.Add(_catalog);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void ImplementedD02IsSelectedWhenIdleAtAwakening()
        {
            FactSnapshot facts = FactSnapshot.CreateBaseline();
            AssertSelected(CounterplayTriggerMode.Entry, facts, "D02");
        }

        [Test]
        public void ImplementedD03PIsSelectedWhenSofaRearCrouched()
        {
            FactSnapshot facts = FactSnapshot.CreateBaseline();
            facts.PlayerZone = PlayerZone.SofaRear;
            facts.PlayerPose = PlayerPose.Crouched;
            facts.HasMeaningfulInput = true;
            facts.IsAtAwakeningPoint = false;

            AssertSelected(CounterplayTriggerMode.Entry, facts, "D03P");
        }

        [Test]
        public void ImplementedD10IsSelectableWhenProjectileIncoming()
        {
            FactSnapshot facts = FactSnapshot.CreateBaseline();
            facts.PlayerZone = PlayerZone.Open;
            facts.HasMeaningfulInput = true;
            facts.IsAtAwakeningPoint = false;
            facts.IncomingProjectile = true;

            AssertSelected(CounterplayTriggerMode.Reactive, facts, "D10");
        }

        [TestCase(PlayerZone.SofaRear, PlayerPose.Standing, ItemCategory.None, "D03")]
        [TestCase(PlayerZone.Open, PlayerPose.Standing, ItemCategory.Weapon, "D07")]
        public void ImplementedM1CounterplaysRemainSelectable(PlayerZone zone, PlayerPose pose,
                                                              ItemCategory heldCategories, string expectedOutcome)
        {
            FactSnapshot facts = FactSnapshot.CreateBaseline();
            facts.PlayerZone = zone;
            facts.PlayerPose = pose;
            facts.HeldItemCategories = heldCategories;
            facts.HasMeaningfulInput = true;
            facts.IsAtAwakeningPoint = false;

            AssertSelected(CounterplayTriggerMode.Entry, facts, expectedOutcome);
        }

        private void AssertSelected(CounterplayTriggerMode trigger, FactSnapshot facts, string expectedOutcome)
        {
            CounterplayDefinitionAsset selected = _catalog.Select(trigger, facts);

            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.OutcomeId, Is.EqualTo(expectedOutcome));
            Assert.That(selected.ContentStatus, Is.EqualTo(ContentStatus.Implemented));
        }
    }
}
