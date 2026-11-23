using DeadlyJenga.CandyAudio;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    [DefaultExecutionOrder(352)]
    public sealed class CandyBarbedBatFeedback : MonoBehaviour
    {
        readonly CandyEquipmentActionCursor cursor = new CandyEquipmentActionCursor();
        IMatchRuntime runtime;
        ParticleSystem sparks, chips;
        Material sparkMaterial, chipMaterial;
        Mesh chipMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += host =>
        {
            if (!host.GetComponent<CandyBarbedBatFeedback>())
                host.AddComponent<CandyBarbedBatFeedback>();
        };

        void Awake()
        {
            var shader = Resources.Load<Shader>("CandyOrdnance");
            if (!shader) { enabled = false; return; }
            sparkMaterial = new Material(shader);
            sparks = CreateEmitter("Barbed Bat contact sparks", sparkMaterial, 96, 0);
            var streaks = sparks.GetComponent<ParticleSystemRenderer>();
            streaks.renderMode = ParticleSystemRenderMode.Stretch;
            streaks.lengthScale = 1.8f;
            streaks.velocityScale = .08f;

            var lit = Shader.Find("Universal Render Pipeline/Particles/Lit");
            if (!lit) return;
            chipMaterial = new Material(lit);
            chips = CreateEmitter("Barbed Bat candy chips", chipMaterial, 96, .75f);
            var shards = chips.GetComponent<ParticleSystemRenderer>();
            shards.renderMode = ParticleSystemRenderMode.Mesh;
            chipMesh = BuildChip();
            shards.mesh = chipMesh;
        }

        ParticleSystem CreateEmitter(string label, Material material, int limit, float gravity)
        {
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            var emitter = item.AddComponent<ParticleSystem>();
            emitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = emitter.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = limit;
            main.gravityModifier = gravity;
            var emission = emitter.emission;
            emission.enabled = false;
            var shape = emitter.shape;
            shape.enabled = false;
            var size = emitter.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            var renderer = item.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return emitter;
        }

        static Mesh BuildChip()
        {
            var mesh = new Mesh
            {
                name = "Barbed Bat candy shard",
                vertices = new[] { new Vector3(-.47f,-.26f,-.24f), new Vector3(.52f,-.20f,-.21f),
                    new Vector3(.12f,-.15f,.49f), new Vector3(-.08f,.60f,.02f) },
                triangles = new[] { 0,1,2,0,3,1,1,3,2,2,3,0 }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            if (!ReferenceEquals(runtime, MatchRuntime.Current))
            {
                runtime = MatchRuntime.Current;
                cursor.Baseline(runtime?.Frame);
                ClearEmitters();
            }
            cursor.Read(runtime?.Frame, Contact);
        }

        void Contact(EquipmentActionFrame action)
        {
            if (action.kind != EquipmentKind.BarbedBat || action.action != EquipmentAction.Impact ||
                !CandyWeaponImpacts.VisibleToAnyCamera(action.position, 45)) return;

            var state = (runtime as IEquipmentRuntime)?.EquipmentFor(action.actor);
            bool actorHit = state?.embedRemaining > 0 && !string.IsNullOrEmpty(state.embeddedVictim) &&
                (state.embeddedPoint - action.position).sqrMagnitude < .12f * .12f;
            CandyWeaponImpacts.SurfaceContact surface = default;
            if (!actorHit)
                CandyWeaponImpacts.TryContact(action.position, Vector3.zero, out surface, .17f);

            var facing = runtime.FindActor(action.actor)?.ViewPose.rotation ?? Quaternion.identity;
            var normal = surface.anchor
                ? (Vector3)(surface.anchor.localToWorldMatrix * surface.localFrame).GetColumn(2)
                : -(facing * Vector3.forward);
            var rotation = Quaternion.LookRotation(normal);
            EmitSparks(action.position + normal.normalized * .024f, rotation, actorHit);
            if (surface.anchor && surface.layer == Layers.Block)
                EmitChips(surface.Position, rotation);
        }

        void EmitSparks(Vector3 point, Quaternion rotation, bool actorHit)
        {
            if (sparks.isStopped) sparks.Play();
            var tint = actorHit ? new Color(.37f,.83f,1) : new Color(1,.50f,.12f);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * 2.39996f;
                var spread = rotation * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .48f);
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = point, velocity = spread * (1.5f + i * .28f),
                    startColor = i % 3 == 0 ? Color.Lerp(tint, Color.white, .58f) : tint,
                    startSize = i == 0 ? .19f : .075f, startLifetime = .115f + i * .008f
                }, 1);
            }
        }

        void EmitChips(Vector3 point, Quaternion rotation)
        {
            if (!chips) return;
            if (chips.isStopped) chips.Play();
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 2.39996f;
                var spread = rotation * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .73f);
                chips.Emit(new ParticleSystem.EmitParams
                {
                    position = point, velocity = spread * (1.3f + i * .26f),
                    rotation = i * 47, startSize = .047f + i % 3 * .012f,
                    startLifetime = .24f + i * .017f,
                    startColor = i % 3 == 0 ? new Color(.98f,.57f,.67f) : new Color(.77f,.23f,.36f)
                }, 1);
            }
        }

        void ClearEmitters()
        {
            if (sparks) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (chips) chips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnDisable() => ClearEmitters();

        void OnDestroy()
        {
            if (sparkMaterial) Destroy(sparkMaterial);
            if (chipMaterial) Destroy(chipMaterial);
            if (chipMesh) Destroy(chipMesh);
        }
    }
}
