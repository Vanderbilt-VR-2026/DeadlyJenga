using System;
using System.Linq;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class ShrineDragonChecks
    {
        const string Pending="ShrineDragonChecks.Pending";
        static double deadline;
        static int phase;
        static bool breathed, steamed, burned, rose;
        static float water;
        static IDisasterState source;

        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start in edit mode.");
            SessionState.SetBool(Pending,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SelectMap()
        {
            if(!SessionState.GetBool(Pending,false))return;
            MatchChoices.MapId="mode-ninja-temple"; MatchChoices.ModeId="demo"; MatchChoices.BotTarget=0; MatchChoices.HandCount=0;
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged+=s=>{
                if(s!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false))return;
                phase=0;deadline=EditorApplication.timeSinceStartup+300;EditorApplication.update+=Tick;
            };
        }
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        static void Tick()
        {
            try{
                Require(EditorApplication.timeSinceStartup<deadline,"Timed out in dragon phase "+phase);
                var session=Services.Session as CastlePlaySession;
                if(!session || Services.World==null || Time.frameCount<25)return;
                if(phase==0 || phase==2){
                    bool bronze=phase==0;
                    string label=bronze?"Dragon Attack":"Flying Dragon";
                    var prefab=DemoDisasters.Prefabs.Single(p=>p.name==label);
                    var schema=DemoDisasters.Parameters(prefab);
                    var values=schema.Select(v=>v.initial).ToArray();values[0]=1;
                    Require(DemoDisasters.ExecuteForMode(session,label,values),"Failed to start "+label);
                    source=DisasterStates.Active.Single(s=>s.Capture()?.kind==(bronze?"DragonAttack":"FlyingDragon"));
                    water=Services.World.WaterHeight(source.Capture().points[0].position);
                    Require(source.Capture().points[0].position.y<water,"Expected underwater emergence");
                    breathed=steamed=burned=rose=false;phase++;return;
                }
                var frame=source.Capture();
                burned|=DisasterStates.Active.Any(s=>s.Capture() is DisasterFrame f && f.kind=="Fire" && f.burns!=null && f.burns.Length>0);
                if(frame!=null){
                    rose|=frame.points[0].position.y>water;
                    steamed|=frame.points[1].size>.1f;
                    if(frame.points[1].id!=0){
                        var root=GameObject.Find(frame.kind+" visual");
                        Require(root && root.GetComponentInChildren<SkinnedMeshRenderer>(),"Missing dragon skin");
                        var anim=root.GetComponentInChildren<Animation>();
                        Require(anim && anim.GetClipCount()>0,"Missing baked animation");
                        Require(root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials.Any(m=>m && m.IsKeywordEnabled("_EMISSION")),"Missing eye emission");
                        breathed|=(frame.points[1].position-frame.points[1].velocity).sqrMagnitude>1;
                    }
                    return;
                }
                Require(rose&&steamed&&breathed,"Missing emergence, steam or breath in phase "+phase);
                Require(burned==(phase==3),"Wrong spreading fire behavior in phase "+phase);
                if(phase==1){phase=2;return;}
                Debug.Log("PASS Shrine dragons: both dev encounters, emergence, animation, eye emission, steam, breath, distinct block ignition and cleanup.");
                Finish(0);
            }catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void Finish(int code){
            SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);
        }
    }
}
