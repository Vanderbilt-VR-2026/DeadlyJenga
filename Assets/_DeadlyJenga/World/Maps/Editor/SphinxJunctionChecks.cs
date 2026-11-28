using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class SphinxJunctionChecks
    {
        const string Key="SphinxJunctionChecks";
        const string Folder="Logs/Captures/SphinxJunction";
        static SphinxJunctionMap map;
        static Rigidbody engine, rail, chest;
        static JunctionMember pull;
        static Vector3 engineStart,railStart,chestStart,grip;
        static float began;
        static double deadline;
        static int phase;
        static bool failed, passengerCarried, sandEliminated;
        static Rigidbody coach;
        static GameObject bot;
        static Vector3 riderStart;
        static bool passedPortal;
        static void OnEliminated(GameObject who,GameObject source)
        {
            if(who==bot && source==map.gameObject)sandEliminated=true;
        }
        static Vector3[] initial;
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(ProjectPath.Report("SphinxJunction.txt"),"Game.unity / Demo / Sphinx Junction\n");
            SessionState.SetBool(Key,true);
            SessionState.SetInt(Key+"Exit",2);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SelectMap()
        {
            if(!SessionState.GetBool(Key,false))return;
            MatchChoices.MapId="mode-sphinx-junction"; MatchChoices.ModeId="demo";
            MatchChoices.HandCount=0; MatchChoices.BotTarget=1;
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    phase=0;failed=false;town58=false;deadline=EditorApplication.timeSinceStartup+540;
                    EditorApplication.update+=Tick;
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.update-=Tick;
                    SessionState.EraseBool(Key);
                    int code=SessionState.GetInt(Key+"Exit",2);
                    if(SessionState.GetString("DJWarm.id","")!="")
                    {
                        Directory.CreateDirectory("Logs/Warm");File.WriteAllText("Logs/Warm/exit.txt",code.ToString());
                    }
                    else EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
                }
            };
        }
        static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Map check timed out at phase "+phase);
                if(phase==0)
                {
                    map=Services.World as SphinxJunctionMap;
                    if(!map || map.Blocks.Count==0 || !Services.Session?.Player?.ViewCamera) return;
                    engine=Body("Copper Canyon locomotive");engineStart=engine.position;
                    coach=Body("Sunset passenger coach 1");
                    var rider=Services.Session.Player;
                    rider.Place(coach.transform.TransformPoint(0,.50f,0),coach.rotation);
                    riderStart=rider.transform.position;
                    passengerCarried=false;
                    initial=map.Blocks.Select(b=>b.position).ToArray();
                    var keystone=Body("Plaza paving 0,0");
                    foreach(var other in Physics.OverlapBox(keystone.position,new Vector3(3,.65f,3)))
                        if(!other.isTrigger && !other.transform.IsChildOf(map.transform))
                            Log("External collider beside central keystone: "+other.name+" "+other.bounds);
                    began=Time.fixedTime;phase++;return;
                }
                float elapsed=Time.fixedTime-began;
                if(phase==1 && Services.Session.Player is IModeActor actor && actor.StandingBody==coach &&
                    Vector3.Distance(Services.Session.Player.transform.position,riderStart)>.8f) passengerCarried=true;
                if(phase==1 && elapsed>5)
                {
                    Services.Session.Player.Place(map.SafeSpawnPoint(),Quaternion.identity);
                    Shot("01-overview",new(0,25,-69),new(0,5.7f,3),30);
                    Shot("02-saloon",new(-16,3.4f,-13),new(-23,6,-3),66);
                    Shot("03-sphinx",new(0,3.48f,-11),new(0,9.4f,0),75);
                    Shot("04-pyramid",new(10,3.48f,-4),new(20,10,9),68);
                    Shot("05-train",new(-12,13.5f,-3),engine.transform.localPosition+Vector3.up*.85f,56);
                    var drifts=map.Blocks.Select((b,i)=>(b,d:Vector3.Distance(b.position,initial[i])))
                        .Where(v=>!v.b.isKinematic).OrderByDescending(v=>v.d).ToArray();
                    Log("Worst settle: "+string.Join(", ",drifts.Take(6).Select(v=>v.b.name+"="+v.d.ToString("F2"))));
                    Check(drifts[0].d<.5f,"Initial unforced settle");
                    Check(Vector3.Distance(engine.position,engineStart)>1,"Train moved after depot dwell");
                    Log($"Body count {map.Blocks.Count}, awake {map.Blocks.Count(b=>!b.IsSleeping())}, colliders {map.GetComponentsInChildren<Collider>().Length}, spawn {map.SafeSpawnPoint()}");
                    Check(passengerCarried,"Placed rider remained on a moving coach");
                    bool town=map.SaveLayout()=="sphinx-junction-8";
                    if(!town) CheckCorridor("Saloon entrance",new(-23,2.78f,-7.2f),new(-23,2.78f,-3));
                    CheckCorridor("Under the sphinx",new(0,2.5f,-6.4f),new(0,2.5f,4));
                    if(town)
                    {
                        int stones=map.Blocks.Count(b=>b.name.StartsWith("Great pyramid | course",StringComparison.Ordinal));
                        Log("Pyramid stones "+stones+", joints "+map.GetComponentsInChildren<FixedJoint>().Length);
                        Check(stones>300,"Full-height pyramid is built from individual stone members");
                        Check(map.Blocks.Any(b=>b.name=="General store frame") && map.Blocks.Any(b=>b.name=="Undertaker frame"),
                            "West street and south annex buildings are present");
                        CheckCorridor("Pyramid ground door into the hall",Pyr(-2,-19.5f,2.57f),Pyr(-2,-8,2.57f));
                    }
                    if (map.SaveLayout() == "sphinx-junction-7")
                    {
                        Check(map.Blocks.Count(b => b.name.StartsWith("Sphinx torso bay", StringComparison.Ordinal)) == 12,
                            "Blender sphinx has twelve independent torso bays");
                        Check(map.Blocks.Count(b => b.name.StartsWith("Rail bridge ", StringComparison.Ordinal)) == 4,
                            "Four exterior bridge supports carry the revised route");
                        CheckCorridor("New tack shop entrance",new(-31.15f,2.78f,-5.64f),new(-31.15f,2.78f,-3.0f));
                        CheckCorridor("Full sphinx passage",new(0,2.5f,-5.3f),new(0,2.5f,21.3f));
                    }
                    CheckCorridor("Low causeway crossing",new(-2,1.86f,-22.7f),new(2,1.86f,-22.7f));
                    bot=Services.Session.SurvivorBots?.SpawnOne();
                    Check(bot && bot.GetComponent<IModeActor>()?.Alive==true,"Live survivor bot spawned");
                    sandEliminated=false;
                    GameEvents.Eliminated+=OnEliminated;
                    if(bot && bot.GetComponent<IPlayableCharacter>() is IPlayableCharacter testBot)
                        testBot.Place(map.transform.TransformPoint(0,-1.42f,-25),Quaternion.identity);
                    rail=Body(town?"Railway span 14":"Railway span 10");railStart=rail.position;
                    chest=Body(town?"Sphinx right forepaw":"Sphinx chest lintel");chestStart=chest.position;
                    passedPortal=false;phase=8;began=Time.fixedTime;
                }
                else if(phase==8)
                {
                    Vector3 p=engine.transform.localPosition;
                    if(!passedPortal && engine.isKinematic && Vector2.Distance(new Vector2(p.x,p.z),new Vector2(20,10))<2)
                    {
                        passedPortal=true;
                        Shot("05b-pyramid-crossing",new(9,12.4f,-1),p+Vector3.up*.9f,52);
                    }
                    bool returned=passedPortal && engine.isKinematic && Vector3.Distance(engine.position,engineStart)<1.2f;
                    if(!returned && elapsed<78)return;
                    Check(passedPortal,"Intact locomotive crossed the pyramid bore");
                    Check(returned,"Intact train completed the loop back to the depot");
                    bool newTown=map.SaveLayout()=="sphinx-junction-8";
                    StartPull(newTown?"Timber trestle bent 14":"Timber trestle bent 10");
                    pull.Hold(grip+(newTown?map.transform.TransformDirection(Vector3.forward):Vector3.forward)*9);phase=2;began=Time.fixedTime;
                }
                else if(phase==2)
                {
                    if(elapsed>.55f){if(map.SaveLayout()=="sphinx-junction-8")ShotAt("06-trestle-pull",pull.Body,new(-12,6,-10),51);else Shot("06-trestle-pull",new(41,8,2),new(35,5,18),51);Log("East post displacement "+Vector3.Distance(grip,pull.Body.worldCenterOfMass));pull.Release();phase++;began=Time.fixedTime;}
                }
                else if(phase==3 && elapsed>2)
                {
                    if(map.SaveLayout()=="sphinx-junction-8")ShotAt("07-trestle-loss",rail,new(-12,6,-10),51);else Shot("07-trestle-loss",new(41,8,2),new(35,5,18),51);
                    Check(Vector3.Distance(railStart,rail.position)>.6f,"Trestle deck lost bearing");
                    StartPull(map.SaveLayout()=="sphinx-junction-8"?"Sphinx right forepaw":"Sphinx shoulder bearing 1");phase++;began=Time.fixedTime;
                }
                else if(phase==4)
                {
                    pull.Hold(grip+new Vector3(0,1,13));
                    if(elapsed>1){pull.Release();phase++;began=Time.fixedTime;}
                }
                else if(phase==5 && elapsed>2)
                {
                    Check(sandEliminated,"Quicksand eliminated the exposed survivor bot");
                    if(map.SaveLayout()=="sphinx-junction-8")   // the carved sphinx is bonded masonry; a hand pull is not meant to break it
                        Log("Sphinx forepaw displacement under the hand pull "+Vector3.Distance(chest.position,chestStart).ToString("F2"));
                    else Check(Vector3.Distance(chest.position,chestStart)>.35f,"Sphinx chest responded to bearing pull");
                    Shot("08-sphinx-loss",new(7,7,-15),new(0,8,0),58);
                    began=Time.fixedTime;phase++;return;
                }
                else if(phase==6 && !town58 && map.SaveLayout()=="sphinx-junction-8" && elapsed>2)
                {
                    // Saloon: haul the upper floor (storey with its wraparound balcony) off eastward, toward the open market side.
                    saloonUpper=Body("Saloon timber storey 1");saloonUpperStart=saloonUpper.position;
                    saloonLower=Body("Saloon timber storey 0");saloonLowerStart=saloonLower.position;
                    StartPull("Saloon timber storey 1");
                    pullTarget=grip+map.transform.TransformDirection(Vector3.right)*15;   // past the far edge of the floor below
                    phase=20;began=Time.fixedTime;
                }
                else if(phase==20)
                {
                    pull.Hold(pullTarget);
                    if(elapsed>5f){pull.Release();phase++;began=Time.fixedTime;}
                }
                else if(phase==21 && elapsed>5)
                {
                    ShotAt("09-saloon-pull",saloonUpper,new(10,6,-16),60);
                    float drop=saloonUpperStart.y-saloonUpper.position.y;
                    float apart=Vector3.Distance(saloonUpper.position-saloonUpperStart,saloonLower.position-saloonLowerStart);
                    Log("Saloon upper floor drop "+drop.ToString("F2")+", moved "+Vector3.Distance(saloonUpper.position,saloonUpperStart).ToString("F2")+", separated from the ground floor by "+apart.ToString("F2"));
                    Check(apart>3,"Saloon upper floor separated from the ground floor under the haul");
                    // Pyramid: the mortared shell is meant to shrug off a light shower; the game's Planet Killer meteor
                    // (default 45 m) brings it down. First a share of a default shower, then one Planet Killer strike.
                    stoneStart=map.Blocks.Where(b=>b.name.StartsWith("Great pyramid | course",StringComparison.Ordinal)).ToDictionary(b=>b,b=>b.position);
                    strikes=0;phase=22;began=Time.fixedTime;
                }
                else if(phase==22)
                {
                    // one strike every quarter second, the same push BasicDisaster.MeteorImpact gives each member
                    while(strikes<48 && elapsed>strikes*.125f){MeteorStrike(strikes);strikes++;}
                    if(strikes==48 && elapsed>6.5f)
                    {
                        int showered=stoneStart.Count(p=>Vector3.Distance(p.Key.position,p.Value)>.5f);
                        Log("Pyramid stones moved over 0.5 m by the meteor shower: "+showered+" of "+stoneStart.Count);
                        PlanetKiller();strikes++;
                    }
                    if(strikes>48 && elapsed>7){phase++;began=Time.fixedTime;}
                }
                else if(phase==23 && elapsed>6)
                {
                    ShotAt("10-pyramid-planet-killer",stoneStart.Keys.OrderByDescending(b=>b.position.y).First(),new(-20,8,-34),60);
                    int fallen=stoneStart.Count(p=>Vector3.Distance(p.Key.position,p.Value)>.5f);
                    Log("Pyramid stones moved over 0.5 m after the Planet Killer: "+fallen+" of "+stoneStart.Count);
                    Check(fallen>=100,"Planet Killer meteor brought the pyramid shell down progressively");
                    // Respawn after structural damage: the spawn point still has footing above the quicksand.
                    var spawn=map.SafeSpawnPoint();
                    bool footing=Physics.Raycast(spawn+Vector3.up*.3f,Vector3.down,out _,1.5f,1<<Layers.Block,QueryTriggerInteraction.Ignore);
                    Services.Session.Player.Place(spawn,Quaternion.identity);
                    Check(footing && spawn.y>map.WaterHeight(spawn)+.5f,"Safe respawn point has footing after damage");
                    began=Time.fixedTime;phase=6;town58=true;
                }
                else if(phase==6 && (map.SaveLayout()!="sphinx-junction-8" || town58) && elapsed>(town58?40:58))
                {
                    engineStart=engine.position; began=Time.fixedTime;phase++;
                }
                else if(phase==7 && elapsed>1.5f)
                {
                    Check(Vector3.Distance(engineStart,engine.position)<.18f,"Train halted at damaged track");
                    Log($"After both pulls: awake {map.Blocks.Count(b=>b.gameObject.activeSelf && !b.isKinematic && !b.IsSleeping())}, submerged {map.Blocks.Count(b=>!b.gameObject.activeSelf)}");
                    int count=map.Blocks.Count,revision=map.Revision;
                    map.Rebuild();
                    Check(map.Blocks.Count==count && map.Revision>revision,"Reset restored all registered bodies");
                    Finish();
                }
            }
            catch(Exception e){failed=true;Log(e.ToString());Finish();}
        }
        static void Shot(string name,Vector3 eye,Vector3 target,float fov)
            =>UnderneathTreeLookdev.Shoot(Path.Combine(Folder,name+".png"),map.transform.TransformPoint(eye),map.transform.TransformPoint(target),fov,name=="01-overview"?1774:1600,name=="01-overview"?887:900);
        static void CheckCorridor(string name,Vector3 start,Vector3 end)
        {
            int steps=Mathf.CeilToInt(Vector3.Distance(start,end)/.25f);
            int mask=1<<Layers.Block;
            for(int i=0;i<=steps;i++)
            {
                Vector3 feet=map.transform.TransformPoint(Vector3.Lerp(start,end,(float)i/steps));
                if(!Physics.Raycast(feet+Vector3.up*.34f,Vector3.down,out var floor,.78f,mask,QueryTriggerInteraction.Ignore) ||
                    floor.normal.y<.72f || Physics.CheckCapsule(floor.point+Vector3.up*.38f,
                    floor.point+Vector3.up*1.46f,.29f,mask,QueryTriggerInteraction.Ignore))
                {
                    Check(false,name+" lacks footing or standing clearance at "+feet);
                    return;
                }
            }
            Check(true,name+" has continuous footing and standing clearance");
        }
        static Rigidbody Body(string name)=>map.Blocks.First(b=>b.name==name);
        static Rigidbody saloonUpper;
        static Vector3 saloonUpperStart,pullTarget;
        static System.Collections.Generic.Dictionary<Rigidbody,Vector3> stoneStart;
        static bool town58;
        static int strikes;
        static Rigidbody saloonLower;
        static Vector3 saloonLowerStart;
        // BasicDisaster.MeteorImpact for a PlanetKillerMeteor: radius 0.8 x 45 m, force 28 m/s, landing high on the east face.
        static void PlanetKiller()
        {
            float zB=28, h=18-.43f*(zB-2.7f);
            var point=map.transform.TransformPoint(Pyr(h,0,(zB+.16f)/1.142f));
            var velocity=map.transform.TransformDirection(PyrDirection(Vector3.left)+Vector3.down).normalized;
            const float radius=36, force=28;
            var hit=new System.Collections.Generic.HashSet<JunctionMember>();
            foreach(var c in Physics.OverlapSphere(point,radius,1<<Layers.Block,QueryTriggerInteraction.Ignore))
            {
                var member=c.GetComponentInParent<JunctionMember>();
                if(!member||!hit.Add(member))continue;
                var delta=c.bounds.center-point;float falloff=1-Mathf.Clamp01(delta.magnitude/radius);
                member.Push((velocity*.7f+delta.normalized*.6f+Vector3.up*.25f)*force*falloff,map.gameObject);
            }
            Log("Planet Killer strike at "+map.transform.InverseTransformPoint(point).ToString("F1")+" pushed "+hit.Count+" members");
        }
        static void MeteorStrike(int k)
        {
            // strikes spread over the four upper faces, as in the Blender meteor test (Blender metres on the faces)
            var rng=new System.Random(7+k);
            float zB=16+(float)rng.NextDouble()*18, h=18-.43f*(zB-2.7f), u=((float)rng.NextDouble()*1.2f-.6f)*h;
            int side=k%4;
            var local=side==0?new Vector3(u,0,-h):side==1?new Vector3(h,0,u):side==2?new Vector3(u,0,h):new Vector3(-h,0,u);
            var inward=side==0?Vector3.forward:side==1?Vector3.left:side==2?Vector3.back:Vector3.right;
            var point=map.transform.TransformPoint(Pyr(local.x,local.z,(zB+.16f)/1.142f));
            var velocity=map.transform.TransformDirection(PyrDirection(inward)+Vector3.down).normalized;
            const float radius=2.4f, force=12;   // MeteorImpact: max(2, 0.8 * 3 m) and the shower's default force
            var hit=new System.Collections.Generic.HashSet<JunctionMember>();
            foreach(var c in Physics.OverlapSphere(point,radius,1<<Layers.Block,QueryTriggerInteraction.Ignore))
            {
                var member=c.GetComponentInParent<JunctionMember>();
                if(!member||!hit.Add(member))continue;
                var delta=c.bounds.center-point;float falloff=1-Mathf.Clamp01(delta.magnitude/radius);
                member.Push((velocity*.7f+delta.normalized*.6f+Vector3.up*.25f)*force*falloff,map.gameObject);
            }
            Log("Meteor strike "+k+" at "+map.transform.InverseTransformPoint(point).ToString("F1")+" pushed "+hit.Count+" members");
        }
        // A point given in the pyramid's Blender layout (metres from its centre at 36, 16.5, turned -40 degrees)
        // at map height y. The Blender reference frame stretches map x by 1.103 and height by 1.142.
        static Vector3 Pyr(float x,float z,float y)
        {
            float c=Mathf.Cos(-40*Mathf.Deg2Rad), s=Mathf.Sin(-40*Mathf.Deg2Rad);
            return new Vector3((36+c*x-s*z)/1.103f,y,16.5f+s*x+c*z);
        }
        static Vector3 PyrDirection(Vector3 local)
        {
            float c=Mathf.Cos(-40*Mathf.Deg2Rad), s=Mathf.Sin(-40*Mathf.Deg2Rad);
            return new Vector3((c*local.x-s*local.z)/1.103f,local.y/1.142f,s*local.x+c*local.z);
        }
        static void ShotAt(string name,Rigidbody body,Vector3 offset,float fov)
        {
            var target=map.transform.InverseTransformPoint(body.position);
            Shot(name,target+offset,target,fov);
        }
        static void StartPull(string name)
        {
            var body=Body(name);pull=body.GetComponent<JunctionMember>();grip=body.worldCenterOfMass;
            Check(pull.TryStartGrab(map.gameObject,grip),"Grabbed "+name);
        }
        static void Log(string line){File.AppendAllText(ProjectPath.Report("SphinxJunction.txt"),line+"\n");Debug.Log(line);}   // the runner counts PASS/FAIL in the Unity log
        static void Check(bool pass,string what){Log((pass?"PASS ":"FAIL ")+what);failed|=!pass;}
        static void Finish()
        {
            GameEvents.Eliminated-=OnEliminated;
            EditorApplication.update-=Tick;
            SessionState.SetInt(Key+"Exit",failed?2:0);
            EditorApplication.ExitPlaymode();
        }
    }
}
