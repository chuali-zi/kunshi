using System.Collections.Generic;
using ConGame.Presentation.Mirror;
using NUnit.Framework;

namespace ConGame.Tests.EditMode
{
    public sealed class MirrorUpdateStateMachineTests
    {
        private MirrorUpdateStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _machine = new MirrorUpdateStateMachine();
        }

        [Test]
        public void InitialState_Hidden_DoesNotRender_UsesIntactMaterial()
        {
            Assert.That(_machine.State, Is.EqualTo(MirrorUpdateState.Hidden));
            Assert.That(_machine.ShouldRender, Is.False);
            Assert.That(_machine.UseBrokenMaterial, Is.False);
        }

        [Test]
        public void VisibleAndIntact_ActivatesRendering()
        {
            MirrorUpdateState? observed = null;
            _machine.StateChanged += state => observed = state;

            _machine.SetVisible(true);

            Assert.That(_machine.State, Is.EqualTo(MirrorUpdateState.Active));
            Assert.That(_machine.ShouldRender, Is.True);
            Assert.That(observed, Is.EqualTo(MirrorUpdateState.Active));
        }

        [Test]
        public void LosingVisibility_StopsRendering()
        {
            _machine.SetVisible(true);
            _machine.SetVisible(false);

            Assert.That(_machine.State, Is.EqualTo(MirrorUpdateState.Hidden));
            Assert.That(_machine.ShouldRender, Is.False);
        }

        [Test]
        public void Broken_StopsRendering_AndSwitchesToBrokenMaterial_EvenWhileVisible()
        {
            _machine.SetVisible(true);
            _machine.SetBroken(true);

            Assert.That(_machine.State, Is.EqualTo(MirrorUpdateState.Broken));
            Assert.That(_machine.ShouldRender, Is.False);
            Assert.That(_machine.UseBrokenMaterial, Is.True);

            _machine.SetVisible(false);
            _machine.SetVisible(true);
            Assert.That(_machine.State, Is.EqualTo(MirrorUpdateState.Broken), "破碎优先于可见性。");
            Assert.That(_machine.ShouldRender, Is.False);
        }

        [Test]
        public void ResetMirror_RestoresIntactAndHidden()
        {
            var observed = new List<MirrorUpdateState>();
            _machine.SetVisible(true);
            _machine.SetBroken(true);
            _machine.StateChanged += state => observed.Add(state);

            _machine.ResetMirror();

            Assert.That(_machine.IsBroken, Is.False);
            Assert.That(_machine.UseBrokenMaterial, Is.False);
            Assert.That(_machine.State, Is.EqualTo(MirrorUpdateState.Hidden));
            Assert.That(observed, Is.EqualTo(new[] { MirrorUpdateState.Hidden }));

            _machine.SetVisible(true);
            Assert.That(_machine.ShouldRender, Is.True, "重置后恢复更新能力。");
        }

        [Test]
        public void IdempotentInputs_DoNotRefireEvents()
        {
            int eventCount = 0;
            _machine.StateChanged += _ => eventCount++;

            _machine.SetVisible(true);
            _machine.SetVisible(true);
            _machine.SetBroken(false);

            Assert.That(eventCount, Is.EqualTo(1));
        }
    }
}
