using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AgentPlaytest
{
    // Explicitly invoked, temporary objects only. Never generates saved scenes or assets.
    public sealed class SmokeScenario : IPlaytestScenario
    {
        public IEnumerator Run(PlaytestContext c)
        {
            var root = new GameObject("AgentPlaytest temporary physics sample");
            Material floorMaterial = null, ballMaterial = null;
            try
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform);
                floor.transform.position = Vector3.zero; floor.transform.localScale = new Vector3(6, .2f, 6);
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.transform.SetParent(root.transform); ball.transform.position = new Vector3(0, 3, 0);
                var body = ball.AddComponent<Rigidbody>();
                var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                bool supportedPipeline = !pipeline;
                for (var type = pipeline ? pipeline.GetType() : null; type != null; type = type.BaseType)
                    supportedPipeline |= type.FullName == "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";
                c.Check(supportedPipeline, "Sample supports Built-in/URP; supply a game adapter for other render pipelines");
                var shader = pipeline ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
                c.Check(shader && shader.isSupported, "A supported sample material shader is available");
                floorMaterial = new Material(shader); floorMaterial.color = new Color(.15f, .55f, .65f);
                ballMaterial = new Material(shader); ballMaterial.color = new Color(1, .35f, .08f);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial; ball.GetComponent<Renderer>().sharedMaterial = ballMaterial;
                var camera = new GameObject("AgentPlaytest evidence camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.enabled = false; camera.stereoTargetEye = StereoTargetEyeMask.None; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .1f, .18f);
                camera.transform.position = new Vector3(6, 4, -7); camera.transform.LookAt(new Vector3(0, 1.2f, 0));
                float firstY = body.position.y; int firstFrame = Time.frameCount;
                c.Check(c.Screenshot(camera, "before").variance > .0001f, "Screenshot contains rendered geometry");
                yield return c.WaitFrames(12);
                yield return c.Record(camera, "motion", .7f, 5);
                yield return c.WaitSeconds(.2f);
                c.Check(Time.frameCount > firstFrame + 5, "Actual Play Mode frames advance");
                c.Check(body.position.y < firstY - .2f, "Actual Rigidbody gravity advances the simulation");
                c.Check(c.Screenshot(camera, "after").variance > .0001f, "Second rendered screenshot is nonuniform");
                yield return c.WaitSeconds(.5f);
                c.Check(body.position.y > .45f && body.position.y < .8f, "Actual collider floor supports the fallen sphere");
            }
            finally { Object.Destroy(root); if (floorMaterial) Object.Destroy(floorMaterial); if (ballMaterial) Object.Destroy(ballMaterial); }
        }
    }
    public sealed class AssertionFailureScenario : IPlaytestScenario
    { public IEnumerator Run(PlaytestContext c) { c.Check(false, "Deliberate failure verifies failure reporting"); yield break; } }
    public sealed class ErrorScenario : IPlaytestScenario
    { public IEnumerator Run(PlaytestContext c) { Debug.LogError("Deliberate runtime error verifies error reporting"); yield return c.WaitFrames(2); } }
    public sealed class TimeoutScenario : IPlaytestScenario
    { public IEnumerator Run(PlaytestContext c) { while (true) yield return null; } }
}
