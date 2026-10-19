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
        Shader healthShader;Material healthMaterial;float scanAt,transitionAt=-100;Color transitionColor;Image curtain;Canvas canvas;Camera view;Material liquid;
        bool wasOutage;string skin="";int winner=-2;readonly Dictionary<Renderer,MaterialPropertyBlock> winnerOriginal=new Dictionary<Renderer,MaterialPropertyBlock>();
        readonly Dictionary<Renderer,Material> blockOriginals=new Dictionary<Renderer,Material>();readonly Dictionary<(Material,string),Material> teamMaterials=new Dictionary<(Material,string),Material>();int claimRevision=-1;
        readonly List<string> staleLooks=new List<string>();CandyEnvironment[] environments;float environmentAt;
        readonly Dictionary<Renderer,MaterialPropertyBlock> handOriginals=new Dictionary<Renderer,MaterialPropertyBlock>();readonly HashSet<Renderer> activeHands=new HashSet<Renderer>();readonly List<Renderer> oldHands=new List<Renderer>();
        readonly HashSet<string> melting=new HashSet<string>();readonly HashSet<Renderer> hidden=new HashSet<Renderer>();
        void OnEnable(){props=new MaterialPropertyBlock();healthShader=Resources.Load<Shader>("CandyScenery/CandyAvatarHealth");if(healthShader&&!healthMaterial){healthMaterial=new Material(healthShader);healthMaterial.enableInstancing=true;healthMaterial.SetFloat("_Smoothness",.8f);}GameEvents.Eliminated+=Eliminated;}
        void OnDisable(){GameEvents.Eliminated-=Eliminated;RestoreHandTints();RestoreLooks();RestoreWinner();RestoreBlocks();}
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
                if(player.role!="hand"||player.team<0)continue;var root=runtime.FindParticipantRoot(player.id);if(!root)continue;
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
                        var saved=new MaterialPropertyBlock();r.GetPropertyBlock(saved);list.Add(new Look{renderer=r,originals=original,health=material,originalProps=saved});r.sharedMaterials=material;
                    }
                    looks[p.id]=list;
                }
                foreach(var look in list)if(look.renderer&&look.health.Length>0&&look.renderer.sharedMaterial!=look.health[0]){look.originals=look.renderer.sharedMaterials;look.renderer.GetPropertyBlock(look.originalProps);look.renderer.sharedMaterials=look.health;}
                float target=f.rules.healthEnabled?Mathf.Clamp01(p.health):1;
                float shown=displayedHealth.TryGetValue(p.id,out var previous)?Mathf.Lerp(previous,target,1-Mathf.Exp(-24*Time.unscaledDeltaTime)):target;
                if(Mathf.Abs(shown-target)<.001f)shown=target;displayedHealth[p.id]=shown;
                float bottom=actor.transform.position.y,top=Mathf.Max(bottom+.1f,actor.ViewPose.position.y);
                foreach(var look in list)if(look.renderer)
                {look.renderer.GetPropertyBlock(props);props.SetColor("_BaseColor",p.color);props.SetFloat("_Health",shown);props.SetFloat("_Sink",runtime.RulesFor(actor.gameObject).sinkProgress);props.SetFloat("_Bottom",bottom);props.SetFloat("_Top",top);look.renderer.SetPropertyBlock(props);}
            }
        }
        void RestoreLooks()
        {
            foreach(var list in looks.Values)ReleaseLooks(list);looks.Clear();displayedHealth.Clear();
        }
        void ReleaseLooks(List<Look> list)
        {foreach(var l in list){if(l.renderer){l.renderer.sharedMaterials=l.originals;l.renderer.SetPropertyBlock(l.originalProps);}}}
        void Winner(MatchFrame f)
        {
            int team=f.phase==RoundPhase.Victory?f.winnerTeam:-2;if(team==winner)return;RestoreWinner();winner=team;if(team<0){RestoreBlocks();claimRevision=-1;return;}
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
            if(actor.IsLocal){transitionAt=Time.unscaledTime;transitionColor=actor.Color;}
        }
        void Deaths(IMatchRuntime runtime,MatchFrame frame)
        {
            foreach(var p in frame.players)
            {
                var actor=runtime.FindActor(p.id);if(actor?.Visual==null)continue;
                if(p.dying&&melting.Add(p.id)){CandyMelt.Spawn(actor.Visual,p.color);if(p.id==runtime.LocalActorId){transitionAt=Time.unscaledTime;transitionColor=p.color;}}
                if(!p.dying&&p.alive)melting.Remove(p.id);
                foreach(var r in actor.Visual.GetComponentsInChildren<Renderer>())
                {if(p.dying||!p.alive){r.forceRenderingOff=true;hidden.Add(r);}else if(hidden.Remove(r))r.forceRenderingOff=false;}
            }
        }
        void Transition()
        {
            var session=Services.Session;if(session?.ActiveCamera==null)return;
            if(!canvas)
            {
                var go=new GameObject("Visual - liquid respawn curtain",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.sortingOrder=100;
                var image=new GameObject("Candy liquid",typeof(RectTransform),typeof(Image));image.transform.SetParent(go.transform,false);curtain=image.GetComponent<Image>();curtain.raycastTarget=false;curtain.rectTransform.anchorMin=Vector2.zero;curtain.rectTransform.anchorMax=Vector2.one;curtain.rectTransform.offsetMin=curtain.rectTransform.offsetMax=Vector2.zero;
                var shader=Resources.Load<Shader>("CandyScenery/CandyLiquidCurtain");if(shader){liquid=new Material(shader);curtain.material=liquid;}
            }
            if(view!=session.ActiveCamera)
            {view=session.ActiveCamera;var r=canvas.GetComponent<RectTransform>();if(session.IsVR){canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view;r.SetParent(view.transform,false);r.sizeDelta=new Vector2(4,4);r.localPosition=new Vector3(0,0,.15f);r.localRotation=Quaternion.identity;r.localScale=Vector3.one;}else{canvas.renderMode=RenderMode.ScreenSpaceOverlay;r.SetParent(transform,false);}}
            float age=Time.unscaledTime-transitionAt;float alpha=age<.3f?Mathf.SmoothStep(0,1,age/.3f):1-Mathf.SmoothStep(0,1,(age-.3f)/.9f);
            var world=Services.World;if(world!=null&&view.transform.position.y<world.WaterHeight(view.transform.position)+.08f&&!(CandyTabletop.Current&&CandyTabletop.Current.Enabled)){alpha=1;transitionColor=new Color(.025f,.48f,.65f);}
            var sink=MatchRuntime.Current!=null&&session.Player!=null?MatchRuntime.Current.RulesFor(session.Player.gameObject):null;
            if(sink!=null&&sink.sinking&&alpha<.001f){alpha=.05f+sink.sinkProgress*.22f;transitionColor=new Color(.82f,.64f,.36f);}
            if(liquid){liquid.SetFloat("_Progress",Mathf.Lerp(-.1f,1.1f,Mathf.Clamp01(alpha)));curtain.color=new Color(transitionColor.r,transitionColor.g,transitionColor.b,1);}else curtain.color=new Color(transitionColor.r,transitionColor.g,transitionColor.b,Mathf.Clamp01(alpha));canvas.enabled=alpha>.001f||Shader.GetGlobalFloat("_CandyHeat")>.01f;
        }
        void OnDestroy(){RestoreHandTints();RestoreLooks();RestoreWinner();RestoreBlocks();foreach(var r in hidden)if(r)r.forceRenderingOff=false;foreach(var m in teamMaterials.Values)if(m)Destroy(m);if(healthMaterial)Destroy(healthMaterial);if(liquid)Destroy(liquid);if(canvas)Destroy(canvas.gameObject);}
    }

    public sealed class CandyMelt : MonoBehaviour
    {
        float born;readonly List<Material> owned=new List<Material>();readonly List<Mesh> meshes=new List<Mesh>();
        public static void Spawn(Transform visual,Color color)
        {
            var shader=Resources.Load<Shader>("CandyScenery/CandyAvatarHealth");if(!shader)return;
            var root=new GameObject("Visual - melting candy ghost");var driver=root.AddComponent<CandyMelt>();driver.born=Time.unscaledTime;
            var bounds=new Bounds(visual.position,Vector3.one*.01f);foreach(var r in visual.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh=null;if(renderer is SkinnedMeshRenderer skinned){mesh=new Mesh();skinned.BakeMesh(mesh);driver.meshes.Add(mesh);}else if(renderer.TryGetComponent<MeshFilter>(out var filter))mesh=filter.sharedMesh;if(!mesh)continue;
                var go=new GameObject("Visual - candy melt");go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);go.transform.localScale=renderer.transform.lossyScale;go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var material=new Material(shader);material.SetColor("_BaseColor",color);material.SetFloat("_Bottom",bounds.min.y);material.SetFloat("_Top",bounds.max.y);material.SetFloat("_Health",1);material.SetFloat("_MeltStart",driver.born);driver.owned.Add(material);var r=go.AddComponent<MeshRenderer>();var array=new Material[mesh.subMeshCount];for(int i=0;i<array.Length;i++)array[i]=material;r.sharedMaterials=array;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        void Update(){if(Time.unscaledTime-born>1.6f)Destroy(gameObject);}
        void OnDestroy(){foreach(var m in owned)if(m)Destroy(m);foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
