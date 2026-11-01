using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.Vfx
{
    /// <summary>
    /// 通用临时 VFX 池(碎片、粒子、枪口火光等)。实例只借出/回收,永不销毁;
    /// 粒子要求 StopAction=None,由池统一 Stop/Clear 与禁用。
    /// 池整体实现 <see cref="ILoopResettable"/>,循环重置全量回收。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxPool : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private int prewarmCount = 4;
        [SerializeField, Min(1)] private int maxInstances = 16;
        [SerializeField] private Transform instanceParent;

        private VfxPoolCore<PooledInstance> _core;

        public int ResetOrder => 30;
        public int TotalCreated => _core == null ? 0 : _core.TotalCreated;
        public int ActiveCount => _core == null ? 0 : _core.ActiveCount;
        public int IdleCount => _core == null ? 0 : _core.IdleCount;

        public void Configure(GameObject poolPrefab, int prewarm, int max, Transform parent = null)
        {
            prefab = poolPrefab;
            prewarmCount = Mathf.Max(0, prewarm);
            maxInstances = Mathf.Max(1, max);
            instanceParent = parent;
            _core = null;
        }

        public GameObject Acquire(Vector3 position, Quaternion rotation)
        {
            EnsureCore();
            PooledInstance instance = _core.Acquire();
            Transform instanceTransform = instance.Root.transform;
            instanceTransform.SetPositionAndRotation(position, rotation);
            return instance.Root;
        }

        public void ReleaseAll()
        {
            _core?.ReleaseAll();
        }

        public void CaptureInitialState()
        {
            EnsureCore();
            _core.Prewarm(prewarmCount);
        }

        public void Quiesce(in LoopResetContext context)
        {
            ReleaseAll();
        }

        public void Restore(in LoopResetContext context)
        {
            ReleaseAll();
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void EnsureCore()
        {
            if (_core != null)
            {
                return;
            }

            _core = new VfxPoolCore<PooledInstance>(CreateInstance, OnAcquire, OnRelease, maxInstances);
        }

        private PooledInstance CreateInstance()
        {
            if (prefab == null)
            {
                Debug.LogError($"[VfxPool] {name} 缺少 prefab,无法扩容。");
                return null;
            }

            Transform parent = instanceParent == null ? transform : instanceParent;
            GameObject instance = Instantiate(prefab, parent);
            instance.name = $"{prefab.name}_Pooled";
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem.MainModule main = particles[i].main;
                if (main.stopAction != ParticleSystemStopAction.None)
                {
                    main.stopAction = ParticleSystemStopAction.None;
                    Debug.LogWarning($"[VfxPool] {instance.name} 粒子 StopAction 已由池强制为 None。");
                }
            }

            instance.SetActive(false);
            return new PooledInstance(instance, particles);
        }

        private static void OnAcquire(PooledInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.Root.SetActive(true);
            for (int i = 0; i < instance.Particles.Length; i++)
            {
                instance.Particles[i].Clear(true);
                instance.Particles[i].Play(true);
            }
        }

        private static void OnRelease(PooledInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            for (int i = 0; i < instance.Particles.Length; i++)
            {
                instance.Particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            instance.Root.SetActive(false);
        }

        private sealed class PooledInstance
        {
            public PooledInstance(GameObject root, ParticleSystem[] particles)
            {
                Root = root;
                Particles = particles;
            }

            public GameObject Root { get; }
            public ParticleSystem[] Particles { get; }
        }
    }
}
