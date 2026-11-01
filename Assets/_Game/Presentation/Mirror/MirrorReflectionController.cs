using ConGame.Domain.Interaction;
using ConGame.Runtime.Interaction;
using ConGame.Runtime.Loop;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConGame.Presentation.Mirror
{
    /// <summary>
    /// 镜面运行时系统：独立低分辨率 UnityEngine.Camera + RenderTexture 的平面反射。
    /// 只在镜面对固定机位可见时按需渲染；破碎后停更并切破裂占位材质；
    /// LoopReset 恢复更新与完整材质。RenderTexture 只创建一次、销毁时释放，不每帧重建。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MirrorReflectionController : MonoBehaviour, ILoopResettable
    {
        [SerializeField] private UnityEngine.Camera fixedCamera;
        [SerializeField] private UnityEngine.Camera mirrorCamera;
        [SerializeField] private Renderer mirrorSurface;
        [SerializeField] private Transform mirrorPlane;
        [SerializeField] private Material brokenPlaceholderMaterial;
        [SerializeField] private StatefulWorldItem breakSource;
        [SerializeField, Min(16)] private int textureWidth = 256;
        [SerializeField, Min(16)] private int textureHeight = 256;
        [SerializeField] private string reflectionTextureProperty = "_BaseMap";

        private readonly MirrorUpdateStateMachine _stateMachine = new MirrorUpdateStateMachine();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private RenderTexture _reflectionTexture;
        private MaterialPropertyBlock _propertyBlock;
        private Material _intactMaterial;
        private bool _ownsMirrorCamera;
        private bool _subscribed;
        private Material _reflectionMaterial;
        private readonly RenderPipeline.StandardRequest _renderRequest = new RenderPipeline.StandardRequest();

        public int ResetOrder => 96;
        public MirrorUpdateStateMachine StateMachine => _stateMachine;
        public RenderTexture ReflectionTexture => _reflectionTexture;

        public void Configure(
            UnityEngine.Camera roomFixedCamera,
            UnityEngine.Camera reflectionCamera,
            Renderer surface,
            Transform plane,
            Material brokenMaterial,
            StatefulWorldItem breakAuthority = null)
        {
            Unsubscribe();
            fixedCamera = roomFixedCamera;
            if (reflectionCamera != null && reflectionCamera != mirrorCamera)
            {
                if (_ownsMirrorCamera && mirrorCamera != null) DestroyResource(mirrorCamera.gameObject);
                mirrorCamera = reflectionCamera;
                _ownsMirrorCamera = false;
            }
            mirrorSurface = surface;
            mirrorPlane = plane;
            brokenPlaceholderMaterial = brokenMaterial;
            breakSource = breakAuthority;
            CaptureIntactMaterial();
            EnsureResources();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            ApplyMaterial();
        }

        /// <summary>
        /// 契约：镜子破碎（挥击/破坏权威）。停更并切破裂占位材质；之后 D17 反射由判定侧自行禁用。
        /// </summary>
        public void NotifyBroken()
        {
            _stateMachine.SetBroken(true);
            ApplyMaterial();
        }

        public void CaptureInitialState()
        {
            CaptureIntactMaterial();
        }

        public void Quiesce(in LoopResetContext context)
        {
            _stateMachine.SetVisible(false);
        }

        public void Restore(in LoopResetContext context)
        {
            _stateMachine.ResetMirror();
            EnsureResources();
            ApplyMaterial();
        }

        public void Resume(in LoopResetContext context)
        {
        }

        private void Awake()
        {
            CaptureIntactMaterial();
            EnsureResources();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

        private void LateUpdate()
        {
            if (_stateMachine.IsBroken)
            {
                _stateMachine.SetVisible(false);
                return;
            }

            _stateMachine.SetVisible(IsVisibleToFixedCamera());
            if (_stateMachine.ShouldRender)
            {
                RenderReflection();
            }
        }

        private void OnBreakSourceStateChanged(ItemPhysicalState state)
        {
            if (state == ItemPhysicalState.Broken)
            {
                NotifyBroken();
            }
        }

        private bool IsVisibleToFixedCamera()
        {
            if (fixedCamera == null || mirrorSurface == null || mirrorPlane == null)
            {
                return false;
            }

            Vector3 toCamera = fixedCamera.transform.position - mirrorPlane.position;
            if (Vector3.Dot(mirrorPlane.forward, toCamera) <= 0f)
            {
                return false;
            }

            GeometryUtility.CalculateFrustumPlanes(fixedCamera, _frustumPlanes);
            return GeometryUtility.TestPlanesAABB(_frustumPlanes, mirrorSurface.bounds);
        }

        private void RenderReflection()
        {
            EnsureResources();
            if (fixedCamera == null || mirrorCamera == null || _reflectionTexture == null)
            {
                return;
            }

            Vector3 normal = mirrorPlane.forward;
            Vector3 planePoint = mirrorPlane.position;
            Transform source = fixedCamera.transform;

            mirrorCamera.transform.position = ReflectPoint(source.position, planePoint, normal);
            Vector3 forward = ReflectDirection(source.forward, normal);
            Vector3 up = ReflectDirection(source.up, normal);
            mirrorCamera.transform.rotation = Quaternion.LookRotation(forward, up);
            mirrorCamera.fieldOfView = fixedCamera.fieldOfView;
            mirrorCamera.aspect = fixedCamera.aspect;
            mirrorCamera.nearClipPlane = fixedCamera.nearClipPlane;
            mirrorCamera.farClipPlane = fixedCamera.farClipPlane;
            mirrorCamera.cullingMask = fixedCamera.cullingMask;
            mirrorCamera.clearFlags = fixedCamera.clearFlags;
            mirrorCamera.backgroundColor = fixedCamera.backgroundColor;

            // A reflected view matrix keeps the handedness reversal that a
            // Transform rotation cannot represent. Clip geometry behind the glass.
            Matrix4x4 reflection = Matrix4x4.identity;
            float d = -Vector3.Dot(normal, planePoint);
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    reflection[row, column] -= 2f * normal[row] * normal[column];
                reflection[row, 3] = -2f * d * normal[row];
            }
            mirrorCamera.worldToCameraMatrix = fixedCamera.worldToCameraMatrix * reflection;
            Vector3 clipPoint = mirrorCamera.worldToCameraMatrix.MultiplyPoint(planePoint + normal * 0.015f);
            Vector3 clipNormal = mirrorCamera.worldToCameraMatrix.MultiplyVector(normal).normalized;
            mirrorCamera.projectionMatrix = fixedCamera.projectionMatrix;
            mirrorCamera.projectionMatrix = mirrorCamera.CalculateObliqueMatrix(
                new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal)));

            bool oldCulling = GL.invertCulling;
            bool oldEnabled = mirrorSurface.enabled;
            try
            {
                mirrorSurface.enabled = false;
                GL.invertCulling = !oldCulling;
                if (GraphicsSettings.currentRenderPipeline == null) mirrorCamera.Render();
                else
                {
                    _renderRequest.destination = _reflectionTexture;
                    RenderPipeline.SubmitRenderRequest(mirrorCamera, _renderRequest);
                }
            }
            finally
            {
                GL.invertCulling = oldCulling;
                mirrorSurface.enabled = oldEnabled;
            }
        }

        private void EnsureResources()
        {
            if (mirrorSurface == null || mirrorPlane == null) return;
            if (_reflectionMaterial == null)
            {
                Shader shader = Shader.Find("ConGame/PlanarMirror");
                if (shader != null) _reflectionMaterial = new Material(shader) { name = "Mirror Reflection (Runtime)" };
            }
            if (_reflectionTexture == null)
            {
                _reflectionTexture = new RenderTexture(
                    Mathf.Max(16, textureWidth),
                    Mathf.Max(16, textureHeight),
                    16,
                    RenderTextureFormat.ARGB32)
                {
                    name = "RT_MirrorReflection",
                    useMipMap = false,
                    antiAliasing = 1
                };
                _reflectionTexture.Create();
            }

            if (mirrorCamera == null)
            {
                var cameraObject = new GameObject("MirrorReflectionCamera");
                cameraObject.transform.SetParent(transform, false);
                mirrorCamera = cameraObject.AddComponent<UnityEngine.Camera>();
                mirrorCamera.clearFlags = CameraClearFlags.SolidColor;
                mirrorCamera.backgroundColor = Color.black;
                _ownsMirrorCamera = true;
            }

            // 永远不进入普通渲染循环，只在可见帧手动 Render()。
            mirrorCamera.enabled = false;
            if (mirrorCamera.targetTexture != _reflectionTexture)
            {
                mirrorCamera.targetTexture = _reflectionTexture;
            }

            ApplyReflectionTexture();
        }

        private void ReleaseResources()
        {
            if (mirrorSurface != null && mirrorSurface.sharedMaterial == _reflectionMaterial)
                mirrorSurface.sharedMaterial = _intactMaterial;
            if (_reflectionMaterial != null) DestroyResource(_reflectionMaterial);
            _reflectionMaterial = null;
            if (mirrorCamera != null)
            {
                mirrorCamera.targetTexture = null;
            }

            if (_reflectionTexture != null)
            {
                _reflectionTexture.Release();
                DestroyResource(_reflectionTexture);
                _reflectionTexture = null;
            }

            if (_ownsMirrorCamera && mirrorCamera != null)
            {
                DestroyResource(mirrorCamera.gameObject);
                mirrorCamera = null;
            }

            _ownsMirrorCamera = false;
        }

        private void CaptureIntactMaterial()
        {
            if (mirrorSurface != null && mirrorSurface.sharedMaterial != _reflectionMaterial)
                _intactMaterial = mirrorSurface.sharedMaterial;
        }

        private void ApplyMaterial()
        {
            if (mirrorSurface == null)
            {
                return;
            }

            if (_stateMachine.IsBroken && brokenPlaceholderMaterial != null)
            {
                mirrorSurface.sharedMaterial = brokenPlaceholderMaterial;
            }
            else if (!_stateMachine.IsBroken && _reflectionMaterial != null)
            {
                mirrorSurface.sharedMaterial = _reflectionMaterial;
            }
            else if (_intactMaterial != null)
            {
                mirrorSurface.sharedMaterial = _intactMaterial;
            }

            ApplyReflectionTexture();
        }

        private void ApplyReflectionTexture()
        {
            if (mirrorSurface == null || _reflectionTexture == null ||
                string.IsNullOrEmpty(reflectionTextureProperty))
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.SetTexture(reflectionTextureProperty, _reflectionTexture);
            mirrorSurface.SetPropertyBlock(_propertyBlock);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (breakSource != null)
            {
                breakSource.StateChanged += OnBreakSourceStateChanged;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (breakSource != null)
            {
                breakSource.StateChanged -= OnBreakSourceStateChanged;
            }

            _subscribed = false;
        }

        private static Vector3 ReflectPoint(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            return point - 2f * Vector3.Dot(point - planePoint, planeNormal) * planeNormal;
        }

        private static Vector3 ReflectDirection(Vector3 direction, Vector3 planeNormal)
        {
            return direction - 2f * Vector3.Dot(direction, planeNormal) * planeNormal;
        }

        private static void DestroyResource(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
