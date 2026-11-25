using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    // Ocean splashes for the sea life, drawn with the tsunami spray material:
    // spray (fast droplets streaked along their flight), mist (big soft clouds that hang and drift)
    // and foam (flat white patches that spread on the water and fade).
    internal sealed class SeaLifeSplash
    {
        readonly ParticleSystem spray, mist, foam;
        readonly System.Random random = new(5);

        public SeaLifeSplash(Transform parent)
        {
            var material = Resources.Load<Material>("Tsunami/Spray");
            spray = Make(parent, "Sea spray", material, 900, 1f, ParticleSystemRenderMode.Stretch);
            var r = spray.GetComponent<ParticleSystemRenderer>();
            r.velocityScale = .045f; r.lengthScale = 1.2f;
            mist = Make(parent, "Sea mist", material, 160, .05f, ParticleSystemRenderMode.Billboard);
            foam = Make(parent, "Sea foam", material, 120, 0, ParticleSystemRenderMode.HorizontalBillboard);
            var grow = mist.sizeOverLifetime; grow.enabled = true; grow.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .55f, 1, 1.5f));
            var spread = foam.sizeOverLifetime; spread.enabled = true; spread.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .3f), new Keyframe(.3f, .85f), new Keyframe(1, 1)));
            Fade(spray, .9f, .55f); Fade(mist, .55f, .4f); Fade(foam, .85f, .5f);
        }

        static ParticleSystem Make(Transform parent, string name, Material material, int max, float gravity, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = gravity;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material; r.renderMode = mode; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.maxParticleSize = 50;
            return ps;
        }

        static void Fade(ParticleSystem ps, float peak, float holdUntil)
        {
            var c = ps.colorOverLifetime; c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(peak, .06f), new GradientAlphaKey(peak * .8f, holdUntil), new GradientAlphaKey(0, 1) });
            c.color = g;
        }

        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

        void Emit(ParticleSystem ps, Vector3 at, Vector3 velocity, float size, float life, Color color)
        {
            if (!ps.isPlaying) ps.Play();
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = at, velocity = velocity, startSize = size, startLifetime = life, startColor = color,
                rotation = Range(0, 360), randomSeed = (uint)random.Next(1, int.MaxValue)
            }, 1);
        }

        static readonly Color White = new(.97f, .99f, 1f, 1f), Sea = new(.78f, .92f, .96f, 1f);

        // A splash crown: droplets thrown up and out around an ellipse (halfLength along `along`, halfWidth across).
        public void Crown(Vector3 centre, Vector3 along, float halfLength, float halfWidth, int count, float speed, float dropSize)
        {
            along.y = 0; along = along.sqrMagnitude > 0 ? along.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, along);
            for (int i = 0; i < count; i++)
            {
                float a = Range(0, Mathf.PI * 2), rim = Mathf.Sqrt(Range(.2f, 1));
                var outward = (along * Mathf.Cos(a) + side * Mathf.Sin(a)).normalized;
                var at = centre + (along * (Mathf.Cos(a) * halfLength) + side * (Mathf.Sin(a) * halfWidth)) * rim;
                var v = outward * (Range(.1f, .5f) * speed) + Vector3.up * (Range(.55f, 1.05f) * speed);
                Emit(spray, at, v, dropSize * Range(.5f, 1.6f), Range(1.4f, 2.8f), Color.Lerp(Sea, White, Range(.3f, 1)));
            }
        }

        // A tall column / wall of white water straight up from a point.
        public void Column(Vector3 at, float radius, int count, float speed, float dropSize)
        {
            for (int i = 0; i < count; i++)
            {
                var off = Random.insideUnitCircle * radius;
                Emit(spray, at + new Vector3(off.x, 0, off.y), new Vector3(off.x * .6f, Range(.6f, 1.1f) * speed, off.y * .6f), dropSize * Range(.8f, 2f), Range(1.6f, 3f), White);
            }
        }

        public void Mist(Vector3 at, float radius, int count, float size, float rise)
        {
            for (int i = 0; i < count; i++)
            {
                var off = Random.insideUnitCircle * radius;
                Emit(mist, at + new Vector3(off.x, Range(0, size * .4f), off.y), new Vector3(off.x * .15f, Range(.4f, 1f) * rise, off.y * .15f), size * Range(.7f, 1.3f), Range(4f, 7f), new Color(.92f, .97f, 1f, 1f));
            }
        }

        public void Foam(Vector3 at, float radius, int count, float size, float life)
        {
            for (int i = 0; i < count; i++)
            {
                var off = Random.insideUnitCircle * radius;
                Emit(foam, at + new Vector3(off.x, .15f, off.y), Vector3.zero, size * Range(.7f, 1.3f), life * Range(.8f, 1.2f), new Color(.95f, .98f, 1f, 1f));
            }
        }

        // Water running off a body as it rises: droplets falling from points along it.
        public void Drip(Vector3 at, float spread, int count)
        {
            for (int i = 0; i < count; i++)
                Emit(spray, at + Random.insideUnitSphere * spread, new Vector3(Range(-1, 1), Range(-2, 1), Range(-1, 1)), Range(.4f, 1.1f), Range(1f, 2f), White);
        }

        public void Clear()
        {
            spray.Clear(); mist.Clear(); foam.Clear();
        }
    }
}
