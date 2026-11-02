using UnityEngine;
using UnityEngine.Rendering;

namespace ConGame.Presentation.Lighting
{
    /// <summary>Faint visible air around the existing gameplay spotlight; never supplies perception.</summary>
    [DisallowMultipleComponent]
    public sealed class MvFlashlightBeam : MonoBehaviour
    {
        private Light _source;
        private Mesh _mesh;
        private MeshRenderer _renderer;

        public void Configure(Light source, Material material)
        {
            _source = source;
            if (_renderer != null) return;
            var host = new GameObject("VisibleFlashlightCone");
            host.transform.SetParent(source.transform, false);
            var filter = host.AddComponent<MeshFilter>();
            _renderer = host.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            const int sides = 32;
            var vertices = new Vector3[sides * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[sides * 6];
            float length = Mathf.Min(source.range, 5f);
            float radius = Mathf.Tan(source.spotAngle * Mathf.Deg2Rad * 0.5f) * length;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 ring = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices[i] = ring * 0.018f;
                vertices[i + sides] = ring * radius + Vector3.forward * length;
                colors[i] = new Color(0.65f, 0.8f, 1f, 0.055f);
                colors[i + sides] = new Color(0.65f, 0.8f, 1f, 0f);
                int next = (i + 1) % sides, t = i * 6;
                triangles[t] = i; triangles[t + 1] = next; triangles[t + 2] = i + sides;
                triangles[t + 3] = next; triangles[t + 4] = next + sides; triangles[t + 5] = i + sides;
            }
            _mesh = new Mesh { name = "MV flashlight air" };
            _mesh.vertices = vertices; _mesh.colors = colors; _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            filter.sharedMesh = _mesh;
        }

        private void LateUpdate()
        {
            if (_renderer != null) _renderer.enabled = _source != null && _source.enabled && _source.gameObject.activeInHierarchy;
        }

        private void OnDisable() { if (_renderer != null) _renderer.enabled = false; }
        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
    }
}
