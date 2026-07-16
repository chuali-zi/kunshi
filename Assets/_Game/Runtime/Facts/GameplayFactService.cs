using ConGame.Domain.Facts;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Facts
{

    [DisallowMultipleComponent]
    public sealed class GameplayFactService : MonoBehaviour, ILoopResettable
    {
        private FactSnapshot _snapshot;
        private FactSnapshot _initialSnapshot;

        public int ResetOrder => 10;

        public FactSnapshot CaptureSnapshot()
        {
            return _snapshot;
        }

        public FactSnapshot CaptureSnapshotForStimulus(KillerStimulus stimulus)
        {
            FactSnapshot copy = _snapshot;
            copy.IncomingProjectile = stimulus.Type == KillerStimulusType.IncomingProjectile;
            copy.TrapPulled = stimulus.Type == KillerStimulusType.TrapPull || copy.TrapPulled;
            copy.IsGlareActive = stimulus.Type == KillerStimulusType.Glare || copy.IsGlareActive;
            return copy;
        }

        public void SetPlayerZone(PlayerZone zone)
        {
            _snapshot.PlayerZone = zone;
            _snapshot.IsAtAwakeningPoint = zone == PlayerZone.Awakening;
        }

        public void SetPlayerPose(PlayerPose pose)
        {
            _snapshot.PlayerPose = pose;
        }

public void SetHeldCategories(ItemCategory categories)
        {
            _snapshot.HeldItemCategories = categories;
        }

        public void SetMoving(bool value)
        {
            _snapshot.IsMoving = value;
        }

        public void SetMeaningfulInput(bool value)
        {
            _snapshot.HasMeaningfulInput = value;
        }

        public void SetSwinging(bool value)
        {
            _snapshot.IsSwinging = value;
        }

        public void SetRoomLight(RoomLightState state)
        {
            _snapshot.RoomLight = state;
        }

        public void SetTelevisionDamage(TelevisionDamageState state)
        {
            _snapshot.TelevisionDamage = state;
        }

        public void SetAmbushing(bool value)
        {
            _snapshot.IsAmbushing = value;
        }

        public float DoorBlockBreachExtraSeconds { get; private set; }

        public void SetDoorBlock(DoorBlockState state, float breachExtraSeconds = 0f)
        {
            _snapshot.DoorBlock = state;
            DoorBlockBreachExtraSeconds =
                state == DoorBlockState.None ? 0f : Mathf.Max(0f, breachExtraSeconds);
        }

        public void SetTrap(TrapState state)
        {
            _snapshot.Trap = state;
        }

        public void SetTrapPulled(bool value)
        {
            _snapshot.TrapPulled = value;
        }

        public void SetStolenPistol(bool value)
        {
            _snapshot.HasStolenPistol = value;
        }

        public void SetGlareActive(bool value)
        {
            _snapshot.IsGlareActive = value;
        }

        public void SetStrikingWall(bool value)
        {
            _snapshot.IsStrikingWall = value;
        }

        public void SetStrikingWindow(bool value)
        {
            _snapshot.IsStrikingWindow = value;
        }

        public void SetAtAwakeningPoint(bool value)
        {
            _snapshot.IsAtAwakeningPoint = value;
        }

        public void CaptureInitialState()
        {
            _snapshot = FactSnapshot.CreateBaseline();
            _initialSnapshot = _snapshot;
            DoorBlockBreachExtraSeconds = 0f;
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            _snapshot = _initialSnapshot;
            DoorBlockBreachExtraSeconds = 0f;
        }

        public void Resume(in LoopResetContext context)
        {
        }
    }
}
