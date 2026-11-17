using System;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class TreeDisasterChecks
    {
        const string Pending="TreeDisasters.Pending", Failed="TreeDisasters.Failed";
        static readonly string[] AllCases={"PowerOutage","EvilSanta","CatAttack","OrnamentShower","PlanetKillerOrnament","Earthquake"};
        static string[] Cases=>SessionState.GetBool("TreeDisasters.Detail",false)?new[]{"EvilSanta","CatAttack","Earthquake"}:AllCases;
        static string Report=>ProjectPath.Report("TreeDisasters.txt");
        static string Folder=>Path.Combine(Path.GetDirectoryName(Report),"TreeDisasterEvidence");
        static double deadline;
        static int index,stage;
        static float begin,ended;
        static IHazard hazard;
        static IDisasterState state;
        static bool seen,hit,shot;
        static Vector3 firstCat;
        static TestDummy dummy;
        public static void Run(){SessionState.SetBool("TreeDisasters.Detail",false);Begin();}
        public static void RunDetail(){SessionState.SetBool("TreeDisasters.Detail",true);Begin();}
        static void Begin()
        {
            if(!Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Use checked batch editor");
            if(!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"))throw new Exception("Missing disaster validator");
            if(!EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Test Disaster Map Contract"))throw new Exception("Missing map contract checks");
            Directory.CreateDirectory(Folder);File.WriteAllText(Report,"Actual Game.unity Tree disaster checks; staged receiver and cameras.\n");
            SessionState.SetBool(Pending,true);SessionState.SetBool(Failed,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Select()
        {if(SessionState.GetBool(Pending,false)){MatchChoices.MapId="workshop-under-the-tree";MatchChoices.ModeId="demo";MatchChoices.BotTarget=1;MatchChoices.HandCount=0;}}
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(!SessionState.GetBool(Pending,false))return;
                if(s==PlayModeStateChange.EnteredPlayMode)
                {index=stage=0;deadline=EditorApplication.timeSinceStartup+480;EditorApplication.update+=Tick;}
                if(s==PlayModeStateChange.EnteredEditMode)
                {EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);int code=SessionState.GetBool(Failed,true)?1:0;EditorApplication.delayCall+=()=>EditorApplication.Exit(code);}
            };
        }
        static void Check(bool ok,string text)
        {if(!ok)throw new Exception(text);File.AppendAllText(Report,"PASS "+text+"\n");}
        static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout at "+index+" stage "+stage);
                var session=Services.Session as CastlePlaySession;var world=Services.World;
                if(!session||world==null||Time.frameCount<15||!session.ActiveCamera)return;
                if(stage==0)
                {
                    Check((world as ISyncedWorld)?.SaveLayout()=="underneath-the-tree-v6-underpass","correct runtime map");
                    Check(DemoDisasters.Prefabs.Select(p=>p.name).OrderBy(n=>n).SequenceEqual(AllCases.OrderBy(n=>n)),"Tree catalog contains exactly the six requested disasters");
                    Capture("00-before");stage=1;ended=Time.time;
                }
                if(stage==1&&Time.time-ended>.4f)
                {
                    if(index>=Cases.Length)
                    {Check(DisasterStates.Active.All(s=>s.Capture()==null||s.Capture().residualOnly),"all hazards cleaned up");SessionState.SetBool(Failed,false);EditorApplication.ExitPlaymode();return;}
                    UnityEngine.Random.InitState(8721+index);
                    var prefab=DemoDisasters.Prefabs.Single(p=>p.name==Cases[index]);
                    var definition=prefab.GetComponent<IMapHazard>();
                    Check(definition.TryValidate(world,out _),Cases[index]+" validates map bounds");
                    var schema=DemoDisasters.Parameters(prefab);var values=schema.Select(p=>p.initial).ToArray();
                    for(int i=0;i<schema.Count;i++)
                    {if(schema[i].id=="warning")values[i]=1;if(schema[i].id=="duration")values[i]=Cases[index]=="CatAttack"?20:Cases[index]=="Earthquake"?14:8;}
                    Check(DemoDisasters.ExecuteForMode(session,prefab.name,values),Cases[index]+" starts through production session path");
                    hazard=session.GetComponentsInChildren<MonoBehaviour>().OfType<IHazard>().Single(h=>h.Active&&h.Type==prefab.GetComponent<IHazard>().Type);
                    state=(IDisasterState)hazard;begin=Time.time;seen=hit=shot=false;
                    if(Cases[index]=="CatAttack")
                    {
                        var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.name="Staged cat contact receiver";go.layer=Layers.Survivor;
                        go.transform.position=new Vector3(world.PlayableBounds.center.x,2,world.PlayableBounds.center.z);dummy=go.AddComponent<TestDummy>();
                        firstCat=state.Capture().points[0].position;
                    }
                    stage=2;
                }
                if(stage==2)
                {
                    var f=state.Capture();float elapsed=Time.time-begin;
                    if(f!=null&&f.warning<=0)
                    {
                        var copy=JsonUtility.FromJson<DisasterFrame>(JsonUtility.ToJson(f));
                        if(copy.kind!=f.kind)throw new Exception("Disaster frame roundtrip failed");
                        if(Cases[index]=="PowerOutage")
                        {
                            var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.name=="Amber lamplight and ceiling bounce").ToArray();
                            seen=lights.Length>0&&lights.All(l=>!l.enabled);
                            hit=seen;
                        }
                        else
                        {
                            seen|=Cases[index]=="Earthquake"||f.points!=null&&f.points.Length>0;
                            hit|=world.Blocks.Count(b=>b&&!b.isKinematic)>0;
                        }
                        if(!shot&&elapsed>2&&(Cases[index]!="CatAttack"||Vector3.Distance(f.points[0].position,world.PlayableBounds.center)<55))
                        {
                            Capture((index+1).ToString("00")+"-"+Cases[index]);
                            if(Cases[index]=="CatAttack"||Cases[index]=="EvilSanta")
                            {
                                var p=f.points[0];var forward=p.velocity.normalized;
                                Capture(Cases[index]+"-close",p.position+forward*70-Vector3.Cross(Vector3.up,forward)*45+Vector3.up*(Cases[index]=="EvilSanta"?80:55),p.position+Vector3.up*(Cases[index]=="EvilSanta"?65:25));
                            }
                            shot=true;
                        }
                    }
                    if(Cases[index]!="PowerOutage")hit|=world.Blocks.Any(b=>b&&!b.isKinematic);
                    bool naturallyEnded=!hazard.Active;
                    bool enough=elapsed>(Cases[index]=="Earthquake"?12:Cases[index]=="CatAttack"?30:Cases[index]=="PlanetKillerOrnament"?12:7);
                    if(naturallyEnded||enough)
                    {
                        Check(seen,Cases[index]+" publishes active visual state");
                        if(!hit)Capture("failed-"+Cases[index]);
                        Check(hit,Cases[index]+(Cases[index]=="PowerOutage"?" switches off room lights":" releases real map rigidbodies"));
                        if(dummy){Check(dummy.Health<dummy.MaxHealth,"one passing cat damages staged survivor receiver");UnityEngine.Object.Destroy(dummy.gameObject);dummy=null;}
                        if(Cases[index]=="CatAttack")Check(naturallyEnded,"single cat completes its run and ends");
                        hazard.End();Check(state.Capture()==null,Cases[index]+" ends and removes active state");
                        ended=Time.time;stage=3;
                    }
                }
                if(stage==3&&Time.time-ended>.7f)
                {
                    if(Cases[index]=="PowerOutage")
                    {Check(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(l=>l.name=="Amber lamplight and ceiling bounce"&&l.enabled),"outage restores room lights");Capture("01-outage-restored");}
                    if(Cases[index]!="PowerOutage")Capture((index+1).ToString("00")+"-aftermath");
                    ((IRebuildable)world).Rebuild();index++;ended=Time.time;stage=1;
                }
            }
            catch(Exception e)
            {File.AppendAllText(Report,"FAIL "+e+"\n");SessionState.SetBool(Failed,true);EditorApplication.ExitPlaymode();}
        }
        static void Capture(string name,Vector3? eye=null,Vector3? target=null)
        {
            var go=new GameObject("Tree hazard test camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(Services.Session.ActiveCamera);camera.enabled=false;
            camera.transform.position=eye??new Vector3(38,125,-208);camera.transform.LookAt(target??new Vector3(-91,35,-94));camera.fieldOfView=eye.HasValue?55:75;
            var rt=new RenderTexture(1280,720,24);var prior=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(Folder,name+".png"),tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);
            }
            finally{RenderTexture.active=prior;camera.targetTexture=null;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(go);}
        }
    }
}
