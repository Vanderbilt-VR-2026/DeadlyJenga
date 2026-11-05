using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.Presentation
{
    // Cosmetic contacts are resolved only beside authoritative endpoints.
    // An unresolved endpoint still keeps its existing travel/impact effects.
    [DefaultExecutionOrder(310)]
    public sealed class CandyWeaponImpacts : MonoBehaviour
    {
        const int Capacity=96, SurfaceBudget=12;
        static readonly Collider[] Nearby=new Collider[8];
        static int queryFrame=-1,queries,viewFrame=-1,viewCount;
        static Camera[] views=new Camera[16];
        readonly CandyEquipmentActionCursor cursor=new CandyEquipmentActionCursor();
        readonly List<Mark> marks=new List<Mark>(Capacity);
        readonly Dictionary<(int,uint),MarkBatch> batches=new Dictionary<(int,uint),MarkBatch>();
        readonly List<(int,uint)> emptyBatches=new List<(int,uint)>();
        Material surfaceMaterial,flashMaterial;
        Mesh quad;
        ParticleSystem flashes;
        int flashFrame=-1,flashCount;
        public int ActiveMarks=>marks.Count;

        internal struct SurfaceContact
        {
            public Transform anchor;
            public Matrix4x4 localFrame;
            public int layer;
            public uint renderingMask;
            public Vector3 Position=>anchor?anchor.localToWorldMatrix.MultiplyPoint3x4(localFrame.GetColumn(3)):Vector3.zero;
        }
        struct Mark
        {
            public Transform anchor;
            public Matrix4x4 localPose;
            public Color tint;
            public float born,life,style;
            public int layer;
            public uint renderingMask;
        }
        sealed class MarkBatch
        {
            public readonly Matrix4x4[] matrices=new Matrix4x4[Capacity];
            public readonly Vector4[] colors=new Vector4[Capacity];
            public readonly Vector4[] settings=new Vector4[Capacity];
            public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
            public int count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetQueries(){queryFrame=viewFrame=-1;queries=viewCount=0;System.Array.Clear(views,0,views.Length);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=host=>
        {
            if(!host.GetComponent<CandyWeaponImpacts>())host.AddComponent<CandyWeaponImpacts>();
        };

        public static bool VisibleToAnyCamera(Vector3 point,float range)
        {
            if(!Finite(point)||range<=0)return false;
            if(viewFrame!=Time.frameCount)
            {
                viewFrame=Time.frameCount;
                int count=Camera.allCamerasCount;
                if(count>views.Length)views=new Camera[Mathf.NextPowerOfTwo(count)];
                viewCount=Camera.GetAllCameras(views);
            }
            float distance=range*range;
            int worldMask=1|(1<<Layers.Block)|(1<<Layers.Surface)|(1<<Layers.Survivor);
            for(int i=0;i<viewCount;i++)
            {
                var camera=views[i];
                if(camera&&camera.isActiveAndEnabled&&camera.cameraType==CameraType.Game&&
                    (camera.cullingMask&worldMask)!=0&&(camera.transform.position-point).sqrMagnitude<=distance)return true;
            }
            var fallback=Services.Session?.ActiveCamera;
            return fallback&&fallback.isActiveAndEnabled&&fallback.cameraType==CameraType.Game&&
                (fallback.cullingMask&worldMask)!=0&&(fallback.transform.position-point).sqrMagnitude<=distance;
        }

        void Awake()
        {
            var shader=Resources.Load<Shader>("CandyImpactMark");
            if(shader)surfaceMaterial=new Material(shader){name="Shared temporary weapon surface marks",enableInstancing=true};
            quad=new Mesh
            {
                name="Shared weapon surface mark",
                vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)},
                uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up},
                triangles=new[]{0,2,1,0,3,2}
            };
            var ordnance=Resources.Load<Shader>("CandyOrdnance");
            if(ordnance)
            {
                flashMaterial=new Material(ordnance);
                var emitter=new GameObject("Shared ranged muzzle pulses");emitter.transform.SetParent(transform,false);
                flashes=emitter.AddComponent<ParticleSystem>();flashes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=flashes.main;main.playOnAwake=false;main.loop=false;main.maxParticles=128;
                main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=flashes.emission;emission.enabled=false;
                var shape=flashes.shape;shape.enabled=false;
                var collision=flashes.collision;collision.enabled=false;
                var renderer=emitter.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial=flashMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                var fade=flashes.colorOverLifetime;fade.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
                fade.color=gradient;
            }
            cursor.Baseline(MatchRuntime.Current?.Frame);
        }

        static bool Ranged(EquipmentActionFrame action)
        {
            var spec=EquipmentSpecifications.Get(action.kind);
            if(action.kind==EquipmentKind.Chainsaw&&action.action==EquipmentAction.Impact)return true;
            return action.kind!=EquipmentKind.None&&!spec.power&&
                (!spec.melee||action.shotgun&&(action.kind==EquipmentKind.Chainsaw||action.kind==EquipmentKind.LaserAxe));
        }
        static Color Tint(EquipmentKind kind)
        {
            if(kind==EquipmentKind.Blaster||kind==EquipmentKind.PoweredArmor)return new Color(.1f,.85f,1);
            if(kind==EquipmentKind.GrenadeLauncher)return new Color(.54f,.28f,1);
            if(kind==EquipmentKind.DualUzi)return new Color(.3f,1,.035f);
            if(kind==EquipmentKind.WristLaser||kind==EquipmentKind.LaserAxe)return new Color(1,.24f,.025f);
            return new Color(1,.57f,.18f);
        }
        void Update()
        {
            cursor.Read(MatchRuntime.Current?.Frame,Action);
        }
        void Action(EquipmentActionFrame action)
        {
            if(!Ranged(action))return;
            if(action.action==EquipmentAction.Shot)
            {
                // BulletTrails already owns hitscan muzzle pulses and arrival timing.
                if(action.kind==EquipmentKind.Blaster||action.kind==EquipmentKind.PoweredArmor||
                    action.kind==EquipmentKind.RocketLauncher||action.kind==EquipmentKind.GrenadeLauncher||
                    action.kind==EquipmentKind.DualUzi||action.kind==EquipmentKind.WristLaser||
                    action.kind==EquipmentKind.LaserAxe||action.kind==EquipmentKind.Chainsaw)
                    Muzzle(action);
                if((action.kind==EquipmentKind.WristLaser||action.kind==EquipmentKind.LaserAxe&&action.shotgun)&&
                    action.endpoints!=null&&action.endpoints.Length>0&&
                    (action.endpoints[0]-action.position).sqrMagnitude<34.88f*34.88f)
                    Surface(action.endpoints[0],action.endpoints[0]-action.position,action.kind);
            }
            else if(action.action==EquipmentAction.Impact)
                Surface(action.position,Vector3.zero,action.kind);
        }

        void Muzzle(EquipmentActionFrame action)
        {
            if(!flashes||!Finite(action.position))return;
            if(!VisibleToAnyCamera(action.position,90))return;
            if(flashFrame!=Time.frameCount){flashFrame=Time.frameCount;flashCount=0;}
            if(flashCount++>=16)return;
            if(flashes.isStopped)flashes.Play();
            Vector3 direction=Vector3.zero;
            if(action.endpoints!=null&&action.endpoints.Length>0)direction=(action.endpoints[0]-action.position).normalized;
            var color=Tint(action.kind);
            int count=action.kind==EquipmentKind.RocketLauncher?7:3;
            for(int i=0;i<count;i++)
            {
                var radial=new Vector3(Mathf.Sin(i*2.4f),Mathf.Cos(i*2.4f),.2f);
                flashes.Emit(new ParticleSystem.EmitParams
                {
                    position=action.position,velocity=direction*1.3f+radial*.28f,
                    startColor=color,startSize=i==0?.16f:.075f,startLifetime=i==0?.075f:.12f
                },1);
            }
        }

        internal void Surface(Vector3 point,Vector3 incoming,EquipmentKind kind)
        {
            if(!VisibleToAnyCamera(point,90))return;
            float reach=kind==EquipmentKind.LaserAxe?.65f:
                kind==EquipmentKind.DualUzi||kind==EquipmentKind.Chainsaw?Mathf.Max(.08f,EquipmentSpecifications.Get(kind).radius)+.02f:.45f;
            if(TryContact(point,incoming,out var contact,reach))Surface(contact,kind);
        }

        internal void Surface(SurfaceContact contact,EquipmentKind kind)
        {
            if(!surfaceMaterial||!contact.anchor||!contact.anchor.gameObject.activeInHierarchy)return;
            if(marks.Count==Capacity)marks.RemoveAt(0);
            float diameter=kind==EquipmentKind.RocketLauncher?.78f:
                kind==EquipmentKind.GrenadeLauncher?.55f:
                kind==EquipmentKind.Blaster||kind==EquipmentKind.PoweredArmor?.42f:
                kind==EquipmentKind.WristLaser||kind==EquipmentKind.LaserAxe?.25f:.14f;
            var localPose=contact.localFrame*Matrix4x4.TRS(Vector3.zero,
                Quaternion.Euler(0,0,(marks.Count*137)%360),Vector3.one*diameter);
            marks.Add(new Mark
            {
                anchor=contact.anchor,localPose=localPose,tint=Tint(kind),
                born=Time.time,life=kind==EquipmentKind.DualUzi?1.6f:4,
                style=kind==EquipmentKind.GrenadeLauncher?2:
                    kind==EquipmentKind.WristLaser||kind==EquipmentKind.LaserAxe?1:0,
                layer=contact.layer,renderingMask=contact.renderingMask
            });
        }

        internal static bool TryContact(Vector3 point,Vector3 incoming,out SurfaceContact contact,float reach=.45f)
        {
            contact=default;
            if(!TrySurface(point,incoming,reach,out var hit,out var anchor))return false;
            var pose=Matrix4x4.TRS(hit.point+hit.normal*.006f,Quaternion.LookRotation(hit.normal),Vector3.one);
            var renderer=anchor.GetComponentInChildren<Renderer>();
            contact=new SurfaceContact
            {
                anchor=anchor,localFrame=anchor.worldToLocalMatrix*pose,
                layer=hit.collider.gameObject.layer,renderingMask=renderer?renderer.renderingLayerMask:1u
            };
            return true;
        }

        static bool TrySurface(Vector3 point,Vector3 incoming,float reach,out RaycastHit hit,out Transform anchor)
        {
            hit=default;anchor=null;
            if(!Finite(point)||!Finite(incoming))return false;
            if(queryFrame!=Time.frameCount){queryFrame=Time.frameCount;queries=0;}
            if(queries++>=SurfaceBudget)return false;
            int mask=(1<<Layers.Block)|(1<<Layers.Surface);
            int queryMask=mask|(1<<Layers.Survivor);
            float tolerance=reach*reach;
            if(incoming.sqrMagnitude>.001f)
            {
                var direction=incoming.normalized;
                if(Physics.Raycast(point-direction*reach,direction,out hit,reach*2,queryMask,QueryTriggerInteraction.Ignore)&&
                    (hit.point-point).sqrMagnitude<=tolerance)
                {
                    if((mask&(1<<hit.collider.gameObject.layer))==0)return false;
                    return Anchor(hit.collider,out anchor);
                }
            }
            int count=Physics.OverlapSphereNonAlloc(point,reach,Nearby,queryMask,QueryTriggerInteraction.Ignore);
            if(count==Nearby.Length){System.Array.Clear(Nearby,0,Nearby.Length);return false;}
            float best=tolerance,actorGap=float.PositiveInfinity;RaycastHit nearest=default;bool found=false;
            for(int i=0;i<count;i++)
            {
                var collider=Nearby[i];Nearby[i]=null;
                if(!collider||!collider.enabled)continue;
                var closest=collider.ClosestPoint(point);
                float distance=(closest-point).sqrMagnitude;
                if((mask&(1<<collider.gameObject.layer))==0){actorGap=Mathf.Min(actorGap,distance);continue;}
                if(distance>best)continue;
                var outward=point-closest;
                if(outward.sqrMagnitude<.00001f)outward=point-collider.bounds.center;
                if(outward.sqrMagnitude<.00001f)continue;
                outward.Normalize();
                if(!collider.Raycast(new Ray(closest+outward*.40f,-outward),out var candidate,.80f))continue;
                float gap=(candidate.point-point).sqrMagnitude;
                if(gap>best)continue;
                nearest=candidate;best=gap;found=true;
            }
            if(!found||actorGap<=best+.00001f)return false;
            hit=nearest;return Anchor(hit.collider,out anchor);
        }
        static bool Anchor(Collider collider,out Transform anchor)
        {
            anchor=HitAbilities.Get<IClimbable>(collider)?.Visual;
            if(!anchor)anchor=collider.transform;
            return anchor&&anchor.gameObject.activeInHierarchy;
        }
        static bool Finite(Vector3 value)=>!float.IsNaN(value.sqrMagnitude)&&!float.IsInfinity(value.sqrMagnitude);

        void LateUpdate()
        {
            for(int i=marks.Count-1;i>=0;i--)
            {
                var mark=marks[i];float age=Time.time-mark.born;
                if(!mark.anchor||!mark.anchor.gameObject.activeInHierarchy||age>=mark.life){marks.RemoveAt(i);continue;}
                var key=(mark.layer,mark.renderingMask);
                if(!batches.TryGetValue(key,out var batch))batches.Add(key,batch=new MarkBatch());
                int index=batch.count++;
                batch.matrices[index]=mark.anchor.localToWorldMatrix*mark.localPose;
                batch.colors[index]=mark.tint;
                batch.settings[index]=new Vector4(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((age-mark.life*.65f)/(mark.life*.35f))),mark.style,age,0);
            }
            emptyBatches.Clear();
            foreach(var pair in batches)
            {
                var batch=pair.Value;
                if(batch.count==0){emptyBatches.Add(pair.Key);continue;}
                batch.properties.SetVectorArray("_MarkTint",batch.colors);
                batch.properties.SetVectorArray("_MarkState",batch.settings);
                var parameters=new RenderParams(surfaceMaterial)
                {
                    layer=pair.Key.Item1,renderingLayerMask=pair.Key.Item2,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=false,matProps=batch.properties
                };
                if(SystemInfo.supportsInstancing)Graphics.RenderMeshInstanced(parameters,quad,0,batch.matrices,batch.count);
                else for(int i=0;i<batch.count;i++)
                {
                    batch.properties.SetVector("_MarkTint",batch.colors[i]);
                    batch.properties.SetVector("_MarkState",batch.settings[i]);
                    Graphics.RenderMesh(parameters,quad,0,batch.matrices[i]);
                }
                batch.count=0;
            }
            foreach(var key in emptyBatches)batches.Remove(key);
        }
        void OnDisable()
        {
            marks.Clear();batches.Clear();
            if(flashes)flashes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDestroy()
        {
            if(surfaceMaterial)Destroy(surfaceMaterial);
            if(flashMaterial)Destroy(flashMaterial);
            if(quad)Destroy(quad);
        }
    }
}
