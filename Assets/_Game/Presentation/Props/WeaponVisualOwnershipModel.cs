using System;

namespace ConGame.Presentation.Props
{
    /// <summary>
    /// 黑衣人武器视觉副本的槽位：右手主枪、枪轨手电、枪套、备用枪。
    /// </summary>
    public enum WeaponVisualSlot
    {
        MainPistol = 0,
        RailFlashlight = 1,
        Holster = 2,
        BackupPistol = 3
    }

    /// <summary>
    /// 武器视觉的权威持有者。任意时刻每个槽位只有一个权威持有者；
    /// Transitioning 表示掉落/回收过渡中，此时两侧都不是权威，角色视觉必须隐藏以避免双份。
    /// </summary>
    public enum WeaponVisualOwner
    {
        CharacterVisual = 0,
        GameplayObject = 1,
        Transitioning = 2
    }

    /// <summary>
    /// 武器视觉唯一所有权状态机（纯 C#，可单测，不引用 UnityEngine）。
    /// 两阶段转移：TryBeginTransfer → CompleteTransfer/CancelTransfer；
    /// 也提供 ForceSetOwner/DropToGameplay 用于权威掉落/拾取的同帧切换。
    /// </summary>
    public sealed class WeaponVisualOwnershipModel
    {
        private const int SlotCount = 4;

        private readonly WeaponVisualOwner[] _owners = new WeaponVisualOwner[SlotCount];
        private readonly WeaponVisualOwner[] _initialOwners = new WeaponVisualOwner[SlotCount];
        private readonly WeaponVisualOwner[] _pendingOwners = new WeaponVisualOwner[SlotCount];
        private readonly WeaponVisualOwner[] _resumeOwners = new WeaponVisualOwner[SlotCount];

        public event Action<WeaponVisualSlot, WeaponVisualOwner> OwnershipChanged;

        public WeaponVisualOwnershipModel()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _owners[i] = WeaponVisualOwner.CharacterVisual;
                _initialOwners[i] = WeaponVisualOwner.CharacterVisual;
                _pendingOwners[i] = WeaponVisualOwner.CharacterVisual;
                _resumeOwners[i] = WeaponVisualOwner.CharacterVisual;
            }
        }

        public WeaponVisualOwner GetOwner(WeaponVisualSlot slot)
        {
            return _owners[Index(slot)];
        }

        public bool IsTransitioning(WeaponVisualSlot slot)
        {
            return GetOwner(slot) == WeaponVisualOwner.Transitioning;
        }

        /// <summary>
        /// 开始进入过渡（如掉落动画接触帧前的预备段）。进入后该槽位两侧都非权威。
        /// </summary>
        public bool TryBeginTransfer(WeaponVisualSlot slot, WeaponVisualOwner target)
        {
            int index = Index(slot);
            if (target == WeaponVisualOwner.Transitioning ||
                _owners[index] == WeaponVisualOwner.Transitioning ||
                _owners[index] == target)
            {
                return false;
            }

            _resumeOwners[index] = _owners[index];
            _pendingOwners[index] = target;
            SetOwner(slot, WeaponVisualOwner.Transitioning);
            return true;
        }

        /// <summary>
        /// 完成过渡，权威转移到 TryBeginTransfer 声明的目标持有者。
        /// </summary>
        public bool CompleteTransfer(WeaponVisualSlot slot)
        {
            int index = Index(slot);
            if (_owners[index] != WeaponVisualOwner.Transitioning)
            {
                return false;
            }

            SetOwner(slot, _pendingOwners[index]);
            return true;
        }

        /// <summary>
        /// 取消过渡，权威回到过渡前的持有者（取消、死亡等原子恢复）。
        /// </summary>
        public bool CancelTransfer(WeaponVisualSlot slot)
        {
            int index = Index(slot);
            if (_owners[index] != WeaponVisualOwner.Transitioning)
            {
                return false;
            }

            SetOwner(slot, _resumeOwners[index]);
            return true;
        }

        /// <summary>
        /// 同帧权威切换（权威掉落/拾取语义事件到达时）。重复设置为同一持有者是幂等空操作。
        /// </summary>
        public void ForceSetOwner(WeaponVisualSlot slot, WeaponVisualOwner owner)
        {
            if (owner == WeaponVisualOwner.Transitioning)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), owner, "Transitioning 只能经 TryBeginTransfer 进入。");
            }

            int index = Index(slot);
            _pendingOwners[index] = owner;
            _resumeOwners[index] = owner;
            SetOwner(slot, owner);
        }

        public void DropToGameplay(WeaponVisualSlot slot)
        {
            ForceSetOwner(slot, WeaponVisualOwner.GameplayObject);
        }

        public void ReturnToCharacter(WeaponVisualSlot slot)
        {
            ForceSetOwner(slot, WeaponVisualOwner.CharacterVisual);
        }

        public void CaptureInitialState()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _initialOwners[i] = _owners[i] == WeaponVisualOwner.Transitioning
                    ? _resumeOwners[i]
                    : _owners[i];
            }
        }

        public void ResetToInitial()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _pendingOwners[i] = _initialOwners[i];
                _resumeOwners[i] = _initialOwners[i];
                SetOwner((WeaponVisualSlot)i, _initialOwners[i]);
            }
        }

        private void SetOwner(WeaponVisualSlot slot, WeaponVisualOwner owner)
        {
            int index = Index(slot);
            if (_owners[index] == owner)
            {
                return;
            }

            _owners[index] = owner;
            OwnershipChanged?.Invoke(slot, owner);
        }

        private static int Index(WeaponVisualSlot slot)
        {
            int index = (int)slot;
            if (index < 0 || index >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "未知的武器视觉槽位。");
            }

            return index;
        }
    }
}
