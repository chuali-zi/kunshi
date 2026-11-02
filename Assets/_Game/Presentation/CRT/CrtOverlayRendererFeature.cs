using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ConGame.Presentation.CRT
{
    /// <summary>Camera color -> separate graph texture -> final resolve. No backbuffer reads or shared RT state.</summary>
    public sealed class CrtOverlayRendererFeature : ScriptableRendererFeature
    {
        private OverlayPass _pass;

        public override void Create() => _pass = new OverlayPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game || renderingData.cameraData.renderType != CameraRenderType.Base) return;
            Material material = CrtFullscreenPass.ForCamera(camera);
            if (material == null) return;
            _pass.Setup(material);
            renderer.EnqueuePass(_pass);
        }

        private sealed class OverlayPass : ScriptableRenderPass
        {
            private Material _material;

            public void Setup(Material material)
            {
                _material = material;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || _material == null) return;
                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.name = "ConGame CRT and MV overlays";
                descriptor.clearBuffer = false;
                var destination = graph.CreateTexture(descriptor);
                // AddBlitPass binds the source as _BlitTexture for the shader's Blit.hlsl vertex/UV convention.
                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
                graph.AddBlitPass(parameters, passName: "ConGame CRT and MV signature beats");
                resources.cameraColor = destination;
            }
        }
    }
}
