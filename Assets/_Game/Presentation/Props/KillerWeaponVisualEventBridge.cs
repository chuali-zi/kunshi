using System;
using ConGame.Domain.Choreography;
using ConGame.Domain.Interaction;
using ConGame.Runtime.Counterplay;
using ConGame.Runtime.Interaction;
using ConGame.Presentation.Choreography;
using ConGame.Presentation.Animation;
using ConGame.Presentation.Audio;
using ConGame.Presentation.Vfx;
using UnityEngine;

namespace ConGame.Presentation.Props
{
    /// <summary>
    /// 消费反制语义事件并驱动武器视觉所有权：
    /// D08 夺枪全胜掉主枪（手电仍挂枪轨）、D21 摔倒后主枪与手电分离、D22 主枪已在地上、
    /// 玩家拾取掉落物。只订阅事件并转发给 <see cref="KillerWeaponVisualPresenter"/>，
    /// 不修改 Runtime 玩法对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillerWeaponVisualEventBridge : MonoBehaviour
    {
        [SerializeField] private KillerWeaponVisualPresenter presenter;
        [SerializeField] private CounterplayPerformanceRuntimeAdapter adapter;
        [SerializeField] private PairedPerformancePlayer pairedPlayer;
        [SerializeField] private StatefulWorldItem pistolDropItem;
        [SerializeField] private StatefulWorldItem flashlightDropItem;
        [SerializeField] private StolenLootDropController stolenLootDropController;

        private bool _subscribed;
        private SemanticAudioDirector _audio;
        private SemanticVfxDirector _vfx;
        private CharacterAnimatorDriver _animatorDriver;

        public void ConfigureShotFeedback(SemanticAudioDirector audio, SemanticVfxDirector vfx,
            CharacterAnimatorDriver animatorDriver)
        {
            _audio = audio;
            _vfx = vfx;
            _animatorDriver = animatorDriver;
        }

        public void Configure(
            KillerWeaponVisualPresenter visualPresenter,
            CounterplayPerformanceRuntimeAdapter performanceAdapter,
            StatefulWorldItem pistolDrop,
            StatefulWorldItem flashlightDrop,
            PairedPerformancePlayer pairedPerformance = null)
        {
            Unsubscribe();
            presenter = visualPresenter;
            adapter = performanceAdapter;
            pairedPlayer = pairedPerformance;
            pistolDropItem = pistolDrop;
            flashlightDropItem = flashlightDrop;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        public void ConfigurePairedPlayer(PairedPerformancePlayer pairedPerformance)
        {
            Unsubscribe();
            pairedPlayer = pairedPerformance;
            if (isActiveAndEnabled) Subscribe();
        }

        public void ConfigureD22Finale(
            StolenLootDropController lootController,
            KillerWeaponVisualPresenter visualPresenter)
        {
            bool wasSubscribed = _subscribed;
            if (wasSubscribed)
            {
                Unsubscribe();
            }

            stolenLootDropController = lootController;
            presenter = visualPresenter;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void ConfigureD22Loot(StolenLootDropController lootController)
        {
            ConfigureD22Finale(lootController, presenter);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnSemanticEvent(PairedPerformanceSemanticEvent semanticEvent)
        {
            if (presenter == null)
            {
                return;
            }

            switch (semanticEvent.EventId)
            {
                case "D08.LootWindow.Open":
                case "D21.Pistol.Dropped":
                    presenter.NotifyMainPistolDropped();
                    break;
                case "D21.Flashlight.Dropped":
                    presenter.NotifyFlashlightDetached();
                    break;
                case "D21.BackupPistol.Draw":
                    presenter.NotifyBackupPistolScriptedVisible(true);
                    break;
                case "D21.BackupPistol.Fire":
                    _animatorDriver?.NotifyScriptedShotPresented();
                    _audio?.NotifyScriptedGunshot(true);
                    _vfx?.NotifyShotFired(presenter.BackupPistolTransform);
                    break;
            }
        }

        private void OnPistolDropStateChanged(ItemPhysicalState state)
        {
            if (state == ItemPhysicalState.Held && presenter != null)
            {
                presenter.NotifyMainPistolDropped();
            }
        }

        private void OnFlashlightDropStateChanged(ItemPhysicalState state)
        {
            if (state == ItemPhysicalState.Held && presenter != null)
            {
                presenter.NotifyFlashlightDetached();
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (pairedPlayer != null)
            {
                pairedPlayer.Events.SemanticEventEmitted += OnSemanticEvent;
            }

            if (pistolDropItem != null)
            {
                pistolDropItem.StateChanged += OnPistolDropStateChanged;
            }

            if (flashlightDropItem != null)
            {
                flashlightDropItem.StateChanged += OnFlashlightDropStateChanged;
            }

            if (stolenLootDropController != null)
            {
                stolenLootDropController.D22BackupVisualRequested += OnD22BackupVisualRequested;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (pairedPlayer != null)
            {
                pairedPlayer.Events.SemanticEventEmitted -= OnSemanticEvent;
            }

            if (pistolDropItem != null)
            {
                pistolDropItem.StateChanged -= OnPistolDropStateChanged;
            }

            if (flashlightDropItem != null)
            {
                flashlightDropItem.StateChanged -= OnFlashlightDropStateChanged;
            }

            if (stolenLootDropController != null)
            {
                stolenLootDropController.D22BackupVisualRequested -= OnD22BackupVisualRequested;
            }

            _subscribed = false;
        }

        private void OnD22BackupVisualRequested(bool visible, int loopEpoch)
        {
            presenter?.NotifyBackupPistolScriptedVisible(visible);
        }
    }
}
