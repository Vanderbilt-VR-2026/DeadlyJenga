using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.Presentation
{
    public sealed class CandySprintCameraFx : ScriptableRendererFeature
    {
        const float ExtraFieldOfView = 7f;
        [SerializeField] Shader shader;
        static Camera sprintView;
        static float sprintBlend;
        Camera widenedView;
        float previousFieldOfView, appliedFieldOfView;
        Material material;
        SprintPass pass;

        internal static void SetView(Camera camera, float blend)
        {
            sprintView = camera;
            sprintBlend = blend;
        }

        internal static void ClearView(Camera camera)
        {
            if (sprintView != camera) return;
            sprintView = null;
            sprintBlend = 0f;
        }

        static bool ActiveFor(Camera camera) =>
            Application.isPlaying && sprintBlend > .001f && camera && camera == sprintView &&
            camera.cameraType == CameraType.Game && !camera.stereoEnabled;

        public override void Create()
        {
            if (pass != null) Release();
            if (!shader || !shader.isSupported) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new SprintPass(material);
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!isActive || !ActiveFor(camera) || widenedView || camera.orthographic) return;
            // Scope zoom owns the base lens. Sprint widening lasts only for this render.
            widenedView = camera;
            previousFieldOfView = camera.fieldOfView;
            appliedFieldOfView = Mathf.Min(179f, previousFieldOfView + ExtraFieldOfView * Mathf.SmoothStep(0, 1, sprintBlend));
            camera.fieldOfView = appliedFieldOfView;
        }

        void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == widenedView) RestoreFieldOfView();
        }

        void RestoreFieldOfView()
        {
            if (widenedView && Mathf.Approximately(widenedView.fieldOfView, appliedFieldOfView))
                widenedView.fieldOfView = previousFieldOfView;
            widenedView = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (pass == null || cameraData.renderType != CameraRenderType.Base || !ActiveFor(cameraData.camera))
                return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => Release();

        void Release()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreFieldOfView();
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }

        sealed class SprintPass : ScriptableRenderPass
        {
            static readonly int Blend = Shader.PropertyToID("_SprintBlend");
            static readonly int Phase = Shader.PropertyToID("_SprintPhase");
            static readonly int Aspect = Shader.PropertyToID("_SprintAspect");
            readonly Material material;
            readonly MaterialPropertyBlock properties = new();

            public SprintPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                // Sample the actual scene color, including transparency, before UI composition.
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var source = resources.activeColorTexture;
                var descriptor = source.GetDescriptor(graph);
                descriptor.name = "Sprint camera color";
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.bindTextureMS = false;
                descriptor.clearBuffer = false;
                var destination = graph.CreateTexture(descriptor);

                var camera = frameData.Get<UniversalCameraData>().camera;
                properties.SetFloat(Blend, Mathf.SmoothStep(0, 1, sprintBlend));
                properties.SetFloat(Phase, Time.unscaledTime);
                properties.SetFloat(Aspect, camera.aspect);
                var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0, properties);
                graph.AddBlitPass(blit, "Sprint peripheral blur and streaks");
                resources.cameraColor = destination;
            }
        }
    }
}
