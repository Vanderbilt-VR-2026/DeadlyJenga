using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CastleSocial;
using DeadlyJenga.World.Hazards;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad]
    public static class CrossSystemSweep
    {
        const string Key="CrossSystemSweep";
        static readonly List<string> lines=new List<string>();
        static readonly List<string> errors=new List<string>();
        static double deadline;
        static CrossSystemSweep()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    Application.logMessageReceived+=Log;deadline=EditorApplication.timeSinceStartup+180;
                    EditorApplication.update+=Watchdog;
                    EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard(Check()));
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;EditorApplication.update-=Watchdog;
                    bool passed=SessionState.GetBool(Key+"Complete",false)&&!SessionState.GetBool(Key+"Failed",false);
                    SessionState.SetBool(Key,false);if(Application.isBatchMode)EditorApplication.Exit(passed?0:1);
                }
            };
        }
        public static void RunBatch()
        {
            lines.Clear();errors.Clear();SessionState.SetBool(Key+"Complete",false);SessionState.SetBool(Key+"Failed",false);
            foreach(var menu in new[]{"Tools/Deadly Jenga/Validate Disaster Setup","Tools/Deadly Jenga/Test Disaster Map Contract","Tools/Deadly Jenga/Test Tsunami Forces"})
                Require(EditorApplication.ExecuteMenuItem(menu),"Editor contract check runs: "+menu);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
        static void Save()=>File.WriteAllLines(ProjectPath.Report("CrossSystemSweep.txt"),lines);
        static void Require(bool ok,string message)
        {lines.Add((ok?"PASS ":"FAIL ")+message);Save();if(!ok)throw new Exception(message);}
        static void Watchdog(){if(EditorApplication.timeSinceStartup>deadline){lines.Add("FAIL Sweep timed out");Save();Finish(false);}}
        static void Finish(bool ok){SessionState.SetBool(Key+"Complete",ok);SessionState.SetBool(Key+"Failed",!ok);EditorApplication.ExitPlaymode();}
        static IEnumerator Guard(IEnumerator root)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                bool next=false;object value=null;Exception error=null;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){lines.Add("FAIL "+error);Save();Finish(false);yield break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
            }
            Finish(true);
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude);
        static IEnumerator Check()
        {
            yield return new WaitForSeconds(3);
            var s=Object.FindFirstObjectByType<CastlePlaySession>();s.SetMenu(true);
            Require(errors.Count==0,"Game enters Play Mode cleanly with current combined changes");
            Require(ReferenceEquals(Services.World,s.generator)&&ReferenceEquals(Services.Session,s),"World/session services point to the active Game");
            Require(s.Player.Grounded&&Finite(s.Player.transform.position),"Local survivor settles on castle");
            Require(s.generator.Bodies.Count>0&&s.generator.Bodies.All(b=>b&&Finite(b.position)),"Castle bodies are present and finite");
            Require(s.Player.jumpHeight<s.generator.settings.blockSize.y,"Session jump tuning stays below a full block");
            foreach(var prefab in new[]{s.survivorPrefab.gameObject,s.handPrefab.gameObject,s.generator.blockPrefab})
                Require(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),prefab.name+" has no missing scripts");
            Require(s.GetComponent<SocialWorld>()&&s.GetComponent<CandyDemoHud>(),"Voice/social and detailed Demo HUD coexist");
            int initialCameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.isActiveAndEnabled);
            for(int i=0;i<6;i++)
            {
                s.SetRole(i%2==0);s.SetMenu(true);yield return null;
                Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.isActiveAndEnabled)==1,"Role switch "+i+" keeps one active camera");
                Require(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c=>c.isActiveAndEnabled)==1,"Role switch "+i+" keeps one audio listener");
            }
            s.SetRole(false);s.SetMenu(true);
            yield return Movement(s);
            for(int i=0;i<3;i++)Require(s.Bots.SpawnBot(),"Live survivor bot "+i+" spawns");
            for(int i=0;i<2;i++)Require(s.HandBots.SpawnHand(),"Live physical hand bot "+i+" spawns");
            var bot=s.Bots.Bots[0];var start=bot.transform.position;yield return new WaitForSeconds(5);
            Require(s.Bots.Bots.All(b=>b&&Finite(b.transform.position)),"Live survivor bots remain finite with two AI hands active");
            Require(Vector3.Distance(start,bot.transform.position)>.02f,"A real survivor bot makes movement decisions in the live castle");
            int began=0,ended=0;Action<HazardType,Vector3> begin=(_,__)=>began++;Action<HazardType,Vector3> end=(_,__)=>ended++;
            GameEvents.HazardStarted+=begin;GameEvents.HazardEnded+=end;
            try
            {
                foreach(var prefab in DemoDisasters.Prefabs)Require(DemoDisasters.Execute(s,prefab.name),"Demo command triggers "+prefab.name);
                Require(!DemoDisasters.Execute(s,"Tsunami"),"Duplicate live tsunami is rejected");
                DemoDisasters.StopAll(s);yield return null;
                Require(began==ended&&Tracked.Waves.Count==0,"Stopping all hazards balances events and clears ocean wave masking");
                Require(DemoDisasters.Execute(s,"Tsunami"),"Tsunami can retrigger after Stop All");
                var wave=s.GetComponentsInChildren<TsunamiHazard>().Single(h=>h.Active);wave.End();
                wave.warningTime=.2f;wave.riseTime=.5f;wave.approachSpeed=25;wave.offshoreDistance=2;wave.recessionDistance=2;wave.fallTime=.5f;wave.randomDirection=false;
                wave.Begin();bool touched=false;float until=Time.time+15;
                while(wave&&wave.Active&&Time.time<until){touched|=wave.LastTargetCount>0;yield return null;}
                Require(touched,"Full tsunami crossing touches targets in the actual live castle");
                Require(!wave||!wave.Active,"Full wave ends without manual stop");
                yield return null;Require(Tracked.Waves.Count==0,"Completed wave leaves no stale global wave source");
                Require(DemoDisasters.Execute(s,"Tsunami"),"Another tsunami starts before a map reset");
                int count=s.generator.Bodies.Count;s.ResetCastle();yield return new WaitForSeconds(3);
                Require(Tracked.Waves.Count==0&&!s.GetComponentsInChildren<MonoBehaviour>().OfType<IHazard>().Any(h=>h.Active),"Rebuild during a disaster cancels hazards and visual mask");
                Require(s.generator.Bodies.Count==count&&s.generator.Bodies.All(b=>b&&Finite(b.position)),"Rebuild restores all block bodies while bots/hands exist");
                Require(s.Player.Grounded&&s.Bots.Bots.All(b=>b&&Finite(b.transform.position)),"Survivor and AI recover after disaster plus reset");
            }
            finally{GameEvents.HazardStarted-=begin;GameEvents.HazardEnded-=end;DemoDisasters.StopAll(s);}
            s.HandBots.ClearHands();s.Bots.ClearBots();yield return null;
            Require(s.HandBots.Count==0&&s.Bots.Count==0,"Clear population removes live AI after stress");
            Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.isActiveAndEnabled)==initialCameras,"No camera leak after bots, hazards and repeated role changes");
            Require(errors.Count==0,"No runtime errors across integrated sweep: "+string.Join("\n",errors));
            lines.Add("COMPLETE: live Game integration; no scenes/prefabs saved. Quest hardware, GPU/stereo and multi-device Wi-Fi remain untested.");Save();
        }
        static IEnumerator Movement(CastlePlaySession s)
        {
            var root=new GameObject("Temporary sweep movement fixture");root.SetActive(false);
            var m=Object.Instantiate(s.survivorPrefab,root.transform).GetComponent<CastleSurvivor>();m.externallyDriven=true;m.enabled=false;
            foreach(var c in m.GetComponentsInChildren<Camera>(true))c.enabled=false;
            foreach(var a in m.GetComponentsInChildren<AudioListener>(true))a.enabled=false;
            root.SetActive(true);m.RefreshMode();
            var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.transform.SetParent(root.transform);ramp.transform.localScale=new Vector3(30,.5f,20);
            try
            {
                void Settle(float angle)
                {
                    ramp.transform.SetPositionAndRotation(new Vector3(150,40,0),Quaternion.Euler(0,0,angle));Physics.SyncTransforms();
                    m.Teleport(ramp.transform.position+ramp.transform.up*.25f+Vector3.up*.5f,Quaternion.LookRotation(Vector3.right));
                    for(int i=0;i<100;i++)m.Simulate(Vector2.zero,false,1f/60);
                }
                Settle(30);var p=m.transform.position;for(int i=0;i<30;i++)m.Simulate(Vector2.up,false,1f/60);
                Require(m.Grounded&&m.transform.position.y>p.y+.5f,"Inclined debris permits uphill walking");
                m.Simulate(Vector2.zero,true,1f/60);Require(m.VerticalSpeed>0,"Inclined support still permits jumping");
                Settle(55);p=m.transform.position;for(int i=0;i<30;i++)m.Simulate(Vector2.zero,false,1f/60);
                Require(m.Sliding&&m.transform.position.x<p.x,"Steep debris slides instead of locking movement");
                m.Simulate(Vector2.zero,true,1f/60);Require(m.VerticalSpeed>0,"Sliding player can jump away");
                ramp.SetActive(false);
                var blocks=new List<CastleBlock>();
                foreach(float x in new[]{150f,156.15f})
                {
                    var b=Object.Instantiate(s.generator.blockPrefab,root.transform).GetComponent<CastleBlock>();
                    b.GetComponent<Rigidbody>().isKinematic=true;b.transform.position=new Vector3(x,45,2);b.transform.localScale=new Vector3(6,10,2);b.SyncSurface();blocks.Add(b);
                }
                Physics.SyncTransforms();m.Teleport(new Vector3(152.7f,42,.82f),Quaternion.identity);
                for(int i=0;i<15;i++)m.Simulate(Vector2.zero,false,1f/60,true);
                for(int i=0;i<35;i++)m.Simulate(Vector2.right,false,1f/60,true);
                Require(m.Climbing&&m.transform.position.x>153.15f,"Climbing crosses a small real block seam");
                m.ApplyTraversalImpulse(new Vector3(5,2,0));
                Require(!m.Climbing,"A disaster/impact breaks climbing rather than permanent latch immunity");
                for(int i=0;i<8;i++)m.Simulate(Vector2.zero,false,1f/60,true);
                Require(!m.Climbing,"Held jump cannot immediately negate the impact by regrabbing");
                Require(Physics.GetIgnoreLayerCollision(Layers.Survivor,Layers.Block),"Survivor movement still cannot push physics blocks");
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
    }
}
