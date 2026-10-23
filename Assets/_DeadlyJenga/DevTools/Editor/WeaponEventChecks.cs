using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Modes;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyAudio;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class WeaponEventChecks
    {
        sealed class Pickup:IEquipmentPickupSource
        {public EquipmentKind Kind{get;set;}public Vector3 Position{get;set;}public float Duration=>20;}
        static int count;
        static string Report=>ProjectPath.Report("WeaponEvents.txt");
        static void Check(bool pass,string message)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+message+"\n");if(!pass)throw new Exception(message);count++;}
        static EquipmentActionFrame Last(MatchModeRuntime runtime)=>runtime.Frame.equipmentActions.LastOrDefault();
        static int Grant(IEquipmentRuntime runtime,EquipmentKind kind,Vector3 point)=>runtime.RegisterPickup(new Pickup{Kind=kind,Position=point});
        public static IEnumerator Run(CastlePlaySession session)
        {
            count=0;File.WriteAllText(Report,"Real Game authoritative equipment commands/casts; plain snapshot cursor and malformed-frame fixtures. No headset.\n");
            var runtime=session.Modes;IEquipmentRuntime inventory=runtime;var player=session.Survivor();
            Check(runtime.Authority&&runtime.Rules.mode==GameModeKind.Demo,"Fixture uses authoritative Demo session");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=Layers.Surface;floor.transform.position=new Vector3(150,9,150);floor.transform.localScale=new Vector3(15,2,15);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=Layers.Surface;wall.transform.position=new Vector3(150,11.5f,156);wall.transform.localScale=new Vector3(5,5,.4f);
            var position=player.transform.position;var rotation=player.transform.rotation;bool suspended=player.InputSuspended;
            try
            {
                player.Place(new Vector3(150,10.05f,150),Quaternion.identity);player.InputSuspended=true;
                yield return new WaitForSeconds(.2f);Physics.SyncTransforms();
                var point=player.transform.position+Vector3.forward*.6f+Vector3.up*.2f;
                int id=Grant(inventory,EquipmentKind.DualRifle,point);Check(id>0,"Registers actual rifle pickup");
                bool occupied=inventory.EquipmentFor(runtime.LocalActorId).kind!=EquipmentKind.None;
                Check(inventory.RequestPickup(id,occupied),"Authority accepts actual rifle collection");
                Check(Last(runtime).action==(occupied?EquipmentAction.Swap:EquipmentAction.Pickup),"Collection announces only after accepted inventory transition");
                Check(inventory.RequestEquipment(true,false,false),"Authority draws rifle");Check(Last(runtime).action==EquipmentAction.Draw,"Draw emits authoritative event");
                int sequence=runtime.Frame.equipmentSequence;
                Check(inventory.RequestEquipment(true,false,false)&&runtime.Frame.equipmentSequence==sequence,"Unchanged state emits no duplicate draw");
                var muzzle=new Pose(player.ViewPose.position+Vector3.forward*.5f,Quaternion.identity);
                Check(inventory.RequestEquipmentAttack(muzzle),"Authority accepts real rifle cast");
                var shot=Last(runtime);Check(shot.action==EquipmentAction.Shot&&shot.kind==EquipmentKind.DualRifle&&!shot.shotgun&&shot.endpoints.Length==1,"Accepted rifle has one authoritative endpoint");
                Check(Vector3.Distance(shot.endpoints[0],new Vector3(muzzle.position.x,muzzle.position.y,155.8f))<.03f,"Rifle endpoint is the real solid-wall SphereCast contact");
                sequence=runtime.Frame.equipmentSequence;
                Check(!inventory.RequestEquipmentAttack(muzzle)&&runtime.Frame.equipmentSequence==sequence,"Cooldown-denied shot emits no event");
                yield return null;
                var liveTrails=session.GetComponent<DeadlyJenga.Presentation.CandyBulletTrails>();
                Check(liveTrails&&liveTrails.ActiveCount==1,"Accepted real rifle shot launches one visible bullet through the session observer");
                var cursor=new CandyEquipmentActionCursor();int received=0;cursor.Baseline(runtime.Frame);cursor.Read(runtime.Frame,_=>received++);
                Check(received==0,"Late observer baselines old history without replay");
                Check(inventory.RequestEquipment(true,false,true),"Authority accepts shotgun transformation");
                cursor.Read(runtime.Frame,_=>received++);cursor.Read(runtime.Frame,_=>received++);
                Check(received==1&&Last(runtime).action==EquipmentAction.ModeSwitch,"Transform event delivered once despite repeated snapshots");
                yield return new WaitForSeconds(.7f);Physics.SyncTransforms();
                Check(inventory.RequestEquipmentAttack(muzzle),"Authority accepts real shotgun pellet casts");
                shot=Last(runtime);Check(shot.shotgun&&shot.endpoints.Length==7,"Shotgun exposes all seven accepted pellet endpoints");
                Check(shot.endpoints.All(end=>end.z<155.83f&&end.z>155.75f),"All shotgun pellets stop at real cover contacts");
                yield return null;
                Check(liveTrails&&liveTrails.ActiveCount==7,"Accepted real shotgun shot launches all seven visible pellet trails");
                Check(ModeFrameValidation.EquipmentActions(runtime.Frame),"Actual authoritative action history validates");
                var saved=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
                Check(saved.equipmentSequence==runtime.Frame.equipmentSequence&&saved.equipmentActions.Last().endpoints.Length==7,"JSON transports the complete event and exact pellet endpoints");
                saved.equipmentActions.Last().endpoints[0]=new Vector3(float.NaN,0,0);Check(!ModeFrameValidation.EquipmentActions(saved),"Malformed NaN endpoint is rejected");
                saved=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());saved.equipmentActions.Last().endpoints=new Vector3[8];Check(!ModeFrameValidation.EquipmentActions(saved),"Oversized pellet list is rejected");
                saved=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());saved.equipmentActions.Last().id=int.MaxValue;Check(!ModeFrameValidation.EquipmentActions(saved),"Future unannounced event ID is rejected");
                for(int i=0;i<40;i++)inventory.RequestEquipment(true,false,i%2==0);
                Check(runtime.Frame.equipmentActions.Length==32&&ModeFrameValidation.EquipmentActions(runtime.Frame),"Authority bounds a burst to 32 ordered events");
                sequence=runtime.Frame.equipmentSequence;
                var blocked=new Pose(player.ViewPose.position+Vector3.forward*100,Quaternion.identity);
                Check(!inventory.RequestEquipmentAttack(blocked)&&runtime.Frame.equipmentSequence==sequence,"Invalid muzzle emits no predicted or accepted event");
                Check(inventory.RequestEquipment(false,false,false),"Authority holsters rifle");Check(Last(runtime).action==EquipmentAction.Holster,"Holster emits accepted event");
                yield return new WaitForSeconds(.9f);
                Check(runtime.Frame.equipmentActions.Length==0&&runtime.Frame.equipmentSequence>0,"Transient events expire while sequence baseline persists");
                // Establish old observer baseline, migrate the real authority, then check the next action.
                cursor.Baseline(runtime.Frame);string epoch=runtime.Frame.equipmentEpoch;sequence=runtime.Frame.equipmentSequence;
                runtime.ResumeAuthorityFromFrame();
                Check(runtime.Frame.equipmentEpoch==epoch&&runtime.Frame.equipmentSequence==sequence&&runtime.Frame.equipmentActions.Length==0,"Host migration preserves sequence and clears historical transients");
                Check(inventory.RequestEquipment(true,false,false),"Restored authority accepts a new draw");
                received=0;cursor.Read(runtime.Frame,_=>received++);Check(received==1,"Established observer receives first post-migration event without replay");
                yield return null;
                var feedback=session.GetComponent<CandyWeaponFeedback>();Check(feedback&&feedback.ConsumedActions>0,"Actual session observer consumes authoritative gun/action events");
                runtime.HandleEliminated(player.gameObject,null);
                Check(runtime.Frame.equipmentActions.Any(item=>item.action==EquipmentAction.Drop&&item.kind==EquipmentKind.DualRifle),"Actual death announces its dropped weapon");
            }
            finally
            {player.Place(position,rotation);player.InputSuspended=suspended;Object.Destroy(wall);Object.Destroy(floor);}
            File.AppendAllText(Report,"COMPLETE checks="+count+"\n");
        }
    }
}
