using NUnit.Framework;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConGame.Tests.PlayMode
{
    /// <summary>
    /// Batch Test Runner has no focused Game View. Route synthetic devices to the
    /// player while these integration tests run; keep the shipped focus policy intact.
    /// This does not replace devices, actions, callbacks, or the player update loop.
    /// </summary>
    public abstract class ProductionInputTestFixture
    {
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;

        [SetUp]
        public void RouteInjectedInputToGame()
        {
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void RestoreInputFocusPolicy()
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
        }
    }

    internal static class ProductionPickupTestDriver
    {
        // Tossed props roll and can land beside other pickups. Stage the player
        // at a clear floor position that actually selects the requested prop;
        // never move the prop or bypass its scanner/capability/handoff.
        public static void Approach(PlayerMotor motor, InteractableEntity target)
        {
            var scanner = motor.GetComponent<InteractionScanner>();
            var controller = motor.GetComponent<CharacterController>();
            foreach (float distance in new[] { .5f, .65f, .4f })
            {
                for (int i = 0; i < 24; i++)
                {
                    Vector3 outward = Quaternion.Euler(0, i * 15f, 0) * Vector3.forward;
                    Vector3 position = target.transform.position + outward * distance;
                    position.y = .08f;
                    bool blocked = false;
                    foreach (var collider in Physics.OverlapCapsule(
                        position + Vector3.up * controller.radius,
                        position + Vector3.up * (controller.height - controller.radius),
                        controller.radius, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (collider.transform.IsChildOf(motor.transform) ||
                            collider.bounds.max.y < position.y + .1f) continue;
                        blocked = true;
                        break;
                    }
                    if (blocked) continue;
                    motor.Teleport(position, Quaternion.LookRotation(-outward, Vector3.up));
                    Physics.SyncTransforms();
                    if (scanner.RefreshForVerb(InteractionVerb.PickUp) == target) return;
                }
            }
            Assert.Fail($"No clear, pickup-scannable floor approach to {target.name} at {target.transform.position}.");
        }
    }
}
