using ConGame.Domain.Interaction;
using UnityEngine;

namespace ConGame.Runtime.Interaction
{
    public sealed class UseCapability : InteractionCapability
    {
        [SerializeField] private MonoBehaviour handlerBehaviour;
        private IUseHandler _handler;

        public override InteractionVerb Verb => InteractionVerb.Use;
        public override int Priority => 40;

        public void Configure(MonoBehaviour handler)
        {
            handlerBehaviour = handler;
            _handler = handlerBehaviour as IUseHandler;
        }

        private void Awake()
        {
            _handler = handlerBehaviour as IUseHandler;
        }

        public override bool CanExecute(in InteractionContext context)
        {
            return _handler != null && _handler.CanUse(context);
        }

        public override InteractionResult Execute(in InteractionContext context)
        {
            return _handler == null ? InteractionResult.Rejected : _handler.Use(context);
        }
    }
}
