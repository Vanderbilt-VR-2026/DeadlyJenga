using System.Collections.Generic;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyPoweredSuitFeedback : MonoBehaviour
    {
        sealed class Jet
        {
            public Transform root, core, envelope;
            public LineRenderer filament;
        }

        struct Pulse
        {
            public LineRenderer line;
            public Vector3 point, right, up;
            public float start, lifetime, radius;
        }

        const int RingSegments = 24;
        readonly Dictionary<string, Jet[]> jets = new Dictionary<string, Jet[]>();
        readonly HashSet<string> visible = new HashSet<string>();
        readonly CandyEquipmentActionCursor cursor = new CandyEquipmentActionCursor();
        readonly Pulse[] pulses = new Pulse[20];
        IMatchRuntime runtime;
        Material glow, ribbon, sparkSurface;
        ParticleSystem impactSparks;
        int nextPulse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += host =>
        {
            if (!host.GetComponent<CandyPoweredSuitFeedback>()) host.AddComponent<CandyPoweredSuitFeedback>();
        };

        public int ActiveSuits { get; private set; }

        void Awake()
        {
            glow = Resources.Load<Material>("CandyUtilityGlow");
            var shader = Resources.Load<Shader>("CandyOrdnance");
            if (shader)
            {
                ribbon = new Material(shader) { name = "Powered suit cyan energy ribbons" };
                ribbon.SetFloat("_Ribbon", 1);
                sparkSurface = new Material(shader) { name = "Powered suit impact glints" };
            }
        }

        void Update()
        {
            visible.Clear();
            ActiveSuits = 0;
            var match = MatchRuntime.Current;
            if (!ReferenceEquals(runtime, match))
            {
                runtime = match;
                cursor.Baseline(match?.Frame);
                ClearPulses();
            }
            if (match?.Frame.phase == RoundPhase.Playing)
            {
                cursor.Read(match.Frame, PresentAction);
                foreach (var state in match.Frame.equipment)
                {
                    if (state.kind != EquipmentKind.PoweredArmor || !state.drawn || !state.thrust) continue;
                    var actor = match.FindActor(state.actor);
                    if (actor == null || !actor.Alive || !glow) continue;
                    visible.Add(state.actor);
                    ActiveSuits++;
                    if (!jets.TryGetValue(state.actor, out var flames))
                    {
                        flames = CreateJets();
                        jets.Add(state.actor, flames);
                    }
                    UpdateJets(actor, state.leftHanded, flames);
                    CandyAudioEngine.Instance?.Loop(CandySound.SuitThruster,actor.transform,.8f,actor.IsLocal);
                }
            }
            foreach (var pair in jets)
                if (!visible.Contains(pair.Key))
                    foreach (var jet in pair.Value) jet.root.gameObject.SetActive(false);
            UpdatePulses();
        }

        static float SuitScale(IModeActor actor) =>
            Mathf.Clamp((actor.ViewPose.position.y-actor.transform.position.y)/1.6f,.2f,3);

        void UpdateJets(IModeActor actor, bool left, Jet[] flames)
        {
            float scale = SuitScale(actor);
            var yaw = Quaternion.Euler(0,actor.ViewPose.rotation.eulerAngles.y,0);
            for (int i=0; i<3; i++)
            {
                bool palm = i==2;
                var pose = palm ? actor.HandPose(left) :
                    new Pose(actor.transform.position+yaw*new Vector3(i==0?-.12f:.12f,.018f,.075f)*scale,Quaternion.identity);
                var direction = palm ? pose.rotation*Vector3.down : Vector3.down;
                float pulse = 1 + Mathf.Sin(Time.time*43+i*2)*.12f;
                float length = (palm ? .42f : .67f)*pulse;
                float width = palm ? .046f : .072f;
                var jet = flames[i];
                jet.root.SetPositionAndRotation(pose.position,Quaternion.FromToRotation(Vector3.up,direction));
                jet.root.localScale = Vector3.one*scale;
                jet.root.gameObject.SetActive(true);
                jet.core.localPosition = Vector3.up*(length*.34f);
                jet.core.localScale = new Vector3(width*.60f,length*.76f,width*.60f);
                jet.envelope.localPosition = Vector3.up*(length*.38f);
                jet.envelope.localScale = new Vector3(width,length,width);
                if (!jet.filament) continue;
                for (int n=0; n<jet.filament.positionCount; n++)
                {
                    float t = n/(jet.filament.positionCount-1f);
                    float sweep = Time.time*20+i+t*1.8f;
                    float radius = width*(.52f+t*.52f);
                    jet.filament.SetPosition(n,new Vector3(Mathf.Cos(sweep)*radius,length*t,Mathf.Sin(sweep)*radius));
                }
            }
        }

        Jet[] CreateJets()
        {
            var result = new Jet[3];
            for (int i=0; i<result.Length; i++)
            {
                var root = new GameObject("Visual - powered suit jet").transform;
                root.SetParent(transform,false);
                var jet = new Jet
                {
                    root = root,
                    envelope = JetMesh(root,"Cyan exhaust envelope",new Color(.095f,.49f,1f)),
                    core = JetMesh(root,"White-hot exhaust core",new Color(.58f,.87f,1f))
                };
                if (ribbon)
                {
                    jet.filament = MakeLine(root,"Tapered exhaust filament",6,false);
                    jet.filament.startColor = new Color(.27f,.76f,1,.66f);
                    jet.filament.endColor = new Color(.13f,.38f,1,0);
                    jet.filament.widthMultiplier = .025f;
                    jet.filament.widthCurve = AnimationCurve.EaseInOut(0,1,1,0);
                }
                result[i] = jet;
            }
            return result;
        }

        Transform JetMesh(Transform parent, string name, Color tint)
        {
            var go = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor",tint);
            properties.SetColor("_EmissionColor",tint*2);
            renderer.SetPropertyBlock(properties);
            return go.transform;
        }

        LineRenderer MakeLine(Transform owner, string name, int points, bool world)
        {
            var go = new GameObject(name,typeof(LineRenderer));
            go.transform.SetParent(owner,false);
            var line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = ribbon;
            line.positionCount = points;
            line.useWorldSpace = world;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        void PresentAction(EquipmentActionFrame action)
        {
            if (action.kind != EquipmentKind.PoweredArmor || !ribbon ||
                !CandyWeaponImpacts.VisibleToAnyCamera(action.position,85)) return;
            var actor = runtime.FindActor(action.actor);
            switch(action.action)
            {
                case EquipmentAction.Pickup:
                case EquipmentAction.Swap:
                    if (!(runtime is IEquipmentRuntime equipment) || !equipment.EquipmentFor(action.actor).drawn) return;
                    goto case EquipmentAction.Draw;
                case EquipmentAction.Draw:
                    if (actor == null) return;
                    float scale = SuitScale(actor);
                    var chest = actor.transform.position+actor.transform.forward*(.21f*scale)+Vector3.up*(1.18f*scale);
                    Ring(chest,actor.transform.forward,.24f*scale,.22f);
                    break;
                case EquipmentAction.Shot:
                    var forward = actor == null ? Vector3.forward : actor.ViewPose.rotation*Vector3.forward;
                    Ring(action.position+forward*.025f,forward,.14f,.15f);
                    Ring(action.position+forward*.097f,forward,.23f,.19f);
                    break;
                case EquipmentAction.Impact:
                    var outward = actor == null ? Vector3.up : (actor.ViewPose.position-action.position).normalized;
                    Ring(action.position+outward*.02f,outward,.39f,.24f);
                    Ring(action.position+outward*.06f,outward,.27f,.17f);
                    ImpactGlints(action.position,outward);
                    break;
            }
        }

        void Ring(Vector3 point, Vector3 axis, float radius, float lifetime)
        {
            if (axis.sqrMagnitude<.01f) axis=Vector3.forward;
            var right=Vector3.Cross(axis,Vector3.up);
            if (right.sqrMagnitude<.01f) right=Vector3.Cross(axis,Vector3.right);
            right.Normalize();
            var line = pulses[nextPulse].line;
            if (!line) line=MakeLine(transform,"Powered suit expanding energy ring",RingSegments+1,true);
            line.enabled = true;
            pulses[nextPulse]=new Pulse { line=line, point=point, right=right, up=Vector3.Cross(axis,right).normalized,
                start=Time.time, lifetime=lifetime, radius=radius };
            nextPulse=(nextPulse+1)%pulses.Length;
        }

        void UpdatePulses()
        {
            foreach (var pulse in pulses)
            {
                if (!pulse.line || !pulse.line.enabled) continue;
                float t=(Time.time-pulse.start)/pulse.lifetime;
                if (t>=1) { pulse.line.enabled=false; continue; }
                var color=new Color(.31f,.82f,1,(1-t)*.83f);
                pulse.line.startColor=color;
                pulse.line.endColor=color;
                pulse.line.widthMultiplier=Mathf.Lerp(.026f,.005f,t);
                float radius=pulse.radius*Mathf.Lerp(.29f,1,t);
                for (int i=0; i<=RingSegments; i++)
                {
                    float a=i*2*Mathf.PI/RingSegments;
                    pulse.line.SetPosition(i,pulse.point+radius*(pulse.right*Mathf.Cos(a)+pulse.up*Mathf.Sin(a)));
                }
            }
        }

        void ImpactGlints(Vector3 point, Vector3 direction)
        {
            if (!sparkSurface) return;
            if (!impactSparks)
            {
                var go=new GameObject("Pooled Powered Iron Suit impact glints",typeof(ParticleSystem));
                go.transform.SetParent(transform,false);
                impactSparks=go.GetComponent<ParticleSystem>();
                impactSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=impactSparks.main;
                main.loop=false;
                main.playOnAwake=false;
                main.maxParticles=128;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=impactSparks.emission; emission.enabled=false;
                var shape=impactSparks.shape; shape.enabled=false;
                var renderer=go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial=sparkSurface;
                renderer.renderMode=ParticleSystemRenderMode.Stretch;
                renderer.lengthScale=1.4f;
                renderer.velocityScale=.1f;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            if (impactSparks.isStopped) impactSparks.Play();
            for(int i=0;i<12;i++)
            {
                var scatter=Quaternion.Euler(47*i,131*i,73*i)*Vector3.right;
                impactSparks.Emit(new ParticleSystem.EmitParams
                {
                    position=point+direction*.025f,
                    velocity=(scatter+direction*.38f).normalized*(1.1f+i*.26f),
                    startSize=.052f+i*.002f,
                    startLifetime=.115f+i*.011f,
                    startColor=new Color(.29f,.85f,1)
                },1);
            }
        }

        void ClearPulses()
        {
            foreach (var pulse in pulses) if (pulse.line) pulse.line.enabled=false;
            if (impactSparks) impactSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnDisable()
        {
            foreach (var pair in jets) foreach (var jet in pair.Value) jet.root.gameObject.SetActive(false);
            ClearPulses();
            ActiveSuits=0;
        }

        void OnDestroy()
        {
            if (ribbon) Destroy(ribbon);
            if (sparkSurface) Destroy(sparkSurface);
        }
    }
}
