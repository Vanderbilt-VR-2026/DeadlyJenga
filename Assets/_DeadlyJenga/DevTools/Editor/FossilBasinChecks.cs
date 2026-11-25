using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastleBots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class FossilBasinChecks
    {
        const string Key="FossilBasinChecks.Active", Folder="Logs/Captures/FossilBasin";
        static int phase;
        static float clock;
        static double deadline;
        static FossilBasinMap map;
        static Vector3[] initial;
        static TestDummy dummy;
        static FossilMember pulled;
        static FossilMember[] outerPosts;
        static int pullIndex;
        static Camera camera;
        static Vector3 grab,footBefore;
        static Rigidbody westRefuge;
        static Vector3 westBefore;
        static Rigidbody foot;
        static string report;
        static bool valid;
        static System.Collections.IEnumerator peer;
        static CastleSurvivor walker;
        static Vector3 destination;
        static float lastWalk;
        static int walkStage;
        static CastleBotBrain routeBot;
        static bool botArrived;
        static readonly Vector3 BotGoal=new Vector3(-12.59f,3.06f,-3.68f);
        static void CheckClimb()
        {
            // Start a measured hand-span from an actual Stegosaurus leg face.
            var femur=map.Blocks.First(b=>b.name=="Stegosaurus upper leg -1");
            var col=femur.GetComponentInChildren<BoxCollider>();
            var ray=new Ray(col.bounds.center+Vector3.back*3,Vector3.forward);
            if(!col.Raycast(ray,out var hit,6)){Check(false,"bone face ray");return;}
            var outward=Vector3.ProjectOnPlane(hit.normal,Vector3.up).normalized;
            var feet=hit.point+outward*(walker.Capsule.radius+.065f)-Vector3.up*(walker.Height*.64f);
            walker.Place(feet,Quaternion.LookRotation(-outward));
            walker.InputSuspended=false;
            bool latched=false;float best=feet.y;
            for(int i=0;i<50;i++)
            {
                walker.Simulate(Vector2.up,false,.02f,true);
                latched|=walker.Climbing;
                if(walker.Climbing)best=Mathf.Max(best,walker.transform.position.y);
            }
            Check(latched&&best>feet.y+.12f,"survivor latched and climbed bone face; rise "+(best-feet.y));
        }
        static FossilBasinChecks(){EditorApplication.playModeStateChanged+=State;}
        public static void Run()=>RunFaceoff();
        public static void RunFaceoff()=>Begin(false, true);
        public static void RunScenery()=>Begin(true);
        static void Begin(bool sceneryOnly, bool faceoff = false)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start in Edit Mode");
            Directory.CreateDirectory(Folder);
            SessionState.SetBool(Key+".scenery",sceneryOnly);
            SessionState.SetBool(Key+".faceoff",faceoff);
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                MatchSetup.MapId="fossil-basin";MatchSetup.ModeId="demo";MatchSetup.FromLobby=false;
                phase=0;valid=true;report="";deadline=EditorApplication.timeSinceStartup+120;
                EditorApplication.update+=Step;
            }
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            SessionState.EraseBool(Key);
            int code=SessionState.GetInt(Key+".result",2);
            if(string.IsNullOrEmpty(SessionState.GetString("DJWarm.id","")))EditorApplication.Exit(code);
            else {Directory.CreateDirectory("Logs/Warm");File.WriteAllText("Logs/Warm/exit.txt",code.ToString());}
        }
        static void Check(bool ok,string label){report+=(ok?"PASS ":"FAIL ")+label+"\n";valid&=ok;}
        static void End()
        {
            File.WriteAllText(Folder+"/checks.txt",report);
            Debug.Log(report);
            SessionState.SetInt(Key+".result",valid?0:2);
            EditorApplication.update-=Step;EditorApplication.ExitPlaymode();
        }
        static void Shot(string name,Vector3 eye,Vector3 look,float fov)
        {
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye));camera.fieldOfView=fov;
            MapLiveCapture.RenderNow(camera,1440,960,Folder+"/"+name+".png");
        }
        static void Wide(string name)=>Shot(name,new Vector3(-.5f,13.6f,-46),new Vector3(0,7.1f,0),34);
        static void StepFaceoff(float dt)
        {
            if(phase==1&&dt>3)
            {
                var bodies=map.Blocks;
                Check(bodies.Count>500,"expanded Blender member count "+bodies.Count);
                float worst=bodies.Select((body,i)=>Vector3.Distance(body.position,initial[i])).Max();
                Check(worst<.7f,"unloaded maximum travel "+worst);
                foreach(var item in bodies.Select((b,i)=>(b,travel:Vector3.Distance(b.position,initial[i]))).OrderByDescending(x=>x.travel).Take(8))
                    report+=item.b.name+" travel "+item.travel+" at "+item.b.position+"\n";
                report+="Joints remaining "+map.GetComponentsInChildren<Joint>().Length+"\n";
                // Renders, bot spawn and the pull get their own time budget after the settle.
                deadline=EditorApplication.timeSinceStartup+120;
                var spawn=map.SafeSpawnPoint();
                // A foot-sized probe: deck centres can sit on the thin seam between two stones.
                Check(Physics.SphereCast(spawn+Vector3.up*.4f,.15f,Vector3.down,out var bearing,.7f,1<<Layers.Block)&&bearing.normal.y>.86f,
                    "survivor spawn has flat bearing at "+spawn);
                var skull=bodies.First(body=>body.name=="Rex skull");
                Check(skull.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount>500,"imported Rex skull mesh");
                Check(Climbables.TryGet(skull.GetComponentInChildren<Collider>(),out var climbable)&&climbable.Body==skull,
                    "imported fossil has moving climb collider");
                foreach(string route in new[]{"West apron to tail cross-span","East apron to main switchback"})
                    Check(bodies.Any(b=>b.name=="Teal flight "+route),"main route "+route);
                var survivor=Services.Session.Player?.gameObject.GetComponent<CastleSurvivor>();
                Check(survivor,"desktop survivor motor present");
                if(survivor)
                {
                    survivor.Place(spawn,Quaternion.identity);
                    survivor.externallyDriven=true;
                    for(int i=0;i<20;i++)survivor.Simulate(Vector2.right,false,.02f);
                    Check(survivor.transform.position.y>map.WaterHeight(survivor.transform.position),"survivor starts above tar and receives movement");
                    survivor.externallyDriven=false;
                }
                var bots=Services.Session.SurvivorBots;
                bots.Running=true;Check(bots.SpawnOne(),"survivor bot spawn");
                Shot("21-faceoff-wide",new Vector3(-4,25,-84),new Vector3(-4,12,1),37);
                Shot("22-faceoff-camp",new Vector3(1,2.38f,-20),new Vector3(-9,10,-6),64);
                var probe=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                probe.transform.position=new Vector3(35,.8f,-2);
                probe.transform.localScale=Vector3.one*.3f;
                probe.AddComponent<Rigidbody>();dummy=probe.AddComponent<TestDummy>();
                // The bay has four posts, so knock out both outer ones (top unit of each).
                outerPosts=bodies.Where(b=>b.name.StartsWith("B5")&&b.name.Contains(" pier "))
                    .GroupBy(b=>b.name.Substring(0,b.name.LastIndexOf('/')))
                    .Select(g=>g.OrderByDescending(b=>b.position.y).First())
                    .OrderByDescending(b=>b.position.x).Take(2).Select(b=>b.GetComponent<FossilMember>()).ToArray();
                pullIndex=0;pulled=outerPosts[0];
                foot=bodies.Where(b=>b.name.StartsWith("Triceratops tarsals")).OrderByDescending(b=>b.position.x).First();
                footBefore=foot.position;
                westRefuge=bodies.First(b=>b.name.StartsWith("A1")&&b.name.Contains(" deck "));
                westBefore=westRefuge.position;grab=pulled.Body.worldCenterOfMass;
                Check(pulled.TryStartGrab(camera.gameObject,grab),"grab Triceratops outer bearing");
                clock=Time.fixedTime;phase=2;return;
            }
            if(phase==2)
            {
                pulled.Hold(grab+Vector3.right*Mathf.Min(6,dt*5));
                if(dt<1.5f)return;
                pulled.Release();
                Check(Vector3.Distance(grab,pulled.Body.worldCenterOfMass)>.6f,"pull moves outer bearing "+pulled.name);
                if(++pullIndex<outerPosts.Length)
                {
                    pulled=outerPosts[pullIndex];grab=pulled.Body.worldCenterOfMass;
                    Check(pulled.TryStartGrab(camera.gameObject,grab),"grab second outer bearing");
                    clock=Time.fixedTime;return;
                }
                clock=Time.fixedTime;phase=3;return;
            }
            if(phase==3&&dt>2)
            {
                Check(dummy.Health==0,"lethal tar contact");
                Check(footBefore.y-foot.position.y>.22f,"Triceratops foot drops after bearing pull: "+(footBefore.y-foot.position.y));
                Check(westRefuge.gameObject.activeSelf&&Vector3.Distance(westRefuge.position,westBefore)<.3f,"west apron survives the east bearing pull");
                Shot("23-faceoff-after-pull",new Vector3(-4,25,-84),new Vector3(-4,12,1),37);
                End();
            }
        }

        static void Step()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("phase "+phase+" after "+(Time.fixedTime-clock)+" s of physics");
                if(Services.Session?.SurvivorBots==null)return;
                if(phase==0)
                {
                    Services.Session.HandBots?.Clear();Services.Session.SurvivorBots.AutoFill=false;Services.Session.SurvivorBots.Clear();
                    MatchSetup.ApplyMap();map=Services.World as FossilBasinMap;
                    if(!map)throw new InvalidOperationException("Game did not select Fossil Basin");
                    initial=map.Blocks.Select(b=>b.position).ToArray();
                    report+="Rigid members "+initial.Length+", bearing shapes "+map.GetComponentsInChildren<BoxCollider>().Length+", joints "+map.GetComponentsInChildren<Joint>().Length+"\n";
                    camera=new GameObject("Fossil Basin review camera").AddComponent<Camera>();
                    camera.enabled=false;camera.farClipPlane=400;camera.nearClipPlane=.05f;
                    // Building the map can take minutes on a busy test PC; time the settle from here.
                    deadline=EditorApplication.timeSinceStartup+240;
                    clock=Time.fixedTime;phase++;return;
                }
                float dt=Time.fixedTime-clock;
                if(SessionState.GetBool(Key+".faceoff",false)){StepFaceoff(dt);return;}
                if(routeBot && routeBot.Motor.Grounded && Vector3.Distance(routeBot.transform.position,BotGoal)<1.8f)botArrived=true;
                if(phase==1&&dt>.05f)
                {
                    var all=map.GetComponentsInChildren<FixedJoint>();
                    var gap=all.Select(j=>Vector3.Distance(j.transform.TransformPoint(j.anchor),j.connectedBody.transform.TransformPoint(j.connectedAnchor))).DefaultIfEmpty(0).Max();
                    report+="First physics frames: joints "+all.Length+", largest socket gap "+gap+"\n";
                    Wide("01-authored");phase++;return;
                }
                if(phase==2&&dt>3)
                {
                    if(SessionState.GetBool(Key+".scenery",false))
                    {
                        var decor=map.GetComponent<DeadlyJenga.Presentation.FossilBasinScenery>();
                        var water=map.gameObject.scene.GetRootGameObjects()
                            .SelectMany(root=>root.GetComponentsInChildren<DeadlyJenga.CandyArt.CandyOceanSurface>())
                            .FirstOrDefault(o=>o.GetComponent<Renderer>().sharedMaterial==decor.tar);
                        Check(water && water.GetComponent<MeshFilter>().sharedMesh && !water.GetComponent<Renderer>().forceRenderingOff,
                            "castle wave mesh renders the Fossil Basin tar material");
                        Check(RenderSettings.skybox==decor.sky && decor.sky.GetTexture("_MainTex"),"Jurassic sunset panorama bound");
                        Wide("07-jurassic-wide");
                        Shot("08-jurassic-waterline",new Vector3(1,2.2f,-12.2f),new Vector3(-27,4,28),66);
                        var swimmer=map.GetComponentInChildren<DeadlyJenga.Presentation.FossilBasinSwimmer>();
                        Check(swimmer && swimmer.GetComponent<MeshFilter>().sharedMesh==decor.swimmerModel &&
                            swimmer.GetComponent<MeshRenderer>().sharedMaterial==decor.swimmerSkin &&
                            decor.swimmerSkin.shader.isSupported, "live Spinosaurus mesh and shader");
                        if(swimmer)
                        {
                            Check(!swimmer.GetComponentInChildren<Collider>() && !swimmer.GetComponentInChildren<Rigidbody>(),
                                "orbit has no gameplay collision or physics bodies");
                            foreach(var sample in new[]{(4f,"09-sail"),(12f,"10-surfacing"),(28f,"11-breach")})
                            {
                                swimmer.SamplePose(sample.Item1);
                                var at=swimmer.transform.position;
                                float waterY=map.WaterHeight(at);
                                float height=at.y-waterY;
                                Check(sample.Item1==28f ? height>4f : height<0f, sample.Item2+" root height "+height);
                                var waterline=new Vector3(at.x,waterY,at.z);
                                var eye=Vector3.Lerp(map.Center,waterline,.38f)+Vector3.up*2.2f;
                                Shot(sample.Item2,eye,waterline+Vector3.up*(sample.Item1==28f?5f:1f),67);
                            }
                        }
                        End();return;
                    }
                    float worst=map.Blocks.Select((b,i)=>Vector3.Distance(b.position,initial[i])).Max();
                    Check(worst<.7f,"unloaded maximum body travel "+worst);
                    foreach(var item in map.Blocks.Select((b,i)=>(b,travel:Vector3.Distance(b.position,initial[i]))).OrderByDescending(x=>x.travel).Take(8))
                        report+=item.b.name+" travel "+item.travel+" at "+item.b.position+"\n";
                    Wide("02-settled");
                    Shot("03-apron-eye",new Vector3(1,2.1f,-12.2f),new Vector3(-4,7,0),70);
                    Shot("04-rib-eye",new Vector3(-11,12.5f,11),new Vector3(2,12,3),70);
                    var spawn=map.SafeSpawnPoint();
                    Check(Physics.Raycast(spawn+Vector3.up*.25f,Vector3.down,out var hit,.7f,1<<Layers.Block)&&hit.normal.y>.86f,"spawn has bearing");
                    Check(!Physics.CheckCapsule(spawn+Vector3.up*.22f,spawn+Vector3.up*1.05f,.20f,1<<Layers.Block),"spawn capsule clearance");
                    var plate=map.Blocks.First(b=>b.name=="Stegosaurus vertebra and ribs 4");
                    Check(Climbables.TryGet(plate.GetComponentInChildren<Collider>(),out var grip)&&grip.CanClimb&&grip.Body==plate,"bone resolves to moving climbing anchor");
                    var bots=Services.Session.SurvivorBots;bots.Running=true;
                    var bot=bots.SpawnOne();Check(bot!=null,"game spawned survivor bot");
                    if(bot)
                    {
                        routeBot=bot.GetComponent<CastleBotBrain>();botArrived=false;
                        if(routeBot)
                        {
                            routeBot.Motor.Place(new Vector3(-8.4f,1.08f,-9.1f),Quaternion.identity);
                            routeBot.SetJourneyDestination(BotGoal,40);
                        }
                    }
                    var probe=GameObject.CreatePrimitive(PrimitiveType.Sphere);probe.name="Tar probe";probe.transform.position=new Vector3(19,.85f,0);
                    probe.transform.localScale=Vector3.one*.3f;probe.AddComponent<Rigidbody>();dummy=probe.AddComponent<TestDummy>();
                    walker=Services.Session.Player?.gameObject.GetComponent<CastleSurvivor>();
                    if(walker)
                    {
                        walker.externallyDriven=true;
                        walker.Place(new Vector3(7,1.12f,-9),Quaternion.identity);
                        destination=new Vector3(7.8f,2.8f,-2.5f);walkStage=0;
                        phase=20;lastWalk=clock=Time.fixedTime;return;
                    }
                    Check(false,"survivor motor available");clock=Time.fixedTime;phase++;return;
                }
                if(phase==20)
                {
                    var flat=Vector3.ProjectOnPlane(destination-walker.transform.position,Vector3.up);
                    var forward=walker.MovementForward;
                    var input=new Vector2(Vector3.Dot(flat.normalized,Vector3.Cross(Vector3.up,forward)),Vector3.Dot(flat.normalized,forward));
                    float elapsed=Time.fixedTime-lastWalk;lastWalk=Time.fixedTime;
                    while(elapsed>.001f){float step=Mathf.Min(.025f,elapsed);walker.Simulate(input,false,step);elapsed-=step;}
                    if(flat.magnitude>.55f && dt<10)return;
                    Check(flat.magnitude<=.55f && Mathf.Abs(walker.transform.position.y-destination.y)<.45f,
                        "real survivor walked flight "+walkStage+" endpoint "+walker.transform.position);
                    if(walkStage++==0){destination=new Vector3(14,4.9f,2);clock=Time.fixedTime;return;}
                    CheckClimb();walker.externallyDriven=false;clock=Time.fixedTime;phase=21;return;
                }
                if(phase==21)
                {
                    if(!botArrived&&routeBot&&dt<12)return;
                    Check(botArrived,"actual bot crossed apron to west landing; last position "+(routeBot?routeBot.transform.position.ToString():"missing"));
                    phase=3;clock=Time.fixedTime;return;
                }
                if(phase==3&&dt>1.5f)
                {
                    Check(dummy.Health==0,"tar eliminated falling damageable");
                    foot=map.Blocks.Where(b=>b.name.StartsWith("Triceratops tarsals")).OrderByDescending(b=>b.position.x).First();
                    footBefore=foot.position;
                    westRefuge=map.Blocks.First(b=>b.name.StartsWith("West switchback deck"));
                    westBefore=westRefuge.position;
                    float loaded=map.Blocks.Select((body,i)=>Vector3.Distance(body.position,initial[i])).Max();
                    Check(loaded<.75f,"intact stability after player and bot loading; worst travel "+loaded);
                    pulled=map.Blocks.Where(b=>b.name.StartsWith("Triceratops rear foot pier 1/-1/"))
                        .OrderByDescending(b=>b.position.y).First().GetComponent<FossilMember>();
                    grab=pulled.Body.worldCenterOfMass;
                    Check(pulled.TryStartGrab(camera.gameObject,grab),"grab outer Triceratops rear-foot bearing");
                    clock=Time.fixedTime;phase++;return;
                }
                if(phase==4)
                {
                    pulled.Hold(grab+Vector3.right*Mathf.Min(5,dt*6));
                    if(dt<1)return;
                    pulled.Release();
                    Check(Vector3.Distance(grab,pulled.Body.worldCenterOfMass)>.8f,"single pier physically withdrawn");
                    Wide("05-pull");
                    foreach(var b in map.Blocks.Where(b=>b.name.StartsWith("Triceratops rear foot pier")&&b!=pulled.Body))
                        b.GetComponent<FossilMember>().Push(Vector3.right*10,camera.gameObject);
                    clock=Time.fixedTime;phase++;return;
                }
                if(phase==5&&dt>2)
                {
                    Wide("06-foot-loss");
                    Check(footBefore.y-foot.position.y>.3f,"foot drop after loss of bearings "+(footBefore.y-foot.position.y));
                    Check(westRefuge.gameObject.activeSelf && Vector3.Distance(westRefuge.position,westBefore)<.30f,
                        "independent western route survives eastern pier loss");
                    File.WriteAllText(Folder+"/desktop.txt",report);
                    peer=FossilBasinPeer.Receive(map,(CastlePlaySession)Services.Session,Check);
                    deadline=EditorApplication.timeSinceStartup+130;phase=6;return;
                }
                if(phase==6&&!peer.MoveNext()){(peer as IDisposable)?.Dispose();End();}
            }
            catch(Exception e){Check(false,e.ToString());End();}
        }
    }
}
