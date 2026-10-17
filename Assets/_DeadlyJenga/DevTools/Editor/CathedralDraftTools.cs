using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.Core;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad] public static class CathedralDraftTools
    {
        const string Key="CathedralDraft",SceneName="MapDrafterEvan1.unity";
        static int errors;
        static string Report=>ProjectPath.Report("CathedralChecks.txt");
        static CathedralDraftTools()
        {
            EditorApplication.update+=()=>
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(File.Exists("Logs/Validation/Cathedral.install.request")){File.Delete("Logs/Validation/Cathedral.install.request");Install();}
                else if(File.Exists("Logs/Validation/Cathedral.request")){File.Delete("Logs/Validation/Cathedral.request");Run();}
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard(Check()));}
                if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Build Evan 1 Cathedral")]
        public static void Install()
        {
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save intended scene edits first.");
            var scene=EditorSceneManager.OpenScene(ProjectPath.Find(SceneName));var generator=Object.FindFirstObjectByType<CastleGenerator>();
            generator.settings.cathedral=true;generator.settings.singleTower=false;generator.environmentOnly=false;generator.generateOnStart=true;
            generator.settings.blockSize=new Vector3(6,2,2);
            VerifyLayout(generator.settings);
            // Save the reproducible generator settings, rather than duplicating thousands of blocks in YAML.
            generator.foundation.localScale=new Vector3(CastleLayout.Footprint(generator.settings)+4,1,CastleLayout.Footprint(generator.settings)+4);
            EditorUtility.SetDirty(generator);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.AppendAllText(Report,"INSTALLED: only Evan 1 is configured for the cathedral; blocks generate from its existing prefab on Play.\n");
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Validate Evan 1 Cathedral")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ProjectPath.Find(SceneName));var generator=Object.FindFirstObjectByType<CastleGenerator>();VerifyLayout(generator.settings);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Require(bool pass,string text)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)throw new InvalidOperationException(text);}
        public static void VerifyLayout(CastleSettings settings)
        {
            File.WriteAllText(Report,"Evan 1 · giant cathedral\n");var blocks=CastleLayout.Build(settings);var groups=blocks.GroupBy(p=>p.Course).ToDictionary(g=>g.Key,g=>g.ToArray());
            Require(settings.cathedral,"The cathedral layout is selected");Require(blocks.Count<20000,"Layout fits the existing snapshot block-count contract: "+blocks.Count);
            int overlaps=0,unsupported=0;
            foreach(var group in groups)
            {
                var items=group.Value;
                for(int i=0;i<items.Length;i++)
                {
                    var p=items[i];var rect=Rect(p,settings.blockSize);var center=new Vector2(p.Position.x,p.Position.z);
                    for(int j=0;j<i;j++)
                    {
                        if((items[j].Position-p.Position).sqrMagnitude>55)continue;
                        var contact=Clip(rect,Rect(items[j],settings.blockSize));
                        if(Area(contact)>.005f){if(overlaps++<12)File.AppendAllText(Report,"OVERLAP c"+p.Course+" "+p.Position+" and "+items[j].Position+" area="+Area(contact)+"\n");}
                    }
                    if(p.Course==0)continue;
                    var supports=new List<Vector2>();
                    foreach(var below in groups[p.Course-1])
                    {
                        if((new Vector2(below.Position.x,below.Position.z)-center).sqrMagnitude>55)continue;
                        var contact=Clip(rect,Rect(below,settings.blockSize));if(Area(contact)>.03f)supports.AddRange(contact);
                    }
                    var hull=Hull(supports);bool stable=hull.Count>=3;
                    for(int n=0;n<hull.Count;n++)if(Cross(hull[(n+1)%hull.Count]-hull[n],center-hull[n])<-.005f)stable=false;
                    if(!stable){if(unsupported++<15)File.AppendAllText(Report,"UNSUPPORTED c"+p.Course+" "+p.Position+"\n");}
                }
            }
            Require(overlaps==0,"No intersecting blocks in any course (or duplicate placements)");
            Require(unsupported==0,"Every elevated block's centre of mass is inside its actual bearing polygon");
            var copy=JsonUtility.FromJson<CastleSettings>(JsonUtility.ToJson(settings));
            var rebuilt=CastleLayout.Build(copy);
            Require(rebuilt.Count==blocks.Count&&rebuilt.Zip(blocks,(a,b)=>a.Position==b.Position&&a.Rotation==b.Rotation).All(equal=>equal),"Serialized map settings regenerate exactly the same ordered block layout");
            var bounds=new Bounds(blocks[0].Position,Vector3.zero);foreach(var p in blocks)foreach(var corner in Rect(p,settings.blockSize))bounds.Encapsulate(new Vector3(corner.x,p.Position.y+settings.blockSize.y*.5f,corner.y));
            Require(bounds.size.x>150&&bounds.size.z>150&&bounds.max.y==180,"Giant footprint and spire height: "+bounds.size+" / top "+bounds.max.y);
            File.AppendAllText(Report,"Geometry/bearing checks are necessary, not proof of whole-structure stability. Gravity simulation follows.\n");
        }
        static List<Vector2> Rect(CastlePlacement p,Vector3 size)
        {
            var result=new List<Vector2>();foreach(var v in new[]{new Vector3(-size.x/2,0,-size.z/2),new Vector3(size.x/2,0,-size.z/2),new Vector3(size.x/2,0,size.z/2),new Vector3(-size.x/2,0,size.z/2)})
            {var point=p.Position+p.Rotation*v;result.Add(new Vector2(point.x,point.z));}return result;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static float Area(List<Vector2> p)
        {float sum=0;for(int i=0;i<p.Count;i++)sum+=Cross(p[i],p[(i+1)%p.Count]);return Mathf.Abs(sum)*.5f;}
        static List<Vector2> Clip(List<Vector2> subject,List<Vector2> clip)
        {
            var output=new List<Vector2>(subject);
            for(int i=0;i<clip.Count&&output.Count>0;i++)
            {
                var input=output;output=new List<Vector2>();var a=clip[i];var edge=clip[(i+1)%clip.Count]-a;var previous=input[input.Count-1];float prev=Cross(edge,previous-a);
                foreach(var current in input)
                {float now=Cross(edge,current-a);if((now>=0)!=(prev>=0))output.Add(Vector2.Lerp(previous,current,prev/(prev-now)));if(now>=0)output.Add(current);previous=current;prev=now;}
            }
            return output;
        }
        static List<Vector2> Hull(List<Vector2> p)
        {
            p=p.Distinct().OrderBy(v=>v.x).ThenBy(v=>v.y).ToList();if(p.Count<3)return p;var h=new List<Vector2>();
            foreach(var v in p){while(h.Count>=2&&Cross(h[h.Count-1]-h[h.Count-2],v-h[h.Count-1])<=0)h.RemoveAt(h.Count-1);h.Add(v);}
            int lower=h.Count;for(int i=p.Count-2;i>=0;i--){var v=p[i];while(h.Count>lower&&Cross(h[h.Count-1]-h[h.Count-2],v-h[h.Count-1])<=0)h.RemoveAt(h.Count-1);h.Add(v);}h.RemoveAt(h.Count-1);return h;
        }
        static IEnumerator Guard(IEnumerator test)
        {
            while(true){bool next=false;Exception error=null;object value=null;try{next=test.MoveNext();if(next)value=test.Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");EditorApplication.ExitPlaymode();yield break;}if(!next)break;yield return value;}
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            yield return null;var generator=Object.FindFirstObjectByType<CastleGenerator>();var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
            Require(generator.Bodies.Count==CastleLayout.Build(generator.settings).Count,"Full scene instantiates the identical physical block prefab");
            Require(generator.Bodies.All(b=>!b.isKinematic&&b.useGravity),"Every structural block is dynamic, with gravity enabled");
            Require(Services.World.PlayableBounds.size.x>160&&Services.World.TopHeight==180,"World bounds and top spawn match the cathedral");
            Require(NetworkActors.Alive(session.Player)&&session.Player.transform.position.y>179,"Current survivor prefab spawns on the highest spire");
            var initial=generator.Bodies.Select(b=>new Pose(b.position,b.rotation)).ToArray();
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                for(int frame=0;frame<1500;frame++)
                {
                    Physics.Simulate(.02f);
                    if(frame%25==24){File.AppendAllText(Report,"Gravity: "+((frame+1)*.02f).ToString("F1")+" simulated seconds; "+watch.Elapsed.TotalSeconds.ToString("F1")+" wall seconds\n");yield return null;}
                }
            }
            finally{Physics.simulationMode=previous;}
            float max=0,angle=0;int shifted=0;
            for(int i=0;i<initial.Length;i++){float d=Vector3.Distance(initial[i].position,generator.Bodies[i].position);max=Mathf.Max(max,d);angle=Mathf.Max(angle,Quaternion.Angle(initial[i].rotation,generator.Bodies[i].rotation));if(d>.5f)shifted++;}
            File.AppendAllText(Report,"Settling: maximum translation "+max+"m; rotation "+angle+"deg; moved >0.5m "+shifted+" / "+initial.Length+"; simulation wall time "+watch.Elapsed.TotalSeconds+"s\n");
            File.AppendAllText(Report,"Sleeping after settling: "+generator.Bodies.Count(b=>b.IsSleeping())+" / "+generator.Bodies.Count+"\n");
            foreach(var group in generator.Bodies.Select((b,i)=>new{body=b,index=i}).GroupBy(x=>Mathf.FloorToInt(initial[x.index].position.y/40)))
                File.AppendAllText(Report,"Height band "+(group.Key*40)+"–"+((group.Key+1)*40)+"m: shifted "+group.Count(x=>Vector3.Distance(initial[x.index].position,x.body.position)>.5f)+" / "+group.Count()+"\n");
            foreach(var sample in generator.Bodies.Select((b,i)=>new{body=b,pose=initial[i],drift=Vector3.Distance(initial[i].position,b.position)}).OrderByDescending(x=>x.drift).Take(8))
                File.AppendAllText(Report,sample.body.name+" start "+sample.pose.position+" drift "+sample.drift+"\n");
            var batching=Resources.FindObjectsOfTypeAll<DeadlyJenga.CandyArt.CandyCrateBatchRenderer>().FirstOrDefault();
            if(batching)batching.enabled=false;
            yield return null;yield return null;
            Capture("cathedral-front.png",new Vector3(175,130,-210),new Vector3(0,70,0));
            Capture("cathedral-overview.png",new Vector3(-170,190,150),new Vector3(0,65,0));
            Capture("cathedral-nave.png",new Vector3(0,6,-42),new Vector3(0,100,0));
            Capture("cathedral-gate.png",new Vector3(25,23,-100),new Vector3(0,24,-69));
            if(batching)batching.enabled=true;
            Require(shifted==0&&angle<6,"Whole cathedral remains standing after thirty simulated seconds of unconstrained gravity");
            Require(errors==0,"Cathedral enters Play Mode without runtime errors");
            Require(EditorApplication.ExecuteMenuItem("Tools/Deadly Jenga/Validate Disaster Setup"),"Map/disaster catalog validation runs");
            File.AppendAllText(Report,"COMPLETE: layout, gravity, spawn and bounds. Not a Quest performance or live two-headset test.\n");
        }
        static void Capture(string name,Vector3 position,Vector3 target)
        {
            var go=new GameObject("Temporary cathedral view");var camera=go.AddComponent<Camera>();camera.farClipPlane=2000;camera.fieldOfView=58;camera.transform.position=position;camera.transform.LookAt(target);
            var rt=new RenderTexture(1800,1200,24);var previous=RenderTexture.active;Texture2D pixels=null;
            var batching=Resources.FindObjectsOfTypeAll<DeadlyJenga.CandyArt.CandyCrateBatchRenderer>().FirstOrDefault();bool batched=batching&&batching.enabled;
            // Camera.Render does not include the normal frame's instanced submissions.
            try{if(batched)batching.enabled=false;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1800,1200,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1800,1200),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());}
            finally{if(batched&&batching)batching.enabled=true;RenderTexture.active=previous;Object.Destroy(rt);if(pixels)Object.Destroy(pixels);Object.Destroy(go);}
        }
    }
}
