using System;
using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Runtime.Ending
{

    [DisallowMultipleComponent]
    public sealed class TelevisionController : MonoBehaviour, IHitReceiver, ILoopResettable
    {
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private GameFlowController flow;

        public event Action<TelevisionDamageState> DamageChanged;
        public event Action LightImpactPulse;

        public int ResetOrder => 60;

        public TelevisionDamageState DamageState { get; private set; }

        public void Configure(GameplayFactService factService, GameplayEventHub eventHub, GameFlowController flowController)
        {
            facts = factService;
            events = eventHub;
            flow = flowController;
        }

        public HitResult ReceiveHit(in HitInfo hit)
        {
            if (DamageState == TelevisionDamageState.Destroyed)
            {
                return HitResult.Ignored;
            }

            if (hit.Kind == ImpactKind.Light || hit.Kind == ImpactKind.Projectile)
            {
                LightImpactPulse?.Invoke();
                return HitResult.Accepted;
            }

            if (hit.Kind != ImpactKind.Heavy)
            {
                return HitResult.Ignored;
            }

            if (DamageState == TelevisionDamageState.Intact)
            {
                SetDamage(TelevisionDamageState.Cracked);
                events?.EmitStimulus(KillerStimulusType.TelevisionHeavyHit, "crt_tv");
                return HitResult.Accepted;
            }

            SetDamage(TelevisionDamageState.Destroyed);
            flow?.Request(GamePhase.EndingE1, TransitionPriority.EndingCommitted, "Television received its second heavy impact.");
            return HitResult.EndingTriggered;
        }

        public void CaptureInitialState()
        {
            SetDamage(TelevisionDamageState.Intact);
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            SetDamage(TelevisionDamageState.Intact);
        }

        public void Resume(in LoopResetContext context)
        {
        }

        public void SetEndingDestroyed()
        {
            SetDamage(TelevisionDamageState.Destroyed);
        }

        public void PulseLightImpact()
        {
            LightImpactPulse?.Invoke();
        }

        private void SetDamage(TelevisionDamageState state)
        {
            DamageState = state;
            facts?.SetTelevisionDamage(state);
            DamageChanged?.Invoke(state);
        }
    }
}
