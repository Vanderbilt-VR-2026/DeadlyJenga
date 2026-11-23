using System;
using System.IO;
using System.Linq;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class GingerbreadKingdomChecks
    {
        const string Key="GingerbreadKingdomChecks";
        static string Folder=>Path.GetDirectoryName(ProjectPath.Report("GingerbreadKingdom.txt"));
        static CastleSurvivor walker;
        static GingerbreadKingdom map;
        static Rigidbody span,bearing,roof;
        static Vector3[] homes;
        static float started,lowestFeet,roofHeight;
        static int phase, waypoint;
        static bool failed,fallEliminated;
        static double boot;
        static Vector3 P(float x,float z)=>new Vector3(x,0,z);
        static readonly Vector3[] Approach={P(-5,-19),P(-5,-10),P(-4,-3),P(-3,3),P(-3,6),P(0,8),P(3,9),P(3,15)};
        static readonly Vector3[] Crossing={P(15,-14),P(15,-11),P(15,-7),P(15,-5)};
        static readonly Vector3[] Detour={P(18,-5),P(18,-10),P(18,-15),P(16,-18),P(15,-17)};
        sealed class Driver:ICastleSurvivorDriver
        {
            internal Vector3 destination;
            public CastleBotCommand ReadCommand(CastleSurvivor motor,float dt)
            {
                Vector3 delta=Vector3.ProjectOnPlane(destination-motor.transform.position,Vector3.up);
                return new CastleBotCommand{move=delta.normalized*Mathf.Clamp01(delta.magnitude*3),look=delta};
            }
        }
        static Driver driver;
        [Serializable] sealed class SavedScenes { public SceneSetup[] scenes; }
        static GingerbreadKingdomChecks()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=Restore;
        }
        static void Restore(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key+"restore",false))return;
            SessionState.SetBool(Key+"restore",false);
            var setup=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+"scenes",""));
            if(setup.scenes.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
            MatchSetup.MapId=SessionState.GetString(Key+"map","");
            MatchSetup.ModeId=SessionState.GetString(Key+"mode","");
            if(!string.IsNullOrEmpty(SessionState.GetString("DJWarm.id","")))
            {
                Directory.CreateDirectory("Logs/Warm");
                File.WriteAllText("Logs/Warm/exit.txt",SessionState.GetInt(Key+"exit",1).ToString());
            }
        }
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Editor already playing");
            if(!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))throw new Exception("Missing disaster validator");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(!Application.isBatchMode && setup.Any(s=>UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("The open scene has unsaved edits");
            SessionState.SetString(Key+"scenes",JsonUtility.ToJson(new SavedScenes{scenes=setup.Where(s=>!string.IsNullOrEmpty(s.path)).ToArray()}));
            SessionState.SetString(Key+"map",MatchSetup.MapId);
            SessionState.SetString(Key+"mode",MatchSetup.ModeId);
            phase=waypoint=0;failed=fallEliminated=false;boot=0;
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Report(string line)=>File.AppendAllText(Path.Combine(Folder,"GingerbreadKingdom.txt"),line+"\n");
        static void Check(bool ok,string line){Report((ok?"PASS ":"FAIL ")+line);failed|=!ok;}
        static void Next(){phase++;started=Time.time;waypoint=0;}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                if(phase==0)
                {
                    if(boot==0)boot=EditorApplication.timeSinceStartup;
                    var session=Object.FindFirstObjectByType<CastlePlaySession>();
                    if(!session||!session.Survivor()){if(EditorApplication.timeSinceStartup-boot>60)throw new Exception("Game session failed to initialize");return;}
                    MatchSetup.MapId="mode-gingerbread-kingdom";MatchSetup.ModeId="demo";
                    if(MatchSetup.SelectedMap?.title!="Gingerbread Kingdom")throw new Exception("Map absent from lobby catalog");
                    MatchSetup.ApplyMap();map=(GingerbreadKingdom)Services.World;
                    session.SurvivorBots.Clear();session.HandBots.Clear();
                    var rules=MatchSetup.DefaultModeRules(GameModeKind.Demo,MatchSetup.SelectedMap);
                    session.Modes.Configure(rules);session.SetMenu(false);
                    // An independent production motor avoids desktop menu focus suspending local input.
                    var probe=session.Bots().SpawnBot();
                    if(!probe)throw new Exception("Could not spawn route survivor");
                    walker=probe.Motor;walker.ApplyModeRules(rules);
                    Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"GingerbreadKingdom.txt"),"Game.unity / demo / production lobby catalog and ApplyMap\n");
                    homes=map.Blocks.Select(b=>b.position).ToArray();walker.Place(map.SafeSpawnPoint(),Quaternion.identity);
                    driver=new Driver{destination=walker.transform.position};walker.externallyDriven=true;walker.InputDriver=driver;walker.InputSuspended=false;
                    span=map.Blocks.First(b=>b.name=="Bridge span");bearing=map.Blocks.First(b=>b.name=="Pullable bridge bearing");Next();
                }
                else if(phase==1 && Time.time-started>3)
                {
                    float drift=map.Blocks.Select((b,i)=>Vector3.Distance(b.position,homes[i])).Max();
                    Check(drift<.30f,$"{homes.Length} bodies, max settle drift {drift:F3} m");
                    CheckSkyline();
                    Shot("01-overview",new Vector3(8,24,-47),new Vector3(0,8f,1),55);
                    Shot("02-fork",new Vector3(-4,.95f,-19),new Vector3(-8,2,-10));
                    Shot("08-skyline",new Vector3(8,16,-29),new Vector3(0,12,-8),62);
                    Shot("03-palace",new Vector3(3,1.05f,8),new Vector3(3,6,18));
                    Shot("04-bridge-before",new Vector3(19,3,-15),new Vector3(15,.8f,-11));Next();
                }
                else if(phase==2 && Walk(Approach))
                {
                    Check(walker.transform.position.y>2.5f,"palace terrace reached at "+walker.transform.position);
                    walker.Place(new Vector3(15,.08f,-17),Quaternion.identity);Next();
                }
                else if(phase==3 && Walk(Crossing))
                {
                    var grab=bearing.GetComponent<IGrabbable>();Check(grab.TryStartGrab(walker.gameObject,bearing.position),"support grabbed");
                    grab.Hold(bearing.position+Vector3.right*4.5f);Next();
                }
                else if(phase==4 && Time.time-started>2.5f)
                {
                    bearing.GetComponent<IGrabbable>().Release();Check(span.position.y<1.05f,"unsupported deck fell to y="+span.position.y);
                    Shot("05-bridge-after",new Vector3(19,3,-15),new Vector3(15,.8f,-11));Next();
                }
                else if(phase==5 && Walk(Detour))
                {
                    // A separate fall trial begins on snow beside the damaged crossing.
                    walker.Place(new Vector3(17.6f,.08f,-11),Quaternion.identity);
                    driver.destination=P(16.50f,-11);
                    lowestFeet=walker.transform.position.y;GameEvents.Eliminated+=OnFall;Next();
                }
                else if(phase==6)
                {
                    lowestFeet=Mathf.Min(lowestFeet,walker.transform.position.y);
                    if(!fallEliminated && Time.time-started<6)return;
                    GameEvents.Eliminated-=OnFall;
                    Check(fallEliminated,"fall from snow edge beside damaged span raised survivor elimination at "+walker.transform.position);
                    walker.Place(map.SafeSpawnPoint(),Quaternion.identity);driver.destination=walker.transform.position;map.Rebuild();Next();
                }
                else if(phase==7 && Time.time-started>2)
                {
                    Check(map.Blocks.Count==homes.Length&&map.Blocks.Select((b,i)=>Vector3.Distance(b.position,homes[i])).Max()<.3f,"reset restores settled map");
                    roof=map.Blocks.First(b=>b.name=="Peppermint Stack removable roof");
                    roofHeight=roof.position.y;
                    var grab=roof.GetComponent<IGrabbable>();
                    Check(grab.TryStartGrab(walker.gameObject,roof.position),"independent gingerbread roof can be grabbed");
                    grab.Hold(roof.position+Vector3.left*6);
                    Next();
                }
                else if(phase==8 && Time.time-started>2.3f)
                {
                    roof.GetComponent<IGrabbable>().Release();
                    Next();
                }
                else if(phase==9 && Time.time-started>1.7f)
                {
                    Check(roof.position.y<roofHeight-1.5f,"removed roof falls while the apartment shell stays in place");
                    map.Rebuild();
                    Check(map.Blocks.Any(b=>b.name=="Peppermint Stack removable roof"&&Mathf.Abs(b.position.y-roofHeight)<.12f),
                        "reset reinstates the removed apartment roof");
                    Finish();
                }
            }
            catch(Exception e){failed=true;Report(e.ToString());Finish();}
        }
        static void CheckSkyline()
        {
            var clock=map.transform.Find("Sugar valley/Clock Court | Gingerbread Big Ben");
            Check(clock && map.TopHeight>=24.8f,"Blender Big Ben crown is above 24.8 m");
            if(!clock)return;

            foreach(var axis in new[]{Vector3.right,Vector3.forward})
            {
                var start=clock.position+Vector3.up*.86f-axis*2.45f;
                Check(!Physics.Raycast(start,axis,4.9f,~0,QueryTriggerInteraction.Ignore),
                    "Clock Court is open through "+axis);
            }

            foreach(var row in new[]{("Peppermint Stack",5,0f),("Evergreen Court",5,.68f),("Lantern Lofts",6,0f)})
            {
                var tower=map.transform.Find("Sugar valley/"+row.Item1);
                Check(tower && tower.GetComponents<BoxCollider>().Length>100,row.Item1+" has independent walls, floor landings, and stairs");
                if(!tower)continue;
                var depth=row.Item1=="Peppermint Stack"?6f:(row.Item1=="Evergreen Court"?5.6f:5.8f);
                var width=row.Item1=="Evergreen Court"?3.8f:4.2f;
                var originalWidth=row.Item1=="Evergreen Court"?4.15f:4.8f;
                var front=-depth*.5f;
                var start=tower.position+tower.rotation*new Vector3(row.Item3*width/originalWidth,.82f,front-.48f);
                Check(!Physics.Raycast(start,tower.forward,.98f,~0,QueryTriggerInteraction.Ignore),row.Item1+" doorway is open");
                float x=(-originalWidth*.5f+.6525f)*width/originalWidth;
                for(int level=0;level<row.Item2-1;level++)
                {
                    float y=level*2.22f+2.22f/14;
                    float z=(-2.85f+1.16f+(2.0f-(-2.85f+1.16f))/14)*depth/5.7f;
                    var tread=tower.position+tower.rotation*new Vector3(x,y+.046f,z);
                    Check(Physics.Raycast(tread,Vector3.down,out var hit,.092f,~0,QueryTriggerInteraction.Ignore)
                        && hit.transform==tower,row.Item1+" stair flight "+(level+1)+" has a first tread");
                }
            }
        }


        static void OnFall(GameObject actor,GameObject source)
        {
            if(walker && actor==walker.gameObject && (source==map.gameObject || lowestFeet<map.WaterHeight(walker.transform.position)+.5f))fallEliminated=true;
        }
        static bool Walk(Vector3[] route)
        {
            driver.destination=route[waypoint];
            if(Vector3.Distance(Vector3.ProjectOnPlane(walker.transform.position,Vector3.up),route[waypoint])<.42f)
            {
                Report($"Route {phase} waypoint {waypoint} at {walker.transform.position}");
                if(phase==2 && waypoint==2)Shot("06-walk-fork",walker.viewCamera.transform.position,new Vector3(-9,2.5f,-10));
                if(phase==2 && waypoint==6)Shot("07-walk-palace",walker.viewCamera.transform.position,new Vector3(3,6,18));
                if(phase==3 && waypoint==1)Check(walker.transform.position.y>1.45f,"crossed the elevated deck");
                waypoint++;started=Time.time;return waypoint==route.Length;
            }
            if(Time.time-started<7)return false;
            Check(false,$"Route {phase} waypoint {waypoint} blocked at {walker.transform.position}");return true;
        }
        static void Finish()
        {
            SessionState.SetBool(Key,false);
            int code=failed?1:0;
            if(Application.isBatchMode && string.IsNullOrEmpty(SessionState.GetString("DJWarm.id","")))
            {
                EditorApplication.Exit(code);return;
            }
            SessionState.SetInt(Key+"exit",code);
            SessionState.SetBool(Key+"restore",true);EditorApplication.ExitPlaymode();
        }
        static void Shot(string label,Vector3 eye,Vector3 target,float fov=58)
        {
            var obj=new GameObject("Kingdom review camera");var cam=obj.AddComponent<Camera>();
            var rt=new RenderTexture(1440,840,24);var previous=RenderTexture.active;
            var png=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            try
            {
                cam.transform.position=eye;cam.transform.LookAt(target);cam.fieldOfView=fov;cam.nearClipPlane=.05f;cam.farClipPlane=220;
                cam.clearFlags=CameraClearFlags.Skybox;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
                png.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);png.Apply();
                File.WriteAllBytes(Path.Combine(Folder,"gingerbread-"+label+".png"),png.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;cam.targetTexture=null;Object.DestroyImmediate(png);Object.DestroyImmediate(rt);Object.DestroyImmediate(obj);}
        }
    }
}
