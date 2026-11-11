using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DeadlyJenga.CandyArt
{
    // Reflections follow the real blocks, including falling blocks, without rendering a second scene camera.
    internal sealed class CandyCoastReflection : IDisposable
    {
        static readonly int ReflectionTexture = Shader.PropertyToID("_CoastReflection");
        static readonly int ReflectionMatrix = Shader.PropertyToID("_CoastReflectionVP");
        static readonly int ReflectionReady = Shader.PropertyToID("_CoastReflectionReady");
        static readonly int BlockTint = Shader.PropertyToID("_CoastBlockTint");
        static readonly int LightDirection = Shader.PropertyToID("_CoastLightDirection");
        static readonly int WaterPlane = Shader.PropertyToID("_CoastWater");
        static readonly int ShadowTexture = Shader.PropertyToID("_CoastShadow");
        static readonly int ShadowMatrix = Shader.PropertyToID("_CoastShadowVP");
        static readonly int ShadowReady = Shader.PropertyToID("_CoastShadowReady");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        const int BatchSize = 192;
        readonly IWorldQuery world;
        readonly Renderer ocean, sand;
        readonly Material material;
        readonly float water;
        readonly int pass;
        readonly List<Piece> pieces = new();
        readonly Matrix4x4[] transforms = new Matrix4x4[BatchSize];
        readonly Vector4[] tints = new Vector4[BatchSize];
        readonly MaterialPropertyBlock drawProperties = new();
        readonly MaterialPropertyBlock waterProperties = new();
        Mesh proxy;
        RenderTexture target, shadow;
        Matrix4x4 viewProjection, shadowProjection;
        int revision = -1, blockCount = -1;
        float refreshAt, shadowAt;
        bool hasReflection, hasShadow;

        readonly struct Piece
        {
            public readonly MeshRenderer Renderer;
            public readonly Matrix4x4 LocalBounds;
            public readonly Color Tint;

            public Piece(MeshRenderer renderer, Bounds bounds, Color tint)
            {
                Renderer = renderer;
                LocalBounds = Matrix4x4.TRS(bounds.center, Quaternion.identity, bounds.size);
                Tint = tint;
            }
        }

        public CandyCoastReflection(IWorldQuery world, Renderer ocean, Renderer sand, Material material, float water)
        {
            this.world = world;
            this.ocean = ocean;
            this.sand = sand;
            this.material = material;
            this.water = water;
            pass = material.FindPass("Castle reflection");
            if (pass < 0 || !SystemInfo.supportsInstancing) return;
            material.enableInstancing = true;
            RenderPipelineManager.beginCameraRendering += Render;
        }

        void Render(ScriptableRenderContext context, Camera camera)
        {
            if (!ocean || !ocean.isVisible && camera.cameraType != CameraType.Game) return;
            ocean.GetPropertyBlock(waterProperties);
            waterProperties.SetFloat(ReflectionReady, 0);
            ocean.SetPropertyBlock(waterProperties);
            if (camera.cameraType != CameraType.Game || CandySceneSkins.Selected != CandySceneSkin.TropicalDay ||
                Shader.GetGlobalFloat("_CandyOutage") > .5f || !ReferenceEquals(world,Services.World))
                return;
            bool mobile = Application.isMobilePlatform;
            Camera active = Services.Session?.ActiveCamera;
            if (camera != active && camera != Camera.main &&
                (mobile || !camera.targetTexture || camera.targetTexture.width < 1024 ||
                    camera.gameObject.scene != ocean.gameObject.scene))
                return;

            float eyeHeight = camera.transform.position.y - water;
            if (eyeHeight < .26f || eyeHeight > 78 ||
                (camera.transform.position - world.Center).sqrMagnitude > 165 * 165)
                return;
            RefreshPieces();
            if (pieces.Count == 0) return;
            bool refreshReflection = !target || !mobile || Time.unscaledTime >= refreshAt;
            bool refreshShadow = !shadow || Time.unscaledTime >= shadowAt;
            if (refreshReflection || refreshShadow)
                Draw(context, camera, mobile, refreshReflection, refreshShadow);
            if (sand && hasShadow)
            {
                sand.GetPropertyBlock(waterProperties);
                waterProperties.SetTexture(ShadowTexture, shadow);
                waterProperties.SetMatrix(ShadowMatrix, shadowProjection);
                waterProperties.SetFloat(ShadowReady, 1);
                sand.SetPropertyBlock(waterProperties);
                ocean.GetPropertyBlock(waterProperties);
            }
            if (!hasReflection) return;

            waterProperties.SetTexture(ReflectionTexture, target);
            waterProperties.SetMatrix(ReflectionMatrix, viewProjection);
            waterProperties.SetFloat(ReflectionReady, 1);
            ocean.SetPropertyBlock(waterProperties);
        }

        void RefreshPieces()
        {
            var blocks = world.Blocks;
            int currentRevision = world is ISyncedWorld synced ? synced.Revision : 0;
            if (currentRevision == revision && blocks.Count == blockCount) return;
            revision = currentRevision;
            blockCount = blocks.Count;
            pieces.Clear();
            hasReflection = hasShadow = false;
            refreshAt = shadowAt = 0;
            foreach (var body in blocks)
            {
                if (!body) continue;
                Transform art = body.GetComponent<IClimbable>()?.Visual;
                if (!art) art = body.transform;
                var renderer = art.GetComponentInChildren<MeshRenderer>();
                if (!renderer || !renderer.TryGetComponent(out MeshFilter mesh) || !mesh.sharedMesh) continue;
                Material surface = renderer.sharedMaterial;
                if (!surface || !surface.HasProperty(BaseColor)) continue;
                Color tint = surface.GetColor(BaseColor);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) tint = tint.linear;
                pieces.Add(new Piece(renderer, mesh.sharedMesh.bounds, tint));
            }
        }

        void Draw(ScriptableRenderContext context, Camera camera, bool mobile, bool reflect, bool castShadow)
        {
            if (!proxy) proxy = BuildProxy();
            CommandBuffer commands = CommandBufferPool.Get("Candy Castle planar coast lighting");
            try
            {
                if (reflect)
                {
                    if (!target) target = CreateTarget(mobile ? 448 : 960, mobile ? 256 : 540, "Castle reflection");
                    Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(1, -1, 1));
                    mirror.m13 = 2 * water;
                    viewProjection = GL.GetGPUProjectionMatrix(camera.nonJitteredProjectionMatrix, true) *
                        camera.worldToCameraMatrix * mirror;
                    DrawPieces(commands, target, viewProjection);
                    hasReflection = true;
                    refreshAt = Time.unscaledTime + .05f;
                }
                if (castShadow)
                {
                    if (!shadow) shadow = CreateTarget(mobile ? 320 : 640, mobile ? 320 : 640, "Castle shore shadow");
                    Vector3 center = world.Center;
                    center.y = water;
                    Vector3 sun = CandyCatwalksCoast.SunDirection;
                    Matrix4x4 lightToWorld = Matrix4x4.TRS(center + sun * 110,
                        Quaternion.LookRotation(-sun), Vector3.one);
                    float coverage = Mathf.Max(68, world.Radius * 1.9f);
                    Matrix4x4 projection = Matrix4x4.Ortho(-coverage, coverage, -coverage, coverage, .5f, 215);
                    shadowProjection = GL.GetGPUProjectionMatrix(projection, true) *
                        Matrix4x4.Scale(new Vector3(1, 1, -1)) * lightToWorld.inverse;
                    DrawPieces(commands, shadow, shadowProjection);
                    hasShadow = true;
                    shadowAt = Time.unscaledTime + (mobile ? .38f : .16f);
                }
                context.ExecuteCommandBuffer(commands);
            }
            finally { CommandBufferPool.Release(commands); }
        }

        static RenderTexture CreateTarget(int width, int height, string label)
        {
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = label,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = true,
                autoGenerateMips = false
            };
            texture.Create();
            return texture;
        }

        void DrawPieces(CommandBuffer commands, RenderTexture output, Matrix4x4 projection)
        {
            commands.SetRenderTarget(output);
            commands.SetViewport(new Rect(0, 0, output.width, output.height));
            commands.ClearRenderTarget(true, true, Color.clear);
            drawProperties.SetMatrix(ReflectionMatrix, projection);
            drawProperties.SetFloat(WaterPlane, water);
            drawProperties.SetVector(LightDirection, CandyCatwalksCoast.SunDirection);
            int count = 0;
            foreach (var piece in pieces)
            {
                if (!piece.Renderer || !piece.Renderer.enabled || !piece.Renderer.gameObject.activeInHierarchy) continue;
                var model = piece.Renderer.localToWorldMatrix * piece.LocalBounds;
                if (model.m13 < water - 5) continue;
                transforms[count] = model;
                tints[count++] = piece.Tint;
                if (count != BatchSize) continue;
                Submit(commands, count);
                count = 0;
            }
            if (count > 0) Submit(commands, count);
            commands.GenerateMips(output);
        }

        void Submit(CommandBuffer commands, int count)
        {
            drawProperties.SetVectorArray(BlockTint, tints);
            commands.DrawMeshInstanced(proxy, 0, material, pass, transforms, count, drawProperties);
        }

        static Mesh BuildProxy()
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var indices = new int[36];
            for (int face = 0; face < 6; face++)
            {
                Vector3 normal = Vector3.zero;
                normal[face / 2] = face % 2 == 0 ? 1 : -1;
                Vector3 u = face / 2 == 0 ? Vector3.forward : Vector3.right;
                Vector3 v = Vector3.Cross(normal, u);
                int corner = 4 * face;
                for (int i = 0; i < 4; i++)
                {
                    vertices[corner + i] = (normal + u * (i % 2 == 0 ? -1 : 1) + v * (i / 2 == 0 ? -1 : 1)) * .5f;
                    normals[corner + i] = normal;
                }
                int triangle = 6 * face;
                indices[triangle] = corner;
                indices[triangle + 1] = corner + 1;
                indices[triangle + 2] = corner + 2;
                indices[triangle + 3] = corner + 1;
                indices[triangle + 4] = corner + 3;
                indices[triangle + 5] = corner + 2;
            }
            var mesh = new Mesh { name = "Castle reflection block", vertices = vertices, normals = normals, triangles = indices };
            mesh.UploadMeshData(true);
            return mesh;
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            if (target)
            {
                target.Release();
                Object.Destroy(target);
            }
            if (shadow)
            {
                shadow.Release();
                Object.Destroy(shadow);
            }
            if (proxy) Object.Destroy(proxy);
        }
    }
}
