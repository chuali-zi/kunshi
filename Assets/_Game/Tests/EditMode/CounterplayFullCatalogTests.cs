using System.Collections.Generic;
using ConGame.Domain.Counterplay;
using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Data;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class CounterplayFullCatalogTests
    {
        private static readonly HashSet<string> ImplementedCounterplays = new HashSet<string>
        {
            "D01", "D02", "D03", "D03P", "D04", "D05", "D06", "D07", "D08", "D09", "D10", "D11", "D12",
            "D13", "D14", "D15", "D16", "D17", "D18", "D19", "D20", "D21", "D22"
        };

        private static readonly HashSet<string> ImplementedItems = new HashSet<string>
        {
            "sofa_awaken", "blanket", "bed", "wood_chair", "bookcase", "baseball_bat", "room_door",
            "ceiling_light", "table_lamp", "walls", "crt_tv", "remote", "dresser", "books", "vase",
            "boarded_window", "rug", "wall_clock", "pistol_main", "pistol_backup", "flashlight",
            "killer_coat", "mirror"
        };

        [Test]
        public void AllTwentyThreeCounterplaySeedsExistWithHonestStatusAndPositiveCases()
        {
            Assert.AreEqual(23, CounterplaySeedLibrary.All.Count);
            HashSet<string> ids = new HashSet<string>();

            foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
            {
                Assert.That(ids.Add(seed.Id), Is.True, $"Duplicate counterplay id {seed.Id}.");
                ContentStatus expected = ImplementedCounterplays.Contains(seed.Id)
                                            ? ContentStatus.Implemented
                                            : ContentStatus.Specified;
                Assert.That(seed.Status, Is.EqualTo(expected), seed.Id);

                FactSnapshot snapshot = CreatePositiveSnapshot(seed.Conditions);
                CounterplayRule rule = new CounterplayRule(
                    seed.Id,
                    seed.SortOrder,
                    seed.TriggerMode,
                    seed.Conditions,
                    seed.Status);
                CounterplayTriggerMode trigger = FirstTrigger(seed.TriggerMode);
                Assert.That(rule.Matches(trigger, snapshot), Is.True, seed.Id);
            }
        }

        [Test]
        public void CompleteCatalogUsesDocumentedPriorityAndFallback()
        {
            List<CounterplayRule> rules = BuildRules();
            CounterplayEvaluator evaluator = new CounterplayEvaluator();

            FactSnapshot blackAmbush = FactSnapshot.CreateBaseline();
            blackAmbush.RoomLight = RoomLightState.PersistentBlack;
            blackAmbush.IsAmbushing = true;
            blackAmbush.HasMeaningfulInput = true;
            Assert.That(
                evaluator.Select(rules, CounterplayTriggerMode.Entry, blackAmbush)?.Id,
                Is.EqualTo("D20"),
                "D20 must beat generic persistent-black D19.");

            FactSnapshot unknown = FactSnapshot.CreateBaseline();
            unknown.PlayerZone = PlayerZone.Open;
            unknown.IsAtAwakeningPoint = false;
            unknown.HasMeaningfulInput = true;
            Assert.That(
                evaluator.Select(rules, CounterplayTriggerMode.Entry, unknown)?.Id,
                Is.EqualTo("D01"));
        }

        [Test]
        public void ItemSeedsUseHonestImplementedStatusesForBatch1()
        {
            Assert.AreEqual(23, ItemSeedLibrary.All.Count);
            HashSet<string> ids = new HashSet<string>();
            foreach (ItemSeed seed in ItemSeedLibrary.All)
            {
                Assert.That(ids.Add(seed.Id), Is.True, $"Duplicate item id {seed.Id}.");
                ContentStatus expected = ImplementedItems.Contains(seed.Id)
                                            ? ContentStatus.Implemented
                                            : ContentStatus.Specified;
                Assert.That(seed.Status, Is.EqualTo(expected), seed.Id);
            }
        }

        private static List<CounterplayRule> BuildRules()
        {
            List<CounterplayRule> rules = new List<CounterplayRule>();
            foreach (CounterplaySeed seed in CounterplaySeedLibrary.All)
            {
                rules.Add(new CounterplayRule(
                    seed.Id,
                    seed.SortOrder,
                    seed.TriggerMode,
                    seed.Conditions,
                    seed.Status));
            }

            return rules;
        }

        private static CounterplayTriggerMode FirstTrigger(CounterplayTriggerMode mode)
        {
            if ((mode & CounterplayTriggerMode.Entry) != 0)
            {
                return CounterplayTriggerMode.Entry;
            }

            if ((mode & CounterplayTriggerMode.Reactive) != 0)
            {
                return CounterplayTriggerMode.Reactive;
            }

            if ((mode & CounterplayTriggerMode.Continuous) != 0)
            {
                return CounterplayTriggerMode.Continuous;
            }

            return CounterplayTriggerMode.Fallback;
        }

        private static FactSnapshot CreatePositiveSnapshot(FactCondition[] conditions)
        {
            FactSnapshot snapshot = default;
            for (int i = 0; i < conditions.Length; i++)
            {
                FactCondition condition = conditions[i];
                int value = condition.Operator == FactOperator.False ? 0 : condition.Expected;
                if (condition.Operator == FactOperator.True && value == 0)
                {
                    value = 1;
                }

                SetFact(ref snapshot, condition.Key, value);
            }

            return snapshot;
        }

        private static void SetFact(ref FactSnapshot snapshot, FactKey key, int value)
        {
            switch (key)
            {
                case FactKey.PlayerZone: snapshot.PlayerZone = (PlayerZone)value; break;
                case FactKey.PlayerPose: snapshot.PlayerPose = (PlayerPose)value; break;
                case FactKey.HeldItemCategories: snapshot.HeldItemCategories = (ItemCategory)value; break;
                case FactKey.RoomLight: snapshot.RoomLight = (RoomLightState)value; break;
                case FactKey.DoorBlock: snapshot.DoorBlock = (DoorBlockState)value; break;
                case FactKey.Trap: snapshot.Trap = (TrapState)value; break;
                case FactKey.TrapPulled: snapshot.TrapPulled = value != 0; break;
                case FactKey.HasStolenPistol: snapshot.HasStolenPistol = value != 0; break;
                case FactKey.IsAmbushing: snapshot.IsAmbushing = value != 0; break;
                case FactKey.IsMoving: snapshot.IsMoving = value != 0; break;
                case FactKey.IsGlareActive: snapshot.IsGlareActive = value != 0; break;
                case FactKey.IsStrikingWall: snapshot.IsStrikingWall = value != 0; break;
                case FactKey.IsStrikingWindow: snapshot.IsStrikingWindow = value != 0; break;
                case FactKey.HasMeaningfulInput: snapshot.HasMeaningfulInput = value != 0; break;
                case FactKey.TelevisionDamage: snapshot.TelevisionDamage = (TelevisionDamageState)value; break;
                case FactKey.IsSwinging: snapshot.IsSwinging = value != 0; break;
                case FactKey.IsAtAwakeningPoint: snapshot.IsAtAwakeningPoint = value != 0; break;
                case FactKey.IncomingProjectile: snapshot.IncomingProjectile = value != 0; break;
            }
        }
    }
}
