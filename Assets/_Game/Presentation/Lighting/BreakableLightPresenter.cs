using System;
using System.Collections.Generic;
using ConGame.Runtime.Lighting;
using ConGame.Runtime.Loop;
using UnityEngine;

namespace ConGame.Presentation.Lighting
{
    [DisallowMultipleComponent]
    public sealed class BreakableLightPresenter : MonoBehaviour, ILoopResettable
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private const string LampEmissionMaterialToken = "Lamp_Emissive";

        [SerializeField] private BreakableLight lightState;
        [SerializeField] private GameObject intactVisual;
        [SerializeField] private GameObject brokenVisual;
        [SerializeField] private Light emissionLight;

        private readonly List<EmissionSlot> _emissionSlots = new List<EmissionSlot>();
        private MaterialPropertyBlock _propertyBlock;
        private bool _emissionSlotsCached;
        private Color _initialEmissionLightColor = Color.white;
        private float _initialEmissionLightIntensity = 1f;

        private readonly struct EmissionSlot
        {
            public EmissionSlot(Renderer renderer, int materialIndex, Color originalColor)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                OriginalColor = originalColor;
            }

            public Renderer Renderer { get; }
            public int MaterialIndex { get; }
            public Color OriginalColor { get; }
        }

        public int ResetOrder => 65;

        public void Configure(BreakableLight state, GameObject intact, GameObject broken)
        {
            Configure(state, intact, broken, emissionLight);
        }

        public void Configure(BreakableLight state, GameObject intact, GameObject broken, Light emitter)
        {
            if (isActiveAndEnabled && lightState != null)
            {
                lightState.BrokenChanged -= OnBrokenChanged;
            }

            lightState = state;
            intactVisual = intact;
            brokenVisual = broken;
            emissionLight = emitter;
            _emissionSlotsCached = false;

            if (isActiveAndEnabled && lightState != null)
            {
                lightState.BrokenChanged += OnBrokenChanged;
            }

            ApplyVisuals();
        }

        public void CaptureInitialState()
        {
            ApplyVisuals();
        }

        public void Quiesce(in LoopResetContext context)
        {
        }

        public void Restore(in LoopResetContext context)
        {
            ApplyVisuals();
        }

        public void Resume(in LoopResetContext context)
        {
            ApplyVisuals();
        }

        private void OnEnable()
        {
            if (lightState != null)
            {
                lightState.BrokenChanged += OnBrokenChanged;
            }

            ApplyVisuals();
        }

        private void OnDisable()
        {
            if (lightState != null)
            {
                lightState.BrokenChanged -= OnBrokenChanged;
            }

            ApplyEmission(false);
        }

        private void LateUpdate()
        {
            // BreakableLight.SetDraped changes the Light directly and has no
            // separate presentation event, so keep the bulb material in sync.
            ApplyEmission(lightState != null && !lightState.IsBroken && lightState.IsEmitting);
        }

        private void OnBrokenChanged(BreakableLight source, bool broken)
        {
            ApplyVisuals(broken);
        }

        private void ApplyVisuals()
        {
            ApplyVisuals(lightState != null && lightState.IsBroken);
        }

        private void ApplyVisuals(bool broken)
        {
            if (intactVisual != null)
            {
                intactVisual.SetActive(!broken);
            }

            if (brokenVisual != null)
            {
                brokenVisual.SetActive(broken);
            }

            ApplyEmission(!broken && lightState != null && lightState.IsEmitting);
        }

        private void ApplyEmission(bool emitting)
        {
            CacheEmissionSlots();
            if (_emissionSlots.Count == 0)
            {
                return;
            }

            Color multiplier = emitting ? ResolveEmissionMultiplier() : Color.black;
            _propertyBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < _emissionSlots.Count; i++)
            {
                EmissionSlot slot = _emissionSlots[i];
                if (slot.Renderer == null)
                {
                    continue;
                }

                _propertyBlock.Clear();
                slot.Renderer.GetPropertyBlock(_propertyBlock, slot.MaterialIndex);
                Color color = emitting
                    ? new Color(
                        slot.OriginalColor.r * multiplier.r,
                        slot.OriginalColor.g * multiplier.g,
                        slot.OriginalColor.b * multiplier.b,
                        slot.OriginalColor.a)
                    : Color.black;
                _propertyBlock.SetColor(EmissionColorId, color);
                slot.Renderer.SetPropertyBlock(_propertyBlock, slot.MaterialIndex);
            }
        }

        private void CacheEmissionSlots()
        {
            if (_emissionSlotsCached)
            {
                return;
            }

            _emissionSlotsCached = true;
            _emissionSlots.Clear();
            _propertyBlock ??= new MaterialPropertyBlock();
            CollectEmissionSlots(intactVisual);
            if (brokenVisual != intactVisual)
            {
                CollectEmissionSlots(brokenVisual);
            }
            ResolveEmissionLight();
            if (emissionLight != null)
            {
                _initialEmissionLightColor = emissionLight.color;
                _initialEmissionLightIntensity = Mathf.Max(0.0001f, emissionLight.intensity);
            }
        }

        private void CollectEmissionSlots(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null || !material.HasProperty(EmissionColorId) ||
                        material.name.IndexOf(LampEmissionMaterialToken, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    _emissionSlots.Add(new EmissionSlot(
                        renderer,
                        materialIndex,
                        material.GetColor(EmissionColorId)));
                }
            }
        }

        private void ResolveEmissionLight()
        {
            if (emissionLight != null || lightState == null)
            {
                return;
            }

            Light[] lights = lightState.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                Light candidate = lights[i];
                if (candidate == null || candidate.transform == lightState.transform)
                {
                    continue;
                }

                if (candidate.name.IndexOf("PracticalEmitter", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    emissionLight = candidate;
                    return;
                }
            }
        }

        private Color ResolveEmissionMultiplier()
        {
            if (emissionLight == null)
            {
                return Color.white;
            }

            float intensity = Mathf.Max(0f, emissionLight.intensity / _initialEmissionLightIntensity);
            return new Color(
                RelativeColorChannel(emissionLight.color.r, _initialEmissionLightColor.r) * intensity,
                RelativeColorChannel(emissionLight.color.g, _initialEmissionLightColor.g) * intensity,
                RelativeColorChannel(emissionLight.color.b, _initialEmissionLightColor.b) * intensity,
                1f);
        }

        private static float RelativeColorChannel(float current, float initial)
        {
            return initial > 0.0001f ? current / initial : 1f;
        }
    }
}
