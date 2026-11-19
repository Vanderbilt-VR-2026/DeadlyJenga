using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Run as a Unity coroutine in the existing QA Game session.
    // It consumes an isolated test round; the QA caller resets/unloads it afterward.
    // It does not open scenes, launch editors, or capture images on its own.
    public static class ProjectileReadabilityChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static string Report=>ProjectPath.Report("ProjectileReadabilityChecks.txt");
        static void Require(bool okay,string message)
        {
            File.AppendAllText(Report,(okay?"PASS ":"FAIL ")+message+"\n");
            if(!okay)throw new InvalidOperationException(message);
        }
        static IEnumerator Until(Func<bool> ready,string label,float seconds=20)
        {
            float limit=Time.realtimeSinceStartup+seconds;
            while(!ready()&&Time.realtimeSinceStartup<limit)yield return null;
            Require(ready(),label);
        }
        static IEnumerator Wait(float seconds)
        {
            float start=Time.time,wallStart=Time.realtimeSinceStartup;
            float limit=start+seconds,deadline=wallStart+Mathf.Max(30,seconds*40);
            int frame=Time.frameCount;
            while(Time.time<limit&&Time.realtimeSinceStartup<deadline)yield return null;
            File.AppendAllText(Report,"CLOCK requested="+seconds+" simulated="+(Time.time-start)+
                " wall="+(Time.realtimeSinceStartup-wallStart)+" frames="+(Time.frameCount-frame)+
                " scale="+Time.timeScale+" captureDelta="+Time.captureDeltaTime+"\n");
            Require(Time.time>=limit,"Requested simulation time advances before checking feedback");
        }

        static IEnumerator HoldShots(CastlePlaySession session,IHeldEquipmentRuntime trigger,
            EquipmentKind kind,Pose pose,int count)
        {
            int baseline=session.Modes.Frame.equipmentSequence;
            float deadline=Time.realtimeSinceStartup+8;
            var observed=new HashSet<int>();bool accepted=false;
            try
            {
                while(observed.Count<count&&Time.realtimeSinceStartup<deadline)
                {
                    accepted|=trigger.RequestEquipmentTrigger(true,pose);
                    yield return null;
                    foreach(var action in session.Modes.Frame.equipmentActions)
                        if(action.id>baseline&&action.kind==kind&&action.action==EquipmentAction.Shot)observed.Add(action.id);
                }
            }
            finally{trigger.RequestEquipmentTrigger(false,pose);}
            Require(accepted&&observed.Count>=count,"Held trigger announces "+count+" real "+kind+" shots");
        }

        static IEnumerator ClearMarks(CandyWeaponImpacts effects)
        {
            effects.enabled=false;
            yield return null;
            effects.enabled=true;
            Require(effects.ActiveMarks==0,"Previous mode marks cleared before the next attack");
        }

        static void RequireTargetHit(EquipmentActionFrame shot,Rigidbody target,string label)
        {
            var colliders=target.GetComponentsInChildren<Collider>();
            Require(shot.endpoints!=null&&shot.endpoints.Length>0&&colliders.Length>0,label+" has endpoints and target colliders");
            float gap=shot.endpoints.Min(point=>colliders.Min(collider=>
                Vector3.Distance(collider.ClosestPoint(point),point)));
            File.AppendAllText(Report,"TARGET sequence="+shot.id+" kind="+shot.kind+" shotgun="+shot.shotgun+
                " endpoints="+shot.endpoints.Length+" targetGap="+gap+"\n");
            Require(gap<.15f,label+" reaches the controlled target");
        }

        static void RequireLiving(CastlePlaySession session,CastleSurvivor player,string label)
        {
            var participant=session.Modes.Frame.players.FirstOrDefault(p=>p.id==player.ActorId);
            File.AppendAllText(Report,"LIFE "+label+" actorAlive="+player.Alive+" health="+player.Health+
                " phase="+session.Modes.Frame.phase+" participant="+JsonUtility.ToJson(participant)+"\n");
            Require(player.Alive&&player.Health>0&&participant!=null&&participant.alive&&!participant.dying&&
                session.Modes.Frame.phase==RoundPhase.Playing,"Living participant "+label);
        }

        static IEnumerator MultiViewCheck()
        {
            var root=new GameObject("Projectile multiview eligibility fixture");
            var target=new RenderTexture(16,16,0);
            var first=new GameObject("Impact view A").AddComponent<Camera>();
            var second=new GameObject("Impact view B").AddComponent<Camera>();
            first.transform.SetParent(root.transform);second.transform.SetParent(root.transform);
            first.transform.position=new Vector3(900,80,900);second.transform.position=new Vector3(1200,80,900);
            first.targetTexture=second.targetTexture=target;
            first.cullingMask=second.cullingMask=1<<Layers.Block;
            first.GetUniversalAdditionalCameraData().allowXRRendering=false;
            second.GetUniversalAdditionalCameraData().allowXRRendering=false;
            try
            {
                yield return null;
                Require(CandyWeaponImpacts.VisibleToAnyCamera(first.transform.position+Vector3.forward*5,90),
                    "First separated Game view admits nearby feedback");
                Require(CandyWeaponImpacts.VisibleToAnyCamera(second.transform.position+Vector3.forward*5,90),
                    "Second separated Game view admits nearby feedback beyond the primary range");
                first.enabled=second.enabled=false;yield return null;
                Require(!CandyWeaponImpacts.VisibleToAnyCamera(new Vector3(1200,80,905),90),
                    "Disabled views do not retain distant feedback eligibility");
            }
            finally{first.targetTexture=second.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(root);}
        }

        public static IEnumerator Run(CastlePlaySession session)=>Run(session,false);
        public static IEnumerator RunLegacyHitscan(CastlePlaySession session)=>Run(session,true);

        static IEnumerator Run(CastlePlaySession session,bool legacyOnly)
        {
            File.WriteAllText(Report,"Actual existing Game session; controlled weapon/target fixtures. Run as native coroutine.\n");
            Require(session&&session.Modes&&Services.World!=null,"Real session, mode runtime and map are ready");
            var effects=session.GetComponent<CandyWeaponImpacts>();
            Require(effects,"Production impact observer is attached through SessionStarted");
            var shader=Resources.Load<Shader>("CandyImpactMark");
            Require(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Impact shader imports on the actual renderer");
            Require(typeof(CandyPlasmaView).GetMethod("OnGUI",Private)==null,"Legacy unconditional cyan cross is absent");
            Require(typeof(CandyWeaponReticle)!=null,"Normal weapon reticle remains available");
            yield return MultiViewCheck();

            var player=session.Survivor();var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;
            bool oldSuspended=player.InputSuspended;
            Require(player.GetComponent<IEquipmentControls>()!=null,"Actual player equipment controls are present");
            Require(session.Modes.Frame.phase==RoundPhase.Playing,"QA provided an already-playing isolated round");
            var inventory=(IEquipmentRuntime)session.Modes;
            var grant=(IDemoEquipmentControls)session.Modes;
            var held=session.Modes as IHeldEquipmentRuntime;
            Require(held!=null,"Normal held-trigger interface is available");
            var stage=new GameObject("Projectile readability fixture");
            Rigidbody target=null;
            void Damaged(GameObject who,float amount)
            {
                if(who!=player.gameObject)return;
                File.AppendAllText(Report,"PLAYER DAMAGE amount="+amount+" health="+player.Health+
                    " frame="+Time.frameCount+" position="+player.transform.position+"\n"+Environment.StackTrace+"\n");
            }
            void Eliminated(GameObject who,GameObject source)
            {
                if(who!=player.gameObject)return;
                File.AppendAllText(Report,"PLAYER ELIMINATED source="+(source?source.name:"null")+
                    " frame="+Time.frameCount+" position="+player.transform.position+"\n"+Environment.StackTrace+"\n");
            }
            GameEvents.Damaged+=Damaged;GameEvents.Eliminated+=Eliminated;
            try
            {
                session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();
                var world=Services.World;
                var killField=world.GetType().GetField("oceanKillHeight",Private);
                File.AppendAllText(Report,"WORLD type="+world.GetType().FullName+" bounds="+world.PlayableBounds+
                    " top="+world.TopHeight+" fixtureWater="+world.WaterHeight(new Vector3(180,20,174))+
                    " oceanKill="+(killField?.GetValue(world)?.ToString()??"unexposed")+"\n");
                RequireLiving(session,player,"before fixture placement");
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Projectile fixture floor";
                floor.transform.SetParent(stage.transform);floor.layer=Layers.Surface;
                floor.transform.position=new Vector3(180,19,180);floor.transform.localScale=new Vector3(18,2,24);
                var platform=floor.AddComponent<Rigidbody>();platform.isKinematic=true;
                player.Teleport(new Vector3(180,20.05f,174),Quaternion.identity);player.InputSuspended=true;
                yield return Until(()=>player.Grounded&&player.StandingBody==platform,
                    "Actual player stands on the physical fixture platform",30);
                var fixtureRules=session.Modes.RulesFor(player.gameObject);
                File.AppendAllText(Report,"FIXTURE SUPPORT body="+player.StandingBody.name+
                    " islandRule="+fixtureRules.island+" sinkGrace="+fixtureRules.sinkGraceSeconds+
                    " deathEnabled="+fixtureRules.deathEnabled+"\n");
                Require(fixtureRules.deathEnabled,"Death remains enabled while testing physical platform support");
                RequireLiving(session,player,"on physical fixture platform");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleBlock.prefab"));
                Require(prefab,"Production block prefab found by name");

                var kinds=legacyOnly?new[]{EquipmentKind.DualRifle,EquipmentKind.SheriffHammer}:new[]{
                    EquipmentKind.DualRifle,EquipmentKind.SheriffHammer,EquipmentKind.Blaster,EquipmentKind.PoweredArmor,
                    EquipmentKind.RocketLauncher,EquipmentKind.DualUzi,EquipmentKind.ChainGun,EquipmentKind.GrenadeLauncher,
                    EquipmentKind.WristLaser,EquipmentKind.NeedleDrill,EquipmentKind.StockPistol,
                    EquipmentKind.Chainsaw,EquipmentKind.LaserAxe};
                foreach(var kind in kinds)
                {
                    if(target)Object.Destroy(target.gameObject);
                    var block=Object.Instantiate(prefab,stage.transform);block.name="Moving impact target "+kind;
                    block.layer=Layers.Block;block.transform.SetPositionAndRotation(new Vector3(180,22,183),Quaternion.identity);
                    block.transform.localScale=new Vector3(7,6,1);
                    target=block.GetComponent<Rigidbody>();Require(target,"Target retains a real production Rigidbody");
                    target.isKinematic=true;
                    effects.enabled=false;yield return null;effects.enabled=true;
                    Require(grant.GiveEquipment(kind),"Normal demo grant "+kind);
                    bool alternate=kind==EquipmentKind.Chainsaw||kind==EquipmentKind.LaserAxe;
                    Require(inventory.RequestEquipment(true,false,alternate),"Normal draw/mode command "+kind);
                    yield return null;yield return null;Physics.SyncTransforms();
                    var muzzle=player.ViewPose.position+Vector3.forward*.7f;
                    var rotation=Quaternion.LookRotation(target.worldCenterOfMass-muzzle);
                    int sequence=session.Modes.Frame.equipmentSequence;
                    var firingPose=new Pose(muzzle,rotation);
                    if(kind<=EquipmentKind.PoweredArmor)
                        Require(inventory.RequestEquipmentAttack(firingPose),"Normal legacy weapon attack "+kind);
                    else yield return HoldShots(session,held,kind,firingPose,
                        kind==EquipmentKind.WristLaser||kind==EquipmentKind.ChainGun?5:1);
                    yield return Until(()=>session.Modes.Frame.equipmentSequence>sequence&&
                        session.Modes.Frame.equipmentActions.Any(a=>a.kind==kind&&a.action==EquipmentAction.Shot),
                        "Authoritative shot announced "+kind,8);
                    if(legacyOnly)RequireTargetHit(session.Modes.Frame.equipmentActions.Last(a=>
                        a.id>sequence&&a.kind==kind&&a.action==EquipmentAction.Shot),target,"Primary "+kind+" endpoint");
                    // Allow genuine projectile travel/ballistic arc, plus one impact-observer frame.
                    yield return Wait(kind==EquipmentKind.RocketLauncher?1.1f:kind==EquipmentKind.GrenadeLauncher?1.2f:.75f);
                    RequireLiving(session,player,"after primary "+kind);
                    Require(effects.ActiveMarks<=96,"Impact population remains bounded for "+kind);
                    Require(effects.ActiveMarks>0,"A real surface hit creates a temporary mark for "+kind);
                    if(kind==EquipmentKind.SheriffHammer||kind==EquipmentKind.StockPistol)
                    {
                        var beforeMode=player.GetComponent<IEquipmentControls>().Equipment;
                        var localFrame=session.Modes.Frame.players.FirstOrDefault(p=>p.id==player.ActorId);
                        File.AppendAllText(Report,"MODE ELIGIBILITY frame="+Time.frameCount+" gameTime="+Time.time+
                            " phase="+session.Modes.Frame.phase+" actor="+player.ActorId+" alive="+player.Alive+
                            " health="+player.Health+" position="+player.transform.position+" view="+player.ViewPose.position+
                            " grounded="+player.Grounded+" climbing="+player.Climbing+" vaulting="+player.Vaulting+
                            " currentRuntime="+ReferenceEquals(MatchRuntime.Current,session.Modes)+
                            " carryingFlag="+session.Modes.Frame.flags.Any(f=>f.carrier==player.ActorId)+
                            " equipment="+JsonUtility.ToJson(beforeMode)+" participant="+JsonUtility.ToJson(localFrame)+"\n");
                        bool modeAccepted=inventory.RequestEquipment(true,false,true);
                        File.AppendAllText(Report,"MODE RESULT accepted="+modeAccepted+"\n");
                        Require(modeAccepted,"Alternate ranged mode "+kind);
                        yield return Wait(Mathf.Max(.45f,EquipmentSpecifications.Get(kind).cooldown)+.08f);
                        yield return ClearMarks(effects);
                        int alternateSequence=session.Modes.Frame.equipmentSequence;
                        var alternateMuzzle=player.ViewPose.position+Vector3.forward*.7f;
                        firingPose=new Pose(alternateMuzzle,Quaternion.LookRotation(target.worldCenterOfMass-alternateMuzzle));
                        if(kind==EquipmentKind.StockPistol)yield return HoldShots(session,held,kind,firingPose,3);
                        else Require(inventory.RequestEquipmentAttack(firingPose),"Sheriff shotgun accepted");
                        yield return Until(()=>session.Modes.Frame.equipmentActions.Any(a=>
                            a.id>alternateSequence&&a.kind==kind&&a.action==EquipmentAction.Shot),
                            "New alternate-mode shot announced "+kind,8);
                        if(legacyOnly)RequireTargetHit(session.Modes.Frame.equipmentActions.Last(a=>
                            a.id>alternateSequence&&a.kind==kind&&a.action==EquipmentAction.Shot),target,"Alternate "+kind+" endpoint");
                        yield return Wait(.4f);
                        Require(effects.ActiveMarks>0&&effects.ActiveMarks<=96,"New alternate-mode hit creates bounded impacts "+kind);
                    }
                    if(kind==EquipmentKind.DualUzi)
                    {
                        Require(inventory.RequestEquipment(true,true,false),"Left-handed spiker command");
                        yield return HoldShots(session,held,kind,firingPose,2);
                        var starts=session.Modes.Frame.equipmentActions.Where(a=>a.kind==kind&&a.action==EquipmentAction.Shot).ToArray();
                        Require(starts.Length>0&&starts.All(a=>(a.position-firingPose.position).magnitude<.37f),
                            "Spiker observer receives unscaled authoritative left/right launch positions");
                    }
                    if(kind==EquipmentKind.DualRifle)
                    {
                        var field=typeof(CandyWeaponImpacts).GetField("marks",Private);
                        var marks=(IList)field.GetValue(effects);
                        object mark=marks[0];var type=mark.GetType();
                        var anchor=(Transform)type.GetField("anchor").GetValue(mark);
                        var local=(Matrix4x4)type.GetField("localPose").GetValue(mark);
                        Require(anchor&&anchor.IsChildOf(target.transform),"Hit mark is anchored to the struck production block");
                        Vector3 before=(anchor.localToWorldMatrix*local).GetColumn(3);
                        target.position+=Vector3.right*1.5f;target.rotation=Quaternion.Euler(0,22,0);
                        Physics.SyncTransforms();yield return null;
                        Vector3 after=(anchor.localToWorldMatrix*local).GetColumn(3);
                        Require((after-before).magnitude>.8f,"Mark pose follows real target translation and rotation");

                        Require(inventory.RequestEquipment(true,false,true),"Normal shotgun mode command");
                        yield return Wait(.8f);
                        yield return ClearMarks(effects);
                        int shotgunSequence=session.Modes.Frame.equipmentSequence;
                        var shotgunMuzzle=player.ViewPose.position+Vector3.forward*.7f;
                        var shotgunPose=new Pose(shotgunMuzzle,Quaternion.LookRotation(target.worldCenterOfMass-shotgunMuzzle));
                        Require(inventory.RequestEquipmentAttack(shotgunPose),"Normal shotgun shot accepted");
                        yield return Until(()=>session.Modes.Frame.equipmentActions.Any(a=>
                            a.id>shotgunSequence&&a.kind==kind&&a.action==EquipmentAction.Shot),
                            "New rifle shotgun shot announced",8);
                        var shot=session.Modes.Frame.equipmentActions.Last(a=>
                            a.id>shotgunSequence&&a.kind==kind&&a.action==EquipmentAction.Shot);
                        RequireTargetHit(shot,target,"Shotgun endpoint");
                        yield return Wait(.5f);
                        Require(effects.ActiveMarks>0&&effects.ActiveMarks<=96,"New shotgun spread creates bounded surface marks");
                    }
                }
                yield return Wait(4.5f);
                Require(effects.ActiveMarks==0,"Marks expire after firing stops");
                var emitter=GameObject.Find("Shared ranged muzzle pulses");
                Require(emitter&&emitter.GetComponentsInChildren<Collider>().Length==0,"Muzzle feedback adds no colliders");
                File.AppendAllText(Report,"COMPLETE controlled native Game fixture. Rendered frames, independent peers and Quest remain separate evidence.\n");
            }
            finally
            {
                GameEvents.Damaged-=Damaged;GameEvents.Eliminated-=Eliminated;
                held.RequestEquipmentTrigger(false,player.ViewPose);
                player.Teleport(oldPosition,oldRotation);player.InputSuspended=oldSuspended;
                inventory.RequestEquipment(false,false,false);
                Object.Destroy(stage);
            }
        }
    }
}
