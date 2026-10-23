using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Modes;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class EquipmentChecks
    {
        sealed class Source:IEquipmentPickupSource
        {public EquipmentKind Kind{get;set;}public Vector3 Position{get;set;}public float Duration=>20;}
        static int checks;
        static string Report=>ProjectPath.Report("Equipment.txt");
        static void Check(bool passed,string text){File.AppendAllText(Report,(passed?"PASS ":"FAIL ")+text+"\n");if(!passed)throw new Exception(text);checks++;}
        static int Pickup(IEquipmentRuntime runtime,EquipmentKind kind,Vector3 point)=>runtime.RegisterPickup(new Source{Kind=kind,Position=point});
        public static IEnumerator Run(CastlePlaySession session)
        {
            checks=0;File.WriteAllText(Report,"Equipment: actual Game runtime and physical collider fixtures; desktop poses, no headset.\n");
            var runtime=session.Modes;IEquipmentRuntime inventory=runtime;var player=session.Survivor();var controls=player.GetComponent<CandyEquipment>();var avatar=player.GetComponent<CandyPlayerAvatar>();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=Layers.Surface;floor.transform.position=new Vector3(110,9,100);floor.transform.localScale=new Vector3(30,2,40);
            player.Place(new Vector3(110,10.05f,100),Quaternion.identity);player.InputSuspended=true;
            yield return new WaitForSeconds(.3f);Physics.SyncTransforms();
            var point=player.transform.position+Vector3.forward*.7f+Vector3.up*.25f;
            foreach(var kind in new[]{EquipmentKind.Blaster,EquipmentKind.MurderSword,EquipmentKind.SheriffHammer,EquipmentKind.DualRifle,EquipmentKind.BoxingGlove})
            {
                var before=inventory.EquipmentFor(runtime.LocalActorId);int id=Pickup(inventory,kind,point);Check(id>0,"Registers "+kind+" as a session pickup");
                if(before.kind!=EquipmentKind.None)
                {
                    Check(!inventory.RequestPickup(id,false),"Occupied slot rejects unconfirmed "+kind+" pickup");
                    Check(inventory.EquipmentFor(runtime.LocalActorId).kind==before.kind,"Declining preserves the current item");
                }
                Check(inventory.RequestPickup(id,before.kind!=EquipmentKind.None),"Confirmed pickup equips "+kind);
                if(before.kind!=EquipmentKind.None)Check(runtime.Frame.pickups.Any(p=>p.kind==before.kind&&Vector3.Distance(p.position,point)<.001f),"Swap drops old item at the new pickup's location");
                controls.SetEquipped(true);yield return null;avatar.RefreshNow();yield return null;
                var state=controls.Equipment;Check(state.kind==kind&&state.drawn,"Exactly one drawn "+kind+" occupies the slot");
                var model=player.transform.Find(kind==EquipmentKind.Blaster?"Visual - three ring alien blaster":"Visual - "+kind);
                Check(model&&model.gameObject.activeInHierarchy,"Held "+kind+" model exists");
                var hand=avatar.LastFrame.right;Check(Vector3.Distance(model.position,hand.position)<.025f,"Held "+kind+" follows the same wrist pose as the body");
                Check(model.GetComponentsInChildren<Collider>(true).Length==0,"Weapon art adds no movement collisions");
                if(kind==EquipmentKind.DualRifle)
                {
                    Check(controls.BlocksTraversal&&!runtime.RulesFor(player.gameObject).canJump&&!runtime.RulesFor(player.gameObject).canClimb&&!runtime.RulesFor(player.gameObject).canVault,"Drawn rifle locks jumping, climbing and vaulting");
                    Check(Vector3.Distance(avatar.LastFrame.left.position,hand.position)>.15f,"Desktop rifle support hand reaches the foregrip");
                    controls.ToggleRifleMode();Check(controls.Equipment.shotgun,"Other-hand mode command selects shotgun");controls.ToggleRifleMode();Check(!controls.Equipment.shotgun,"Other-hand mode command restores long rifle");
                    Capture(model,"Rifle-Long.png");
                }
                controls.SetEquipped(false);yield return null;
                Check(model.gameObject.activeInHierarchy,"Holstered "+kind+" remains externally visible");
                var expected=CandyWeaponVisual.Holster(kind,avatar.LastFrame.feet,avatar.LastFrame.head,Quaternion.Euler(0,avatar.LastFrame.headRotation.eulerAngles.y,0),player.playerScale,false);
                Check(Vector3.Distance(model.position,expected.position)<.025f,"Holstered "+kind+" uses its body attachment");
                if(kind==EquipmentKind.DualRifle)Check(!controls.BlocksTraversal&&runtime.RulesFor(player.gameObject).canJump,"Holstering restores rifle traversal permissions");
            }
            int blaster=Pickup(inventory,EquipmentKind.Blaster,point);Check(inventory.RequestPickup(blaster,true),"Pickup blaster works with testing disabled");controls.SetEquipped(true);yield return null;
            player.InputSuspended=false;Check(controls.FireOnce(),"A collected blaster fires without the demo testing toggle");player.InputSuspended=true;yield return null;
            Check(!runtime.BlasterTestingEnabled&&runtime.Frame.blaster.enabled&&runtime.Frame.blaster.shots.Length>0,"Collected blaster projectiles remain visible without enabling demo testing");
            int speed=Pickup(inventory,EquipmentKind.Speed,point);Check(inventory.RequestPickup(speed,true),"Timed speed power replaces a weapon through the same slot");
            yield return null;player.InputSuspended=false;yield return null;player.InputSuspended=true;
            Check(player.PowerupSpeedMultiplier==2&&player.PowerupUnlimitedStamina,"Owned speed item applies the actual movement effect");
            int jump=Pickup(inventory,EquipmentKind.Jump,point);Check(inventory.RequestPickup(jump,true),"Jump power swaps with speed power");
            yield return null;
            Check(player.PowerupSpeedMultiplier==1&&player.EffectiveJumpHeight>player.jumpHeight*2.9f,"Swap removes old speed effect and applies jump without stacking");
            int glove=Pickup(inventory,EquipmentKind.BoxingGlove,point);Check(inventory.RequestPickup(glove,true),"Glove replaces timed power");yield return null;
            Check(!player.PowerupUnlimitedStamina,"Replacing a power removes its stamina benefit");
            var target=session.SurvivorBots.SpawnOne();Check(target,"Physical glove damage target spawns");var motor=target.GetComponent<CastleSurvivor>();target.GetComponent<CastleBotBrain>().enabled=false;motor.InputSuspended=true;
            motor.Place(player.transform.position+Vector3.forward*.8f,Quaternion.identity);yield return new WaitForSeconds(.6f);Physics.SyncTransforms();
            player.viewCamera.transform.rotation=Quaternion.LookRotation(motor.Capsule.bounds.center-player.viewCamera.transform.position);controls.SetEquipped(true);yield return null;
            float health=motor.Health;player.InputSuspended=false;Check(controls.FireOnce(),"Glove executes a real authoritative melee attack");player.InputSuspended=true;
            Check(health-motor.Health>=79,"Giant glove deals 80 damage on a physical hit");
            Check(motor.HazardVelocity.magnitude>15,"Giant glove adds a strong traversal impulse");
            yield return new WaitForSeconds(1);
            int rifle=Pickup(inventory,EquipmentKind.DualRifle,point);Check(inventory.RequestPickup(rifle,true),"Rifle available after other weapons");controls.SetEquipped(true);yield return null;
            player.Place(new Vector3(115,10.05f,100),Quaternion.identity);player.InputSuspended=true;
            motor.Place(new Vector3(115,10.05f,112),Quaternion.identity);yield return new WaitForSeconds(.2f);Physics.SyncTransforms();player.viewCamera.transform.rotation=Quaternion.LookRotation(motor.Capsule.bounds.center-player.viewCamera.transform.position);
            health=motor.Health;player.InputSuspended=false;Check(controls.FireOnce(),"Long rifle fires through its authoritative command");player.InputSuspended=true;Check(motor.Health<health,"Long rifle hits the actual distant collider");
            yield return new WaitForSeconds(.9f);motor.Place(new Vector3(115,10.05f,106),Quaternion.identity);motor.InputSuspended=true;controls.ToggleRifleMode();yield return null;Physics.SyncTransforms();
            player.viewCamera.transform.rotation=Quaternion.LookRotation(motor.Capsule.bounds.center-player.viewCamera.transform.position);health=motor.Health;
            player.InputSuspended=false;Check(controls.FireOnce(),"Shotgun mode executes its real pellet attack");player.InputSuspended=true;Check(motor.Health<health&&health-motor.Health<=91,"Shotgun pellets hit with bounded combined damage");Capture(player.transform.Find("Visual - DualRifle"),"Rifle-Shotgun.png");
            yield return new WaitForSeconds(.9f);motor.Place(new Vector3(115,10.05f,106),Quaternion.identity);motor.InputSuspended=true;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=Layers.Surface;wall.transform.position=new Vector3(115,11,104);wall.transform.localScale=new Vector3(4,3,.3f);Physics.SyncTransforms();health=motor.Health;
            player.InputSuspended=false;Check(controls.FireOnce(),"Rifle fires toward solid cover");player.InputSuspended=true;Check(motor.Health==health,"Solid cover blocks shotgun pellets");Object.Destroy(wall);
            var remote=new GameObject("Equipment remote view fixture");var view=remote.AddComponent<SurvivorNetworkView>().Init(new NetworkContext{actorId=runtime.LocalActorId,localSurvivor=player.gameObject});
            using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))avatar.WriteState(writer);stream.Position=0;using(var reader=new BinaryReader(stream))view.ReadState(reader,true);}view.Render(.1f);
            Check(remote.transform.Find("Visual - DualRifle")?.gameObject.activeInHierarchy==true,"External network copy reconstructs the same equipped rifle");Object.Destroy(remote);
            var clone=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Check(ModeFrameValidation.Frame(clone,Services.World.Blocks.Count),"Inventory and pickup snapshot survives validated JSON transport");
            clone.pickups[0].position=new Vector3(float.NaN,0,0);Check(!ModeFrameValidation.Frame(clone,Services.World.Blocks.Count),"Malformed pickup coordinates are rejected");
            Check(!inventory.RequestPickup(int.MaxValue,true),"Unknown pickup IDs cannot grant items");
            var deathPoint=player.transform.position;runtime.HandleEliminated(player.gameObject,target);Check(inventory.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.None,"Elimination immediately clears the held slot");
            Check(runtime.Frame.pickups.Any(p=>p.kind==EquipmentKind.DualRifle&&Vector3.Distance(p.position,deathPoint)<.5f),"Death drops the rifle at the death position before respawn");
            yield return new WaitForSeconds(1.2f);Object.Destroy(target);Object.Destroy(floor);
            MatchSetup.MapId="mode-powerup-test";MatchSetup.ApplyMap();var rules=runtime.Rules.Copy();rules.island=IslandRule.Walkable;rules.countdownSeconds=0;runtime.Configure(rules);runtime.StartRound();
            player.Place(Services.World.SafeSpawnPoint(),Quaternion.identity);player.InputSuspended=true;yield return new WaitForSeconds(.5f);
            Check(Services.World is PowerupTestMap,"Actual powerup map loads");
            Check(runtime.Frame.pickups.Count(p=>p.station)==8&&runtime.Frame.pickups.Select(p=>p.kind).Distinct().Count()==8,"Powerup map spawns all eight power / weapon stations");
            File.AppendAllText(Report,"COMPLETE checks="+checks+"\n");
        }
        static void Capture(Transform model,string name)
        {
            var go=new GameObject("Weapon capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.01f;camera.fieldOfView=42;camera.transform.position=model.TransformPoint(new Vector3(.9f,.55f,-1.1f));camera.transform.LookAt(model.TransformPoint(new Vector3(0,.04f,.3f)));
            var target=new RenderTexture(1024,768,24);var previous=RenderTexture.active;camera.targetTexture=target;camera.Render();RenderTexture.active=target;var pixels=new Texture2D(1024,768,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1024,768),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(pixels);Object.Destroy(target);Object.Destroy(go);
        }
    }
}
