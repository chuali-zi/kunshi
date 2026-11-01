using System;
using System.Collections.Generic;
using ConGame.Presentation.Props;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class WeaponVisualOwnershipModelTests
    {
        private WeaponVisualOwnershipModel _model;

        [SetUp]
        public void SetUp()
        {
            _model = new WeaponVisualOwnershipModel();
        }

        [Test]
        public void InitialState_CharacterVisualOwnsEverySlot()
        {
            foreach (WeaponVisualSlot slot in Enum.GetValues(typeof(WeaponVisualSlot)))
            {
                Assert.That(_model.GetOwner(slot), Is.EqualTo(WeaponVisualOwner.CharacterVisual), slot.ToString());
            }
        }

        [Test]
        public void DropToGameplay_MovesAuthorityOffCharacter_Idempotent()
        {
            _model.DropToGameplay(WeaponVisualSlot.MainPistol);

            Assert.That(_model.GetOwner(WeaponVisualSlot.MainPistol), Is.EqualTo(WeaponVisualOwner.GameplayObject));
            Assert.That(_model.GetOwner(WeaponVisualSlot.RailFlashlight), Is.EqualTo(WeaponVisualOwner.CharacterVisual));

            int eventCount = 0;
            _model.OwnershipChanged += (_, _) => eventCount++;
            _model.DropToGameplay(WeaponVisualSlot.MainPistol);
            Assert.That(eventCount, Is.EqualTo(0), "重复掉落同一槽位不得重复发事件。");
        }

        [Test]
        public void TwoPhaseTransfer_BeginHidesBothSides_CompleteMovesToGameplay()
        {
            var events = new List<WeaponVisualOwner>();
            _model.OwnershipChanged += (slot, owner) =>
            {
                if (slot == WeaponVisualSlot.RailFlashlight)
                {
                    events.Add(owner);
                }
            };

            Assert.That(_model.TryBeginTransfer(WeaponVisualSlot.RailFlashlight, WeaponVisualOwner.GameplayObject), Is.True);
            Assert.That(_model.GetOwner(WeaponVisualSlot.RailFlashlight), Is.EqualTo(WeaponVisualOwner.Transitioning));
            Assert.That(_model.IsTransitioning(WeaponVisualSlot.RailFlashlight), Is.True);

            Assert.That(_model.CompleteTransfer(WeaponVisualSlot.RailFlashlight), Is.True);
            Assert.That(_model.GetOwner(WeaponVisualSlot.RailFlashlight), Is.EqualTo(WeaponVisualOwner.GameplayObject));
            Assert.That(events, Is.EqualTo(new[] { WeaponVisualOwner.Transitioning, WeaponVisualOwner.GameplayObject }));
        }

        [Test]
        public void CancelTransfer_ReturnsToPreviousOwner()
        {
            Assert.That(_model.TryBeginTransfer(WeaponVisualSlot.MainPistol, WeaponVisualOwner.GameplayObject), Is.True);
            Assert.That(_model.CancelTransfer(WeaponVisualSlot.MainPistol), Is.True);
            Assert.That(_model.GetOwner(WeaponVisualSlot.MainPistol), Is.EqualTo(WeaponVisualOwner.CharacterVisual));
        }

        [Test]
        public void InvalidTransitions_AreRejected()
        {
            Assert.That(_model.TryBeginTransfer(WeaponVisualSlot.MainPistol, WeaponVisualOwner.CharacterVisual), Is.False,
                "目标与当前持有者相同不得进入过渡。");
            Assert.That(_model.TryBeginTransfer(WeaponVisualSlot.MainPistol, WeaponVisualOwner.Transitioning), Is.False);
            Assert.That(_model.CompleteTransfer(WeaponVisualSlot.MainPistol), Is.False, "非过渡中不得完成。");
            Assert.That(_model.CancelTransfer(WeaponVisualSlot.MainPistol), Is.False, "非过渡中不得取消。");

            Assert.That(_model.TryBeginTransfer(WeaponVisualSlot.MainPistol, WeaponVisualOwner.GameplayObject), Is.True);
            Assert.That(_model.TryBeginTransfer(WeaponVisualSlot.MainPistol, WeaponVisualOwner.CharacterVisual), Is.False,
                "过渡中不得叠加第二个过渡。");
        }

        [Test]
        public void ForceSetOwner_RejectsTransitioning()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _model.ForceSetOwner(WeaponVisualSlot.MainPistol, WeaponVisualOwner.Transitioning));
        }

        [Test]
        public void ForceSetOwner_DuringTransition_CompletesAuthoritatively()
        {
            _model.TryBeginTransfer(WeaponVisualSlot.Holster, WeaponVisualOwner.GameplayObject);
            _model.ForceSetOwner(WeaponVisualSlot.Holster, WeaponVisualOwner.GameplayObject);

            Assert.That(_model.GetOwner(WeaponVisualSlot.Holster), Is.EqualTo(WeaponVisualOwner.GameplayObject));
            Assert.That(_model.IsTransitioning(WeaponVisualSlot.Holster), Is.False);
        }

        [Test]
        public void CaptureAndReset_RestoresInitialGunBeltState()
        {
            _model.CaptureInitialState();
            _model.DropToGameplay(WeaponVisualSlot.MainPistol);
            _model.DropToGameplay(WeaponVisualSlot.RailFlashlight);
            _model.TryBeginTransfer(WeaponVisualSlot.BackupPistol, WeaponVisualOwner.GameplayObject);

            _model.ResetToInitial();

            foreach (WeaponVisualSlot slot in Enum.GetValues(typeof(WeaponVisualSlot)))
            {
                Assert.That(_model.GetOwner(slot), Is.EqualTo(WeaponVisualOwner.CharacterVisual), slot.ToString());
            }
        }

        [Test]
        public void EverySlot_HasExactlyOneAuthoritativeOwner_AtAllTimes()
        {
            var slots = (WeaponVisualSlot[])Enum.GetValues(typeof(WeaponVisualSlot));
            _model.DropToGameplay(slots[0]);
            _model.TryBeginTransfer(slots[1], WeaponVisualOwner.GameplayObject);

            foreach (WeaponVisualSlot slot in slots)
            {
                WeaponVisualOwner owner = _model.GetOwner(slot);
                bool singleAuthority =
                    (owner == WeaponVisualOwner.CharacterVisual) ^ (owner == WeaponVisualOwner.GameplayObject);
                Assert.That(singleAuthority || owner == WeaponVisualOwner.Transitioning, Is.True,
                    $"{slot} 必须只有一个权威持有者，或处于无权威的过渡中。");
            }
        }
    }
}
