using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.CandyArt
{
    // Enqueued only for the active view while it crosses or sits inside the wave.
    sealed class CandyWaveImmersionPass : ScriptableRenderPass
    {
        static readonly int WaterWash = Shader.PropertyToID("_WaterWash");
        static readonly int HasDepth = Shader.PropertyToID("_WaveHasDepth");
        static readonly int BlitSource = Shader.PropertyToID("_BlitTexture");
        static readonly int ScaleBias = Shader.PropertyToID("_BlitScaleBias");
        readonly Material material;

        sealed class PassData
        {
            public TextureHandle source;
            public Material material;
            public MaterialPropertyBlock properties;
        }

        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        public Vector4 State { get; set; }

        public CandyWaveImmersionPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>();
            if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid()) return;

            var source = resources.activeColorTexture;
            var desc = graph.GetTextureDesc(source);
            desc.name = "Tsunami underwater camera color";
            desc.clearBuffer = false;
            var destination = graph.CreateTexture(desc);
            // Reuse depth where the camera already provides it. Mobile doesn't need a depth copy.
            bool hasDepth = camera.requiresDepthTexture && resources.cameraDepthTexture.IsValid();
            properties.SetVector(WaterWash, State);
            properties.SetFloat(HasDepth, hasDepth ? 1 : 0);
            properties.SetVector(ScaleBias, new Vector4(1, 1, 0, 0));
            using (var builder = graph.AddRasterRenderPass<PassData>("DJ. Tsunami underwater", out var data))
            {
                data.source = source;
                data.material = material;
                data.properties = properties;
                builder.UseTexture(source);
                if (hasDepth) builder.UseTexture(resources.cameraDepthTexture);
                builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                {
                    pass.properties.SetTexture(BlitSource, pass.source);
                    context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 0, MeshTopology.Triangles, 3, 1, pass.properties);
                });
            }
            resources.cameraColor = destination;
        }
    }
}
