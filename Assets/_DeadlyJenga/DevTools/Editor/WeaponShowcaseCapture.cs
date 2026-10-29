using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit process-addressed capture only. Uses production meshes and scene lighting.
    [InitializeOnLoad] public static class WeaponShowcaseCapture
    {
        const string Key="WeaponShowcase";
        static string Report=>ProjectPath.Report("WeaponShowcase.txt");
        static string Output=>Path.Combine(Path.GetDirectoryName(Application.dataPath),"ArtDirection","WeaponShowcase-20261026");
        [Serializable] sealed class Saved {public SceneSetup[] scenes;}
        [Serializable] public sealed class Shot {public string weapon,mode,view,file;}
        [Serializable] sealed class Manifest {public string captured;public string evidence="Actual Unity Game scene lighting; production CandyWeaponVisual and CandyPlayerRig, staged poses. No weapon art changes.";public List<Shot> shots=new();}
        static WeaponShowcaseCapture()
        {
            EditorApplication.update+=()=>{
                var path=ProjectPath.Report("WeaponShowcase.request");
                if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(File.ReadAllText(path).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString())return;
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(path);Run();
            };
            EditorApplication.playModeStateChanged+=state=>{
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode){LockCapture();EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode){
                    SessionState.SetBool(Key,false);ValidationRunGate.Release(Key);
                    if(SessionState.GetBool(Key+"Locked",false)){AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();SessionState.SetBool(Key+"Locked",false);}
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Saved>(SessionState.GetString(Key+"Scenes","")).scenes);
                }
            };
        }
        public static void Run()
        {
            var scenes=EditorSceneManager.GetSceneManagerSetup();
            if(scenes.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty)){ValidationRunGate.Release(Key);throw new InvalidOperationException("Unsaved scenes left untouched.");}
            Directory.CreateDirectory(Output);File.WriteAllText(Report,"Actual production weapon capture starting.\n");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Saved{scenes=scenes}));SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            LockCapture();
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void LockCapture(){if(SessionState.GetBool(Key+"Locked",false))return;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);}
        static IEnumerator Guard()
        {
            var run=Capture();while(true){bool more=false;object value=null;Exception error=null;try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){error=e;}if(error!=null){File.AppendAllText(Report,"ERROR "+error+"\n");break;}if(!more)break;yield return value;}
            (run as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();
        }
        static IEnumerator Capture()
        {
            float until=Time.realtimeSinceStartup+40;CastlePlaySession session;
            while(!(session=Object.FindFirstObjectByType<CastlePlaySession>())||session.Player==null||Services.World==null){if(Time.realtimeSinceStartup>until)throw new Exception("Game not ready");yield return null;}
            bool background=Application.runInBackground;float volume=AudioListener.volume;Application.runInBackground=true;AudioListener.volume=0;
            GameObject fixture=null;RenderTexture rt=null;Texture2D pixels=null;
            try{
                session.SetMenu(true);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots?.Clear();session.Demo.Run(DemoCommand.StopDisasters);
                if(session.Social!=null)session.Social.VoiceEnabled=false;
                yield return new WaitForSecondsRealtime(2);session.Modes.enabled=false;
                var world=Services.World;var top=world.Blocks.Where(b=>b&&b.GetComponent<Collider>()).Select(b=>b.GetComponent<Collider>().bounds).OrderByDescending(b=>b.max.y).First();
                var feet=new Vector3(top.center.x,top.max.y+.025f,top.center.z);
                fixture=new GameObject("Temporary production weapon showcase");
                var prefab=session.Player.gameObject.GetComponent<CandyPlayerAvatar>().visualPrefab;
                var rig=Object.Instantiate(prefab,fixture.transform);rig.ownerCamera=null;rig.SetColor(5);
                var weaponHost=new GameObject("Production held weapon").transform;weaponHost.SetParent(fixture.transform);
                var cam=new GameObject("Capture camera").AddComponent<Camera>();cam.transform.SetParent(fixture.transform);cam.CopyFrom(session.ActiveCamera);cam.enabled=false;cam.fieldOfView=38;cam.nearClipPlane=.025f;cam.farClipPlane=1500;cam.rect=new Rect(0,0,1,1);cam.stereoTargetEye=StereoTargetEyeMask.None;
                session.Player.gameObject.SetActive(false);
                rt=new RenderTexture(1600,1200,24);rt.Create();cam.targetTexture=rt;pixels=new Texture2D(1600,1200,TextureFormat.RGB24,false);
                var manifest=new Manifest{captured=DateTime.Now.ToString("O")};
                foreach(EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind))){
                    if(kind<EquipmentKind.Blaster||kind==EquipmentKind.PoweredArmor||kind==EquipmentKind.FlyingHand)continue;
                    bool two=kind==EquipmentKind.DualRifle||kind==EquipmentKind.SheriffHammer||kind==EquipmentKind.StockPistol||kind==EquipmentKind.Chainsaw||kind==EquipmentKind.LaserAxe;
                    for(int mode=0;mode<(two?2:1);mode++){
                        foreach(Transform child in weaponHost)Object.Destroy(child.gameObject);yield return null;
                        var visual=new CandyWeaponVisual(weaponHost,rig.style.material);var state=new EquipmentState{kind=kind,drawn=true,shotgun=mode==1};
                        var frame=new CandyAvatarFrame{feet=feet,head=feet+Vector3.up*1.6f,headRotation=Quaternion.identity,scale=1};
                        frame.right=new CandyHandPose{position=feet+new Vector3(.28f,1.12f,.38f),rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(false,1),skeletonTracked=true};
                        frame.left=new CandyHandPose{position=feet+new Vector3(-.28f,1.12f,.38f),rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(true,1),skeletonTracked=true};
                        if(EquipmentSpecifications.Get(kind).backHolster)frame.left.position=feet+new Vector3(-.035f,1.14f,.72f);
                        rig.ApplyFrame(frame);visual.Present(state,frame,true);yield return null;
                        string modeName=two?(mode==0?"Primary":"Alternate"):"Default";
                        var bounds=BoundsOf(weaponHost.gameObject);
                        rig.gameObject.SetActive(false);
                        var angles=new[]{new Vector3(1,.48f,1.15f),new Vector3(-1,.25f,.05f),new Vector3(.8f,.55f,-1)};
                        for(int i=0;i<3;i++){Take(cam,rt,pixels,bounds,angles[i],kind,modeName,"Weapon "+(i+1),manifest);yield return null;}
                        rig.gameObject.SetActive(true);rig.ApplyFrame(frame);
                        bounds=BoundsOf(rig.gameObject);bounds.Encapsulate(BoundsOf(weaponHost.gameObject));
                        for(int i=0;i<3;i++){Take(cam,rt,pixels,bounds,angles[i],kind,modeName,"Held "+(i+1),manifest);yield return null;}
                        state.drawn=false;visual.Present(state,frame,true);yield return null;
                        bounds=BoundsOf(rig.gameObject);bounds.Encapsulate(BoundsOf(weaponHost.gameObject));
                        Take(cam,rt,pixels,bounds,new Vector3(1,.35f,-1.3f),kind,modeName,"Holstered",manifest);yield return null;
                        File.WriteAllText(Path.Combine(Output,"manifest.json"),JsonUtility.ToJson(manifest,true));File.AppendAllText(Report,kind+" "+modeName+" captured\n");
                    }
                }
                File.AppendAllText(Report,"COMPLETE images="+manifest.shots.Count+"\n");
            }finally{Application.runInBackground=background;AudioListener.volume=volume;if(fixture)Object.Destroy(fixture);if(rt){rt.Release();Object.Destroy(rt);}if(pixels)Object.Destroy(pixels);}
        }
        static Bounds BoundsOf(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();if(rs.Length==0)throw new Exception("No visible model: "+go.name);
            Bounds total=default;bool first=true;
            foreach(var r in rs){var b=r.bounds;if(r is SkinnedMeshRenderer skin){var mesh=new Mesh();skin.BakeMesh(mesh);var vertices=mesh.vertices;if(vertices.Length>0){b=new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);foreach(var v in vertices)b.Encapsulate(skin.transform.TransformPoint(v));}Object.Destroy(mesh);}if(first){total=b;first=false;}else total.Encapsulate(b);}return total;
        }
        static void Take(Camera cam,RenderTexture rt,Texture2D pixels,Bounds bounds,Vector3 direction,EquipmentKind kind,string mode,string view,Manifest manifest)
        {
            float radius=bounds.extents.magnitude;float distance=radius/Mathf.Sin(cam.fieldOfView*Mathf.Deg2Rad*.5f)*1.12f;
            cam.transform.position=bounds.center+direction.normalized*distance;cam.transform.LookAt(bounds.center);
            var old=RenderTexture.active;try{cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();string file=kind+"-"+mode+"-"+view.Replace(' ','-')+".jpg";File.WriteAllBytes(Path.Combine(Output,file),pixels.EncodeToJPG(95));manifest.shots.Add(new Shot{weapon=kind.ToString(),mode=mode,view=view,file=file});}finally{RenderTexture.active=old;}
        }
    }
}
