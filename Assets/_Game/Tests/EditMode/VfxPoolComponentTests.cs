using ConGame.Presentation.Vfx;
using ConGame.Runtime.Loop;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.EditMode
{
    public sealed class VfxPoolComponentTests
    {
        private GameObject _poolObject;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("VfxPrefab");
            _prefab.AddComponent<ParticleSystem>();
            _prefab.SetActive(false);
            _poolObject = new GameObject("VfxPool");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_poolObject);
            Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void CapturePrewarmsAndAcquireActivatesPooledInstance()
        {
            VfxPool pool = _poolObject.AddComponent<VfxPool>();
            pool.Configure(_prefab, 2, 4);
            pool.CaptureInitialState();

            Assert.That(pool.TotalCreated, Is.EqualTo(2));
            Assert.That(pool.IdleCount, Is.EqualTo(2));

            GameObject instance = pool.Acquire(new Vector3(1f, 2f, 3f), Quaternion.identity);

            Assert.That(instance.activeSelf, Is.True);
            Assert.That(instance.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void LoopResetRecallsAllInstancesWithoutDestroyingThem()
        {
            VfxPool pool = _poolObject.AddComponent<VfxPool>();
            pool.Configure(_prefab, 2, 4);
            pool.CaptureInitialState();

            GameObject first = pool.Acquire(Vector3.zero, Quaternion.identity);
            pool.Acquire(Vector3.one, Quaternion.identity);
            LoopResetContext context = new LoopResetContext(0, 1);

            pool.Quiesce(context);
            pool.Restore(context);
            pool.Resume(context);

            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.IdleCount, Is.EqualTo(2));
            Assert.That(first, Is.Not.Null, "循环重置不得销毁池内实例");
            Assert.That(first.activeSelf, Is.False);
        }

        [Test]
        public void ExhaustedPoolRecyclesInsteadOfGrowingPastMax()
        {
            VfxPool pool = _poolObject.AddComponent<VfxPool>();
            pool.Configure(_prefab, 1, 1);
            pool.CaptureInitialState();

            pool.Acquire(Vector3.zero, Quaternion.identity);
            pool.Acquire(Vector3.one, Quaternion.identity);

            Assert.That(pool.TotalCreated, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }
    }
}
