using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.World.Hazards;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad] public static class AllDisasterChecks
    {
        const string Key="AllDisasterChecks",Request="Logs/Validation/AllDisasters.request";
        static int errors;
        [Serializable] sealed class ReceivedPacket { public DisasterFrame[] frames; }
        sealed class DeniedSession : ISessionInfo,IWorldAuthority
        {public bool MenuOpen=>false;public void SetMenu(bool open){}public bool HasWorldAuthority=>false;}
        static AllDisasterChecks()
        {
            EditorApplication.update+=()=>{if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){File.Delete(Request);Run();}};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard(Check()));}
                if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);string original=SessionState.GetString(Key+"Original","");if(File.Exists(original))EditorSceneManager.OpenScene(original);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Test All Disasters")]
        public static void Run()
        {
            File.WriteAllText(ProjectPath.Report("AllDisasters.txt"),"All disasters / tuning / bot spawning\n");
            SessionState.SetString(Key+"Original",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(ProjectPath.Report("AllDisasters.txt"),"ERROR "+message+"\n"+trace+"\n");}}
        static void Require(bool ok,string message){File.AppendAllText(ProjectPath.Report("AllDisasters.txt"),(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static IEnumerator Guard(IEnumerator test)
        {
            var stack=new Stack<IEnumerator>();stack.Push(test);
            while(stack.Count>0){bool next=false;object value=null;Exception error=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(ProjectPath.Report("AllDisasters.txt"),"FAIL "+error+"\n");EditorApplication.ExitPlaymode();yield break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;}
            EditorApplication.ExitPlaymode();
        }
        static float[] Values(IDemoHazard tuning,float duration=25)
        {return tuning.Parameters.Select(p=>p.id=="warning"?0:p.id=="duration"?duration:p.initial).ToArray();}
        static void Set(IDemoHazard tuning,float[] values,string id,float value)
        {for(int i=0;i<values.Length;i++)if(tuning.Parameters[i].id==id)values[i]=value;}
        static void Step(MapHazard hazard,int count)
        {var method=(hazard is BasicDisaster?typeof(BasicDisaster):typeof(TsunamiHazard)).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);for(int i=0;i<count&&hazard.Active;i++)method.Invoke(hazard,null);}
        static DisasterProbe Probe(Transform parent,Vector3 local)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.transform.SetParent(parent,false);go.transform.localPosition=local;go.layer=Layers.Survivor;return go.AddComponent<DisasterProbe>();}
        static IEnumerator Check()
        {
            yield return new WaitForSecondsRealtime(1);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
            Require(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Catalog authoring validation runs");
            var oldWorld=Services.World;var oldSession=Services.Session;
            var root=new GameObject("Temporary disaster sandbox");root.transform.position=new Vector3(10000,0,10000);
            var map=root.AddComponent<FakeWorldQuery>();map.playableBounds=new Bounds(Vector3.up*8,new Vector3(40,20,40));map.topHeight=16;map.radius=20;Services.Session=null;
            for(int i=0;i<8;i++)
            {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(0,1+i*2,0);go.transform.localScale=Vector3.one*2;go.layer=Layers.Block;var rb=go.AddComponent<Rigidbody>();rb.useGravity=false;map.blocks.Add(rb);}
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform,false);floor.transform.localPosition=Vector3.down;floor.transform.localScale=new Vector3(60,1,60);floor.layer=Layers.Surface;
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.transform.SetParent(root.transform,false);roof.transform.localPosition=new Vector3(6,6,0);roof.transform.localScale=new Vector3(4,1,4);roof.layer=Layers.Block;
            var exposed=Probe(root.transform,new Vector3(-6,2,0));var sheltered=Probe(root.transform,new Vector3(6,2,0));
            Physics.SyncTransforms();GameObject instance=null;
            try
            {
                foreach(var prefab in DemoDisasters.Prefabs)
                {
                    instance=Object.Instantiate(prefab);instance.transform.SetParent(root.transform,true);var hazard=instance.GetComponent<MapHazard>();var tuning=instance.GetComponent<IDemoHazard>();
                    Require(tuning!=null&&tuning.Parameters.Count>0,prefab.name+" exposes configurable settings");
                    Services.Session=new DeniedSession();hazard.Begin();Require(!hazard.Active,prefab.name+" refuses client-side physics simulation");Services.Session=null;
                    var settings=Values(tuning);Set(tuning,settings,"contact",.2f);Set(tuning,settings,"seeds",1);
                    if(hazard is BasicDisaster waiting)
                    {
                        var warning=settings.ToArray();Set(tuning,warning,"warning",2);tuning.Configure(warning);hazard.Begin();Vector3 previous=exposed.impulse;float previousHealth=exposed.Health;Step(hazard,10);
                        Require(waiting.Capture().warning>1&&waiting.Capture().points.Length==0&&waiting.Capture().blocks.Length==0&&previous==exposed.impulse&&previousHealth==exposed.Health,prefab.name+" warning gives players time before effects begin");hazard.End();
                    }
                    tuning.Configure(settings);hazard.Begin();
                    Require(hazard.Active&&hazard.TryValidate(map,out _),prefab.name+" starts on translated sandbox bounds");
                    var timer=System.Diagnostics.Stopwatch.StartNew();Vector3 before=exposed.impulse;float health=exposed.Health,safeHealth=sheltered.Health;
                    if(hazard is BasicDisaster basic)
                    {
                        Step(hazard,5);var f=basic.Capture();
                        Require(f!=null&&DisasterStates.Active.Contains(basic),prefab.name+" publishes its visual state");
                        if(f.kind=="Fire"){Step(hazard,200);Require(basic.Capture().blocks.Length>1,"Fire spreads beyond its single seed through block contact");}
                        else if(f.kind=="Icey")
                        {Require(map.blocks[0].GetComponent<Collider>().sharedMaterial&&DisasterStates.Slipperiness>0,"Ice changes collision friction and player traction state");}
                        else if(f.kind=="MeteorShower"||f.kind=="PlanetKillerMeteor")
                        {
                            Require(f.points.Length>0&&f.points.Length<=128,"Meteor bodies are visible within the active budget");
                            if(f.kind=="PlanetKillerMeteor")Require(f.points.Length==1&&f.points[0].size>=40,"Planet killer is one massive sphere");
                            int impacts=0;Action<Vector3,float> hit=(_,__)=>impacts++;GameEvents.BlockHit+=hit;Step(hazard,900);GameEvents.BlockHit-=hit;
                            Require(impacts>0,f.kind+" swept collisions produce impacts");
                        }
                        else if(f.kind=="AcidRain")Require(exposed.Health<health&&sheltered.Health==safeHealth,"Acid damages exposed players while shelter protects them");
                        else Require((exposed.impulse-before).sqrMagnitude>0,f.kind+" affects the player ability stand-in");
                        string json=JsonUtility.ToJson(f);var copy=JsonUtility.FromJson<DisasterFrame>(System.Text.Encoding.UTF8.GetString(CastleFusionWire.Decompress(CastleFusionWire.Compress(System.Text.Encoding.UTF8.GetBytes(json)))));
                        Require(copy!=null&&copy.kind==f.kind,prefab.name+" state round-trips through compressed network encoding");
                    }
                    else
                    {var wave=(TsunamiHazard)hazard;Require(wave.Wave.width>0&&wave.height==settings[2],"Tsunami uses selected height and map-fitted width");}
                    timer.Stop();File.AppendAllText(ProjectPath.Report("AllDisasters.txt"),"TIMING "+prefab.name+" checks "+timer.ElapsedMilliseconds+" ms\n");
                    hazard.End();Require(!hazard.Active&&(!(hazard is IDisasterState source)||!DisasterStates.Active.Contains(source)),prefab.name+" stops and unregisters cleanly");
                    if(hazard is IceyHazard)Require(!map.blocks[0].GetComponent<Collider>().sharedMaterial&&DisasterStates.Slipperiness==0,"Ice restores original friction on stop");
                    Object.Destroy(instance);instance=null;
                }
            }
            finally{if(instance)Object.Destroy(instance);Services.World=oldWorld;Services.Session=oldSession;Object.Destroy(root);}
            yield return null;
            var transportObject=new GameObject("Disaster client receive probe");var transport=transportObject.AddComponent<CastleFusionSession>();
            var received=new ReceivedPacket{frames=new[]{new DisasterFrame{kind="Icey",warning=0,remaining=-1,values=new[]{1f},points=Array.Empty<DisasterPoint>(),blocks=Array.Empty<int>()}}};
            byte[] packet=CastleFusionWire.Compress(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(received)));
            typeof(CastleFusionSession).GetMethod("ReceiveDisasters",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(transport,new object[]{packet});
            Require(DisasterStates.Slipperiness==1,"Client receive path publishes ice traction without client hazard physics");
            typeof(CastleFusionSession).GetMethod("ResetDisasters",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(transport,null);
            Require(DisasterStates.Slipperiness==0,"Network teardown clears replicated disaster effects");Object.Destroy(transportObject);
            var tsunami=DemoDisasters.Prefabs.First(p=>p.GetComponent<IWaveState>()!=null);var invalid=DemoDisasters.Values(session,tsunami).ToArray();invalid[0]=float.NaN;
            Require(!DemoDisasters.Execute(session,tsunami.name,values:invalid),"Host rejects nonfinite generic command settings");
            var request=new CastleDemoCommand{action=CastleDemoAction.Disaster,rules=tsunami.name,hazardValues=DemoDisasters.Values(session,tsunami)};
            Require(JsonUtility.FromJson<CastleDemoCommand>(JsonUtility.ToJson(request)).hazardValues.Length==5,"Full per-disaster settings survive command serialization");
            var menu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);var cam=session.Player.viewCamera;menu.Show(true,cam);
            typeof(CastleWorldMenu).GetField("disasterPage",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,true);
            var selection=typeof(CastleWorldMenu).GetField("disasterSelection",BindingFlags.Instance|BindingFlags.NonPublic);selection.SetValue(menu,Array.FindIndex(DemoDisasters.Prefabs,p=>p.GetComponent<MeteorShowerHazard>()));menu.Point(cam,false);
            var sliders=(Slider[])typeof(CastleWorldMenu).GetField("disasterSliders",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
            Require(sliders.Count(s=>s.gameObject.activeSelf)==6,"VR meteor page includes four requested sliders plus warning and duration");
            Require(Mathf.Abs(sliders[5].value-25)<.01f,"Opening VR settings preserves authored duration defaults");
            cam.transform.LookAt(sliders[2].transform.TransformPoint(new Vector3(125,-13,0)));menu.Point(cam,true,true);
            Require(sliders[2].normalizedValue>.7f,"VR held input changes a generic disaster slider");cam.transform.LookAt(menu.transform.position);Capture(cam,"disaster-meteor-controls.png");menu.Show(false,cam);
            session.Bots.targetPopulation=1;
            Require(session.Bots.Capacity==int.MaxValue,"Survivor spawning has no fixed population cap");
            for(int i=0;i<12;i++){var bot=session.Bots.SpawnBot();if(bot)bot.enabled=false;}
            Require(session.Bots.Count>9,"Survivor bots spawn beyond the former demo limit");int survivors=session.Bots.Count;
            for(int i=0;i<12;i++){var hand=session.HandBots.SpawnHand();if(hand)hand.enabled=false;}
            Require(session.HandBots.Count==12&&session.Bots.Count==survivors,"Twelve hands coexist without trimming survivor bots");
            session.Bots.ClearBots();session.HandBots.ClearHands();yield return null;
            var tornado=DemoDisasters.Prefabs.First(p=>p.GetComponent<TornadoHazard>());var v=DemoDisasters.Values(session,tornado);Set(tornado.GetComponent<IDemoHazard>(),v,"warning",0);DemoDisasters.Request(session,tornado);
            yield return new WaitForSecondsRealtime(1);
            var active=session.GetComponentsInChildren<BasicDisaster>().FirstOrDefault(h=>h.Active);
            Require(active&&active.Capture().age>.5f,"Real Unity FixedUpdate advances derived disasters without the test driver");
            Require(Object.FindFirstObjectByType<DeadlyJenga.CandyArt.CandyDisasterVisuals>(),"Map loads the candy disaster renderer");
            Orbit(oldWorld,"disaster-tornado.png");DemoDisasters.StopAll(session);yield return null;
            foreach(string kind in new[]{"MeteorShower","Fire","Icey","AcidRain","AntiGravity"})
            {
                var prefab=DemoDisasters.Prefabs.First(p=>p.name==kind);var options=prefab.GetComponent<IDemoHazard>();var settings=DemoDisasters.Values(session,prefab);Set(options,settings,"warning",0);
                Require(DemoDisasters.Execute(session,prefab.name,values:settings),kind+" launches in the real castle");
                yield return new WaitForSecondsRealtime(kind=="MeteorShower"?2.5f:.6f);Orbit(oldWorld,"disaster-"+kind.ToLowerInvariant()+".png");DemoDisasters.StopAll(session);yield return null;
            }
            session.ResetCastle();yield return new WaitForSecondsRealtime(.5f);
            foreach(var prefab in DemoDisasters.Prefabs)
            {var options=prefab.GetComponent<IDemoHazard>();var settings=DemoDisasters.Values(session,prefab);Set(options,settings,"warning",0);Require(DemoDisasters.Execute(session,prefab.name,values:settings),"Concurrent launch: "+prefab.name);}
            yield return new WaitForSecondsRealtime(.5f);DemoDisasters.StopAll(session);yield return null;
            Require(DisasterStates.Active.Count==0&&Tracked.Waves.Count==0,"Stopping concurrent disasters clears every shared effect source");
            Require(session.generator.Bodies.All(b=>!b||b.useGravity),"Temporary disaster changes leave normal block gravity intact");
            Require(errors==0,"Game Play Mode, menus and disaster visuals produce no errors");
            File.AppendAllText(ProjectPath.Report("AllDisasters.txt"),"COMPLETE: editor + stand-in physics + encoding; real two-headset networking and Quest performance remain device tests.\n");
        }
        static void Capture(Camera cam,string name)
        {
            var rt=new RenderTexture(1400,1000,24);var previous=RenderTexture.active;var target=cam.targetTexture;Texture2D texture=null;
            try{foreach(var visual in Object.FindObjectsByType<DeadlyJenga.CandyArt.CandyDisasterVisuals>(FindObjectsSortMode.None))visual.SendMessage("LateUpdate");cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;texture=new Texture2D(1400,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1400,1000),0,0);texture.Apply();File.WriteAllBytes(ProjectPath.Report(name),texture.EncodeToPNG());}
            finally{cam.targetTexture=target;RenderTexture.active=previous;Object.Destroy(rt);if(texture)Object.Destroy(texture);}
        }
        static void Orbit(IWorldQuery world,string name)
        {
            var go=new GameObject("Temporary disaster screenshot camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=60;
            camera.transform.position=world.Center+new Vector3(world.Radius*.7f,world.TopHeight*.65f,-world.Radius-65);camera.transform.LookAt(world.Center+Vector3.up*world.TopHeight*.4f);
            Capture(camera,name);Object.Destroy(go);
        }
    }
}
