using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicitly requested appearance recordings; never saves scenes or changes assets.
    [InitializeOnLoad] public static class AppearanceCapture
    {
        const string Key="AppearanceCapture";
        static string Report=>ProjectPath.Report("AppearanceCapture.txt");
        static string Folder=>Path.Combine(Path.GetDirectoryName(Report),"AppearanceCapture");
        static Camera camera;static RenderTexture target;static Texture2D pixels;
        static AppearanceCapture()
        {
            EditorApplication.update+=()=>
            {
                var request=ProjectPath.Report("AppearanceCapture.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                File.Delete(request);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Temporary appearance recording");Object.DontDestroyOnLoad(go);go.AddComponent<AppearanceCaptureDriver>().StartCoroutine(Guard());};
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key,false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
                    var original=SessionState.GetString(Key+"Scene","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Capture/Requested Appearance Clips")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Report,"Current production visuals, staged outside cameras. No gameplay assets changed.\n");
            SessionState.SetString(Key+"Scene",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.ModeId="demo";MatchSetup.MapId="castle";
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static IEnumerator Guard()
        {
            var sequence=Capture();
            while(true)
            {
                bool more=false;object value=null;Exception failure=null;
                try{more=sequence.MoveNext();if(more)value=sequence.Current;}catch(Exception e){failure=e;}
                if(failure!=null){File.AppendAllText(Report,"ERROR "+failure+"\n");break;}
                if(!more)break;yield return value;
            }
            if(camera)Object.Destroy(camera.gameObject);if(target){target.Release();Object.Destroy(target);}if(pixels)Object.Destroy(pixels);
            File.AppendAllText(Report,"COMPLETE\n");EditorApplication.ExitPlaymode();
        }
        static byte[] Frame()
        {
            var previous=RenderTexture.active;camera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=previous;
            return pixels.EncodeToJPG(90);
        }
        static CandyPlayerRig Actor(CandyPlayerRig prefab,Vector3 feet,int color,bool attack)
        {
            var rig=Object.Instantiate(prefab);rig.ownerCamera=null;rig.SetColor(color);
            var rotation=Quaternion.Euler(0,180,0);
            var frame=new CandyAvatarFrame{feet=feet,head=feet+Vector3.up*1.6f,headRotation=rotation,scale=1};
            frame.left=new CandyHandPose{position=feet+rotation*new Vector3(-.25f,1.15f,.3f),rotation=rotation,skeleton=CandyHandSkeleton.Rest(true,.15f),skeletonTracked=true};
            frame.right=new CandyHandPose{position=feet+rotation*new Vector3(.25f,attack?1.25f:1.1f,attack?.65f:.3f),rotation=rotation*Quaternion.Euler(attack?-10:0,0,0),skeleton=CandyHandSkeleton.Rest(false,attack?1:.15f),skeletonTracked=true};
            rig.ApplyFrame(frame);return rig;
        }
        static IEnumerator Capture()
        {
            yield return new WaitForSeconds(2);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();if(!session||Services.World==null)throw new Exception("Game not ready");
            session.SetMenu(true);session.Modes.enabled=false;
            var gameView=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");if(gameView!=null)EditorWindow.GetWindow(gameView).Focus();
            var go=new GameObject("Temporary outside camera");camera=go.AddComponent<Camera>();camera.CopyFrom(session.ActiveCamera);camera.enabled=false;camera.fieldOfView=45;camera.nearClipPlane=.05f;camera.farClipPlane=1500;
            target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var world=Services.World;
            var top=world.Blocks.Where(b=>b&&b.GetComponent<Collider>()).OrderByDescending(b=>b.GetComponent<Collider>().bounds.max.y).First();
            var bounds=top.GetComponent<Collider>().bounds;var feet=new Vector3(bounds.center.x,bounds.max.y+.01f,bounds.center.z);
            var prefab=session.Player.gameObject.GetComponent<CandyPlayerAvatar>().visualPrefab;
            var actor=Actor(prefab,feet,1,false);
            camera.transform.position=feet+new Vector3(2.6f,1.9f,-4);camera.transform.LookAt(feet+Vector3.up*.75f);
            File.AppendAllText(Report,"Death: actual CandyMelt.Spawn on shared player visual, fixed exterior camera.\n");
            var death=Path.Combine(Folder,"death");Directory.CreateDirectory(death);var times=new System.Collections.Generic.List<float>();
            float start=Time.realtimeSinceStartup;bool dead=false;
            while(Time.realtimeSinceStartup-start<4)
            {
                float age=Time.realtimeSinceStartup-start;
                if(age>=1&&!dead){dead=true;CandyMelt.Spawn(actor.transform,actor.style.ColorAt(1));foreach(var r in actor.GetComponentsInChildren<Renderer>())r.enabled=false;}
                times.Add(age);File.WriteAllBytes(Path.Combine(death,$"{times.Count-1:D4}.jpg"),Frame());yield return new WaitForSecondsRealtime(1f/15);
            }
            Timeline(death,times);Object.Destroy(actor.gameObject);
            var left=Actor(prefab,feet+Vector3.left*1.15f,1,true);var right=Actor(prefab,feet+Vector3.right*1.15f,5,true);
            var weapons=new GameObject("Temporary special punch preview").AddComponent<CandyMysteryView>();weapons.enabled=false;
            var make=typeof(CandyMysteryView).GetMethod("MakeWeapon",BindingFlags.Instance|BindingFlags.NonPublic);
            var blade=(Transform)make.Invoke(weapons,new object[]{MysteryRole.Murderer});blade.SetPositionAndRotation(left.RightPalm.position,left.RightPalm.rotation);
            var hammer=(Transform)make.Invoke(weapons,new object[]{MysteryRole.Sheriff});hammer.SetPositionAndRotation(right.RightPalm.position,right.RightPalm.rotation);
            camera.transform.position=feet+new Vector3(3.3f,2.3f,-6);camera.transform.LookAt(feet+new Vector3(0,.95f,-.25f));
            yield return new WaitForSecondsRealtime(.2f);Frame();File.WriteAllBytes(Path.Combine(Folder,"Murderer-and-Sheriff.png"),pixels.EncodeToPNG());
            File.AppendAllText(Report,"Weapons: actual MakeWeapon meshes, held at shared avatar right-palm special punch poses.\n");
            Object.Destroy(left.gameObject);Object.Destroy(right.gameObject);Object.Destroy(weapons.gameObject);
            session.SetSeaSpirit(true);session.SetMenu(true);
            var spirit=session.Hand.gameObject.GetComponent<SeaSpiritController>();if(!spirit)throw new Exception("Sea spirit did not activate");
            spirit.orbitAngle=0;spirit.reach=25;
            yield return null;
            var center=world.Center;float water=world.WaterHeight(center);
            // Eye height at the island rim, facing the actual offshore spirit.
            camera.fieldOfView=70;camera.transform.position=new Vector3(center.x+world.Radius*1.1f,water+2.5f,center.z-world.Radius*.8f);
            float radius=Mathf.Max(world.Radius*1.7f,20);
            var aim=new Vector3(center.x,Mathf.Max(water+4,world.TopHeight*.35f),center.z+radius);camera.transform.LookAt(aim);camera.ResetWorldToCameraMatrix();camera.ResetProjectionMatrix();
            File.AppendAllText(Report,$"World center={center} water={water} radius={world.Radius} top={world.TopHeight}; island camera={camera.transform.position} aim={aim}; spirit anchor={spirit.BustAnchor.position} active={spirit.Active}\n");
            var sea=Path.Combine(Folder,"sea-spirit");Directory.CreateDirectory(sea);times.Clear();start=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-start<9)
            {
                float age=Time.realtimeSinceStartup-start;times.Add(age);File.WriteAllBytes(Path.Combine(sea,$"{times.Count-1:D4}.jpg"),Frame());
                if(age>5&&age<5.2f){File.WriteAllBytes(Path.Combine(Folder,"Sea-spirit-preview.png"),pixels.EncodeToPNG());File.AppendAllText(Report,$"At five seconds camera={camera.transform.position} direction={camera.transform.forward}; anchor={spirit.BustAnchor.position}; spirit visuals={Object.FindObjectsByType<CandySeaSpirit>(FindObjectsSortMode.None).Length}; renderers="+string.Join(";",Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.name=="Water torso"||r.name=="Water head").Select(r=>$"{r.name} {r.bounds} active={r.gameObject.activeInHierarchy}"))+"\n");}
                yield return new WaitForSecondsRealtime(1f/15);
            }
            Timeline(sea,times);File.AppendAllText(Report,"Sea spirit: actual controller entrance and ocean spirit visuals, fixed island-rim eye-height camera.\n");
            session.SetSeaSpirit(false);
        }
        static void Timeline(string folder,System.Collections.Generic.List<float> times)
        {
            using(var writer=new StreamWriter(Path.Combine(folder,"frames.txt")))for(int i=0;i<times.Count;i++)
            {writer.WriteLine($"file '{i:D4}.jpg'");writer.WriteLine("duration "+(i+1<times.Count?times[i+1]-times[i]:1f/15).ToString("0.0000",CultureInfo.InvariantCulture));}
        }
    }
    public sealed class AppearanceCaptureDriver:MonoBehaviour{}
}
