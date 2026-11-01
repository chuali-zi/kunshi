using ConGame.Runtime.AI;
using ConGame.Runtime.Config;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Input;
using ConGame.Runtime.Loop;
using ConGame.Runtime.Player;
using UnityEngine;

namespace ConGame.Bootstrap
{

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class RoomContext : MonoBehaviour
    {
        [SerializeField] private GameConfigAsset config;
        [SerializeField] private GameFlowController flow;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private LoopResetRegistry resetRegistry;
        [SerializeField] private LoopDirector loopDirector;
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private KillerDirector killer;

        private bool _initialized;

        public void Configure(
            GameConfigAsset gameConfig,
            GameFlowController flowController,
            GameplayFactService factService,
            GameplayEventHub eventHub,
            LoopResetRegistry registry,
            LoopDirector loop,
            PlayerInputRouter inputRouter,
            PlayerMotor motor,
            KillerDirector killerDirector)
        {
            config = gameConfig;
            flow = flowController;
            facts = factService;
            events = eventHub;
            resetRegistry = registry;
            loopDirector = loop;
            input = inputRouter;
            playerMotor = motor;
            killer = killerDirector;
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (config == null || flow == null || facts == null || events == null ||
                resetRegistry == null || loopDirector == null || input == null ||
                playerMotor == null || killer == null)
            {
                Debug.LogError("RoomContext is missing required references; run ConGame > Scaffold > Build M1 Greybox.", this);
                enabled = false;
                return;
            }

            RoomDesignGapInstaller.Apply(this);
            resetRegistry.CaptureInitialState();
            loopDirector.Initialize();
            _initialized = true;
        }
    }
}
