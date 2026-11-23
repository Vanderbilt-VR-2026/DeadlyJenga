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
            var eye=new Vector3(-6,22,-87);var target=new Vector3(0,14,4);
            Capture(eye,target,29,"Needlefrost-Overview.png");
            Capture(new Vector3(-16,4.35f,-20),new Vector3(-10,8,3),67,"Needlefrost-Coast.png");
            Capture(new Vector3(12.8f,4.35f,-15),new Vector3(10,5,-9),70,"Needlefrost-Camp.png");
            Capture(new Vector3(.3f,7.55f,-3),new Vector3(0,12,16),67,"Needlefrost-Bridge.png");
            Capture(new Vector3(15,17.4f,12),new Vector3(-2,15,16),75,"Needlefrost-Tower.png");
            float drift=placements.Select((p,i)=>Vector3.Distance(map.Bodies[i].position,map.transform.TransformPoint(p.Position))).Max();
            foreach(var index in Enumerable.Range(0,placements.Count).OrderByDescending(i=>Vector3.Distance(map.Bodies[i].position,map.transform.TransformPoint(placements[i].Position))).Take(6))
                File.AppendAllText(Report,$"Settle #{index} course {placements[index].Course} home {placements[index].Position} actual {map.Bodies[index].position}\n");
            File.AppendAllText(Report,"Initial maximum drift "+drift+" m; bodies "+placements.Count+"\n");
            bool routes=true;
            yield return WalkKeep(motor,new Vector3(-8,3.1f,-16),new[]{new Vector3(-8,6,-5),new Vector3(-6,6,-3),
                new Vector3(0,6.15f,-3),new Vector3(7,6,-3)},result=>routes &= result,"intact coast stair and low bridge");
            yield return WalkKeep(motor,new Vector3(16,3.1f,-8),new[]{new Vector3(16,3,-6.45f)},result=>routes &= result,"camp to grotto interior");
            yield return WalkKeep(motor,new Vector3(20.4f,9.1f,9.38f),new[]{new Vector3(20.4f,15.9f,16.26f),
                new Vector3(19.1f,16,16.1f),new Vector3(16,16,14),new Vector3(13,16,13),
                new Vector3(10,16,13),new Vector3(5,16.15f,13)},result=>routes &= result,"right tower to upper crossing");
            var cornice=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x+21.28f)<.02f)];
            float carried=0,drop=0;
            yield return DeadlyJenga.HandValidation.FlyingHandFeelValidation.PullMapMember(cornice,(travel,fallDepth)=>{carried=travel;drop=fallDepth;});
            File.AppendAllText(Report,$"Production hand cornice pull {carried:F2} m outward while closed, drop {drop:F2} m after opening\n");
            var pier=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x+18)<.01f && p.Position.z==-19 && p.Position.y<1)];
            var deck=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x+18)<.01f && p.Position.z==-19 && p.Position.y>1)];
            float deckY=deck.position.y;
            // A horizontal load fractures the socket, then gravity carries the now unsupported slab.
            pier.GetComponent<IPushable>().Push(Vector3.left*44,null);
            deck.WakeUp();
            for(int i=0;i<5;i++)
            {
                yield return new WaitForSeconds(.15f);
                Capture(eye,target,29,"Needlefrost-Fracture-"+i+".png");
            }
            float fall=deckY-deck.position.y;
            var bridge=map.Bodies[placements.FindIndex(p=>p.Position.x==0 && p.Position.z==-3)];
            bridge.GetComponent<IPushable>().Push(Vector3.forward*12,null);
            yield return new WaitForSeconds(1);
            File.AppendAllText(Report,$"Displaced low bridge now at {bridge.position}\n");
            var driver=new MarinaWalkDriver();
            motor.externallyDriven=true;motor.InputDriver=driver;motor.InputSuspended=false;
            motor.Place(new Vector3(-8,3.12f,-16)+captureOrigin,Quaternion.identity);
            driver.target=new Vector3(-8,6,-5)+captureOrigin;
            float end=Time.time+12;
            while(Time.time<end && (driver.target-motor.transform.position).sqrMagnitude>1)yield return null;
            float walk=Vector3.Distance(driver.target,motor.transform.position);
            File.AppendAllText(Report,$"Motor ended at {motor.transform.position}, target {driver.target}, status {motor.ActionStatus}\n");
            motor.InputDriver=null;motor.externallyDriven=false;
            var upper=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x-8.55f)<.01f && p.Size.x>8)];
            var oldUpper=upper.position;upper.GetComponent<IPushable>().Push(Vector3.forward*16,null);
            yield return new WaitForSeconds(.8f);
            float upperTravel=Vector3.Distance(upper.position,oldUpper);
            yield return WalkKeep(motor,new Vector3(16,3.1f,-8.8f),new[]{new Vector3(24.3f,6,-8.8f),new Vector3(24.3f,6,-5)},result=>routes &= result,"east ascent after upper crossing loss");
            bool eliminated=false;
            void Observe(GameObject who,GameObject source){if(who==motor.gameObject)eliminated=true;}
            GameEvents.Eliminated+=Observe;
            try
            {
                motor.Place(captureOrigin+new Vector3(0,1,-16),Quaternion.identity);
                float deadline=Time.time+3;
                while(!eliminated && Time.time<deadline)yield return null;
            }
            finally{GameEvents.Eliminated-=Observe;}
            map.Rebuild();yield return new WaitForSeconds(1);
            motor.Place(map.SafeSpawnPoint(),Quaternion.identity);
            File.AppendAllText(Report,$"Ocean elimination {eliminated}; upper bridge moved {upperTravel:F2} m; route checks {routes}\n");
            Check(placements.Count==map.Bodies.Count,"Reset restores physical body count");
            bool bot=session.SurvivorBots.SpawnOne();
            File.AppendAllText(Report,$"Coast fall {fall:F2} m; ascent remaining {walk:F2} m; bot spawn {bot}\n");
            Check(drift<.6f && fall>.45f && walk<1.2f && bot && routes && eliminated && upperTravel>1 && carried>.5f && drop>.3f,"Intact settling, coast collapse, alternate ascent and bot smoke");
        }

        static IEnumerator WalkKeep(CastleSurvivor motor,Vector3 start, Vector3[] goals,System.Action<bool> result,string label)
        {
            var driver=new MarinaWalkDriver();
            motor.externallyDriven=true;motor.InputDriver=driver;motor.InputSuspended=false;
            motor.Place(captureOrigin+start,Quaternion.identity);
            try
            {
                foreach(var local in goals)
                {
                    driver.target=captureOrigin+local;float deadline=Time.time+6;
                    while(Time.time<deadline && motor.Alive &&
                        Vector3.Distance(motor.transform.position,driver.target)>.49f)yield return null;
                    float remaining=Vector3.Distance(motor.transform.position,driver.target);
                    File.AppendAllText(Report,$"WALK {label}: goal {local}, feet {motor.transform.position-captureOrigin}, remaining {remaining:F2}, grounded {motor.Grounded}, alive {motor.Alive}\n");
                    if(!motor.Alive || remaining>.65f){result(false);yield break;}
                }
                result(true);
            }
            finally{motor.InputDriver=null;motor.externallyDriven=false;}
        }
    }
}
