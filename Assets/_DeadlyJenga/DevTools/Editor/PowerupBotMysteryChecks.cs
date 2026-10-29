using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.Modes;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class PowerupBotMysteryChecks
    {
        public static IEnumerator Run(CastlePlaySession session,Action<bool,string> check)
        {
            var runtime=session.Modes;BotObjectives.Current=runtime;
            for(int attempt=0;session.SurvivorBots.Count<4&&attempt<4;attempt++)
                if(!session.SurvivorBots.SpawnOne())break;
            check(session.SurvivorBots.Count>=4,"Mystery fixture can spawn four participants: "+session.SurvivorBots.Status);
            yield return DrawnWeaponSight(session,check);
            foreach(var go in session.SurvivorBots.Members)go.GetComponent<CastleSurvivor>().InputSuspended=true;
            var rules=DeadlyJenga.Modes.ModeSimulation.Defaults(GameModeKind.MurderMystery);rules.countdownSeconds=0;rules.mystery.attackGraceSeconds=0;runtime.Configure(rules);runtime.StartRound();
            yield return new WaitForSeconds(.4f);runtime.SetDemoRole(MysteryRole.Murderer);
            var people=session.SurvivorBots.Members.Select(go=>go.GetComponent<CastleSurvivor>()).ToArray();
            CastleSurvivor sheriff=null,victim=null,witness=null;
            foreach(var motor in people)
            {
                motor.InputSuspended=true;motor.Place(new Vector3(180,10.05f,180),Quaternion.identity);
                var secret=JsonUtility.FromJson<MysteryPrivateFrame>(runtime.CaptureSecret(motor.ActorId));
                if(secret.role==MysteryRole.Sheriff)sheriff=motor;
                else if(secret.role==MysteryRole.Innocent){if(victim==null)victim=motor;else if(witness==null)witness=motor;}
            }
            check(sheriff&&victim&&witness,"Production Mystery has sheriff and innocent witnesses");
            var murderer=session.Survivor();murderer.Place(new Vector3(150,10.05f,150),Quaternion.identity);murderer.InputSuspended=true;
            victim.Place(new Vector3(150.9f,10.05f,150.6f),Quaternion.identity);victim.InputSuspended=true;victim.GetComponent<CastleBotBrain>().enabled=false;victim.InputDriver=null;
            sheriff.Place(new Vector3(150,10.05f,162),Quaternion.LookRotation(Vector3.back));sheriff.GetComponent<CastleBotBrain>().enabled=true;sheriff.InputDriver=sheriff.GetComponent<CastleBotBrain>();
            witness.Place(new Vector3(157,10.05f,155),Quaternion.LookRotation(new Vector3(-7,0,-5)));witness.GetComponent<CastleBotBrain>().enabled=true;witness.InputDriver=witness.GetComponent<CastleBotBrain>();Physics.SyncTransforms();
            yield return new WaitForSeconds(.6f);
            var goals=new List<BotObjective>();runtime.Collect(sheriff.gameObject,goals);
            check(goals.All(g=>g.kind!=BotObjectiveKind.Attack||g.subject!=murderer.gameObject),"Sheriff does not acquire an attack on an unrevealed murderer by reading the secret role");
            check(!runtime.EquipmentFor(sheriff.ActorId).drawn,"Unthreatened sheriff keeps the role weapon concealed");
            int accepted=sheriff.GetComponent<CastleBotBrain>().WeaponShotsAccepted;
            runtime.SetSpecialAttack(true);sheriff.InputSuspended=false;witness.InputSuspended=false;
            var delta=victim.transform.position+Vector3.up*.6f-murderer.ViewPose.position;
            check(runtime.RequestPunch(murderer.gameObject,murderer.ViewPose.position,delta.normalized,3),"Actual murderer special attack triggers a witnessable strike");
            float deadline=Time.realtimeSinceStartup+12;bool escaped=false;
            while(murderer.Alive&&Time.realtimeSinceStartup<deadline)
            {
                escaped|=runtime.ObjectiveDirector.History.Any(h=>h.actor==witness.ActorId&&h.objective.StartsWith("escape-witnessed/",StringComparison.Ordinal));yield return null;
            }
            check(sheriff.GetComponent<CastleBotBrain>().WeaponShotsAccepted>accepted,"A sheriff bot draws and fires its revolver after witnessing the actual attack");
            check(!murderer.Alive,"Production sheriff weapon eliminates the witnessed murderer");
            check(escaped,"A witnessing innocent receives an escape route instead of pursuing an unknown role");
        }
        static IEnumerator DrawnWeaponSight(CastlePlaySession session,Action<bool,string> check)
        {
            var runtime=session.Modes;BotObjectives.Current=runtime;
            var rules=DeadlyJenga.Modes.ModeSimulation.Defaults(GameModeKind.MurderMystery);rules.countdownSeconds=0;rules.mystery.attackGraceSeconds=0;rules.island=IslandRule.Walkable;
            runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.4f);runtime.SetDemoRole(MysteryRole.Murderer);yield return new WaitForSeconds(.3f);
            CastleSurvivor sheriff=null,witness=null;
            foreach(var go in session.SurvivorBots.Members)
            {
                var motor=go.GetComponent<CastleSurvivor>();motor.InputSuspended=true;motor.Place(new Vector3(180,10.05f,180),Quaternion.identity);
                var role=JsonUtility.FromJson<MysteryPrivateFrame>(runtime.CaptureSecret(motor.ActorId)).role;
                if(role==MysteryRole.Sheriff)sheriff=motor;else if(role==MysteryRole.Innocent&&witness==null)witness=motor;
            }
            check(sheriff&&witness,"drawn-weapon fixture has a sheriff and innocent");
            var murderer=session.Survivor();murderer.InputSuspended=true;murderer.Place(new Vector3(150,10.05f,150),Quaternion.identity);
            sheriff.Place(new Vector3(150,10.05f,162),Quaternion.identity);
            runtime.SetSpecialAttack(true);yield return new WaitForSeconds(.9f);
            var goals=new List<BotObjective>();runtime.Collect(sheriff.gameObject,goals);
            check(goals.All(g=>g.kind!=BotObjectiveKind.Attack||g.subject!=murderer.gameObject),"drawing behind the sheriff does not reveal a concealed role");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Weapon sight cover";wall.layer=Layers.Surface;wall.transform.position=new Vector3(150,12,156);wall.transform.localScale=new Vector3(8,6,1);
            try
            {
                sheriff.Place(new Vector3(150,10.05f,162),Quaternion.LookRotation(Vector3.back));Physics.SyncTransforms();yield return new WaitForSeconds(.9f);
                goals.Clear();runtime.Collect(sheriff.gameObject,goals);
                check(goals.All(g=>g.kind!=BotObjectiveKind.Attack||g.subject!=murderer.gameObject),"drawn blade behind solid cover is not recognized");
                wall.SetActive(false);runtime.SetSpecialAttack(false);yield return new WaitForSeconds(.8f);
                goals.Clear();runtime.Collect(sheriff.gameObject,goals);
                check(goals.All(g=>g.kind!=BotObjectiveKind.Attack||g.subject!=murderer.gameObject),"unobstructed concealed murderer remains unknown");
                witness.Place(new Vector3(157,10.05f,155),Quaternion.LookRotation(new Vector3(-7,0,7)));
                for(int i=0;i<20;i++){runtime.SetBotWeapon(sheriff.gameObject,true,false);yield return new WaitForSeconds(.04f);}
                runtime.SetBotWeapon(sheriff.gameObject,true,false);
                check(runtime.EquipmentFor(sheriff.ActorId).drawn&&runtime.EquipmentFor(sheriff.ActorId).kind==EquipmentKind.SheriffHammer,"legitimate sheriff weapon is actually drawn in the sight fixture");
                goals.Clear();runtime.Collect(witness.gameObject,goals);
                check(goals.All(g=>!g.id.StartsWith("escape-witnessed/",StringComparison.Ordinal)),"innocent does not flee merely because the sheriff draws a blue revolver");
                runtime.SetBotWeapon(sheriff.gameObject,false,false);
                witness.Place(new Vector3(157,10.05f,155),Quaternion.LookRotation(new Vector3(-7,0,-5)));
                runtime.SetSpecialAttack(true);yield return new WaitForSeconds(1);
                goals.Clear();runtime.Collect(sheriff.gameObject,goals);
                check(goals.Any(g=>g.kind==BotObjectiveKind.Attack&&g.subject==murderer.gameObject),"sheriff confronts a visible drawn blade before any attack");
                goals.Clear();runtime.Collect(witness.gameObject,goals);
                check(goals.Any(g=>g.id.StartsWith("escape-witnessed/",StringComparison.Ordinal)),"innocent receives an escape objective from seeing the drawn blade alone");
                check(runtime.Clues.strikes.Length==0,"weapon recognition occurs before any special strike");
                var witnessBrain=witness.GetComponent<CastleBotBrain>();witnessBrain.enabled=true;witness.InputDriver=witnessBrain;
                float distance=Vector3.Distance(witness.transform.position,murderer.transform.position);witness.InputSuspended=false;
                float deadline=Time.realtimeSinceStartup+5;
                while(Vector3.Distance(witness.transform.position,murderer.transform.position)<distance+.5f&&Time.realtimeSinceStartup<deadline)yield return null;
                witness.InputSuspended=true;
                check(Vector3.Distance(witness.transform.position,murderer.transform.position)>distance+.4f,"ordinary innocent motor actually runs away from the visible weapon");
                runtime.SetSpecialAttack(false);wall.SetActive(true);murderer.Place(new Vector3(180,10.05f,180),Quaternion.identity);Physics.SyncTransforms();yield return new WaitForSeconds(.8f);
                goals.Clear();runtime.Collect(sheriff.gameObject,goals);
                check(goals.All(g=>g.kind!=BotObjectiveKind.Attack||g.subject==null),"lost sight retains no live hidden-attacker transform");
                check(ModeBotTactics.NoticeDrawnWeapon(Vector3.zero,Quaternion.identity,Vector3.forward*14)&&!ModeBotTactics.NoticeDrawnWeapon(Vector3.zero,Quaternion.identity,Vector3.forward*16),"weapon recognition respects its bounded distance");
            }
            finally{Object.Destroy(wall);}
        }
    }
}
