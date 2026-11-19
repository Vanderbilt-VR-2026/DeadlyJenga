using System;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.CandyArt
{
    internal sealed class CandyReefLight : ScriptableRenderPass, IDisposable
    {
        static readonly int Water = Shader.PropertyToID("_ReefWater");
        static readonly int Sun = Shader.PropertyToID("_ReefSun");
        static readonly int Caustics = Shader.PropertyToID("_CoastCaustics");
        readonly IWorldQuery world;
        readonly Renderer ocean;
        readonly Material material;
        readonly float water;

        public CandyReefLight(IWorldQuery world, Renderer ocean, Texture2D caustics, float water)
        {
            this.world = world;
            this.ocean = ocean;
            this.water = water;
            var shader = Resources.Load<Shader>("CandyScenery/CandyReefLight");
            if (!shader || !shader.isSupported || !caustics) return;
            material = new Material(shader) { name = "Underwater reef light" };
            material.SetTexture(Caustics, caustics);
            material.SetFloat(Water, water);
            material.SetVector(Sun, CandyCatwalksCoast.SunDirection);
            ConfigureInput(ScriptableRenderPassInput.Depth);
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
        }

        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!ocean || camera.cameraType != CameraType.Game || !ReferenceEquals(Services.World, world) ||
                CandySceneSkins.Selected != CandySceneSkin.TropicalDay ||
                Shader.GetGlobalFloat("_CandyOutage") > .5f ||
                !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                return;
            Vector3 eye = camera.transform.position;
            float depth = water - eye.y;
            if (depth < .12f || depth > 8 || (eye-world.Center).sqrMagnitude > 125 * 125) return;
            if (camera != Services.Session?.ActiveCamera && camera != Camera.main &&
                (Application.isMobilePlatform || !camera.targetTexture || camera.targetTexture.width < 1024 ||
                    camera.gameObject.scene != ocean.gameObject.scene))
                return;
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(this);
        }

        sealed class PassData { public Material Material; }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (!resources.cameraDepthTexture.IsValid() || !resources.activeColorTexture.IsValid()) return;

            using var builder = graph.AddRasterRenderPass<PassData>("DJ. Reef underwater light", out var data);
            data.Material = material;
            builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                context.cmd.DrawProcedural(Matrix4x4.identity, pass.Material, 0, MeshTopology.Triangles, 3));
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            if (material) UnityEngine.Object.Destroy(material);
        }
    }
}
