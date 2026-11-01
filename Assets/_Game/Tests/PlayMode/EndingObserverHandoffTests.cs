using ConGame.Bootstrap;
using ConGame.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.PlayMode
{
    public sealed class EndingObserverHandoffTests
    {
        private GameObject _root;
        private GameObject _screen;
        private GameObject _exit;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("EndingObserverHandoffTestRoot");
            _screen = new GameObject("ScreenAnchor");
            _exit = new GameObject("ExitAnchor");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_screen);
            Object.DestroyImmediate(_exit);
            GameObject clone = GameObject.Find("EndingOriginalActorVisual");
            if (clone != null)
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void TakeoverMovesMotorAndCreatesVisualOnlyClone()
        {
            PlayerMotor motor = _root.AddComponent<PlayerMotor>();
            GameObject visual = new GameObject("CH_Player_Model");
            visual.transform.SetParent(_root.transform, false);
            visual.AddComponent<Animator>();
            Vector3 originalPosition = _root.transform.position;
            _screen.transform.position = new Vector3(3f, 0f, 2f);

            EndingObserverHandoff handoff = _root.AddComponent<EndingObserverHandoff>();
            handoff.Configure(motor, null, _screen.transform, visual.transform, _exit.transform);
            handoff.CaptureOriginalPose();

            Assert.That(handoff.BeginObserverTakeover(), Is.True);
            Assert.That(handoff.HasObserverClone, Is.True);
            Assert.That(handoff.ObserverOwnsOriginalVisual, Is.True);
            Assert.That(motor.transform.position, Is.EqualTo(_screen.transform.position));
            Assert.That(GameObject.Find("EndingOriginalActorVisual"), Is.Not.Null);
            Assert.That(GameObject.Find("EndingOriginalActorVisual").GetComponent<PlayerMotor>(), Is.Null);
            Assert.That(handoff.CapturedPlayerPosition, Is.EqualTo(originalPosition));
        }

        [Test]
        public void ResetRestoresOriginalVisualAndRemovesClone()
        {
            PlayerMotor motor = _root.AddComponent<PlayerMotor>();
            GameObject visual = new GameObject("CH_Player_Model");
            visual.transform.SetParent(_root.transform, false);
            EndingObserverHandoff handoff = _root.AddComponent<EndingObserverHandoff>();
            handoff.Configure(motor, null, _screen.transform, visual.transform, _exit.transform);
            handoff.CaptureOriginalPose();
            Assert.That(handoff.BeginObserverTakeover(), Is.True);

            handoff.ResetHandoff();

            Assert.That(handoff.HasObserverClone, Is.False);
            Assert.That(visual.activeSelf, Is.True);
        }
    }
}
