using System.Collections.Generic;
using ConGame.Domain.Interaction;
using ConGame.Presentation.Vfx;
using ConGame.Runtime.Events;
using ConGame.Runtime.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class SemanticVfxDirectorTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private GameObject _hubObject;
        private GameObject _directorObject;
        private GameplayEventHub _hub;
        private SemanticVfxDirector _director;
        private readonly Dictionary<VfxCueId, VfxPool> _pools = new Dictionary<VfxCueId, VfxPool>();

        [SetUp]
        public void SetUp()
        {
            _hubObject = Track(new GameObject("Hub"));
            _hub = _hubObject.AddComponent<GameplayEventHub>();
            _directorObject = Track(new GameObject("VfxDirector"));
            _directorObject.SetActive(false);
            _director = _directorObject.AddComponent<SemanticVfxDirector>();
            _directorObject.SetActive(true);
            // EditMode does not invoke this runtime component's OnEnable. Configure
            // the active director explicitly, as the room's composition root does.
            _director.Configure(_hub, null, null, null, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _cleanup.Count; i++)
            {
                if (_cleanup[i] != null)
                {
                    Object.DestroyImmediate(_cleanup[i]);
                }
            }

            _cleanup.Clear();
            _pools.Clear();
        }

        [Test]
        public void DefaultSemanticsMapBulletToSparkAndHeavyToWallDust()
        {
            Assert.That(VfxImpactSemantics.TryGetDefaultCue(ImpactKind.Bullet, HitResult.Accepted,
                        out VfxCueId bullet), Is.True);
            Assert.That(bullet, Is.EqualTo(VfxCueId.ImpactSpark));
            Assert.That(VfxImpactSemantics.TryGetDefaultCue(ImpactKind.Heavy, HitResult.Accepted,
                        out VfxCueId heavy), Is.True);
            Assert.That(heavy, Is.EqualTo(VfxCueId.WallDust));
            Assert.That(VfxImpactSemantics.TryGetDefaultCue(ImpactKind.Light, HitResult.Accepted,
                        out _), Is.False, "轻击未破不触发默认 VFX");
        }

        [Test]
        public void ImpactResolvedAcquiresDefaultCueFromPool()
        {
            VfxPool sparks = CreatePool(VfxCueId.ImpactSpark);
            HitInfo hit = new HitInfo(ImpactKind.Bullet, "pistol_main", Vector3.zero, Vector3.forward, 10f);

            _hub.EmitImpact(hit, HitResult.Accepted, null);

            Assert.That(sparks.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void ReceiverMappingOverridesDefaultCue()
        {
            VfxPool sparks = CreatePool(VfxCueId.ImpactSpark);
            VfxPool blood = CreatePool(VfxCueId.BloodImpact);
            GameObject playerBody = Track(new GameObject("PlayerBody"));
            _director.MapReceiver(playerBody.transform, VfxCueId.BloodImpact);
            HitInfo hit = new HitInfo(ImpactKind.Bullet, "pistol_main", Vector3.zero, Vector3.forward, 10f);

            _hub.EmitImpact(hit, HitResult.Accepted, playerBody.transform);

            Assert.That(blood.ActiveCount, Is.EqualTo(1));
            Assert.That(sparks.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void BreakableItemBrokenStatePlaysRegisteredCueOnce()
        {
            VfxPool wood = CreatePool(VfxCueId.WoodDebris);
            GameObject chair = Track(new GameObject("wood_chair"));
            StatefulWorldItem item = chair.AddComponent<StatefulWorldItem>();
            _director.RegisterBreakableItem(item, VfxCueId.WoodDebris);

            item.MarkBroken();
            item.MarkBroken();

            Assert.That(wood.ActiveCount, Is.EqualTo(1), "重复 MarkBroken 不得重复触发(StateChanged 幂等)");
        }

        [Test]
        public void ShotFiredPlaysMuzzleFlashAtAnchor()
        {
            VfxPool muzzle = CreatePool(VfxCueId.MuzzleFlash);
            GameObject anchorObject = Track(new GameObject("MuzzleAnchor"));
            anchorObject.transform.position = new Vector3(0.5f, 1.4f, 0.2f);
            _director.Configure(_hub, null, null, null, anchorObject.transform, null);

            _director.NotifyShotFired();

            Assert.That(muzzle.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void ReconfigureMovesSubscriptionsWithoutDuplicatingBreakableHandlers()
        {
            VfxPool sparks = CreatePool(VfxCueId.ImpactSpark);
            VfxPool wood = CreatePool(VfxCueId.WoodDebris);
            var chair = Track(new GameObject("chair")).AddComponent<StatefulWorldItem>();
            _director.RegisterBreakableItem(chair, VfxCueId.WoodDebris);
            var replacementHub = Track(new GameObject("ReplacementHub")).AddComponent<GameplayEventHub>();
            _director.Configure(replacementHub, null, null, null, null, null);
            _director.Configure(replacementHub, null, null, null, null, null);
            var hit = new HitInfo(ImpactKind.Bullet, "pistol_main", Vector3.zero, Vector3.forward, 10f);

            _hub.EmitImpact(hit, HitResult.Accepted, null);
            Assert.That(sparks.ActiveCount, Is.Zero, "The old hub must be unsubscribed.");
            replacementHub.EmitImpact(hit, HitResult.Accepted, null);
            chair.MarkBroken();
            Assert.That(sparks.ActiveCount, Is.EqualTo(1));
            Assert.That(wood.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void UnregisteredCueIsRejectedWithoutThrowing()
        {
            Assert.That(_director.PlayCueAt(VfxCueId.TelevisionBurst, Vector3.zero, Vector3.up), Is.False);
        }

        private VfxPool CreatePool(VfxCueId cue)
        {
            GameObject prefab = Track(new GameObject(cue + "Prefab"));
            prefab.AddComponent<ParticleSystem>();
            prefab.SetActive(false);
            GameObject poolObject = Track(new GameObject(cue + "Pool"));
            VfxPool pool = poolObject.AddComponent<VfxPool>();
            pool.Configure(prefab, 1, 4);
            pool.CaptureInitialState();
            _director.RegisterPool(cue, pool);
            _pools[cue] = pool;
            return pool;
        }

        private GameObject Track(GameObject gameObject)
        {
            _cleanup.Add(gameObject);
            return gameObject;
        }
    }
}
