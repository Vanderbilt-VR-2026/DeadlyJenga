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
            if(kind==EquipmentKind.Chainsaw&&EquipmentModelData.Parts.TryGetValue(kind,out var parts))
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
        sealed class Shell
        {
            public Transform model;
            public Vector3 origin,velocity,spin;
            public Quaternion rotation;
            public float born,floor,scale;
        }

        readonly List<Shell> shells=new List<Shell>(48);
        int shellWrite;
        Material shellMaterial;
        Mesh shellMesh;

        static Vector3 ShotAnchor(EquipmentKind kind,bool alternate) => kind switch
        {
            EquipmentKind.Blaster => new(0,.125f,.57f),
            EquipmentKind.SheriffHammer => new(0,.12f,.53f),
            EquipmentKind.DualRifle => new(0,.15f,alternate?.73f:1f),
            EquipmentKind.StockPistol or EquipmentKind.DualUzi => new(0,.12f,.51f),
            EquipmentKind.ChainGun => new(0,-.235f,1f),
            EquipmentKind.RocketLauncher => new(0,.205f,1.065f),
            EquipmentKind.GrenadeLauncher => new(0,.235f,.80f),
            EquipmentKind.WristLaser => new(0,.12f,.17f),
            EquipmentKind.NeedleDrill => new(0,.145f,.72f),
            EquipmentKind.Chainsaw => new(0,0,.78f),
            _ => Vector3.zero
        };

        static (Color tint,float length,int sparks) FlashProfile(EquipmentKind kind,bool alternate)
        {
            var fire=new Color(1,.48f,.11f);
            return kind switch
            {
                EquipmentKind.RocketLauncher => (fire,.58f,12),
                EquipmentKind.GrenadeLauncher => (new Color(.69f,.38f,1),.32f,10),
                EquipmentKind.ChainGun => (fire,.29f,5),
                EquipmentKind.DualRifle => (fire,alternate?.45f:.30f,alternate?10:6),
                EquipmentKind.SheriffHammer => (fire,alternate?.40f:.30f,8),
                EquipmentKind.StockPistol => (fire,.22f,6),
                EquipmentKind.DualUzi => (new Color(.45f,1,.10f),.18f,4),
                EquipmentKind.Blaster or EquipmentKind.PoweredArmor => (new Color(.19f,.85f,1),.28f,7),
                EquipmentKind.WristLaser => (new Color(1,.24f,.045f),.12f,3),
                EquipmentKind.NeedleDrill => (new Color(1,.77f,.23f),.16f,4),
                EquipmentKind.Chainsaw when alternate => (new Color(1,.74f,.32f),.26f,9),
                EquipmentKind.LaserAxe when alternate => (LightningBlue,.37f,9),
                _ => (Color.clear,0,0)
            };
        }

        void PresentShot(EquipmentActionFrame action)
        {
            var profile=FlashProfile(action.kind,action.shotgun);
            if(profile.length<=0||effectsRuntime==null||string.IsNullOrEmpty(action.actor))return;
            var owner=effectsRuntime.FindParticipantRoot(action.actor);
            var model=owner?owner.Find("Visual - "+action.kind):null;
            var anchor=ShotAnchor(action.kind,action.shotgun);
            if(owner&&(action.kind==EquipmentKind.DualUzi||action.kind==EquipmentKind.PoweredArmor))
            {
                var second=owner.Find("Visual - paired "+action.kind);
                if(second&&(!model||
                    (second.TransformPoint(anchor)-action.position).sqrMagnitude<
                    (model.TransformPoint(anchor)-action.position).sqrMagnitude))model=second;
            }
            bool mounted=model&&model.gameObject.activeInHierarchy;
            float scale=mounted?Mathf.Clamp(Mathf.Abs(model.lossyScale.z),.1f,2f):1;
            var origin=action.position;
            if(mounted&&anchor!=Vector3.zero)
            {
                var socket=model.TransformPoint(anchor);
                if((socket-origin).sqrMagnitude<.16f*scale*scale)origin=socket;
            }
            Vector3 direction=mounted?model.forward:owner?owner.forward:Vector3.forward;
            if(action.endpoints!=null&&action.endpoints.Length>0&&
                (action.endpoints[0]-action.position).sqrMagnitude>.005f)
                direction=(action.endpoints[0]-action.position).normalized;
            else if(EquipmentSpecifications.Get(action.kind).projectileSpeed>0||action.kind==EquipmentKind.Chainsaw)
            {
                // Travelling shots have no endpoint until impact.
                float best=.36f;
                foreach(var shot in effectsRuntime.Frame.weaponProjectiles)
                {
                    if(shot.kind!=action.kind||shot.owner!=action.actor||shot.velocity.sqrMagnitude<.001f)continue;
                    float distance=(shot.position-action.position).sqrMagnitude;
                    if(distance>=best)continue;
                    best=distance;direction=shot.velocity.normalized;
                }
            }
            var orientation=Quaternion.LookRotation(direction);
            EmitMuzzle(origin,orientation,profile.tint,profile.length*scale,profile.sparks,action.id);
            if(mounted&&action.kind==EquipmentKind.RocketLauncher)
                EmitMuzzle(model.TransformPoint(new Vector3(0,.205f,-.43f)),
                    Quaternion.LookRotation(-model.forward),new Color(1,.32f,.07f),.70f*scale,8,action.id);
            if(mounted&&(action.kind==EquipmentKind.StockPistol||action.kind==EquipmentKind.DualRifle||
                action.kind==EquipmentKind.ChainGun))EjectShell(model,action,scale);
        }

        void EmitMuzzle(Vector3 origin,Quaternion orientation,Color tint,float size,int sparks,int seed)
        {
            if(burstParticles.isStopped)burstParticles.Play();
            var forward=orientation*Vector3.forward;
            burstParticles.Emit(new ParticleSystem.EmitParams{
                position=origin+forward*size*.16f,velocity=forward*size*12,startLifetime=.041f,
                startSize=size*.82f,startColor=Color.Lerp(tint,Color.white,.76f)},1);
            burstParticles.Emit(new ParticleSystem.EmitParams{
                position=origin+forward*size*.22f,velocity=forward*size*8,startLifetime=.076f,
                startSize=size*1.04f,startColor=tint},1);
            if(size>.27f)
                burstParticles.Emit(new ParticleSystem.EmitParams{
                    position=origin+forward*size*.24f,velocity=forward*size*2.6f+Vector3.up*.38f,
                    startLifetime=.27f,startSize=size*.62f,startColor=new Color(.63f,.59f,.57f,.34f)},1);
            for(int i=0;i<sparks;i++)
            {
                float angle=i*2.39996f+seed*.59f;
                var radial=orientation*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                var direction=forward*(2.6f+i%4*.72f)+radial*(.55f+i%3*.43f);
                burstParticles.Emit(new ParticleSystem.EmitParams{
                    position=origin+forward*size*.22f,velocity=direction,startLifetime=.08f+i%4*.024f,
                    startSize=size*(.12f+i%3*.044f),startColor=tint},1);
            }
        }

        void EjectShell(Transform weapon,EquipmentActionFrame action,float scale)
        {
            if(!shellMesh)
            {
                shellMesh=BuildShellMesh();
                shellMaterial=new Material(ordnanceMetal){name="Ejected cartridge brass",color=new Color(.84f,.57f,.22f)};
            }
            Shell shell=null;
            foreach(var available in shells)if(!available.model.gameObject.activeSelf){shell=available;break;}
            if(shell==null)
            {
                if(shells.Count<48)
                {
                    shell=new Shell{model=ModelPiece(transform,"Ejected cartridge",shellMesh,shellMaterial,Vector3.zero,Vector3.one)};
                    shells.Add(shell);
                }
                else{shell=shells[shellWrite];shellWrite=(shellWrite+1)%shells.Count;}
            }
            bool rotary=action.kind==EquipmentKind.ChainGun;
            bool rifle=action.kind==EquipmentKind.DualRifle;
            var port=rotary?new Vector3(.27f,-.19f,.23f):rifle?new Vector3(.086f,.10f,.14f):
                new Vector3(.060f,.145f,.125f);
            shell.origin=weapon.TransformPoint(port);
            shell.velocity=(weapon.right*(2.2f+action.id%5*.24f)+weapon.up*(1.3f+action.id%3*.24f)
                -weapon.forward*.66f)*scale;
            shell.spin=new Vector3(620+action.id%7*84,580+action.id%5*114,130);
            shell.rotation=weapon.rotation*Quaternion.Euler(0,0,25);
            shell.born=Time.time;
            shell.scale=scale*(rotary?1.22f:rifle?1.14f:.9f);
            shell.floor=shell.origin.y-3;
            if(Physics.Raycast(shell.origin+Vector3.up*.04f,Vector3.down,out var floor,2.6f,
                (1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore))
                shell.floor=floor.point.y+.024f*scale;
            shell.model.SetPositionAndRotation(shell.origin,shell.rotation);
            shell.model.localScale=Vector3.one*shell.scale;
            shell.model.gameObject.SetActive(true);
        }

        void UpdateShells()
        {
            foreach(var shell in shells)
            {
                if(!shell.model.gameObject.activeSelf)continue;
                float age=Time.time-shell.born;
                if(age>=1.7f){shell.model.gameObject.SetActive(false);continue;}
                const float gravity=9.81f;
                float flight=(shell.velocity.y+Mathf.Sqrt(shell.velocity.y*shell.velocity.y+
                    2*gravity*Mathf.Max(0,shell.origin.y-shell.floor)))/gravity;
                float t=Mathf.Min(age,flight);
                var position=shell.origin+shell.velocity*t+Vector3.down*(.5f*gravity*t*t);
                if(age>flight)
                {
                    float bounce=age-flight;
                    position.y=shell.floor+Mathf.Max(0,(gravity*flight-shell.velocity.y)*.30f*bounce-.5f*gravity*bounce*bounce);
                    position+=Vector3.ProjectOnPlane(shell.velocity,Vector3.up)*(.26f*Mathf.Min(.28f,bounce));
                }
                shell.model.SetPositionAndRotation(position,shell.rotation*Quaternion.Euler(shell.spin*Mathf.Min(age,flight+.28f)));
                shell.model.localScale=Vector3.one*(shell.scale*Mathf.Clamp01((1.7f-age)/.24f));
            }
        }

        static Mesh BuildShellMesh()
        {
            const int sides=12;
            Vector2[] rings={new(-.025f,0),new(-.025f,.013f),new(-.021f,.014f),new(-.016f,.011f),
                new(-.012f,.012f),new(.024f,.010f),new(.024f,.007f),new(.009f,.007f),new(.009f,0)};
            var vertices=new Vector3[rings.Length*sides];
            var triangles=new List<int>((rings.Length-1)*sides*6);
            for(int ring=0;ring<rings.Length;ring++)for(int i=0;i<sides;i++)
            {
                float angle=i*2*Mathf.PI/sides;
                int a=ring*sides+i,b=ring*sides+(i+1)%sides;
                vertices[a]=new Vector3(Mathf.Cos(angle)*rings[ring].y,Mathf.Sin(angle)*rings[ring].y,rings[ring].x);
                if(ring<rings.Length-1)triangles.AddRange(new[]{a,b,a+sides,b,b+sides,a+sides});
            }
            var mesh=new Mesh{name="Open brass cartridge with extractor rim",vertices=vertices,triangles=triangles.ToArray()};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
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
