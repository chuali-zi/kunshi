using System.Collections.Generic;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Counterplay;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{

    public sealed class CounterplayEvaluatorTests
    {
        private List<CounterplayRule> _rules;
        private CounterplayEvaluator _evaluator;

        [SetUp]
        public void SetUp()
        {
            _rules = new List<CounterplayRule>();
            foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
            {
                _rules.Add(new CounterplayRule(seed.Id, seed.SortOrder, seed.TriggerMode, seed.Conditions, seed.Status));
            }

            _evaluator = new CounterplayEvaluator();
        }

        [Test]
        public void SofaRearStandingSelectsD03()
        {
            FactSnapshot facts = FactSnapshot.CreateBaseline();
            facts.PlayerZone = PlayerZone.SofaRear;
            facts.PlayerPose = PlayerPose.Standing;
            facts.HasMeaningfulInput = true;

            Assert.That(SelectEntry(facts), Is.EqualTo("D03"));
        }

        [Test]
        public void WeaponInOpenSelectsD07()
        {
            FactSnapshot facts = FactSnapshot.CreateBaseline();
            facts.PlayerZone = PlayerZone.Open;
            facts.IsAtAwakeningPoint = false;
            facts.HasMeaningfulInput = true;
            facts.HeldItemCategories = ItemCategory.Weapon;

            Assert.That(SelectEntry(facts), Is.EqualTo("D07"));
        }

        [Test]
        public void BlackAmbushBeatsGenericBlackResponse()
        {
            FactSnapshot facts = FactSnapshot.CreateBaseline();
            facts.RoomLight = RoomLightState.PersistentBlack;
            facts.IsAmbushing = true;
            facts.HasMeaningfulInput = true;

            Assert.That(SelectEntry(facts), Is.EqualTo("D20"));
        }

        [Test]
        public void UnknownActiveStateFallsBackToD01()
        {
            FactSnapshot facts = FactSnapshot.CreateBaseline();
            facts.PlayerZone = PlayerZone.Open;
            facts.IsAtAwakeningPoint = false;
            facts.HasMeaningfulInput = true;

            Assert.That(SelectEntry(facts), Is.EqualTo("D01"));
        }

        private string SelectEntry(FactSnapshot facts)
        {
            return _evaluator.Select(_rules, CounterplayTriggerMode.Entry, facts)?.Id;
        }
    }
}
