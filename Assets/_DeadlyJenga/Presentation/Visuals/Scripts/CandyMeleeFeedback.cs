using DeadlyJenga.CandyAudio;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    [DefaultExecutionOrder(351)]
    public sealed class CandyMeleeFeedback : MonoBehaviour
    {
        enum Shape { Thrust, Slash, Cleave, Punch, Teeth, Claws, Ember, DaggerPlayer, DaggerBlock }

        struct Strike
        {
            public LineRenderer line;
            public float start, duration, width, size;
            public int lane;
            public bool hit;
            public Shape shape;
            public Color color;
            public Vector3 point, axis, side, up;
        }

        const int Segments = 12;
        readonly Strike[] strikes = new Strike[16];
        readonly CandyEquipmentActionCursor cursor = new CandyEquipmentActionCursor();
        IMatchRuntime runtime;
        Material ribbon;
        int next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += host =>
        {
            if (!host.GetComponent<CandyMeleeFeedback>()) host.AddComponent<CandyMeleeFeedback>();
        };

        void Awake()
        {
            var shader = Resources.Load<Shader>("CandyOrdnance");
            if (!shader) shader = Resources.Load<Material>("CandyUtilityGlow")?.shader;
            if (!shader) { enabled = false; return; }
            ribbon = new Material(shader);
            if (ribbon.HasProperty("_Ribbon")) ribbon.SetFloat("_Ribbon", 1);
        }

        void Update()
        {
            if (!ReferenceEquals(runtime, MatchRuntime.Current))
            {
                runtime = MatchRuntime.Current;
                cursor.Baseline(runtime?.Frame);
                foreach (var strike in strikes)
                    if (strike.line) strike.line.enabled = false;
            }

            cursor.Read(runtime?.Frame, Present);
            foreach (var strike in strikes)
            {
                if (!strike.line || !strike.line.enabled) continue;
                float t = (Time.time - strike.start) / strike.duration;
                if (t >= 1) { strike.line.enabled = false; continue; }
                Paint(strike, t);
            }
        }

        void Present(EquipmentActionFrame shot)
        {
            if(shot.kind==EquipmentKind.Dagger&&shot.action is (EquipmentAction.Shot or EquipmentAction.Impact))
            {
                PresentDagger(shot);
                return;
            }
            var spec = EquipmentSpecifications.Get(shot.kind);
            bool bash = shot.action == EquipmentAction.Bash;
            if (!bash && (shot.action != EquipmentAction.Shot || !spec.melee || shot.shotgun)) return;
            var camera = Services.Session?.ActiveCamera;
            if (!camera || (camera.transform.position - shot.position).sqrMagnitude > 45 * 45) return;

            bool hasEndpoint = shot.endpoints != null && shot.endpoints.Length > 0;
            var travel = hasEndpoint ? shot.endpoints[0] - shot.position : Vector3.zero;
            bool hit = hasEndpoint && (bash || travel.magnitude < spec.range - .015f);
            var forward = (runtime.FindActor(shot.actor)?.ViewPose.rotation ?? Quaternion.identity) * Vector3.forward;
            var axis = travel.sqrMagnitude > .0001f ? travel.normalized : forward;
            var side = Vector3.Cross(Vector3.up, axis);
            if (side.sqrMagnitude < .01f) side = Vector3.Cross(Vector3.forward, axis);
            side.Normalize();
            var up = Vector3.Cross(axis, side).normalized;

            var profile = shot.kind switch
            {
                EquipmentKind.MurderSword => (Shape.Slash, new Color(1f, .19f, .32f), .24f, .074f, .59f),
                EquipmentKind.BoxingGlove => (Shape.Punch, new Color(1f, .76f, .24f), .24f, .11f, .51f),
                EquipmentKind.BarbedBat => (Shape.Slash, new Color(1f, .48f, .13f), .26f, .11f, .64f),
                EquipmentKind.SawStaff => (Shape.Teeth, new Color(1f, .84f, .35f), .34f, .045f, .44f),
                EquipmentKind.Chainsaw => (Shape.Teeth, new Color(1f, .39f, .09f), .30f, .065f, .37f),
                EquipmentKind.LaserAxe => (Shape.Cleave, new Color(.23f, .91f, 1f), .34f, .14f, .77f),
                EquipmentKind.LaserClaws => (Shape.Claws, new Color(.33f, .85f, 1f), .23f, .055f, .38f),
                _ => (Shape.Ember, new Color(1f, .29f, .055f), .36f, .075f, .36f)
            };
            if (bash) profile = (Shape.Punch, new Color(1, .82f, .44f), .22f, .075f, .29f);
            var point = hit ? shot.endpoints[0] : shot.position + axis * (bash ? .65f : spec.range * .58f);
            int lanes = profile.Item1 == Shape.Claws ? 3 : hit ? 2 : 1;
            for(int lane=0;lane<lanes;lane++)
                SpawnStrike(profile.Item1,profile.Item2,point,axis,side,up,hit,lane,
                    profile.Item3,profile.Item4,profile.Item5,"Visual - melee strike");
        }

        void PresentDagger(EquipmentActionFrame action)
        {
            if(action.remaining<.55f||!CandyWeaponImpacts.VisibleToAnyCamera(action.position,45))return;
            var actor=runtime.FindActor(action.actor);
            var tint=actor?.Color??Color.white;
            var forward=(actor?.ViewPose.rotation??Quaternion.identity)*Vector3.forward;
            bool impact=action.action==EquipmentAction.Impact;
            var offset=action.endpoints!=null&&action.endpoints.Length>0?
                action.endpoints[0]-action.position:Vector3.zero;
            var axis=offset.sqrMagnitude>.0001f?offset.normalized:forward;
            var point=action.position;
            var shape=Shape.Thrust;
            int lanes=1;
            string name="Visual - dagger trail";
            if(impact)
            {
                bool solid=CandyWeaponImpacts.TryContact(point,axis,out var surface,.23f);
                if(solid)
                {
                    var frame=surface.anchor.localToWorldMatrix*surface.localFrame;
                    point=surface.Position;
                    axis=((Vector3)frame.GetColumn(2)).normalized;
                }
                else axis=-axis;
                shape=solid?Shape.DaggerBlock:Shape.DaggerPlayer;
                name=solid?"Visual - dagger block sparks":"Visual - dagger player sparks";
                lanes=solid?6:4;
            }
            else
            {
                point+=axis*Mathf.Min(offset.magnitude,.70f);
                tint=Color.Lerp(tint,Color.white,.18f);
            }
            var side=Vector3.Cross(Vector3.up,axis);
            if(side.sqrMagnitude<.01f)side=Vector3.Cross(Vector3.forward,axis);
            side.Normalize();
            var up=Vector3.Cross(axis,side);
            for(int lane=0;lane<lanes;lane++)
                SpawnStrike(shape,tint,point,axis,side,up,impact,lane,
                    impact?.24f:.17f,impact?.022f:.030f,impact?.30f:.35f,name);
        }

        void SpawnStrike(Shape shape,Color color,Vector3 point,Vector3 axis,Vector3 side,Vector3 up,
            bool hit,int lane,float duration,float width,float size,string name)
        {
            var line=strikes[next].line;
            if(!line)
            {
                var go=new GameObject(name);
                go.transform.SetParent(transform,false);
                line=go.AddComponent<LineRenderer>();
                line.sharedMaterial=ribbon;
                line.positionCount=Segments;
                line.shadowCastingMode=ShadowCastingMode.Off;
                line.receiveShadows=false;
            }
            line.gameObject.name=name;
            line.enabled=true;
            strikes[next]=new Strike
            {
                line=line,start=Time.time,duration=duration,color=color,width=width,size=size,
                shape=shape,hit=hit,axis=axis,side=side,up=up,point=point,lane=lane
            };
            next=(next+1)%strikes.Length;
        }

        static void Paint(Strike strike, float t)
        {
            float fade = (1 - t) * (strike.hit ? 1 : .62f);
            var color = strike.color;
            color.a = fade;
            strike.line.startColor = color;
            strike.line.endColor = new Color(color.r, color.g, color.b, fade * .22f);
            strike.line.widthMultiplier = strike.width * (.95f - .70f * t);

            for (int i = 0; i < Segments; i++)
            {
                float u = i / (Segments - 1f);
                float arc = Mathf.Lerp(-1.15f, 1.15f, u);
                float orbit = u * 2 * Mathf.PI;
                float radius = strike.size * Mathf.Lerp(.44f, 1, t);
                float sparkAngle=strike.lane*2.39996f;
                var spark=(strike.side*Mathf.Cos(sparkAngle)+strike.up*Mathf.Sin(sparkAngle));
                Vector3 offset = strike.shape switch
                {
                    Shape.DaggerBlock => (spark+strike.axis*.32f)*radius*(t*.77f+u*.38f)+Vector3.down*(.060f*t*t),
                    Shape.DaggerPlayer => (spark*.64f+strike.axis*.52f)*radius*(t*.59f+u*.41f),
                    Shape.Thrust => strike.axis * Mathf.Lerp(-radius * 1.5f, radius * .23f, u),
                    Shape.Punch => (strike.side * Mathf.Cos(orbit) + strike.up * Mathf.Sin(orbit)) * radius,
                    Shape.Cleave => strike.up * (Mathf.Sin(arc) * radius) + strike.axis * ((Mathf.Cos(arc) - 1) * radius),
                    Shape.Teeth => (strike.side * Mathf.Cos(orbit) + strike.up * Mathf.Sin(orbit)) * radius * (i % 2 == 0 ? .72f : 1),
                    Shape.Claws => strike.side * ((strike.lane - 1) * .13f + (u - .5f) * radius) +
                        strike.up * ((.5f - u) * radius) + strike.axis * ((Mathf.Sin(u * Mathf.PI) - 1) * radius),
                    Shape.Ember => (strike.side * Mathf.Cos(orbit) + strike.up * Mathf.Sin(orbit)) * radius + strike.up * (u * radius + t * .28f),
                    _ => strike.side * (Mathf.Sin(arc) * radius) + strike.axis * ((Mathf.Cos(arc) - 1) * radius)
                };
                if (strike.lane == 1 && strike.shape is not (Shape.Claws or Shape.DaggerBlock or Shape.DaggerPlayer))
                    offset = offset * .64f + strike.axis * (.12f * t) + strike.up * (.09f + .16f * t);
                strike.line.SetPosition(i, strike.point + offset);
            }
        }

        void OnDestroy()
        {
            if (ribbon) Destroy(ribbon);
        }
    }
}
