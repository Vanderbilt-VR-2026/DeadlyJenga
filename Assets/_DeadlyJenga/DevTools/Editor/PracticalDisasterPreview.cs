using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // An explicit request renders isolated art studies. It never installs art into the game.
    [InitializeOnLoad]
    public static class PracticalDisasterPreview
    {
        const string Key = "PracticalDisasterPreview";
        static readonly List<Object> temporary = new List<Object>();
        static PracticalDisasterPreview() { EditorApplication.update += Tick; }
        static void Tick()
        {
            string request = ProjectPath.Report(Key + ".request");
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.ReadAllText(request).Trim() != System.Diagnostics.Process.GetCurrentProcess().Id.ToString()) return;
            if (!ValidationRunGate.TryAcquire(Key)) return;
            File.Delete(request);
            try { Capture(); }
            catch (Exception e) { File.WriteAllText(ProjectPath.Report(Key + "-error.txt"), e.ToString()); }
            finally { ValidationRunGate.Release(Key); }
        }

        static T Keep<T>(T value) where T : Object { temporary.Add(value); return value; }
        static Material Material(string shader, Texture detail)
        {
            var found = Shader.Find(shader);
            if (!found || !found.isSupported || ShaderUtil.ShaderHasError(found)) throw new Exception("Unsupported prototype shader: " + shader);
            var material = Keep(new Material(found)); material.SetTexture("_Detail", detail); return material;
        }
        static Texture2D Detail()
        {
            var texture = Keep(new Texture2D(256, 256, TextureFormat.RGBA32, true, true));
            texture.name = "One packed 256px 2D detail mask"; texture.wrapMode = TextureWrapMode.Repeat; texture.filterMode = FilterMode.Trilinear;
            var colors = new Color[256 * 256];
            float Noise(float x, float y, float offset)
            {
                float sum = 0, weight = 0;
                for (int octave = 0; octave < 4; octave++)
                {
                    float scale = 4 * (1 << octave), amplitude = 1f / (1 << octave);
                    float a = Mathf.PerlinNoise(x * scale + offset, y * scale + offset);
                    float b = Mathf.PerlinNoise((x - 1) * scale + offset, y * scale + offset);
                    float c = Mathf.PerlinNoise(x * scale + offset, (y - 1) * scale + offset);
                    float d = Mathf.PerlinNoise((x - 1) * scale + offset, (y - 1) * scale + offset);
                    sum += Mathf.Lerp(Mathf.Lerp(a, b, x), Mathf.Lerp(c, d, x), y) * amplitude; weight += amplitude;
                }
                return sum / weight;
            }
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float u = x / 256f, v = y / 256f;
                colors[y * 256 + x] = new Color(Noise(u,v,17), Noise(u,v,71), Noise(u,v,123), 1);
            }
            texture.SetPixels(colors); texture.Apply(true, false); return texture;
        }

        static Mesh Grid(string name, int columns, int rows, Func<float,float,Vector3> point)
        {
            var vertices = new Vector3[(columns + 1) * (rows + 1)]; var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6]; int cursor = 0;
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                int i = y * (columns + 1) + x; uv[i] = new Vector2(x / (float)columns, y / (float)rows); vertices[i] = point(uv[i].x, uv[i].y);
            }
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x, b = a + columns + 1;
                triangles[cursor++] = a; triangles[cursor++] = b; triangles[cursor++] = a + 1;
                triangles[cursor++] = a + 1; triangles[cursor++] = b; triangles[cursor++] = b + 1;
            }
            var mesh = Keep(new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles });
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static GameObject Draw(GameObject root, Mesh mesh, Material material, string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return go;
        }
        static Vector3 FunnelPoint(float u, float v, bool ribbon)
        {
            float a = u * Mathf.PI * 2;
            float r = Mathf.Lerp(.6f, ribbon ? 6.4f : 9.2f, Mathf.Pow(v, 1.45f));
            r *= 1 + .09f * Mathf.Sin(a * 5 + v * 27) + .045f * Mathf.Sin(a * 9 - v * 35);
            return new Vector3(Mathf.Cos(a) * r + Mathf.Sin(v * 3) * v * 3, v * 65, Mathf.Sin(a) * r + Mathf.Cos(v * 4) * v * 2);
        }
        static GameObject Tornado(Scene scene, Texture detail, bool ribbons)
        {
            var root = new GameObject(ribbons ? "Wind ribbons" : "Sculpted spiral"); SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = new Vector3(0,.08f,6.006f);
            var body = Material("Deadly Jenga/Prototypes/Practical Funnel", detail);
            body.SetColor("_Dark", new Color(.10f,.16f,.21f)); body.SetColor("_Light", new Color(.67f,.77f,.83f));
            Draw(root, Grid("Static lobed funnel", 48, 48, (u,v) => FunnelPoint(u,v,ribbons)), body, "Funnel");
            if (ribbons)
            {
                var ribbon = Material("Deadly Jenga/Prototypes/Practical Funnel", detail);
                ribbon.SetColor("_Dark", new Color(.33f,.44f,.55f)); ribbon.SetColor("_Light", new Color(.87f,.94f,1));
                for (int i = 0; i < 3; i++)
                {
                    float phase = i / 3f;
                    Draw(root, Grid("Helical wind strip", 3, 112, (u,v) =>
                    {
                        float a = (v * 2.7f + phase) * Mathf.PI * 2;
                        float r = Mathf.Lerp(.9f,9.6f,Mathf.Pow(v,1.4f));
                        return new Vector3(Mathf.Cos(a)*r+Mathf.Sin(v*3)*v*3,v*65+(u-.5f)*Mathf.Lerp(.2f,2.1f,v),Mathf.Sin(a)*r+Mathf.Cos(v*4)*v*2);
                    }), ribbon, "Wind ribbon");
                }
            }
            return root;
        }
        static float Surface(float t)
        {
            float q = t < .72f ? Mathf.Clamp01(t/.72f) : Mathf.Clamp01((t-.72f)/.28f);
            float smooth = q*q*(3-2*q); return t < .72f ? smooth : 1-smooth;
        }
        static GameObject Tsunami(Scene scene, Texture detail, bool whitewater)
        {
            var root = new GameObject(whitewater ? "Whitewater crest" : "Lagoon crest"); SceneManager.MoveGameObjectToScene(root,scene);
            var framePath=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","DisasterRedesign-20261026","tsunami-frame.json");
            var wave=JsonUtility.FromJson<WaveFrame>(File.ReadAllText(framePath));
            root.transform.SetPositionAndRotation(wave.center,Quaternion.LookRotation(wave.direction));
            var material=Material("Deadly Jenga/Prototypes/Practical Surge",detail); material.SetFloat("_Whitewater",whitewater?1:0);
            Draw(root,Grid("Production-profile wave",96,40,(u,v)=>
            {
                float edge=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,Mathf.Abs(u-.5f)*2));
                return new Vector3((u-.5f)*wave.width,Surface(v)*wave.height*edge+.03f,(v-.5f)*wave.depth);
            }),material,"Wave surface");
            return root;
        }
        static void Shot(Camera camera, string path, Vector3 position, Vector3 target)
        {
            camera.transform.position=position;camera.transform.LookAt(target);
            var rt=RenderTexture.GetTemporary(1920,1080,24);var old=RenderTexture.active;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally {camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
        static void Capture()
        {
            string output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","DisasterPractical-20261026");Directory.CreateDirectory(output);
            var previousWorld=Services.World;var previousSession=Services.Session;
            bool ignored=Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block);
            var report=new StringBuilder("Actual Unity URP Editor renders; isolated Game scene copy; no simulation; no image generation or image retouching.\nNo live disaster/scene/prefab was changed. No headset or frame-rate validation.\n");
            var scene=EditorSceneManager.OpenPreviewScene(ProjectPath.Find("Game.unity"));
            try
            {
                var roots=scene.GetRootGameObjects();
                var map=roots.SelectMany(r=>r.GetComponentsInChildren<CastleGenerator>(true)).First();
                Services.World=map;Services.Session=null;
                if(map.Blocks.Count==0 && !map.GetComponentsInChildren<Rigidbody>().Any())map.Rebuild();
                var environment=roots.SelectMany(r=>r.GetComponentsInChildren<CandyEnvironment>(true)).First();environment.Refresh();
                var cameraObject=new GameObject("Isolated art study camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.clearFlags=CameraClearFlags.Skybox;camera.nearClipPlane=.2f;camera.farClipPlane=1600;camera.fieldOfView=52;
                var texture=Detail();File.WriteAllBytes(Path.Combine(output,"packed-detail-256.png"),texture.EncodeToPNG());
                foreach(string name in new[]{"tornado-sculpted","tornado-ribbons","tsunami-lagoon","tsunami-whitewater"})
                {
                    bool tornado=name.StartsWith("tornado");
                    var root=tornado?Tornado(scene,texture,name.EndsWith("ribbons")):Tsunami(scene,texture,name.EndsWith("whitewater"));
                    try
                    {
                        var meshes=root.GetComponentsInChildren<MeshFilter>();
                        report.AppendLine(name+": "+meshes.Sum(m=>m.sharedMesh.vertexCount)+" vertices; "+meshes.Sum(m=>m.sharedMesh.triangles.Length/3)+" triangles; "+meshes.Length+" renderers; "+root.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial).Distinct().Count()+" materials; colliders="+root.GetComponentsInChildren<Collider>().Length+"; rigidbodies="+root.GetComponentsInChildren<Rigidbody>().Length);
                        var position=tornado?new Vector3(88,40,94):new Vector3(-96,32,-82);
                        var target=tornado?new Vector3(0,31,6):new Vector3(-24,12,0);
                        Shot(camera,Path.Combine(output,name+".png"),position,target);
                        Shot(camera,Path.Combine(output,name+"-close.png"),tornado?new Vector3(28,44,54):new Vector3(-76,17,-45),tornado?new Vector3(0,42,6):new Vector3(-49,12,0));
                    }
                    finally {Object.DestroyImmediate(root);}
                }
                foreach(var shaderName in new[]{"Deadly Jenga/Prototypes/Practical Funnel","Deadly Jenga/Prototypes/Practical Surge"})
                {
                    var shader=Shader.Find(shaderName);report.AppendLine(shaderName+": supported="+shader.isSupported+"; errors="+ShaderUtil.ShaderHasError(shader));
                    foreach(var message in ShaderUtil.GetShaderMessages(shader))report.AppendLine(message.severity+": "+message.message);
                    if(ShaderUtil.ShaderHasError(shader))throw new Exception("Shader compile failed: "+shaderName);
                }
                report.AppendLine("One shared 256x256 RGBA 2D mask with mipmaps. Zero 3D textures, particles, transparent passes, screen refraction, volumetric clouds or additional physics. Surface motion is UV animation; mesh shapes are fixed. Tsunami height profile preserves the production 0.72 crest and 15% side taper. Opaque water is an explicit visual tradeoff; no transparency promised.\nComplete.");
                File.WriteAllText(Path.Combine(output,"Capture.txt"),report.ToString());
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);Services.World=previousWorld;Services.Session=previousSession;Physics.IgnoreLayerCollision(Layers.Survivor,Layers.Block,ignored);
                foreach(var value in temporary)if(value)Object.DestroyImmediate(value);temporary.Clear();
            }
        }
    }
}
