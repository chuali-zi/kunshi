using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.Props
{
    /// <summary>
    /// 黑衣人武器视觉副本（Socket 下主枪、枪轨手电、枪套、备用枪）的显隐表现层。
    /// 只消费 <see cref="WeaponVisualOwnershipModel"/> 的所有权状态，保证手上副本与
    /// 地上玩法物件不会同时可见。掉落/拾取等权威语义由 Notify* 契约入口接收。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillerWeaponVisualPresenter : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private GameObject mainPistolVisual;
        [SerializeField] private GameObject railFlashlightVisual;
        [SerializeField] private GameObject holsterVisual;
        [SerializeField] private GameObject backupPistolVisual;
        [SerializeField] private Transform backupPistolHandSocket;
        [SerializeField] private Vector3 backupPistolGripPoint = new Vector3(0f, -0.04f, 0f);
        [SerializeField] private float backupPistolAimHeight = 1.35f;

        private readonly WeaponVisualOwnershipModel _model = new WeaponVisualOwnershipModel();
        private bool _backupPistolScriptedVisible;
        private Transform _backupPistolInitialParent;
        private Vector3 _backupPistolInitialLocalPosition;
        private Quaternion _backupPistolInitialLocalRotation;
        private Vector3 _backupPistolInitialLocalScale;
        private Vector3 _backupPistolInitialWorldScale;
        private Transform _backupPistolAimTarget;

        public int ResetOrder => 96;
        public WeaponVisualOwnershipModel Model => _model;
        public Transform BackupPistolTransform => backupPistolVisual == null ? null : backupPistolVisual.transform;

        public void Configure(
            GameObject mainPistol,
            GameObject railFlashlight,
            GameObject holster,
            GameObject backupPistol,
            Transform backupHandSocket = null)
        {
            mainPistolVisual = mainPistol;
            railFlashlightVisual = railFlashlight;
            holsterVisual = holster;
            backupPistolVisual = backupPistol;
            backupPistolHandSocket = backupHandSocket;
            CaptureBackupPistolMount();
            ApplyVisuals();
        }

        public void ConfigureAimTarget(Transform aimTarget)
        {
            _backupPistolAimTarget = aimTarget;
            if (_backupPistolScriptedVisible)
            {
                UpdateBackupPistolAimPose();
            }
        }

        /// <summary>
        /// 契约：权威掉枪（D08 夺枪全胜、D21 入场分离、D22）。手电仍挂在枪轨上时随之隐藏。
        /// </summary>
        public void NotifyMainPistolDropped()
        {
            _model.DropToGameplay(WeaponVisualSlot.MainPistol);
        }

        /// <summary>
        /// 契约：D21 手电与主枪分离（或玩家拾取掉落手电）。手电视觉副本转由玩法物件持有。
        /// </summary>
        public void NotifyFlashlightDetached()
        {
            _model.DropToGameplay(WeaponVisualSlot.RailFlashlight);
        }

        /// <summary>
        /// 契约：演出绑定备用枪视觉（D21/D22 风衣掏枪）。默认隐藏，LoopReset 恢复隐藏。
        /// </summary>
        public void NotifyBackupPistolScriptedVisible(bool visible)
        {
            _backupPistolScriptedVisible = visible;
            if (visible)
            {
                AttachBackupPistolToHand();
            }
            else
            {
                RestoreBackupPistolToHolster();
            }

            ApplyVisuals();
        }

        public void CaptureInitialState()
        {
            CaptureBackupPistolMount();
            _model.CaptureInitialState();
            ApplyVisuals();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            _model.ResetToInitial();
            _backupPistolScriptedVisible = false;
            RestoreBackupPistolToHolster();
            ApplyVisuals();
        }

        public void Resume(in LoopResetContext context)
        {
            ApplyVisuals();
        }

        private void Awake()
        {
            _model.OwnershipChanged += OnOwnershipChanged;
        }

        private void OnDestroy()
        {
            _model.OwnershipChanged -= OnOwnershipChanged;
        }

        private void LateUpdate()
        {
            if (_backupPistolScriptedVisible && backupPistolVisual != null &&
                backupPistolHandSocket != null && _backupPistolAimTarget != null)
            {
                UpdateBackupPistolAimPose();
            }
        }

        private void OnOwnershipChanged(WeaponVisualSlot slot, WeaponVisualOwner owner)
        {
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            bool pistolOnCharacter = _model.GetOwner(WeaponVisualSlot.MainPistol) == WeaponVisualOwner.CharacterVisual;
            bool flashlightOnCharacter = pistolOnCharacter &&
                _model.GetOwner(WeaponVisualSlot.RailFlashlight) == WeaponVisualOwner.CharacterVisual;

            SetActive(mainPistolVisual, pistolOnCharacter);
            SetActive(railFlashlightVisual, flashlightOnCharacter);
            SetActive(holsterVisual, _model.GetOwner(WeaponVisualSlot.Holster) == WeaponVisualOwner.CharacterVisual);
            SetActive(backupPistolVisual,
                _backupPistolScriptedVisible &&
                _model.GetOwner(WeaponVisualSlot.BackupPistol) == WeaponVisualOwner.CharacterVisual);
        }

        private void CaptureBackupPistolMount()
        {
            if (backupPistolVisual == null || _backupPistolInitialParent != null)
            {
                return;
            }

            Transform item = backupPistolVisual.transform;
            _backupPistolInitialParent = item.parent;
            _backupPistolInitialLocalPosition = item.localPosition;
            _backupPistolInitialLocalRotation = item.localRotation;
            _backupPistolInitialLocalScale = item.localScale;
            _backupPistolInitialWorldScale = item.lossyScale;
        }

        private void AttachBackupPistolToHand()
        {
            if (backupPistolVisual == null || backupPistolHandSocket == null)
            {
                return;
            }

            Transform item = backupPistolVisual.transform;
            item.SetParent(backupPistolHandSocket, false);
            SetBackupPistolHandLocalScale(item);
            if (_backupPistolAimTarget != null)
            {
                UpdateBackupPistolAimPose();
            }
            else if (mainPistolVisual != null && mainPistolVisual.transform.parent == backupPistolHandSocket)
            {
                item.localPosition = mainPistolVisual.transform.localPosition;
                item.localRotation = mainPistolVisual.transform.localRotation;
            }
            else
            {
                item.localPosition = Vector3.zero;
                item.localRotation = Quaternion.identity;
            }
        }

        private void UpdateBackupPistolAimPose()
        {
            if (backupPistolVisual == null || backupPistolHandSocket == null || _backupPistolAimTarget == null)
            {
                return;
            }

            Transform item = backupPistolVisual.transform;
            if (item.parent != backupPistolHandSocket)
            {
                item.SetParent(backupPistolHandSocket, false);
                SetBackupPistolHandLocalScale(item);
            }

            Vector3 aimPoint = _backupPistolAimTarget.position + Vector3.up * backupPistolAimHeight;
            Transform muzzle = FindMuzzle(item);
            for (int i = 0; i < 3; i++)
            {
                Vector3 aimOrigin = muzzle == null ? backupPistolHandSocket.position : muzzle.position;
                Vector3 aimDirection = aimPoint - aimOrigin;
                if (aimDirection.sqrMagnitude <= 0.0001f)
                {
                    break;
                }

                item.rotation = Quaternion.LookRotation(aimDirection.normalized, backupPistolHandSocket.up);
                item.position = backupPistolHandSocket.position - item.TransformVector(backupPistolGripPoint);
            }

            item.position = backupPistolHandSocket.position - item.TransformVector(backupPistolGripPoint);
        }

        private static Transform FindMuzzle(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (string.Equals(child.name, "SOCKET_Muzzle", System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                Transform nested = FindMuzzle(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void RestoreBackupPistolToHolster()
        {
            if (backupPistolVisual == null || _backupPistolInitialParent == null)
            {
                return;
            }

            Transform item = backupPistolVisual.transform;
            item.SetParent(_backupPistolInitialParent, false);
            item.localPosition = _backupPistolInitialLocalPosition;
            item.localRotation = _backupPistolInitialLocalRotation;
            item.localScale = _backupPistolInitialLocalScale;
        }

        private void SetBackupPistolHandLocalScale(Transform item)
        {
            Vector3 parentScale = backupPistolHandSocket.lossyScale;
            item.localScale = new Vector3(
                DivideScale(_backupPistolInitialWorldScale.x, parentScale.x),
                DivideScale(_backupPistolInitialWorldScale.y, parentScale.y),
                DivideScale(_backupPistolInitialWorldScale.z, parentScale.z));
        }

        private static float DivideScale(float worldScale, float parentScale)
        {
            return Mathf.Abs(parentScale) < 0.0001f ? worldScale : worldScale / parentScale;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
