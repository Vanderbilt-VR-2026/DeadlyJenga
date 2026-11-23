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
                root.localScale=Vector3.one*actor.size; positioned=true;
                if(clip)clip.SampleAnimation(animation.gameObject,actor.life+(actor.id==1 ? 61f/12 : 0));
                bool firing=breath.id==1;
                if(clip && jaw && firing)jaw.rotation=Quaternion.AngleAxis(16,root.right)*jaw.rotation;
                foreach(var i in sides)if(nostrils[i])nostrils[i].localScale=nasalRest[i]*(1+.22f*breath.size);
                if(firing)
                {
                    Vector3 ray=breath.velocity-breath.position;
                    emberCount+=Mathf.Min(.06f,Time.deltaTime)*230;
                    while(emberCount>=1){
                        emberCount--;
                        float t=Random.value;
                        Vector3 jitter=Random.insideUnitSphere*actor.size*(.04f+.26f*t);
                        var p=new ParticleSystem.EmitParams{
                            position=Vector3.Lerp(breath.position,breath.velocity,t)+jitter,
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
    }
}
