using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class DisasterArtChecks
    {
        const string Key="DisasterArtChecks";
        static int errors;
        static string Report=>ProjectPath.Report("DisasterArt.txt");
        static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        sealed class FrameSource:IDisasterState {public DisasterFrame frame;public DisasterFrame Capture()=>frame;}
        static DisasterArtChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>{var go=new GameObject("Disaster art check");go.AddComponent<DisasterArtDriver>().StartCoroutine(Guard(Check()));};}
                if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);var path=SessionState.GetString(Key+"Scene","");if(File.Exists(path))EditorSceneManager.OpenScene(path);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Disaster Art %&d")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            SessionState.SetString(Key+"Scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);File.WriteAllText(Report,"Disaster visual regression\n");
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+stack+"\n");}}
        static void Require(bool value,string label){File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");if(!value)throw new Exception(label);}
        static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
        static IEnumerator Guard(IEnumerator check)
        {
            while(true){bool next;object current=null;try{next=check.MoveNext();if(next)current=check.Current;}catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");break;}if(!next)break;yield return current;}
            (check as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            yield return new WaitForSeconds(1);
            Require(Services.World!=null&&Services.Session?.Player!=null,"Game enters Play Mode with castle and survivor");
            var art=Object.FindFirstObjectByType<CandyDisasterVisuals>();var lighting=Object.FindFirstObjectByType<CandyHazardLighting>();
            foreach(string name in new[]{"CandyDisasterSmoke","CandyStormFunnel","CandyMeteorStone","CandyVampireDarkness","CandyGravityBubble"}){var shader=Resources.Load<Shader>("CandyScenery/"+name);Require(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),name+" compiles and is supported");}
            var original=Services.Session;var oldCamera=original.ActiveCamera;string oldTag=oldCamera.tag;
            var rig=new GameObject("Disaster art capture camera");var camera=rig.AddComponent<Camera>();camera.enabled=true;camera.depth=20;camera.fieldOfView=60;camera.useOcclusionCulling=false;oldCamera.tag="Untagged";rig.tag="MainCamera";
            var fake=new GameObject("Visual-only session stand-in");fake.AddComponent<FakeSession>();
            var source=new FrameSource();DisasterStates.Active.Add(source);
            try
            {
                var center=Services.World.Center;float water=Services.World.WaterHeight(center);
                source.frame=new DisasterFrame{kind="Tornado",center=new Vector3(center.x+30,water,center.z),values=new[]{65f,20f,12f,38f},warning=0};
                camera.transform.position=center+new Vector3(65,35,-80);camera.transform.LookAt(center+new Vector3(12,24,0));
                Call(art,"LateUpdate");Capture(camera,"DisasterArt-Tornado.png");
                var funnel=(Mesh)Get(art,"funnel");Require(funnel&&funnel.uv.Length==funnel.vertexCount&&funnel.vertexCount<2000,"Tornado has animated textured funnel with a bounded mesh budget");
                yield return null;source.frame=new DisasterFrame{kind="PlanetKillerMeteor",sampledAt=Time.time,points=new[]{new DisasterPoint{id=101,position=center+new Vector3(4,32,0),velocity=new Vector3(-8,-24,0),size=16,life=4}}};
                camera.transform.position=center+new Vector3(40,32,-58);camera.transform.LookAt(center+Vector3.up*23);Call(art,"LateUpdate");Capture(camera,"DisasterArt-Meteor.png");
                int bodies=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
                GameEvents.RaiseBlockHit(source.frame.points[0].position,28);Require(((IList)Get(art,"fragments")).Count==0,"Nearby contact waits for confirmed meteor disappearance");source.frame.points=Array.Empty<DisasterPoint>();yield return new WaitForSeconds(.06f);Call(art,"LateUpdate");Require(((IList)Get(art,"fragments")).Count==72,"Planet killer bursts into 72 visual rock fragments on contact");
                source.frame.points=Array.Empty<DisasterPoint>();yield return new WaitForSeconds(.25f);Call(art,"LateUpdate");Capture(camera,"DisasterArt-Rubble.png");
                source.frame=new DisasterFrame{kind="MeteorShower",sampledAt=Time.time,points=new[]{new DisasterPoint{id=102,position=center+new Vector3(0,30,0),velocity=Vector3.down*45,size=3,life=4}}};
                Call(art,"LateUpdate");int before=((IList)Get(art,"fragments")).Count;GameEvents.RaiseBlockHit(source.frame.points[0].position,12);source.frame.points=Array.Empty<DisasterPoint>();yield return new WaitForSeconds(.06f);Call(art,"LateUpdate");
                Require(((IList)Get(art,"fragments")).Count==before+12,"Shower uses a smaller 12-fragment burst");
                for(int i=0;i<40;i++)Call(art,"Burst",center+Vector3.up*30,new DisasterPoint{id=i,size=10,velocity=Vector3.down*20},true);
                Require(((IList)Get(art,"fragments")).Count==384&&bodies==Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length,"Fragment pool remains capped at 384 and adds no physics bodies");
                yield return null;source.frame=new DisasterFrame{kind="Vampire",values=new[]{0f,.1f,45f}};
                camera.transform.position=center+new Vector3(100,20,0);camera.transform.rotation=Quaternion.identity;
                Call(lighting,"LateUpdate");Require((bool)Get(lighting,"vampireLighting"),"Active Vampire state drives the local darkness effect");lighting.GetType().GetField("exposure",Flags).SetValue(lighting,0f);
                var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",Color.white);
                var near=GameObject.CreatePrimitive(PrimitiveType.Cube);var far=GameObject.CreatePrimitive(PrimitiveType.Cube);
                near.transform.position=camera.transform.TransformPoint(new Vector3(-.36f,0,1));near.transform.localScale=new Vector3(.3f,.6f,.1f);near.GetComponent<Renderer>().sharedMaterial=material;
                far.transform.position=camera.transform.TransformPoint(new Vector3(1.44f,0,4));far.transform.localScale=new Vector3(1.2f,2.4f,.1f);far.GetComponent<Renderer>().sharedMaterial=material;
                Call(lighting,"DrawVampireShade",camera);var image=Capture(camera,"DisasterArt-VampireShade.png");
                float nearLight=image.GetPixel(440,400).grayscale,farLight=image.GetPixel(840,400).grayscale;
                Require(nearLight>.02f&&farLight<.015f,"Vampire shade preserves nearby visibility and fully blacks out distant geometry");Object.Destroy(image);
                source.frame=null;Call(lighting,"LateUpdate");Require(!(bool)Get(lighting,"vampireLighting"),"Stopping Vampire restores lighting and disables local darkness");
                Object.Destroy(near);Object.Destroy(far);Object.Destroy(material);
                yield return new WaitForSeconds(3.1f);Call(art,"LateUpdate");Require(((IList)Get(art,"fragments")).Count==0,"Impact debris expires and releases its slots");
                Require(errors==0,"No runtime errors during visual regression");File.AppendAllText(Report,"COMPLETE. Desktop render/cleanup checked. Quest stereo and GPU budget require device testing.\n");
            }
            finally{DisasterStates.Active.Remove(source);oldCamera.tag=oldTag;Services.Session=original;Object.Destroy(fake);Object.Destroy(rig);}
        }
        static Texture2D Capture(Camera camera,string name)
        {
            var renderers=Object.FindObjectsByType<CandyCrateVisual>(FindObjectsSortMode.None).Select(v=>v.GetComponent<Renderer>()).ToArray();var hidden=renderers.Select(r=>r.forceRenderingOff).ToArray();foreach(var r in renderers)r.forceRenderingOff=false;
            var rt=RenderTexture.GetTemporary(1280,800,24);var old=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());return image;}
            finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].forceRenderingOff=hidden[i];}
        }
    }
    public sealed class DisasterArtDriver:MonoBehaviour{}
}




