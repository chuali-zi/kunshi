using ConGame.Domain.Facts;
using ConGame.Domain.Flow;
using ConGame.Domain.Interaction;
using ConGame.Presentation.Vfx;
using ConGame.Runtime.AI;
using ConGame.Runtime.Ending;
using ConGame.Runtime.Events;
using ConGame.Runtime.Facts;
using ConGame.Runtime.Flow;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.CRT
{
    /// <summary>Presentation only. Never requests a phase, death, input lock or saved fact.</summary>
    [DisallowMultipleComponent]
    public sealed class MvSignaturePresenter : MonoBehaviour, ILoopResettable, IHitReceiver
    {
        [SerializeField] private MvSignatureSettings settings;
        [SerializeField] private GameFlowController flow;
        [SerializeField] private LoopDirector loop;
        [SerializeField] private GameplayFactService facts;
        [SerializeField] private GameplayEventHub events;
        [SerializeField] private KillerDirector killer;
        [SerializeField] private TelevisionController television;
        [SerializeField] private SemanticVfxDirector vfx;
        [SerializeField] private CrtFullscreenPass fullscreen;
        [SerializeField] private Light muzzleLight;

        private int _epoch;
        private bool _death, _shotPresented, _realisationShown;
        private float _deathElapsed, _cutInRemaining, _flashRemaining;
        private float _screenCrack;

        public int ResetOrder => 95;
        public float DarkAmount { get; private set; }
        public float TitleAmount { get; private set; }
        public float CutInAmount { get; private set; }
        public float ScreenCrackAmount => _screenCrack;
        public Light MuzzleLight => muzzleLight;
        public MvSignatureSettings Settings => settings;

        public void Configure(MvSignatureSettings config, GameFlowController gameFlow, LoopDirector loopDirector,
            GameplayFactService factService, GameplayEventHub eventHub, KillerDirector killerDirector,
            TelevisionController tv, SemanticVfxDirector semanticVfx, CrtFullscreenPass pass)
        {
            Unbind();
            settings = config;
            flow = gameFlow;
            loop = loopDirector;
            facts = factService;
            events = eventHub;
            killer = killerDirector;
            television = tv;
            vfx = semanticVfx;
            fullscreen = pass;
            _epoch = loop == null ? 0 : loop.LoopEpoch;
            EnsureLight();
            fullscreen?.ConfigureSignatureArt(settings?.Title, settings?.Realisation, settings?.Glass);
            if (isActiveAndEnabled) Bind();
            Advance(0f);
        }

        private void OnEnable() => Bind();
        private void OnDisable()
        {
            Unbind();
            Clear();
        }

        private void Bind()
        {
            Unbind();
            if (flow != null) flow.PhaseChanged += OnPhaseChanged;
            if (killer != null) killer.ShotFired += OnKillerShot;
            if (television != null) television.DamageChanged += OnTelevisionDamage;
            if (vfx != null) vfx.ShotPresented += OnShotPresented;
            if (events != null) events.ImpactResolved += OnImpact;
        }

        private void Unbind()
        {
            if (flow != null) flow.PhaseChanged -= OnPhaseChanged;
            if (killer != null) killer.ShotFired -= OnKillerShot;
            if (television != null) television.DamageChanged -= OnTelevisionDamage;
            if (vfx != null) vfx.ShotPresented -= OnShotPresented;
            if (events != null) events.ImpactResolved -= OnImpact;
        }

        private void Update() => Advance(Time.deltaTime);

        public void Advance(float gameSeconds)
        {
            float dt = Mathf.Max(0f, gameSeconds);
            if (_death) _deathElapsed += dt;
            _cutInRemaining = Mathf.Max(0f, _cutInRemaining - dt);
            _flashRemaining = Mathf.Max(0f, _flashRemaining - dt);
            TitleAmount = _death ? Mathf.Clamp01((_deathElapsed - (settings == null ? 0.22f : settings.DeathTitleDelay)) /
                Mathf.Max(0.01f, settings == null ? 0.1f : settings.TitleFadeSeconds)) : 0f;
            CutInAmount = Mathf.Clamp01(_cutInRemaining / 0.12f);
            RoomLightState light = facts == null ? RoomLightState.Lit : facts.CaptureSnapshot().RoomLight;
            DarkAmount = light == RoomLightState.PersistentBlack || light == RoomLightState.RecoverableDark
                ? (settings == null ? 1f : settings.DarkGrade) : 0f;
            if (muzzleLight != null)
            {
                muzzleLight.enabled = _flashRemaining > 0f;
                float lifetime = Mathf.Max(0.01f, settings == null ? 0.085f : settings.MuzzleLightSeconds);
                muzzleLight.intensity = (settings == null ? 9f : settings.MuzzleLightIntensity) *
                    Mathf.Clamp01(_flashRemaining / lifetime);
            }
            fullscreen?.SetSignatureState(DarkAmount, TitleAmount, _screenCrack, CutInAmount);
        }

        private bool CurrentEpoch(int epoch) => epoch == (loop == null ? _epoch : loop.LoopEpoch);

        private void OnKillerShot(string outcome, int epoch)
        {
            if (!CurrentEpoch(epoch)) return;
            // D12 is the authored camera-blind shot; E2 places the viewer in the line of fire.
            if (outcome == "D12" || (flow != null && flow.CurrentPhase == GamePhase.EndingE2))
                NotifyCameraBullet(epoch);
        }

        public void NotifyCameraBullet(int epoch)
        {
            if (!CurrentEpoch(epoch)) return;
            _screenCrack = Mathf.Min(1f, _screenCrack + 0.7f);
            fullscreen?.SetSignatureState(DarkAmount, TitleAmount, _screenCrack, CutInAmount);
        }

        public HitResult ReceiveHit(in HitInfo hit)
        {
            if (hit.Kind != ImpactKind.Bullet) return HitResult.Ignored;
            // Explicit camera hit receiver: ordinary room impacts cannot crack the viewer's glass.
            if (events != null) events.EmitImpact(hit, HitResult.Accepted, this);
            else NotifyCameraBullet(loop == null ? _epoch : loop.LoopEpoch);
            return HitResult.Accepted;
        }

        private void OnImpact(ImpactEvent impact)
        {
            if (impact.Receiver == this && impact.Hit.Kind == ImpactKind.Bullet)
                NotifyCameraBullet(loop == null ? _epoch : loop.LoopEpoch);
        }

        private void OnShotPresented(Vector3 origin)
        {
            _shotPresented = true;
            EnsureLight();
            muzzleLight.transform.position = origin;
            _flashRemaining = Mathf.Max(0.01f, settings == null ? 0.085f : settings.MuzzleLightSeconds);
            Advance(0f);
        }

        private void OnTelevisionDamage(TelevisionDamageState state)
        {
            if (state != TelevisionDamageState.Cracked || _realisationShown || _death ||
                (flow != null && flow.CurrentPhase != GamePhase.Awakening &&
                 flow.CurrentPhase != GamePhase.FreeRoam && flow.CurrentPhase != GamePhase.Breach)) return;
            _realisationShown = true;
            _cutInRemaining = settings == null ? 0.65f : settings.RealisationSeconds;
            Advance(0f);
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase current, string reason)
        {
            if (current == GamePhase.KillImpact)
            {
                _death = true;
                _deathElapsed = 0f;
                _cutInRemaining = 0f;
                // Legacy paired outcomes have no live ShotFired event. Present their terminal shot once.
                if (!_shotPresented) vfx?.NotifyShotFired();
                if (loop != null && loop.LastOutcomeId == "D12" && _screenCrack == 0f)
                    NotifyCameraBullet(loop.LoopEpoch);
            }
            else if (current == GamePhase.LoopReset || current == GamePhase.Awakening ||
                     current == GamePhase.EndingE1 || current == GamePhase.EndingE3 || current == GamePhase.Finished)
                Clear();
            Advance(0f);
        }

        private void EnsureLight()
        {
            if (muzzleLight == null)
            {
                var host = new GameObject("ShotRoomLight");
                host.transform.SetParent(transform, false);
                muzzleLight = host.AddComponent<Light>();
            }
            muzzleLight.type = LightType.Point;
            muzzleLight.color = new Color(1f, 0.86f, 0.64f);
            muzzleLight.range = settings == null ? 8f : settings.MuzzleLightRange;
            muzzleLight.shadows = LightShadows.Soft;
            muzzleLight.enabled = false;
        }

        private void Clear()
        {
            _death = _shotPresented = false;
            _deathElapsed = _cutInRemaining = _flashRemaining = _screenCrack = 0f;
            TitleAmount = CutInAmount = 0f;
            if (muzzleLight != null) muzzleLight.enabled = false;
            fullscreen?.SetSignatureState(0f, 0f, 0f, 0f);
        }

        public void CaptureInitialState() { _realisationShown = false; Clear(); }
        public void Quiesce(in LoopResetContext context) { _epoch = context.NextEpoch; Clear(); }
        public void Restore(in LoopResetContext context) { _realisationShown = false; Clear(); }
        public void Resume(in LoopResetContext context) => Advance(0f);
    }
}
