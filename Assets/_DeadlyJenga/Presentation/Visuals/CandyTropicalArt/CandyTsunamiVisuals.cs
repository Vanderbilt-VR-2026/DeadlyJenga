using UnityEngine;
using UnityEngine.Rendering;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    [DisallowMultipleComponent]
    public sealed class CandyTsunamiVisuals : MonoBehaviour
    {
        CandyEnvironment environment;
        GameObject visual;
        Mesh mesh;
        MeshRenderer water;
        Material material, sprayMaterial;
        ParticleSystem spray;
        MaterialPropertyBlock properties;
        bool initialized;
        WaveFrame wave;

        void Awake() { environment = GetComponent<CandyEnvironment>(); properties = new MaterialPropertyBlock(); }
        void Build()
        {
            initialized = true;
            var template = Resources.Load<Material>("Tsunami/Water");
            var mist = Resources.Load<Material>("Tsunami/Spray");
            if (!template || !mist) { Debug.LogError("Tsunami presentation materials are missing.", this); return; }
            visual = new GameObject("Visual - Tsunami");
            var filter = visual.AddComponent<MeshFilter>(); water = visual.AddComponent<MeshRenderer>();
            material = new Material(template); water.sharedMaterial = material;
            water.shadowCastingMode = ShadowCastingMode.Off; water.receiveShadows = false;
            const int columns = 96, rows = 40;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var triangles = new int[columns * rows * 6];
            for (int z = 0; z <= rows; z++)
            for (int x = 0; x <= columns; x++) vertices[z * (columns + 1) + x] = new Vector3((float)x / columns - .5f, 0, (float)z / rows - .5f);
            int index = 0;
            for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                int a = z * (columns + 1) + x, b = a + columns + 1;
                triangles[index++] = a; triangles[index++] = b; triangles[index++] = a + 1;
                triangles[index++] = a + 1; triangles[index++] = b; triangles[index++] = b + 1;
            }
            mesh = new Mesh { name = "Tsunami static GPU surface", vertices = vertices, triangles = triangles };
            filter.sharedMesh = mesh;
            var sprayObject = new GameObject("Crest spray (pooled)"); sprayObject.transform.SetParent(visual.transform, false);
            spray = sprayObject.AddComponent<ParticleSystem>(); spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main; main.loop = true; main.playOnAwake = false; main.maxParticles = 72;
            main.startLifetime = .9f; main.startSpeed = 1.5f; main.startSize = new ParticleSystem.MinMaxCurve(.25f, .65f);
            main.startColor = new Color(.87f, 1, .97f, .55f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = .15f;
            var emission = spray.emission; emission.rateOverTime = 24;
            var shape = spray.shape; shape.shapeType = ParticleSystemShapeType.Box;
            var collision = spray.collision; collision.enabled = false;
            var color = spray.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            sprayMaterial = new Material(mist);
            var renderer = spray.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = sprayMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        void LateUpdate()
        {
            if (!environment || !environment.waterRenderer) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            wave = default;
            foreach (var source in Tracked.Waves) if (source.Wave.active) { wave = source.Wave; break; }
            if (wave.active && !initialized) Build();
            environment.waterRenderer.GetPropertyBlock(properties);
            SetWave(properties, visual ? wave : default);
            environment.waterRenderer.SetPropertyBlock(properties);
            if (!visual) return;
            bool shown = wave.active && wave.height > .02f;
            water.enabled = shown;
            if (!shown) { spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return; }
            material.CopyPropertiesFromMaterial(environment.waterRenderer.sharedMaterial);
            visual.transform.SetPositionAndRotation(wave.center, Quaternion.LookRotation(wave.direction));
            visual.transform.localScale = Vector3.one;
            mesh.bounds = new Bounds(Vector3.up * wave.height * .5f, new Vector3(wave.width + 4, wave.height + 4, wave.depth + 4));
            water.SetPropertyBlock(properties);
            spray.transform.localPosition = new Vector3(0, wave.height + .15f, wave.depth * .22f);
            spray.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var shape = spray.shape; shape.scale = new Vector3(wave.width * .8f, .1f, .5f);
            var camera = Camera.main;
            bool close = camera && Vector3.Distance(camera.transform.position, visual.transform.position) < 100;
            if (wave.height > 1 && close) { if (!spray.isPlaying) spray.Play(); }
            else spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        static void SetWave(MaterialPropertyBlock block, WaveFrame frame)
        {
            block.SetVector("_SurgeOrigin", frame.center);
            block.SetVector("_SurgeDirection", frame.direction);
            block.SetFloat("_SurgeWidth", frame.width); block.SetFloat("_SurgeDepth", frame.depth);
            block.SetFloat("_SurgeHeight", frame.active ? frame.height : 0);
        }
        void OnDisable()
        {
            if (water) water.enabled = false;
            if (spray) spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (environment && environment.waterRenderer && properties != null)
            { environment.waterRenderer.GetPropertyBlock(properties); SetWave(properties, default); environment.waterRenderer.SetPropertyBlock(properties); }
        }
        void OnDestroy()
        {
            Release(visual); Release(mesh); Release(material); Release(sprayMaterial);
        }
        static void Release(Object value)
        { if (value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
    }
}
