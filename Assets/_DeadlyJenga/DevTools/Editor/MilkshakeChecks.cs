using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeadlyJenga.World.Maps;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    [InitializeOnLoad] public static class MilkshakeChecks
    {
        const string Key="MilkshakeChecks",Request="Logs/Validation/Milkshake.request";
        static int errors;
        static MilkshakeChecks()
        {
            EditorApplication.update+=()=>{if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){File.Delete(Request);Run();}};
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){errors=0;Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>IslandLobby.Current.StartCoroutine(Guard(Check()));}
                if(state==PlayModeStateChange.EnteredEditMode){Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);string original=SessionState.GetString(Key+"Original","");if(File.Exists(original))EditorSceneManager.OpenScene(original);}
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validate Milkshake Parkour")]
        public static void Run()
        {File.WriteAllText(ProjectPath.Report("MilkshakeChecks.txt"),"Giant milkshake traversal\n");SessionState.SetString(Key+"Original",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(ProjectPath.Find("Lobby.unity"));EditorApplication.EnterPlaymode();}
        static void Log(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(ProjectPath.Report("MilkshakeChecks.txt"),"ERROR "+m+"\n"+trace+"\n");}}
        static void Require(bool ok,string text){File.AppendAllText(ProjectPath.Report("MilkshakeChecks.txt"),(ok?"PASS ":"FAIL ")+text+"\n");if(!ok)throw new Exception(text);}
        static IEnumerator Guard(IEnumerator test)
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(test);
            while(stack.Count>0){bool next=false;object value=null;Exception error=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){File.AppendAllText(ProjectPath.Report("MilkshakeChecks.txt"),"FAIL "+error+"\n");EditorApplication.ExitPlaymode();yield break;}
                if(!next){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;}
            EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            yield return new WaitForSecondsRealtime(1);var lobby=IslandLobby.Current;lobby.SetMenu(true);var shake=Object.FindFirstObjectByType<LobbyMilkshake>();
            Require(shake&&shake.Geometry,"Milkshake collision geometry exists independently of its artwork");
            Require(shake.GetComponentsInChildren<Rigidbody>().Length==0,"Milkshake and clutter use static collision, with no network physics objects");
            Require(shake.Geometry.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"Presentation replaces all gray-box renderers");
            Vector3 c=shake.transform.position;
            Require(Physics.Raycast(c+Vector3.up*8,Vector3.down,out var inside,10)&&Mathf.Abs(inside.point.y-shake.MilkHeight)<.02f,"The open centre has no invisible lid; the first surface is the half-full chocolate");
            Require(Physics.Raycast(c+new Vector3(-shake.radius,8,0),Vector3.down,out var rim,10)&&Mathf.Abs(rim.point.y-shake.RimHeight)<.02f,"The glass rim is a real supporting surface");
            Capture("lobby-milkshake-overview.png",new Vector3(-5,6,-12),new Vector3(2,2.6f,-1.5f));
            Capture("lobby-milkshake-clutter.png",new Vector3(8,3.4f,-7.6f),new Vector3(3.4f,2.7f,-2.4f));
            Capture("lobby-milkshake-inside.png",c+new Vector3(0,shake.height+1.9f,-.3f),c+Vector3.up*1.9f);
            var motor=Object.Instantiate(lobby.catalog.survivorPrefab);motor.name="Temporary milkshake traversal probe";motor.enabled=false;motor.externallyDriven=true;motor.RefreshMode();motor.viewCamera.gameObject.SetActive(false);
            try
            {
                // These runs use the current player's capsule, jump, collision and vault code.
                Vector3 cooler=c+new Vector3(2.4f,1.06f,-.3f),crate=c+new Vector3(2.8f,2.56f,1.5f),shade=c+new Vector3(1.8f,3.71f,2.9f);
                Require(JumpTo(motor,c+new Vector3(1.4f,.08f,-1f),cooler,.5f),"Ground to cooler is reachable with the current movement controller");
                Require(JumpTo(motor,cooler+Vector3.up*.02f,crate,.38f),"Cooler to delivery stack is reachable");
                Require(JumpTo(motor,crate+Vector3.up*.02f,shade,.65f),"Delivery stack to parasol is reachable");
                // Aim for the nearest rim, then run the real falling/collision step into the opening.
                Vector3 approach=Vector3.ProjectOnPlane(shade-c,Vector3.up).normalized;
                Vector3 rimTarget=c+approach*shake.radius+Vector3.up*shake.height;
                Require(JumpTo(motor,shade+Vector3.up*.02f,rimTarget,.2f),"Parasol to the narrow glass rim is reachable");
                motor.Teleport(c+Vector3.up*(shake.height+.3f),Quaternion.identity);
                for(int i=0;i<100;i++)motor.Simulate(Vector2.zero,false,.02f);
                Require(Mathf.Abs(motor.transform.position.y-shake.MilkHeight)<.05f&&motor.Grounded,"A player dropped through the opening lands inside on the chocolate");
                Vector3 cherry=c+new Vector3(-.52f,shake.height*shake.fill+.5f,.1f);
                Require(JumpTo(motor,c+Vector3.up*(shake.height*shake.fill+.03f),cherry,.2f),"The cherry gives an interior foothold");
                Require(JumpTo(motor,cherry+Vector3.up*.02f,c+Vector3.left*shake.radius+Vector3.up*shake.height,.2f),"The interior foothold permits an escape jump and vault to the rim");
                motor.walkSpeed=motor.vrWalkSpeed;
                Require(JumpTo(motor,c+new Vector3(1.4f,.08f,-1f),cooler,.5f),"The first jump also works at the slower VR movement speed");
                Require(JumpTo(motor,cooler+Vector3.ProjectOnPlane(crate-cooler,Vector3.up).normalized*.3f+Vector3.up*.02f,crate,.38f),"An edge takeoff reaches the crates at VR speed");
                Require(JumpTo(motor,crate+Vector3.ProjectOnPlane(shade-crate,Vector3.up).normalized*.25f+Vector3.up*.02f,shade,.65f),"An edge takeoff reaches the parasol at VR speed");
                Require(JumpTo(motor,shade+Vector3.ProjectOnPlane(rimTarget-shade,Vector3.up).normalized*.65f+Vector3.up*.02f,rimTarget,.2f),"Careful parasol edge positioning reaches the rim at VR speed");
                Require(errors==0,"Milkshake scene runs without runtime errors");
                File.AppendAllText(ProjectPath.Report("MilkshakeChecks.txt"),"COMPLETE: desktop capsule/jump/vault simulation. Quest comfort and human difficulty still need hands-on play.\n");
            }
            finally{Object.Destroy(motor.gameObject);}
        }
        static bool JumpTo(CastleSurvivor motor,Vector3 start,Vector3 target,float tolerance)
        {
            if(start.y<1&&Physics.Raycast(start+Vector3.up*10,Vector3.down,out var ground,20))start.y=ground.point.y+.02f;
            // Search release/steering timing; no teleporting once the attempt starts.
            for(int stop=1;stop<65;stop++)
            {
                Vector3 flat=Vector3.ProjectOnPlane(target-start,Vector3.up);motor.Teleport(start,Quaternion.LookRotation(flat));
                for(int settle=0;settle<20;settle++){motor.Simulate(Vector2.zero,false,.02f);if(motor.Grounded)break;}
                if(!motor.Grounded)continue;
                bool launched=false;
                for(int i=0;i<110;i++)
                {
                    Vector3 delta=Vector3.ProjectOnPlane(target-motor.transform.position,Vector3.up);float distance=delta.magnitude;
                    Vector2 input=i<stop?Vector2.up:Vector2.zero;
                    motor.Simulate(input,i==0,.02f);if(i==0)launched=motor.VerticalSpeed>0;
                    if(i>8&&!motor.Grounded&&!motor.Vaulting&&motor.VerticalSpeed<3)motor.TryStartVault();
                    if(launched&&i>8&&motor.Grounded&&Mathf.Abs(motor.transform.position.y-target.y)<.09f&&distance<tolerance)return true;
                    if(motor.transform.position.y<start.y-1)break;
                }
            }
            File.AppendAllText(ProjectPath.Report("MilkshakeChecks.txt"),"Route failed from "+start+" to "+target+"; final feet "+motor.transform.position+"\n");return false;
        }
        static void Capture(string name,Vector3 position,Vector3 target)
        {
            var go=new GameObject("Temporary milkshake screenshot");var camera=go.AddComponent<Camera>();camera.fieldOfView=65;camera.transform.position=position;camera.transform.LookAt(target);
            var rt=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;Object.Destroy(rt);if(pixels)Object.Destroy(pixels);Object.Destroy(go);}
        }
    }
}

