using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastleBots;

namespace DeadlyJenga.CastlePrototype.Editor
{
    public static class BotHazardRiskValidation
    {
        sealed class State:IDisasterState {public DisasterFrame frame;public int calls;public DisasterFrame Capture(){calls++;return frame;}}
        sealed class WaveStandIn:IWaveState {public WaveFrame frame;public WaveFrame Wave=>frame;}
        public static void RunBatch()
        {
            var report=new StringBuilder();int failed=0;var old=Services.World;var host=new GameObject("Temporary Core hazard standins");
            var state=new State();var wave=new WaveStandIn();
            void Check(bool pass,string text){report.AppendLine((pass?"PASS ":"FAIL ")+text);if(!pass)failed++;}
            try
            {
                var world=host.AddComponent<FakeWorldQuery>();Services.World=world;world.playableBounds=new Bounds(Vector3.zero,Vector3.one*100);
                var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.transform.SetParent(host.transform);block.transform.position=new Vector3(10,2,10);block.AddComponent<Rigidbody>().isKinematic=true;world.blocks.Add(block.GetComponent<Rigidbody>());Physics.SyncTransforms();
                DisasterStates.Active.Add(state);Tracked.Waves.Add(wave);
                state.frame=new DisasterFrame{kind="Fire",blocks=new[]{0}};BotHazardRisk.Reset();
                Check(BotHazardRisk.Cost(block.transform.position)>0&&BotHazardRisk.Cost(new Vector3(-20,2,-20))==0,"Burning block cells penalize nearby footing, not distant safe platforms");
                int calls=state.calls;for(int i=0;i<1000;i++)BotHazardRisk.Cost(Vector3.zero);Check(state.calls==calls,"1000 route queries reuse shared half-second capture cache");
                state.frame=new DisasterFrame{kind="MeteorShower",points=new[]{new DisasterPoint{position=new Vector3(0,20,0),velocity=Vector3.down*20,size=2}}};BotHazardRisk.Reset();
                Check(BotHazardRisk.Cost(new Vector3(0,10,0))>0&&BotHazardRisk.Cost(new Vector3(20,10,0))==0,"Meteor risk predicts incoming trajectory without map-wide penalties");
                state.frame=new DisasterFrame{kind="Tornado",center=Vector3.zero,values=new[]{20f,10f,1f,12f}};BotHazardRisk.Reset();
                Check(BotHazardRisk.Cost(new Vector3(2,3,2))>BotHazardRisk.Cost(new Vector3(25,3,25)),"Tornado risk declines beyond actual pull reach");
                state.frame=null;wave.frame=new WaveFrame{active=true,center=Vector3.zero,direction=Vector3.forward,width=10,depth=2,height=5,speed=10,waterHeight=0};BotHazardRisk.Reset();
                Check(BotHazardRisk.Cost(new Vector3(0,2,4))>0&&BotHazardRisk.Cost(new Vector3(0,8,4))==0&&BotHazardRisk.Cost(new Vector3(15,2,4))==0,"Wave risk favors lateral escape and high ground above the crest");
            }
            catch(Exception error){report.AppendLine("FAIL "+error);failed++;}
            finally{DisasterStates.Active.Remove(state);Tracked.Waves.Remove(wave);UnityEngine.Object.DestroyImmediate(host);Services.World=old;BotHazardRisk.Reset();}
            Directory.CreateDirectory("Logs/Validation");report.AppendLine("Failures="+failed+"; Core standins only, actual shelter execution is a separate runtime check.");File.WriteAllText("Logs/Validation/BotHazardRisk.txt",report.ToString());
            if(Application.isBatchMode)EditorApplication.Exit(failed==0?0:1);
        }
    }
}


