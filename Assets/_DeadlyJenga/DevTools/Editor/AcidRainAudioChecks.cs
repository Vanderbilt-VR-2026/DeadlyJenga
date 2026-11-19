using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DeadlyJenga.CandyAudio;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class AcidRainAudioChecks
    {
        const string Key="AcidRainAudioChecks";
        static string Report=>ProjectPath.Report("AcidRainAudio.txt");
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] sealed class Packet { public DisasterFrame[] frames; }

        static AcidRainAudioChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                    new GameObject("Acid Rain audio test").AddComponent<AcidRainAudioDriver>().StartCoroutine(Guard());
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key,false);
                    EditorApplication.Exit(SessionState.GetBool(Key+"Passed",false)?0:1);
                }
            };
        }

        public static void RunBatch()
        {
            File.WriteAllText(Report,"Actual Unity audio requests on a local authority fixture and a serialized guest receive fixture. No independent peer or headset.\n");
            SessionState.SetBool(Key+"Passed",false);
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static IEnumerator Guard()
        {
            using(var check=Checks() as IDisposable)
            {
                var iterator=(IEnumerator)check;
                while(true)
                {
                    bool more;
                    try{more=iterator.MoveNext();}
                    catch(Exception error){File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                    if(!more){SessionState.SetBool(Key+"Passed",true);File.AppendAllText(Report,"COMPLETE\n");break;}
                    yield return iterator.Current;
                }
            }
            EditorApplication.ExitPlaymode();
        }

        static void Require(bool result,string label)
        {
            if(!result)throw new InvalidOperationException(label);
            File.AppendAllText(Report,"PASS "+label+"\n");
        }

        static float Target(CandyAudioEngine engine,CandySound sound)
        {
            var voices=(IEnumerable)typeof(CandyAudioEngine).GetField("voices",PrivateInstance).GetValue(engine);
            foreach(var voice in voices)
            {
                var type=voice.GetType();
                if(type.GetField("cue").GetValue(voice) is CandyAudioCue cue&&cue.sound==sound)
                    return (float)type.GetField("target").GetValue(voice);
            }
            return 0;
        }

        static IEnumerator Checks()
        {
            var until=Time.realtimeSinceStartup+30;
            while((!(Services.Session is CastlePlaySession ready)||ready.Player==null||!CandyAudioEngine.Instance||!CandyAudioEngine.Instance.Listener)&&Time.realtimeSinceStartup<until)
                yield return null;
            var session=Services.Session as CastlePlaySession;
            var engine=CandyAudioEngine.Instance;
            Require(session&&session.Player!=null&&engine&&engine.Listener&&session.HasWorldAuthority,"Game and local authority audio are ready");
            session.SetMenu(false);
            session.SurvivorBots.AutoFill=false;
            session.SurvivorBots.Clear();
            var player=session.Player;
            var oldWorld=Services.World;
            var oldPosition=player.transform.position;
            var oldRotation=player.transform.rotation;
            var root=new GameObject("Acid audio shelter fixture");
            root.transform.position=new Vector3(10000,0,10000);
            var map=root.AddComponent<FakeWorldQuery>();
            map.playableBounds=new Bounds(Vector3.up*5,new Vector3(40,20,40));
            map.spawnPoint=root.transform.position+Vector3.up*.1f;
            map.waterHeight=-100;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(root.transform,false);
            floor.transform.localPosition=Vector3.down;
            floor.transform.localScale=new Vector3(40,2,40);
            floor.layer=Layers.Surface;
            var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.SetParent(root.transform,false);
            roof.transform.localPosition=new Vector3(7,5,0);
            roof.transform.localScale=new Vector3(6,1,6);
            roof.layer=Layers.Block;
            var outside=root.transform.position+new Vector3(-7,.1f,0);
            var covered=root.transform.position+new Vector3(7,.1f,0);
            GameObject guest=null;
            IHazard rain=null;
            try
            {
                player.Place(outside,Quaternion.identity);
                Physics.SyncTransforms();
                var prefab=DemoDisasters.Prefabs.First(p=>p.name=="AcidRain");
                var tuning=prefab.GetComponent<IDemoHazard>();
                var values=tuning.Parameters.Select(p=>p.id=="warning"?0:p.initial).ToArray();
                Require(DemoDisasters.ExecuteForMode(session,prefab.name,values),"Authority starts actual Acid Rain");
                var source=DisasterStates.Active.First(s=>s.Capture()?.kind=="AcidRain");
                rain=(IHazard)source;
                yield return new WaitForSecondsRealtime(.65f);
                Require(!map.IsSheltered(engine.Listener.position)&&Target(engine,CandySound.AcidSizzle)>.5f&&Target(engine,CandySound.AcidRoofRain)<.15f,"Authority outdoor rain mix");
                player.Place(covered,Quaternion.identity);
                Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(.75f);
                Require(map.IsSheltered(engine.Listener.position)&&Target(engine,CandySound.AcidRoofRain)>.35f&&Target(engine,CandySound.AcidSizzle)<.2f,"Authority covered roof mix");
                var packet=CastleFusionWire.Compress(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Packet{frames=new[]{source.Capture()}})));
                rain.End();
                rain=null;
                guest=new GameObject("Guest disaster receive fixture");
                var transport=guest.AddComponent<CastleFusionSession>();
                var receive=typeof(CastleFusionSession).GetMethod("ReceiveDisasters",PrivateInstance);
                receive.Invoke(transport,new object[]{packet});
                yield return new WaitForSecondsRealtime(.65f);
                Require(Target(engine,CandySound.AcidRoofRain)>.35f&&Target(engine,CandySound.AcidSizzle)<.2f,"Received guest snapshot uses covered roof mix");
                player.Place(outside,Quaternion.identity);
                Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(.75f);
                Require(Target(engine,CandySound.AcidSizzle)>.5f&&Target(engine,CandySound.AcidRoofRain)<.15f,"Received guest snapshot uses outdoor rain mix");
                typeof(CastleFusionSession).GetMethod("ResetDisasters",PrivateInstance).Invoke(transport,null);
            }
            finally
            {
                rain?.End();
                if(guest)Object.Destroy(guest);
                player.Place(oldPosition,oldRotation);
                Services.World=oldWorld;
                Object.Destroy(root);
            }
        }
    }

    public sealed class AcidRainAudioDriver : MonoBehaviour { }
}
