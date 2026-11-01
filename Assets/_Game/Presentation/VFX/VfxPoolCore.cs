using System;
using System.Collections.Generic;

namespace ConGame.Presentation.Vfx
{
    /// <summary>
    /// 纯 C# 池记账核心,不引用 UnityEngine,EditMode 可直接单测。
    /// 策略:空闲优先 → 未达上限时扩容 → 达到上限后回收最早活跃的实例。
    /// 永不销毁实例;循环重置由 <see cref="ReleaseAll"/> 全量回收。
    /// </summary>
    public sealed class VfxPoolCore<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly Action<T> _onAcquire;
        private readonly Action<T> _onRelease;
        private readonly List<T> _idle = new List<T>();
        private readonly List<T> _active = new List<T>();

        public VfxPoolCore(Func<T> factory, Action<T> onAcquire, Action<T> onRelease, int maxInstances = 16)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _onAcquire = onAcquire;
            _onRelease = onRelease;
            MaxInstances = Math.Max(1, maxInstances);
        }

        public int MaxInstances { get; }
        public int TotalCreated { get; private set; }
        public int IdleCount => _idle.Count;
        public int ActiveCount => _active.Count;

        public void Prewarm(int count)
        {
            int target = Math.Min(count, MaxInstances);
            while (TotalCreated < target)
            {
                _idle.Add(CreateInstance());
            }
        }

        public T Acquire()
        {
            T instance;
            if (_idle.Count > 0)
            {
                int last = _idle.Count - 1;
                instance = _idle[last];
                _idle.RemoveAt(last);
            }
            else if (TotalCreated < MaxInstances)
            {
                instance = CreateInstance();
            }
            else
            {
                // 池耗尽时回收最早活跃的实例,代替销毁重建。
                instance = _active[0];
                _active.RemoveAt(0);
                _onRelease?.Invoke(instance);
            }

            _active.Add(instance);
            _onAcquire?.Invoke(instance);
            return instance;
        }

        public bool Release(T instance)
        {
            if (instance == null || !_active.Remove(instance))
            {
                return false;
            }

            _onRelease?.Invoke(instance);
            _idle.Add(instance);
            return true;
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                _onRelease?.Invoke(_active[i]);
                _idle.Add(_active[i]);
            }

            _active.Clear();
        }

        private T CreateInstance()
        {
            T instance = _factory();
            if (instance == null)
            {
                throw new InvalidOperationException("VfxPoolCore factory returned null.");
            }

            TotalCreated++;
            return instance;
        }
    }
}
