using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed class ShrineDragonPresentation : MonoBehaviour
    {
        readonly Dictionary<string, View> views = new();
        readonly List<string> retired = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var host = new GameObject("Shrine dragon presentation");
            DontDestroyOnLoad(host);
            host.AddComponent<ShrineDragonPresentation>();
        }

        void LateUpdate()
        {
            foreach (var pair in views) retired.Add(pair.Key);
            foreach (var state in DisasterStates.Active)
            {
                var frame = state.Capture();
                if (frame == null || (frame.kind != "FlyingDragon" && frame.kind != "DragonAttack") ||
                    frame.points == null || frame.points.Length < 2) continue;
                if (!views.TryGetValue(frame.kind, out var view))
                    views.Add(frame.kind, view = new View(frame.kind, transform));
                retired.Remove(frame.kind);
                view.Draw(frame);
            }
            foreach (var state in retired) { views[state].Dispose(); views.Remove(state); }
            retired.Clear();
        }

        sealed class View
        {
            readonly Transform root, jaw;
            readonly Transform[] vents = new Transform[2], nostrils = new Transform[2];
            readonly Vector3[] nasalRest = new Vector3[2];
            readonly Animation animation;
            readonly AnimationClip clip;
            readonly ParticleSystem fire, steam;
            readonly List<Material> materials = new();
            float emberCount, mistCount;
            bool positioned;
            readonly Spine spine;       // Jade Coil: follow-the-leader body
            readonly PoseFade fade;     // Bronze Bell: crossfade over clip jumps
            readonly Transform fireSocket;
            Quaternion jawRest;
            float jawOpen;

            public View(string kind, Transform host)
            {
                root = new GameObject(kind + " visual").transform;
                root.SetParent(host, false);
                var asset = Resources.Load<GameObject>("ShrineDragons/" + kind);
                var model = Instantiate(asset, root).transform;
                animation = model.GetComponentInChildren<Animation>();
                if (animation)
                {
                    animation.playAutomatically = false;
                    animation.Stop();
                    foreach (AnimationState state in animation) { clip = state.clip; break; }
                }
                var bones = new Dictionary<string, Transform>();
                foreach (var bone in model.GetComponentsInChildren<Transform>())
                    bones[bone.name.Replace(".", "").Replace("_", "")] = bone;
                Transform Bone(string name) => bones.TryGetValue(name, out var bone) ? bone : null;
                var origin = Bone("StageOrigin");
                var front = Bone("StageForward");
                var up = Bone("StageUp");
                if (origin && front && up)
                {
                    Vector3 f = front.position - origin.position;
                    model.rotation = Quaternion.Inverse(Quaternion.LookRotation(f, up.position - origin.position)) * model.rotation;
                    model.localScale /= Mathf.Max(.00001f, f.magnitude);
                    model.position += root.position - origin.position;
                }
                jaw = Bone("Jaw");
                if (jaw) jawRest = jaw.localRotation;
                fireSocket = Bone("FireSocket");
                for (int i=0; i<2; i++)
                {
                    string side = i==0 ? "L" : "R";
                    vents[i] = Bone("SteamSocket"+side);
                    nostrils[i] = Bone("Nostril"+side);
                    if (nostrils[i]) nasalRest[i] = nostrils[i].localScale;
                }
                bool jade = kind=="FlyingDragon";
                var palette = new Dictionary<string, Material>();
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.updateWhenOffscreen = true;
                    var slots = renderer.sharedMaterials;
                    for (int i=0;i<slots.Length;i++)
                    {
                        string name = slots[i] ? slots[i].name : "Hide";
                        if (!palette.TryGetValue(name, out var mat))
                        {
                            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            var color = Tint(name,jade);
                            mat.color = color;
                            mat.SetFloat("_Metallic",name.Contains("Armor") || name.Contains("Gold") ? .7f : .18f);
                            mat.SetFloat("_Smoothness",name.Contains("Mane") ? .25f : .46f);
                            if(name.Contains("EyeGlow")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",color*5); }
                            palette.Add(name,mat); materials.Add(mat);
                        }
                        slots[i]=mat;
                    }
                    renderer.sharedMaterials=slots;
                }
                fire = Particles("Fire breath",384);
                steam = Particles("Nostril steam",96);
                if (jade) spine = Spine.Build(Bone);
                if (spine == null && animation) fade = new PoseFade(model);
            }

            static Color Tint(string n,bool jade)
            {
                if(n.Contains("EyeGlow"))return new Color(1,.56f,.12f);
                if(n.Contains("Gold"))return new Color(.82f,.54f,.19f);
                if(n.Contains("Ivory"))return new Color(.91f,.76f,.53f);
                if(n.Contains("Shadow"))return new Color(.12f,.064f,.067f);
                if(n.Contains("Armor"))return new Color(.28f,.20f,.135f);
                if(n.Contains("Mane"))return jade?new Color(1,.87f,.65f):new Color(.50f,.105f,.045f);
                if(n.Contains("ScaleLight"))return jade?new Color(.18f,.55f,.49f):new Color(.44f,.28f,.17f);
                return jade?new Color(.07f,.36f,.32f):new Color(.29f,.19f,.14f);
            }

            ParticleSystem Particles(string label,int limit)
            {
                var o = new GameObject(label); o.transform.SetParent(root,false);
                var ps = o.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main; main.simulationSpace=ParticleSystemSimulationSpace.World; main.maxParticles=limit;
                var emission=ps.emission; emission.enabled=false; var shape=ps.shape; shape.enabled=false;
                var renderer=o.GetComponent<ParticleSystemRenderer>();
                var shader=Shader.Find("Sprites/Default");
                var mat=new Material(shader);
                // A soft radial sprite hides billboard corners, also at headset distances.
                mat.mainTexture=SoftDisc(); renderer.sharedMaterial=mat; materials.Add(mat);
                var fade=ps.colorOverLifetime; fade.enabled=true;
                var g=new Gradient(); g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(.6f,.65f),new GradientAlphaKey(0,1)});
                fade.color=g; ps.Play(); return ps;
            }
            static Texture2D disc;
            static Texture2D SoftDisc()
            {
                if(disc)return disc;
                disc=new Texture2D(32,32,TextureFormat.RGBA32,false);
                var pixels=new Color[1024];
                for(int i=0;i<pixels.Length;i++){
                    float r=new Vector2((i%32-15.5f)/16,(i/32-15.5f)/16).magnitude;
                    pixels[i]=new Color(1,1,1,1-Mathf.SmoothStep(0,1,r));
                }
                disc.SetPixels(pixels);disc.Apply(false,true);return disc;
            }

            public void Draw(DisasterFrame frame)
            {
                var actor=frame.points[0]; var breath=frame.points[1];
                float blend=positioned ? 1-Mathf.Exp(-24*Time.deltaTime) : 1;
                root.position=Vector3.Lerp(root.position,actor.position,blend);
                if(actor.velocity.sqrMagnitude>.001f)root.rotation=Quaternion.Slerp(root.rotation,Quaternion.LookRotation(actor.velocity),blend);
                root.localScale=Vector3.one*actor.size;
                if(spine!=null)spine.Pose(root.position,root.forward,actor.size,!positioned);
                else if(clip)
                {
                    float t=actor.life+(actor.id==1 ? 61f/12 : 0);
                    fade?.Before(t);
                    clip.SampleAnimation(animation.gameObject,t);
                    fade?.After(Time.deltaTime);
                }
                positioned=true;
                bool firing=breath.id==1;
                jawOpen=Mathf.MoveTowards(jawOpen,firing?1:0,Time.deltaTime*4);
                if(jaw)
                {
                    if(spine!=null)jaw.localRotation=jawRest;
                    jaw.rotation=Quaternion.AngleAxis(jawOpen*(spine!=null?24:16),root.right)*jaw.rotation;
                }
                Vector3 flameStart=spine!=null && fireSocket ? fireSocket.position : breath.position;
                foreach(var i in sides)if(nostrils[i])nostrils[i].localScale=nasalRest[i]*(1+.22f*breath.size);
                if(firing)
                {
                    Vector3 ray=breath.velocity-flameStart;
                    emberCount+=Mathf.Min(.06f,Time.deltaTime)*230;
                    while(emberCount>=1){
                        emberCount--;
                        float t=Random.value;
                        Vector3 jitter=Random.insideUnitSphere*actor.size*(.04f+.26f*t);
                        var p=new ParticleSystem.EmitParams{
                            position=Vector3.Lerp(flameStart,breath.velocity,t)+jitter,
                            velocity=ray.normalized*Mathf.Min(ray.magnitude*(1-t)*5,actor.size*5),
                            startColor=Color.Lerp(new Color(1,.86f,.31f),new Color(1,.19f,.025f),t),
                            startSize=actor.size*(.21f+.70f*t),startLifetime=.16f};
                        fire.Emit(p,1);
                    }
                }
                mistCount+=Mathf.Min(.06f,Time.deltaTime)*breath.size*32;
                while(mistCount>=1){
                    mistCount--;
                    foreach(var i in sides){
                        Vector3 side=root.right*(i==0?-1:1);
                        var p=new ParticleSystem.EmitParams{
                            position=vents[i]?vents[i].position:breath.position+side*actor.size*.18f,
                            velocity=(root.forward+side*.5f+Vector3.up*.45f+Random.insideUnitSphere*.2f)*actor.size*1.7f,
                            startSize=actor.size*Random.Range(.15f,.34f),startLifetime=.72f,
                            startColor=new Color(.83f,.88f,.91f,.64f)};
                        steam.Emit(p,1);
                    }
                }
            }
            static readonly int[] sides={0,1};
            public void Dispose(){Destroy(root.gameObject);foreach(var mat in materials)Destroy(mat);}
        }

        // Jade Coil's body follows the path its head actually flew: each spine bone sits at its rest
        // distance behind the head along that path, with a travelling wave and banking into turns.
        // Any flight path then reads as one continuous serpentine motion.
        sealed class Spine
        {
            readonly Transform[] bones;
            readonly float[] along;             // rest distance from the head, in model units
            readonly Quaternion[] restFrame;    // undoes each bone's rest (forward, up) frame
            readonly List<Vector3> trail = new();
            readonly List<Vector3> trailUp = new();
            readonly Vector3[] placed;
            readonly float length;
            Vector3 lastForward;
            float bank, wave;

            Spine(Transform[] bones)
            {
                this.bones = bones;
                int n = bones.Length;
                along = new float[n]; restFrame = new Quaternion[n]; placed = new Vector3[n];
                var rest = new Vector3[n];
                for (int i = 0; i < n; i++) rest[i] = bones[i].position;
                for (int i = 1; i < n; i++) along[i] = along[i - 1] + Vector3.Distance(rest[i], rest[i - 1]);
                length = Mathf.Max(.001f, along[n - 1]);
                Vector3 up = Vector3.up;
                for (int i = 0; i < n; i++)
                {
                    Vector3 f = (rest[Mathf.Max(0, i - 1)] - rest[Mathf.Min(n - 1, i + 1)]).normalized;
                    up = Vector3.ProjectOnPlane(up, f);
                    if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.forward, f);
                    up.Normalize();
                    var inv = Quaternion.Inverse(bones[i].rotation);
                    restFrame[i] = Quaternion.Inverse(Quaternion.LookRotation(inv * f, inv * up));
                }
            }

            public static Spine Build(System.Func<string, Transform> bone)
            {
                var list = new List<Transform>();
                for (int i = 0; i < 99; i++)
                {
                    var b = bone("Spine" + i.ToString("00"));
                    if (!b) break;
                    list.Add(b);
                }
                if (list.Count < 4) return null;
                return new Spine(list.ToArray());
            }

            public void Pose(Vector3 head, Vector3 forward, float scale, bool reset)
            {
                float body = length * scale, dt = Mathf.Max(1e-4f, Time.deltaTime);
                // Bank from the turn rate, smoothed so quick jukes roll the body instead of flicking it.
                float yaw = lastForward == Vector3.zero ? 0 : Vector3.SignedAngle(
                    Vector3.ProjectOnPlane(lastForward, Vector3.up), Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.up);
                bank = Mathf.Lerp(bank, Mathf.Clamp(-yaw / dt * .45f, -55, 55), 1 - Mathf.Exp(-3 * dt));
                lastForward = forward;
                Vector3 level = Vector3.ProjectOnPlane(Vector3.up, forward);
                if (level.sqrMagnitude < 1e-6f) level = Vector3.ProjectOnPlane(Vector3.forward, forward);
                Vector3 up = Quaternion.AngleAxis(bank, forward) * level.normalized;
                if (reset || trail.Count == 0)
                {
                    trail.Clear(); trailUp.Clear();
                    for (float d = 0; d <= body * 1.3f; d += scale * .25f) { trail.Add(head - forward * d); trailUp.Add(up); }
                }
                else if ((head - trail[0]).sqrMagnitude > scale * scale * .0004f) { trail.Insert(0, head); trailUp.Insert(0, up); }
                else { trail[0] = head; trailUp[0] = up; }
                // Forget history beyond the tail.
                float run = 0;
                for (int i = 1; i < trail.Count; i++)
                {
                    run += Vector3.Distance(trail[i], trail[i - 1]);
                    if (run > body * 1.3f && i + 1 < trail.Count)
                    {
                        trail.RemoveRange(i + 1, trail.Count - i - 1);
                        trailUp.RemoveRange(i + 1, trailUp.Count - i - 1);
                        break;
                    }
                }
                float speed = trail.Count > 1 ? Mathf.Clamp01((head - trail[1]).magnitude / dt / (scale * 6)) : 0;
                wave += dt * (2.2f + 2.5f * speed);
                // Place every bone along the trail, then add a lateral wave that fades in behind the head.
                for (int i = 0; i < bones.Length; i++)
                {
                    float s = along[i] * scale;
                    Sample(s, out var p, out var u, out var f);
                    float env = Mathf.SmoothStep(0, 1, Mathf.Clamp01(s / (body * .18f))) * (1 - .4f * (s / body));
                    Vector3 side = Vector3.Cross(u, f).normalized;
                    float k = 2 * Mathf.PI * s / (body * .55f) - wave;
                    placed[i] = p + side * (Mathf.Sin(k) * scale * .09f * env) + u * (Mathf.Sin(k + 1.3f) * scale * .035f * env);
                }
                for (int i = 0; i < bones.Length; i++)
                {
                    Sample(along[i] * scale, out _, out var u, out var f);
                    Vector3 dir = i == 0 ? forward : placed[i - 1] - placed[Mathf.Min(bones.Length - 1, i + 1)];
                    if (dir.sqrMagnitude < 1e-8f) dir = f;
                    bones[i].SetPositionAndRotation(placed[i], Quaternion.LookRotation(dir.normalized, u) * restFrame[i]);
                }
            }

            void Sample(float distance, out Vector3 p, out Vector3 up, out Vector3 forward)
            {
                float run = 0;
                for (int i = 1; i < trail.Count; i++)
                {
                    float seg = Vector3.Distance(trail[i], trail[i - 1]);
                    if (run + seg >= distance || i == trail.Count - 1)
                    {
                        float t = seg > 1e-5f ? Mathf.Clamp01((distance - run) / seg) : 0;
                        p = Vector3.Lerp(trail[i - 1], trail[i], t);
                        up = Vector3.Slerp(trailUp[i - 1], trailUp[i], t);
                        forward = seg > 1e-5f ? (trail[i - 1] - trail[i]) / seg : Vector3.forward;
                        return;
                    }
                    run += seg;
                }
                p = trail[0]; up = trailUp[0]; forward = Vector3.forward;
            }
        }

        // Bronze Bell plays baked phases from one clip. When playback jumps (a phase repeats or changes),
        // blend from the last shown pose over a third of a second so the pose never pops.
        sealed class PoseFade
        {
            readonly Transform[] bones;
            readonly Vector3[] fromPos;
            readonly Quaternion[] fromRot;
            float last = -1, weight = 1;

            public PoseFade(Transform model)
            {
                bones = model.GetComponentsInChildren<Transform>();
                fromPos = new Vector3[bones.Length]; fromRot = new Quaternion[bones.Length];
            }

            public void Before(float time)
            {
                if (last >= 0 && (time < last - .02f || time > last + .5f))
                {
                    for (int i = 0; i < bones.Length; i++) { fromPos[i] = bones[i].localPosition; fromRot[i] = bones[i].localRotation; }
                    weight = 0;
                }
                last = time;
            }

            public void After(float dt)
            {
                if (weight >= 1) return;
                weight = Mathf.Min(1, weight + dt / .35f);
                float w = Mathf.SmoothStep(0, 1, weight);
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i].localPosition = Vector3.Lerp(fromPos[i], bones[i].localPosition, w);
                    bones[i].localRotation = Quaternion.Slerp(fromRot[i], bones[i].localRotation, w);
                }
            }
        }
    }
}
