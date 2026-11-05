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
            float limit=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<limit)yield return null;
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

        public static IEnumerator Run(CastlePlaySession session)
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
            try
            {
                session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Projectile fixture floor";
                floor.transform.SetParent(stage.transform);floor.layer=Layers.Surface;
                floor.transform.position=new Vector3(180,19,180);floor.transform.localScale=new Vector3(18,2,24);
                player.Place(new Vector3(180,20.05f,174),Quaternion.identity);player.InputSuspended=true;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleBlock.prefab"));
                Require(prefab,"Production block prefab found by name");

                foreach(var kind in new[]{
                    EquipmentKind.DualRifle,EquipmentKind.SheriffHammer,EquipmentKind.Blaster,EquipmentKind.PoweredArmor,
                    EquipmentKind.RocketLauncher,EquipmentKind.DualUzi,EquipmentKind.ChainGun,EquipmentKind.GrenadeLauncher,
                    EquipmentKind.WristLaser,EquipmentKind.NeedleDrill,EquipmentKind.StockPistol,
                    EquipmentKind.Chainsaw,EquipmentKind.LaserAxe})
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
                    // Allow genuine projectile travel/ballistic arc, plus one impact-observer frame.
                    yield return Wait(kind==EquipmentKind.RocketLauncher?1.1f:kind==EquipmentKind.GrenadeLauncher?1.2f:.75f);
                    Require(effects.ActiveMarks<=96,"Impact population remains bounded for "+kind);
                    Require(effects.ActiveMarks>0,"A real surface hit creates a temporary mark for "+kind);
                    if(kind==EquipmentKind.SheriffHammer||kind==EquipmentKind.StockPistol)
                    {
                        Require(inventory.RequestEquipment(true,false,true),"Alternate ranged mode "+kind);
                        yield return Wait(EquipmentSpecifications.Get(kind).cooldown+.08f);
                        yield return ClearMarks(effects);
                        int alternateSequence=session.Modes.Frame.equipmentSequence;
                        if(kind==EquipmentKind.StockPistol)yield return HoldShots(session,held,kind,firingPose,3);
                        else Require(inventory.RequestEquipmentAttack(firingPose),"Sheriff shotgun accepted");
                        yield return Until(()=>session.Modes.Frame.equipmentActions.Any(a=>
                            a.id>alternateSequence&&a.kind==kind&&a.action==EquipmentAction.Shot),
                            "New alternate-mode shot announced "+kind,8);
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
                        var shotgunPose=new Pose(muzzle,Quaternion.LookRotation(target.worldCenterOfMass-muzzle));
                        Require(inventory.RequestEquipmentAttack(shotgunPose),"Normal shotgun shot accepted");
                        yield return Until(()=>session.Modes.Frame.equipmentActions.Any(a=>
                            a.id>shotgunSequence&&a.kind==kind&&a.action==EquipmentAction.Shot),
                            "New rifle shotgun shot announced",8);
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
                held.RequestEquipmentTrigger(false,player.ViewPose);
                player.Place(oldPosition,oldRotation);player.InputSuspended=oldSuspended;
                inventory.RequestEquipment(false,false,false);
                Object.Destroy(stage);
            }
        }
    }
}
