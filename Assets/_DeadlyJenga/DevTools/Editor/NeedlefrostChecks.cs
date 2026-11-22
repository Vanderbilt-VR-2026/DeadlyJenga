using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class MayhemMarinaChecks
    {
        const string KeepKey="NeedlefrostPhysics";
        public static void RunNeedlefrost()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report,"Needlefrost Keep / Game.unity\n");
            SessionState.SetBool(KeepKey,true);SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static IEnumerator RunKeepScene()
        {
            yield return RunSoloScene();
            MatchSetup.MapId="mode-needlefrost-keep";MatchSetup.ModeId="demo";
            Check(MatchSetup.SelectedMap?.title=="Needlefrost Keep","Needlefrost registered in live catalog");
            MatchSetup.ApplyMap();
            var map=Services.World as CastleGenerator;
            Check(map && map.settings.layoutPreset=="needlefrost-keep" && !map.foundation,"Physical map loaded without foundation");
            captureOrigin=map.transform.position;
            var placements=CastleLayout.Build(map.settings);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            var motor=session.Survivor();motor.Place(map.SafeSpawnPoint(),Quaternion.identity);
            yield return new WaitForSeconds(3);
            var eye=new Vector3(2,19,-61);var target=new Vector3(0,14,3);
            Capture(eye,target,32,"Needlefrost-Overview.png");
            Capture(new Vector3(-16,4.35f,-20),new Vector3(-10,8,3),67,"Needlefrost-Coast.png");
            Capture(new Vector3(12.8f,4.35f,-15),new Vector3(10,5,-9),70,"Needlefrost-Camp.png");
            Capture(new Vector3(.3f,7.55f,-3),new Vector3(0,12,16),67,"Needlefrost-Bridge.png");
            Capture(new Vector3(15,17.4f,12),new Vector3(-2,15,16),75,"Needlefrost-Tower.png");
            float drift=placements.Select((p,i)=>Vector3.Distance(map.Bodies[i].position,map.transform.TransformPoint(p.Position))).Max();
            File.AppendAllText(Report,"Initial maximum drift "+drift+" m; bodies "+placements.Count+"\n");
            var pier=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x+18)<.01f && p.Position.z==-19 && p.Position.y<1)];
            var deck=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x+18)<.01f && p.Position.z==-19 && p.Position.y>1)];
            float deckY=deck.position.y;
            // A horizontal load fractures the socket, then gravity carries the now unsupported slab.
            pier.GetComponent<IPushable>().Push(Vector3.left*44,null);
            deck.WakeUp();
            for(int i=0;i<5;i++)
            {
                yield return new WaitForSeconds(.15f);
                Capture(eye,target,32,"Needlefrost-Fracture-"+i+".png");
            }
            float fall=deckY-deck.position.y;
            var bridge=map.Bodies[placements.FindIndex(p=>p.Position.x==0 && p.Position.z==-3)];
            bridge.GetComponent<IPushable>().Push(Vector3.forward*12,null);
            yield return new WaitForSeconds(1);
            var driver=new MarinaWalkDriver();
            motor.externallyDriven=true;motor.InputDriver=driver;motor.InputSuspended=false;
            motor.Place(new Vector3(-8,3.12f,-16)+captureOrigin,Quaternion.identity);
            driver.target=new Vector3(-8,6,-5)+captureOrigin;
            float end=Time.time+12;
            while(Time.time<end && (driver.target-motor.transform.position).sqrMagnitude>1)yield return null;
            float walk=Vector3.Distance(driver.target,motor.transform.position);
            motor.InputDriver=null;motor.externallyDriven=false;
            map.Rebuild();yield return new WaitForSeconds(1);
            Check(placements.Count==map.Bodies.Count,"Reset restores physical body count");
            bool bot=session.SurvivorBots.SpawnOne();
            File.AppendAllText(Report,$"Coast fall {fall:F2} m; ascent remaining {walk:F2} m; bot spawn {bot}\n");
            Check(drift<.6f && fall>.45f && walk<1.2f && bot,"Intact settling, coast collapse, alternate ascent and bot smoke");
        }
    }
}
