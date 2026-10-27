using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.CandyArt;

namespace DeadlyJenga.Presentation
{
    [DefaultExecutionOrder(650)]
    public sealed class CandyMatchVisuals : MonoBehaviour
    {
        sealed class Look {public Renderer renderer;public Material[] originals;public Material[] health;public MaterialPropertyBlock originalProps;}
        readonly Dictionary<string,List<Look>> looks=new Dictionary<string,List<Look>>();
        readonly Dictionary<string,float> displayedHealth=new Dictionary<string,float>();
        MaterialPropertyBlock props;
        Shader healthShader;Material healthMaterial;float scanAt,transitionAt=-100;Color transitionColor;Image curtain;Canvas canvas;Camera view;Material liquid,deathPaint;float paintAt=-100;bool viewVR;Color paintColor;
        bool wasOutage;string skin="";int winner=-2;readonly Dictionary<Renderer,MaterialPropertyBlock> winnerOriginal=new Dictionary<Renderer,MaterialPropertyBlock>();
        readonly Dictionary<Renderer,Material> blockOriginals=new Dictionary<Renderer,Material>();readonly Dictionary<(Material,string),Material> teamMaterials=new Dictionary<(Material,string),Material>();int claimRevision=-1;
        readonly List<string> staleLooks=new List<string>();CandyEnvironment[] environments;float environmentAt;
        readonly Dictionary<Renderer,MaterialPropertyBlock> handOriginals=new Dictionary<Renderer,MaterialPropertyBlock>();readonly HashSet<Renderer> activeHands=new HashSet<Renderer>();readonly List<Renderer> oldHands=new List<Renderer>();
        readonly HashSet<string> melting=new HashSet<string>();readonly HashSet<Renderer> hidden=new HashSet<Renderer>();
        void OnEnable(){props=new MaterialPropertyBlock();healthShader=Resources.Load<Shader>("CandyScenery/CandyAvatarHealth");if(healthShader&&!healthMaterial){healthMaterial=new Material(healthShader);healthMaterial.enableInstancing=true;healthMaterial.SetFloat("_Smoothness",.8f);}GameEvents.Eliminated+=Eliminated;GameEvents.RoundEnded+=RoundEnded;}
        void RoundEnded(){transitionAt=Time.unscaledTime;transitionColor=new Color(.96f,.97f,1);}
        void OnDisable(){GameEvents.Eliminated-=Eliminated;GameEvents.RoundEnded-=RoundEnded;RestoreHandTints();RestoreLooks();RestoreWinner();RestoreBlocks();foreach(var r in hidden)if(r)r.forceRenderingOff=false;hidden.Clear();melting.Clear();}
        void Update()
        {
            var runtime=MatchRuntime.Current;if(runtime?.Frame==null)return;var f=runtime.Frame;
            if(skin!=f.rules.environmentSkin){skin=f.rules.environmentSkin;CandySceneSkins.Select(SkinIndex(skin));}
            bool outage=false;foreach(var hazard in DisasterStates.Active){var state=hazard.Capture();if(state!=null&&state.warning<=0&&state.kind=="PowerOutage"&&f.phase!=RoundPhase.Victory&&f.phase!=RoundPhase.Results){outage=true;break;}}
            bool restoringOutage=wasOutage&&!outage;wasOutage=outage;
            if(!outage)Health(runtime,f);
            if(Time.unscaledTime>=scanAt&&!restoringOutage){scanAt=Time.unscaledTime+.2f;Deaths(runtime,f);TeamHands(runtime,f);if(!outage){Winner(f);if(f.phase!=RoundPhase.Victory)Claims(f);}else claimRevision=-1;}
            Transition();
            if(Time.unscaledTime>=environmentAt){environmentAt=Time.unscaledTime+1;environments=FindObjectsByType<CandyEnvironment>(FindObjectsSortMode.None);}
            if(f.phase==RoundPhase.Victory&&f.winnerTeam>=0)foreach(var team in f.teams)if(team.id==f.winnerTeam)foreach(var env in environments??System.Array.Empty<CandyEnvironment>())if(env&&env.waterRenderer)Tint(env.waterRenderer,team.color);
        }
        public static int SkinIndex(string value)
        {string s=(value??"").ToLowerInvariant();return s.Contains("night")?1:s.Contains("sunset")?2:s.Contains("volcan")||s.Contains("lava")?3:s.Contains("cloud")||s=="sky"?4:s.Contains("desert")?5:s.Contains("ice")?6:0;}
        void TeamHands(IMatchRuntime runtime,MatchFrame frame)
        {
            activeHands.Clear();
            foreach(var player in frame.players)
            {
                if(player.role!="hand")continue;var root=runtime.FindParticipantRoot(player.id);if(!root)continue;
                bool spirit=false;foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))if(component is ISeaSpiritView waterView&&waterView.Active){spirit=true;break;}if(spirit)continue;
                Color color=player.color;foreach(var team in frame.teams)if(team.id==player.team){color=team.color;break;}
                TintHandTree(root,color);
                foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))if(component is IFlyingState flyer&&flyer.ContactBodies!=null&&flyer.ContactBodies.Count>0&&flyer.ContactBodies[0])
                {var palm=flyer.ContactBodies[0].transform;TintHandTree(palm.parent?palm.parent:palm,color);}
            }
            oldHands.Clear();foreach(var pair in handOriginals)if(!pair.Key||!activeHands.Contains(pair.Key))oldHands.Add(pair.Key);
            foreach(var renderer in oldHands){if(renderer)renderer.SetPropertyBlock(handOriginals[renderer]);handOriginals.Remove(renderer);}
        }
        void TintHandTree(Transform root,Color color)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer))continue;activeHands.Add(renderer);
                if(!handOriginals.ContainsKey(renderer)){var saved=new MaterialPropertyBlock();renderer.GetPropertyBlock(saved);handOriginals[renderer]=saved;}
                renderer.GetPropertyBlock(props);props.SetColor("_BaseColor",color);props.SetColor("_Color",color);renderer.SetPropertyBlock(props);
            }
        }
        void RestoreHandTints(){foreach(var pair in handOriginals)if(pair.Key)pair.Key.SetPropertyBlock(pair.Value);handOriginals.Clear();}
        void Health(IMatchRuntime runtime,MatchFrame f)
        {
            staleLooks.Clear();foreach(var pair in looks)if(runtime.FindActor(pair.Key)?.Visual==null)staleLooks.Add(pair.Key);foreach(var id in staleLooks){ReleaseLooks(looks[id]);looks.Remove(id);displayedHealth.Remove(id);}
            bool sinking=false;foreach(var p in f.players){var a=runtime.FindActor(p.id);if(a!=null&&runtime.RulesFor(a.gameObject).sinking){sinking=true;break;}}
            if(!f.rules.healthEnabled&&!sinking){RestoreLooks();return;}
            if(!healthShader)return;
            foreach(var p in f.players)
            {
                var actor=runtime.FindActor(p.id);if(actor?.Visual==null)continue;
                if(looks.TryGetValue(p.id,out var cached)&&cached.Exists(l=>!l.renderer)){ReleaseLooks(cached);looks.Remove(p.id);}
                if(!looks.TryGetValue(p.id,out var list))
                {
                    list=new List<Look>();foreach(var r in actor.Visual.GetComponentsInChildren<Renderer>())
                    {
                        if(!(r is MeshRenderer)&&!(r is SkinnedMeshRenderer))continue;var original=r.sharedMaterials;var material=new Material[original.Length];
                        for(int i=0;i<material.Length;i++){material[i]=healthMaterial;}
                        var saved=new MaterialPropertyBlock();r.GetPropertyBlock(saved);list.Add(new Look{renderer=r,originals=original,health=material,originalProps=saved});PreserveSurface(r,original,saved);r.sharedMaterials=material;
                        if(!saved.HasColor("_BaseColor")&&original.Length>0&&original[0]&&original[0].HasProperty("_BaseColor")){r.GetPropertyBlock(props);props.SetColor("_BaseColor",original[0].GetColor("_BaseColor"));r.SetPropertyBlock(props);}
                    }
                    looks[p.id]=list;
                }
                foreach(var look in list)if(look.renderer&&look.health.Length>0&&look.renderer.sharedMaterial!=look.health[0]){look.originals=look.renderer.sharedMaterials;look.renderer.GetPropertyBlock(look.originalProps);PreserveSurface(look.renderer,look.originals,look.originalProps);look.renderer.sharedMaterials=look.health;}
                float target=f.rules.healthEnabled?Mathf.Clamp01(p.health):1;
                float shown=displayedHealth.TryGetValue(p.id,out var previous)?Mathf.Lerp(previous,target,1-Mathf.Exp(-24*Time.unscaledDeltaTime)):target;
                if(Mathf.Abs(shown-target)<.001f)shown=target;displayedHealth[p.id]=shown;
                float bottom=actor.transform.position.y,top=Mathf.Max(bottom+.1f,actor.ViewPose.position.y);
                foreach(var look in list)if(look.renderer)
                {look.renderer.GetPropertyBlock(props);props.SetFloat("_Health",shown);props.SetFloat("_Recovering",target>previous+.0001f?1:0);props.SetFloat("_Sink",runtime.RulesFor(actor.gameObject).sinkProgress);props.SetFloat("_Bottom",bottom);props.SetFloat("_Top",top);look.renderer.SetPropertyBlock(props);}
            }
        }
        void RestoreLooks()
        {
            foreach(var list in looks.Values)ReleaseLooks(list);looks.Clear();displayedHealth.Clear();
        }
        void PreserveSurface(Renderer renderer,Material[] materials,MaterialPropertyBlock saved)
        {
            if(materials.Length==0||!materials[0])return;var source=materials[0];renderer.GetPropertyBlock(props);
            var map=saved.GetTexture("_BaseMap");if(!map&&source.HasProperty("_BaseMap"))map=source.GetTexture("_BaseMap");
            props.SetTexture("_BaseMap",map?map:Texture2D.whiteTexture);
            var scale=source.HasProperty("_BaseMap")?source.GetTextureScale("_BaseMap"):Vector2.one;var offset=source.HasProperty("_BaseMap")?source.GetTextureOffset("_BaseMap"):Vector2.zero;
            props.SetVector("_BaseMap_ST",saved.HasVector("_BaseMap_ST")?saved.GetVector("_BaseMap_ST"):new Vector4(scale.x,scale.y,offset.x,offset.y));
            var normal=saved.GetTexture("_BumpMap");if(!normal&&source.HasProperty("_BumpMap")&&source.IsKeywordEnabled("_NORMALMAP"))normal=source.GetTexture("_BumpMap");
            if(normal)props.SetTexture("_BumpMap",normal);
            props.SetFloat("_BumpScale",normal?(saved.HasFloat("_BumpScale")?saved.GetFloat("_BumpScale"):source.HasProperty("_BumpScale")?source.GetFloat("_BumpScale"):1):0);
            foreach(var property in new[]{"_Smoothness","_Metallic"})if(!saved.HasFloat(property)&&source.HasProperty(property))props.SetFloat(property,source.GetFloat(property));
            if(!saved.HasColor("_EmissionColor")&&source.HasProperty("_EmissionColor"))props.SetColor("_EmissionColor",source.GetColor("_EmissionColor"));
            renderer.SetPropertyBlock(props);
        }
        void ReleaseLooks(List<Look> list)
        {
            foreach(var l in list)if(l.renderer)
            {
                l.renderer.GetPropertyBlock(props);
                // Wardrobe and team colors may change while the health overlay is active.
                foreach(var property in new[]{"_BaseColor","_Color","_EmissionColor"})if(props.HasColor(property))l.originalProps.SetColor(property,props.GetColor(property));
                foreach(var property in new[]{"_Smoothness","_Metallic"})if(props.HasFloat(property))l.originalProps.SetFloat(property,props.GetFloat(property));
                l.renderer.sharedMaterials=l.originals;l.renderer.SetPropertyBlock(l.originalProps);
            }
        }
        void Winner(MatchFrame f)
        {
            int team=f.phase==RoundPhase.Victory?f.winnerTeam:-2;if(team==winner)return;
            if(team>=0)foreach(var effect in FindObjectsByType<CandyDisasterVisuals>(FindObjectsSortMode.None))effect.ReleaseFireSurfaces();
            RestoreWinner();winner=team;if(team<0){RestoreBlocks();claimRevision=-1;return;}
            Color c=Color.white;foreach(var t in f.teams)if(t.id==team)c=t.color;
            var world=Services.World;if(world!=null)foreach(var b in world.Blocks)
            {var visual=b?b.GetComponent<IClimbable>()?.Visual:null;if(visual)foreach(var r in visual.GetComponentsInChildren<Renderer>())BlockTint(r,c);}
            foreach(var env in FindObjectsByType<CandyEnvironment>(FindObjectsSortMode.None))if(env.waterRenderer)Tint(env.waterRenderer,c);
        }
        void Tint(Renderer renderer,Color color)
        {if(!winnerOriginal.ContainsKey(renderer)){var saved=new MaterialPropertyBlock();renderer.GetPropertyBlock(saved);winnerOriginal[renderer]=saved;}renderer.GetPropertyBlock(props);props.SetColor("_BaseColor",color);props.SetColor("_Color",color);props.SetColor("_Accent",color);props.SetColor("_Shallow",color);props.SetColor("_Deep",color*.65f);renderer.SetPropertyBlock(props);}
        void RestoreWinner(){foreach(var pair in winnerOriginal)if(pair.Key)pair.Key.SetPropertyBlock(pair.Value);winnerOriginal.Clear();winner=-2;}
        void Claims(MatchFrame f)
        {
            if(f.phase==RoundPhase.Victory)return;
            if(f.rules.mode!=GameModeKind.BlockControl){RestoreBlocks();return;}
            if(claimRevision==f.revision)return;claimRevision=f.revision;var world=Services.World;if(world==null)return;
            var owners=new int[world.Blocks.Count];for(int i=0;i<owners.Length;i++)owners[i]=-1;foreach(var claim in f.claims)if(claim.block>=0&&claim.block<owners.Length)owners[claim.block]=claim.owner;
            for(int i=0;i<owners.Length;i++)
            {
                Color c=Color.white;foreach(var t in f.teams)if(t.id==owners[i]){c=t.color;break;}
                var visual=world.Blocks[i]?world.Blocks[i].GetComponent<IClimbable>()?.Visual:null;if(visual)foreach(var r in visual.GetComponentsInChildren<Renderer>())BlockTint(r,c);
            }
        }
        void BlockTint(Renderer renderer,Color c)
        {
            if(!blockOriginals.TryGetValue(renderer,out var original)){original=renderer.sharedMaterial;blockOriginals[renderer]=original;}if(!original)return;
            var key=(original,ColorUtility.ToHtmlStringRGB(c));if(!teamMaterials.TryGetValue(key,out var material)){material=new Material(original);material.SetColor("_BaseColor",c);if(material.HasProperty("_Color"))material.SetColor("_Color",c);material.enableInstancing=true;teamMaterials[key]=material;}renderer.sharedMaterial=material;
        }
        void RestoreBlocks(){foreach(var pair in blockOriginals)if(pair.Key)pair.Key.sharedMaterial=pair.Value;blockOriginals.Clear();claimRevision=-1;}
        void Eliminated(GameObject who,GameObject source)
        {
            var actor=who?who.GetComponent<IModeActor>():null;if(actor==null)return;
            // Enemy NPCs are intentionally absent from the player frame and may already be inactive.
            if(who.GetComponent<IEnemyActor>() is IEnemyActor enemy&&enemy.IsEnemy){if(enemy.ArchetypeId=="longhand"||enemy.ArchetypeId=="gum-brute")CandyMayhemEnemyFinish.Splat(actor.Visual);else if(enemy.ArchetypeId=="mushroom")CandyMelt.Spawn(actor.Visual,actor.Color);else CandyEnemyHairball.Spawn(actor.Visual);return;}
            if(actor.IsLocal)BeginDeathPaint(actor.Color);
        }
        void Deaths(IMatchRuntime runtime,MatchFrame frame)
        {
            foreach(var p in frame.players)
            {
                var actor=runtime.FindActor(p.id);if(actor?.Visual==null)continue;
                if(p.dying&&melting.Add(p.id)){CandyMelt.Spawn(actor.Visual,p.color,actor.transform.position.y);if(p.id==runtime.LocalActorId)BeginDeathPaint(p.color);}
                if(!p.dying&&p.alive)melting.Remove(p.id);
                foreach(var r in actor.Visual.GetComponentsInChildren<Renderer>())
                {if(p.dying||!p.alive){r.forceRenderingOff=true;hidden.Add(r);}else if(hidden.Remove(r))r.forceRenderingOff=false;}
            }
        }
        void BeginDeathPaint(Color color)
        {
            // The elimination event and the replicated dying frame can describe the same death.
            if(Time.unscaledTime-paintAt<.5f)return;
            paintAt=Time.unscaledTime;paintColor=new Color(color.r,color.g,color.b,1);
        }
        void Transition()
        {
            var session=Services.Session;if(session?.ActiveCamera==null)return;
            if(!canvas)
            {
                var go=new GameObject("Visual - liquid respawn curtain",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.sortingOrder=100;
                var image=new GameObject("Candy liquid",typeof(RectTransform),typeof(Image));image.transform.SetParent(go.transform,false);curtain=image.GetComponent<Image>();curtain.raycastTarget=false;curtain.rectTransform.anchorMin=Vector2.zero;curtain.rectTransform.anchorMax=Vector2.one;curtain.rectTransform.offsetMin=curtain.rectTransform.offsetMax=Vector2.zero;
                var shader=Resources.Load<Shader>("CandyScenery/CandyLiquidCurtain");if(shader){liquid=new Material(shader);curtain.material=liquid;}
                var paintShader=Resources.Load<Shader>("CandyScenery/CandyDeathPaint");if(paintShader)deathPaint=new Material(paintShader);
            }
            if(view!=session.ActiveCamera||viewVR!=session.IsVR)
            {view=session.ActiveCamera;viewVR=session.IsVR;var r=canvas.GetComponent<RectTransform>();if(session.IsVR){canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view;r.SetParent(view.transform,false);r.sizeDelta=new Vector2(4,4);r.localPosition=new Vector3(0,0,.15f);r.localRotation=Quaternion.identity;r.localScale=Vector3.one;}else{canvas.renderMode=RenderMode.ScreenSpaceOverlay;r.SetParent(transform,false);}}
            float paintAge=Time.unscaledTime-paintAt;
            if(deathPaint&&paintAge>=0&&paintAge<2.2f)
            {
                curtain.material=deathPaint;curtain.color=paintColor;
                if(session.IsVR){var plane=canvas.GetComponent<RectTransform>();plane.sizeDelta=new Vector2(2.8f,2);plane.localPosition=new Vector3(0,0,1.1f);}
                deathPaint.SetFloat("_Age",paintAge);
                deathPaint.SetFloat("_Aspect",session.IsVR?1.4f:Mathf.Max(.1f,view.aspect));
                canvas.enabled=true;return;
            }
            if(session.IsVR){var plane=canvas.GetComponent<RectTransform>();plane.sizeDelta=new Vector2(4,4);plane.localPosition=new Vector3(0,0,.15f);}
            curtain.material=liquid;
            float age=Time.unscaledTime-transitionAt;float alpha=age<.3f?Mathf.SmoothStep(0,1,age/.3f):1-Mathf.SmoothStep(0,1,(age-.3f)/.9f);
            var world=Services.World;if(world!=null&&view.transform.position.y<world.WaterHeight(view.transform.position)+.08f&&!(CandyTabletop.Current&&CandyTabletop.Current.Enabled)){alpha=1;transitionColor=new Color(.025f,.48f,.65f);}
            var sink=MatchRuntime.Current!=null&&session.Player!=null?MatchRuntime.Current.RulesFor(session.Player.gameObject):null;
            if(sink!=null&&sink.sinking&&alpha<.001f){alpha=.05f+sink.sinkProgress*.22f;transitionColor=new Color(.82f,.64f,.36f);}
            if(liquid){liquid.SetFloat("_Progress",Mathf.Lerp(-.1f,1.1f,Mathf.Clamp01(alpha)));curtain.color=new Color(transitionColor.r,transitionColor.g,transitionColor.b,1);}else curtain.color=new Color(transitionColor.r,transitionColor.g,transitionColor.b,Mathf.Clamp01(alpha));canvas.enabled=alpha>.001f||Shader.GetGlobalFloat("_CandyHeat")>.01f;
        }
        void OnDestroy(){RestoreHandTints();RestoreLooks();RestoreWinner();RestoreBlocks();foreach(var r in hidden)if(r)r.forceRenderingOff=false;foreach(var m in teamMaterials.Values)if(m)Destroy(m);if(healthMaterial)Destroy(healthMaterial);if(liquid)Destroy(liquid);if(deathPaint)Destroy(deathPaint);if(canvas)Destroy(canvas.gameObject);}
    }

    public sealed class CandyMelt : MonoBehaviour
    {
        public const float RagdollSeconds=1.4f, LiquefySeconds=4.5f, PuddleSeconds=1.25f, DrySeconds=2.75f;
        public const float Lifetime=RagdollSeconds+LiquefySeconds+PuddleSeconds+DrySeconds;
        float born,height,meltAt=-1,simulationTime;Vector3 meltCenter;VisualDeathRagdoll ragdoll;Transform puddle;readonly List<Material> owned=new List<Material>();readonly List<Mesh> meshes=new List<Mesh>();
        readonly Dictionary<(Color,float,Texture,Vector4),Material> materials=new Dictionary<(Color,float,Texture,Vector4),Material>();
        public float Age=>Time.unscaledTime-born;
        public float MeltProgress=>meltAt<0?0:Mathf.SmoothStep(0,1,(Age-meltAt)/LiquefySeconds);
        public bool RagdollSettled=>ragdoll!=null&&ragdoll.Settled;
        public int RagdollConstraintCount=>ragdoll?.ConstraintCount??0;
        public Bounds SnapshotBounds {get;private set;}
        public Bounds SkinnedSnapshotBounds {get;private set;}
        public int SkinnedSnapshotVertices {get;private set;}
        public ulong SkinnedSnapshotIndices {get;private set;}
        public Bounds CurrentBodyBounds=>ragdoll!=null?ragdoll.Bounds:SnapshotBounds;
        public static void Spawn(Transform visual,Color color)=>Spawn(visual,color,float.NaN);
        public static void Spawn(Transform visual,Color color,float floor)
        {
            if(!visual)return;
            var shader=Resources.Load<Shader>("CandyScenery/CandyDeathMelt");if(!shader)return;
            var root=new GameObject("Visual - melting candy ghost");var driver=root.AddComponent<CandyMelt>();driver.born=Time.unscaledTime;
            var snapshots=new List<(Renderer renderer,Mesh mesh)>();Bounds bounds=default;bool found=false;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if(!SnapshotRendererVisible(renderer,visual))continue;
                Mesh mesh=null;if(renderer is SkinnedMeshRenderer skinned){mesh=BakeSnapshot(skinned);if(mesh)driver.meshes.Add(mesh);}else if(renderer.TryGetComponent<MeshFilter>(out var filter))mesh=filter.sharedMesh;
                if(!mesh)continue;snapshots.Add((renderer,mesh));
                // Rig renderer bounds include a large animation safety margin, not the actual body.
                var actual=WorldBounds(mesh.bounds,renderer.transform);
                if(renderer is SkinnedMeshRenderer)
                {
                    if(driver.SkinnedSnapshotVertices==0)driver.SkinnedSnapshotBounds=actual;
                    else{var skinBounds=driver.SkinnedSnapshotBounds;skinBounds.Encapsulate(actual);driver.SkinnedSnapshotBounds=skinBounds;}
                    driver.SkinnedSnapshotVertices+=mesh.vertexCount;
                    for(int part=0;part<mesh.subMeshCount;part++)driver.SkinnedSnapshotIndices+=mesh.GetIndexCount(part);
                }
                if(!found){bounds=actual;found=true;}else bounds.Encapsulate(actual);
            }
            if(!found){Destroy(root);return;}
            if(float.IsNaN(floor)||float.IsInfinity(floor))floor=bounds.min.y;
            driver.SnapshotBounds=bounds;driver.height=Mathf.Max(.2f,bounds.size.y);
            // Actor roots may already be sinking or teleporting. Keep the exact visible pose at birth.
            var center=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);root.transform.position=center;
            driver.ragdoll=new VisualDeathRagdoll(visual,bounds);
            Color puddleColor=color;bool bodyPigmentFound=false;
            foreach(var snapshot in snapshots)
            {
                var renderer=snapshot.renderer;var mesh=snapshot.mesh;
                if(!(renderer is SkinnedMeshRenderer)){mesh=Instantiate(mesh);driver.meshes.Add(mesh);}
                var go=new GameObject("Visual - candy melt / "+renderer.name);go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);go.transform.localScale=renderer.transform.lossyScale;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var sourceMaterials=renderer.sharedMaterials;var source=new MaterialPropertyBlock();renderer.GetPropertyBlock(source);
                var array=new Material[mesh.subMeshCount];
                for(int i=0;i<array.Length;i++)
                {
                    Color tint=color;var original=i<sourceMaterials.Length?sourceMaterials[i]:null;
                    if(original&&original.HasProperty("_BaseColor"))tint=original.GetColor("_BaseColor");
                    if(source.HasColor("_BaseColor"))tint=source.GetColor("_BaseColor");
                    var perMaterial=new MaterialPropertyBlock();if(i<sourceMaterials.Length)renderer.GetPropertyBlock(perMaterial,i);
                    if(perMaterial.HasColor("_BaseColor"))tint=perMaterial.GetColor("_BaseColor");
                    Texture texture=null;var uv=new Vector4(1,1,0,0);
                    if(original)
                    {
                        string map=original.HasProperty("_BaseMap")?"_BaseMap":original.HasProperty("_MainTex")?"_MainTex":null;
                        if(map!=null){texture=original.GetTexture(map);var scale=original.GetTextureScale(map);var offset=original.GetTextureOffset(map);uv=new Vector4(scale.x,scale.y,offset.x,offset.y);}
                    }
                    if(source.GetTexture("_BaseMap"))texture=source.GetTexture("_BaseMap");
                    if(source.HasVector("_BaseMap_ST"))uv=source.GetVector("_BaseMap_ST");
                    if(perMaterial.GetTexture("_BaseMap"))texture=perMaterial.GetTexture("_BaseMap");
                    if(perMaterial.HasVector("_BaseMap_ST"))uv=perMaterial.GetVector("_BaseMap_ST");
                    var material=driver.MakeMaterial(shader,tint,center,0,texture,uv);array[i]=material;
                    if(!bodyPigmentFound&&renderer is SkinnedMeshRenderer&&i==0){puddleColor=Pigment(tint,texture);bodyPigmentFound=true;}
                }
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=array;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                // Deformation spreads beyond the original renderer bounds.
                r.localBounds=new Bounds(mesh.bounds.center,mesh.bounds.size+Vector3.one*driver.height*3);
                driver.ragdoll.Attach(renderer,go.transform,mesh);
            }
            var disk=new GameObject("Visual - spreading candy puddle");disk.transform.SetParent(root.transform,false);disk.transform.localPosition=Vector3.up*.014f;driver.puddle=disk.transform;
            var diskMesh=MakePuddle();driver.meshes.Add(diskMesh);disk.AddComponent<MeshFilter>().sharedMesh=diskMesh;
            var diskRenderer=disk.AddComponent<MeshRenderer>();diskRenderer.sharedMaterial=driver.MakeMaterial(shader,puddleColor,center,1);diskRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            driver.Present();
        }
        static Mesh BakeSnapshot(SkinnedMeshRenderer renderer)
        {
            var source=renderer.sharedMesh;if(!source)return null;
            if(!source.isReadable||source.blendShapeCount>0)
            {var fallback=new Mesh();renderer.BakeMesh(fallback);fallback.RecalculateBounds();return fallback;}
            var weights=source.boneWeights;var vertices=source.vertices;var normals=source.normals;
            var bones=renderer.bones;var bindposes=source.bindposes;
            if(weights.Length!=vertices.Length||bones.Length==0||bindposes.Length!=bones.Length)
            {var fallback=new Mesh();renderer.BakeMesh(fallback);fallback.RecalculateBounds();return fallback;}
            var pose=new Matrix4x4[bones.Length];var normalPose=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                pose[i]=bones[i]?renderer.transform.worldToLocalMatrix*bones[i].localToWorldMatrix*bindposes[i]:Matrix4x4.identity;
                normalPose[i]=pose[i].inverse.transpose;
            }
            // CPU skin in an explicit coordinate space. Native BakeMesh can fold a scaled
            // runtime rig into its root when its source renderer was just deactivated.
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var original=vertices[i];var normal=i<normals.Length?normals[i]:Vector3.up;
                float total=w.weight0+w.weight1+w.weight2+w.weight3;if(total<.0001f)continue;
                Vector3 p=Vector3.zero,n=Vector3.zero;
                void Add(int index,float strength)
                {if(strength<=0||index<0||index>=pose.Length)return;p+=pose[index].MultiplyPoint3x4(original)*strength;n+=normalPose[index].MultiplyVector(normal)*strength;}
                Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                vertices[i]=p/total;if(i<normals.Length)normals[i]=n.normalized;
            }
            var mesh=Instantiate(source);mesh.name="Death snapshot / "+source.name;
            mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;else mesh.RecalculateNormals();
            mesh.RecalculateBounds();return mesh;
        }
        internal static bool SnapshotRendererVisible(Renderer renderer,Transform visual)
        {
            if(!renderer||!renderer.enabled)return false;
            // Despawn deactivates the avatar root. Only previously inactive child branches are excluded.
            for(var node=renderer.transform;node&&node!=visual;node=node.parent)if(!node.gameObject.activeSelf)return false;
            return true;
        }
        static Color Pigment(Color tint,Texture texture)
        {
            if(!(texture is Texture2D pixels)||!pixels.isReadable)return tint;
            Color average=Color.clear;
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)average+=pixels.GetPixelBilinear((x+.5f)/4,(y+.5f)/4);
            average/=16;return new Color(tint.r*average.r,tint.g*average.g,tint.b*average.b,tint.a);
        }
        static Bounds WorldBounds(Bounds local,Transform source)
        {
            var matrix=source.localToWorldMatrix;var e=local.extents;
            Vector3 x=matrix.MultiplyVector(Vector3.right*e.x),y=matrix.MultiplyVector(Vector3.up*e.y),z=matrix.MultiplyVector(Vector3.forward*e.z);
            var extents=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center),extents*2);
        }
        Material MakeMaterial(Shader shader,Color color,Vector3 center,float disk,Texture texture=null,Vector4? textureUV=null)
        {
            var uv=textureUV??new Vector4(1,1,0,0);var key=(color,disk,texture,uv);
            if(materials.TryGetValue(key,out var cached))return cached;
            var material=new Material(shader);material.SetColor("_BaseColor",color);material.SetColor("_PigmentColor",Pigment(color,texture));material.SetVector("_MeltCenter",center);
            if(texture)material.SetTexture("_BaseMap",texture);material.SetVector("_BaseMap_ST",uv);
            material.SetFloat("_BodyHeight",height);material.SetFloat("_PuddleMesh",disk);owned.Add(material);materials[key]=material;return material;
        }
        static Mesh MakePuddle()
        {
            const int count=64;var vertices=new Vector3[count+1];var normals=new Vector3[count+1];var triangles=new int[count*3];normals[0]=Vector3.up;
            for(int i=0;i<count;i++)
            {
                float angle=i*Mathf.PI*2/count;float radius=1+.055f*Mathf.Sin(angle*5+.7f)+.035f*Mathf.Sin(angle*9);
                vertices[i+1]=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);normals[i+1]=Vector3.up;
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%count+1;triangles[i*3+2]=i+1;
            }
            var mesh=new Mesh{name="Candy puddle"};mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        void Present()
        {
            float progress=MeltProgress;
            float meltingAge=meltAt<0?0:Mathf.Max(0,Age-meltAt);
            float dry=Mathf.Clamp01((meltingAge-LiquefySeconds-PuddleSeconds)/DrySeconds);
            foreach(var material in owned){material.SetFloat("_MeltProgress",progress);material.SetFloat("_DryProgress",dry);}
            if(puddle){float spread=Mathf.SmoothStep(0,1,Mathf.Clamp01((meltingAge-.15f)/(LiquefySeconds-.15f)));puddle.localScale=new Vector3(height*.72f*spread,.01f,height*.59f*spread);}
        }
        void Update()
        {
            if(meltAt<0)
            {
                // Thirty visual steps per second, independent of gameplay physics and time scale.
                simulationTime+=Mathf.Min(Time.unscaledDeltaTime,.1f);
                bool stepped=false;
                while(simulationTime>=1f/30){ragdoll.Step(1f/30);simulationTime-=1f/30;stepped=true;}
                if(stepped)ragdoll.Present();
                if(Age>=RagdollSeconds&&(ragdoll.Settled||Age>=5))
                {
                    meltAt=Age;var body=ragdoll.Bounds;
                    var center=new Vector3(body.center.x,ragdoll.GroundHeight,body.center.z);meltCenter=center;
                    foreach(var material in owned)material.SetVector("_MeltCenter",center);
                    puddle.position=center+Vector3.up*.014f;
                }
            }
            else
            {
                var delta=ragdoll.FollowSurface();
                if(delta.sqrMagnitude>0){meltCenter+=delta;puddle.position+=delta;foreach(var material in owned)material.SetVector("_MeltCenter",meltCenter);}
                if(Age-meltAt>=LiquefySeconds+PuddleSeconds+DrySeconds){Destroy(gameObject);return;}
            }
            Present();
        }
        void OnDestroy(){foreach(var m in owned)if(m)Destroy(m);foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
