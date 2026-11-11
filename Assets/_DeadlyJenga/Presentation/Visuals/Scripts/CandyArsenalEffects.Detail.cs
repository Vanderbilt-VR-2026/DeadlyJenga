using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyArsenalEffects
    {
        sealed class Residue {public Transform root;public float born,life;public Vector3 from,to;public bool spike;public float radius;public LineRenderer[] arcs;public Color tint;public CandyWeaponImpacts.SurfaceContact surface;}
        Material detailGlow;
        readonly List<Residue> residues=new List<Residue>();
        void LateUpdate()
        {
            for(int i=residues.Count-1;i>=0;i--)
            {
                var r=residues[i];if(!r.root){residues.RemoveAt(i);continue;}float age=Time.time-r.born;
                if(age>=r.life){Destroy(r.root.gameObject);residues.RemoveAt(i);continue;}
                if(r.spike)
                {
                    if(r.surface.anchor)
                    {
                        var pose=r.surface.anchor.localToWorldMatrix*r.surface.localFrame;
                        r.root.SetPositionAndRotation(pose.GetColumn(3),pose.rotation*Quaternion.Euler(90,0,0));
                    }
                    else r.root.position=Vector3.Lerp(r.from,r.to,Mathf.Clamp01(age/.065f));
                    float fade=Mathf.Clamp01((r.life-age)/.45f);r.root.localScale=new Vector3(.035f,.22f,.035f)*fade;
                }
                else
                {
                    float fraction=Mathf.Clamp01(age/r.life);
                    r.root.localScale=Vector3.one*Mathf.Lerp(.25f,r.radius,1-Mathf.Pow(1-fraction,2));
                    if(r.arcs!=null)foreach(var line in r.arcs){var color=r.tint;color.a=Mathf.Pow(1-fraction,2);line.startColor=line.endColor=color;}
                }
            }
        }
        void Keep(Residue r){if(residues.Count>=96){if(residues[0].root)Destroy(residues[0].root.gameObject);residues.RemoveAt(0);}residues.Add(r);}
        Transform Visual(string name,Mesh mesh,Vector3 position,Color color)
        {
            if(!detailGlow){detailGlow=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));detailGlow.SetFloat("_Surface",1);detailGlow.SetFloat("_Blend",0);detailGlow.SetFloat("_SrcBlend",5);detailGlow.SetFloat("_DstBlend",10);detailGlow.SetFloat("_ZWrite",0);detailGlow.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");detailGlow.renderQueue=3000;}
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.transform.position=position;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=detailGlow;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);go.GetComponent<MeshRenderer>().SetPropertyBlock(block);return go.transform;
        }
        void Spike(EquipmentActionFrame action)
        {
            var end=action.endpoints[0];
            if(!CandyWeaponImpacts.TryContact(end,end-action.position,out var contact,.10f))return;
            var spike=Visual("Visual - steaming neon spike",Resources.GetBuiltinResource<Mesh>("Capsule.fbx"),action.position,new Color(.3f,1,.02f));spike.rotation=Quaternion.FromToRotation(Vector3.up,(end-action.position).sqrMagnitude>.001f?(end-action.position).normalized:Vector3.forward);
            var steam=spike.gameObject.AddComponent<ParticleSystem>();steam.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=steam.main;main.startLifetime=.3f;main.startSpeed=.2f;main.startSize=.05f;main.maxParticles=6;main.startColor=new Color(.45f,1,.15f,.35f);var emission=steam.emission;emission.rateOverTime=8;steam.GetComponent<ParticleSystemRenderer>().sharedMaterial=glow;steam.Play();
            spike.position=contact.Position;
            Keep(new Residue{root=spike,born=Time.time,life=1.6f,from=action.position,to=end,spike=true,surface=contact});
        }
        readonly Dictionary<Transform,EquipmentKind> modelKind=new Dictionary<Transform,EquipmentKind>();
        readonly Dictionary<EquipmentKind,Stack<Transform>> modelPool=new Dictionary<EquipmentKind,Stack<Transform>>();
        readonly Dictionary<EquipmentKind,Mesh> projectileMeshes=new Dictionary<EquipmentKind,Mesh>();
        Material ordnanceRibbon,ordnanceMetal,ordnanceHot,ordnanceBurst;
        ParticleSystem burstParticles;
        int pooled;
        void PrepareOrdnance()
        {
            var shader=Resources.Load<Shader>("CandyOrdnance");
            ordnanceRibbon=new Material(shader?shader:glow.shader);if(shader)ordnanceRibbon.SetFloat("_Ribbon",1);
            ordnanceMetal=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Brushed projectile casing",color=new Color(.39f,.46f,.53f)};
            ordnanceMetal.SetFloat("_Metallic",.77f);ordnanceMetal.SetFloat("_Smoothness",.72f);
            ordnanceHot=new Material(ordnanceMetal){name="Projectile energy accents"};ordnanceHot.EnableKeyword("_EMISSION");ordnanceHot.SetFloat("_Metallic",.12f);
            ordnanceBurst=new Material(shader?shader:glow.shader);
            var emitter=new GameObject("Bounded ordnance blast particles");emitter.transform.SetParent(transform,false);
            burstParticles=emitter.AddComponent<ParticleSystem>();burstParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=burstParticles.main;main.loop=false;main.playOnAwake=false;main.maxParticles=512;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=.26f;
            var emission=burstParticles.emission;emission.enabled=false;var shape=burstParticles.shape;shape.enabled=false;
            var fade=burstParticles.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.48f,.48f,.48f),1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.87f,.2f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var r=emitter.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=ordnanceBurst;r.renderMode=ParticleSystemRenderMode.Stretch;r.lengthScale=1.45f;r.velocityScale=.11f;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        static Color Energy(EquipmentKind kind)=>kind==EquipmentKind.GrenadeLauncher?new Color(.54f,.28f,1):kind==EquipmentKind.DualUzi?new Color(.33f,1,.07f):kind==EquipmentKind.Chainsaw?new Color(.8f,.82f,1):new Color(1,.46f,.12f);
        Transform TakeProjectile(EquipmentKind kind,Vector3 at)
        {
            if(modelPool.TryGetValue(kind,out var pool)&&pool.Count>0)
            {
                pooled--;var item=pool.Pop();item.position=at;item.gameObject.SetActive(true);item.GetComponent<TrailRenderer>().Clear();return item;
            }
            var root=new GameObject("Visual - "+kind+" projectile").transform;root.SetParent(transform,false);root.position=at;modelKind[root]=kind;
            var casing=ModelPiece(root,"Projectile casing",ProjectileMesh(kind),ordnanceMetal,Vector3.zero,Vector3.one);
            if(kind==EquipmentKind.Chainsaw&&EquipmentModelData.TryGet(kind,out var parts))
                foreach(var part in parts)if(part.mesh.name.Contains("Serrated saw wheel"))
                {casing.GetComponent<MeshFilter>().sharedMesh=part.mesh;casing.localRotation=Quaternion.Euler(0,0,90);break;}
            Color color=Energy(kind);var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);block.SetColor("_EmissionColor",color*2.2f);
            if(kind!=EquipmentKind.Chainsaw)
            {
                var orb=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                var hot=ModelPiece(root,"Visible charge",orb,ordnanceHot,kind==EquipmentKind.RocketLauncher?Vector3.back*.38f:Vector3.zero,
                    kind==EquipmentKind.RocketLauncher?new Vector3(.077f,.077f,.16f):kind==EquipmentKind.GrenadeLauncher?new Vector3(.066f,.066f,.066f):new Vector3(.047f,.047f,.25f));
                hot.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                if(kind==EquipmentKind.GrenadeLauncher)
                    for(int i=0;i<4;i++)
                    {
                        var band=ModelPiece(root,"Charged grenade cell",orb,ordnanceHot,Quaternion.Euler(0,0,90*i)*Vector3.right*.106f,new Vector3(.029f,.029f,.071f));band.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                    }
            }
            var trail=root.gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=ordnanceRibbon;trail.time=kind==EquipmentKind.RocketLauncher?.41f:.24f;
            trail.widthMultiplier=kind==EquipmentKind.RocketLauncher?.34f:kind==EquipmentKind.GrenadeLauncher?.22f:.095f;
            trail.widthCurve=new AnimationCurve(new Keyframe(0,1),new Keyframe(.62f,.52f),new Keyframe(1,0));
            trail.minVertexDistance=.11f;trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.receiveShadows=false;
            trail.startColor=Color.white;trail.endColor=new Color(1,1,1,0);trail.SetPropertyBlock(block);trail.Clear();
            return root;
        }
        void ReturnProjectile(Transform model)
        {
            if(!model)return;
            if(pooled>=32){modelKind.Remove(model);Destroy(model.gameObject);return;}
            var kind=modelKind[model];if(!modelPool.TryGetValue(kind,out var pool))modelPool[kind]=pool=new Stack<Transform>();
            model.gameObject.SetActive(false);pool.Push(model);pooled++;
        }
        static Transform ModelPiece(Transform parent,string name,Mesh mesh,Material material,Vector3 at,Vector3 scale)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=scale;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return go.transform;
        }
        Mesh ProjectileMesh(EquipmentKind kind)
        {
            if(projectileMeshes.TryGetValue(kind,out var cached))return cached;
            Vector2[] p=kind==EquipmentKind.RocketLauncher?
                new[]{new Vector2(-.36f,0),new Vector2(-.36f,.103f),new Vector2(-.30f,.112f),new Vector2(-.26f,.082f),new Vector2(.18f,.082f),new Vector2(.24f,.072f),new Vector2(.42f,.018f),new Vector2(.44f,0)}:
                kind==EquipmentKind.GrenadeLauncher?new[]{new Vector2(-.16f,0),new Vector2(-.16f,.082f),new Vector2(-.112f,.118f),new Vector2(-.075f,.12f),new Vector2(-.06f,.085f),new Vector2(.06f,.085f),new Vector2(.075f,.12f),new Vector2(.113f,.118f),new Vector2(.158f,.08f),new Vector2(.158f,0)}:
                kind==EquipmentKind.Chainsaw?new[]{new Vector2(-.025f,0),new Vector2(-.025f,.24f),new Vector2(.025f,.24f),new Vector2(.025f,0)}:
                new[]{new Vector2(-.16f,0),new Vector2(-.13f,.024f),new Vector2(.055f,.042f),new Vector2(.20f,0)};
            var v=new List<Vector3>();var uv=new List<Vector2>();var idx=new List<int>();int sides=kind==EquipmentKind.DualUzi?6:20;
            for(int j=0;j<p.Length;j++)for(int i=0;i<=sides;i++)
            {
                float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*p[j].y,Mathf.Sin(a)*p[j].y,p[j].x));uv.Add(new Vector2(i/(float)sides,j/(float)(p.Length-1)));
                if(i==sides||j==p.Length-1)continue;int k=j*(sides+1)+i;idx.AddRange(new[]{k,k+1,k+sides+1,k+1,k+sides+2,k+sides+1});
            }
            var mesh=new Mesh{name="Sculpted "+kind+" projectile"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(idx,0);mesh.RecalculateNormals();return projectileMeshes[kind]=mesh;
        }
        void ReleaseOrdnance()
        {
            foreach(var mesh in projectileMeshes.Values)if(mesh)Destroy(mesh);
            if(ordnanceRibbon)Destroy(ordnanceRibbon);if(ordnanceMetal)Destroy(ordnanceMetal);if(ordnanceHot)Destroy(ordnanceHot);if(detailGlow)Destroy(detailGlow);if(ordnanceBurst)Destroy(ordnanceBurst);
        }
        void EmitBlast(EquipmentActionFrame action)
        {
            bool rocket=action.kind==EquipmentKind.RocketLauncher;
            bool saw=action.kind==EquipmentKind.Chainsaw;
            bool spike=action.kind==EquipmentKind.DualUzi;
            if(burstParticles.isStopped)burstParticles.Play();
            int count=rocket?52:saw?12:spike?8:28;Color tint=Energy(action.kind);
            for(int i=0;i<count;i++)
            {
                float y=1-2*(i+.5f)/count,a=i*2.399963f,r=Mathf.Sqrt(1-y*y);
                var direction=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
                burstParticles.Emit(new ParticleSystem.EmitParams{position=action.position+direction*.14f,
                    velocity=direction*(rocket?6+i%9*.83f:saw||spike?2+i%4*.3f:3+i%7*.69f),startColor=tint,
                    startSize=(rocket?.35f:saw||spike?.065f:.21f)+(i%4)*(saw||spike?.012f:.085f),startLifetime=(rocket?.44f:saw||spike?.14f:.24f)+i%9*(saw||spike?.012f:.047f)},1);
            }
        }
        void BlastDetail(EquipmentActionFrame action)
        {
            bool electric=action.kind==EquipmentKind.GrenadeLauncher;
            var shell=new GameObject("Visual - expanding blast shock fronts").transform;
            shell.SetParent(transform,false);shell.position=action.position;
            Color color=Energy(action.kind);
            int count=electric?7:3;var arcs=new LineRenderer[count];
            for(int i=0;i<count;i++)
            {
                var go=new GameObject(electric?"Ionized discharge fork":"Expanding exhaust shock ring");go.transform.SetParent(shell,false);
                var line=go.AddComponent<LineRenderer>();arcs[i]=line;line.sharedMaterial=ordnanceRibbon;line.useWorldSpace=false;
                line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
                line.positionCount=electric?8:36;line.loop=!electric;line.startWidth=electric?.055f:.06f;line.endWidth=electric?.004f:.06f;
                line.startColor=line.endColor=color;var tilt=Quaternion.Euler(i*63,i*137,0);
                for(int n=0;n<line.positionCount;n++)
                {
                    float f=n/(float)line.positionCount,a=f*Mathf.PI*2;
                    var point=electric?Vector3.forward*f+new Vector3(Mathf.Sin(n*5+i),Mathf.Cos(n*3+i),0)*.09f:
                        new Vector3(Mathf.Cos(a),.035f*Mathf.Sin(a*5),Mathf.Sin(a));
                    line.SetPosition(n,tilt*point);
                }
            }
            Keep(new Residue{root=shell,born=Time.time,life=electric?.57f:.83f,radius=action.kind==EquipmentKind.RocketLauncher?8:3.5f,arcs=arcs,tint=color});
        }
    }
}
