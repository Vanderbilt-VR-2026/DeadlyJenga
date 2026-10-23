using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CharacterNetwork;
using DeadlyJenga.Modes;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class PoweredSuitChecks
    {
        public static MatchFrame ThrustFrame {get;private set;}
        public static MatchFrame StoppedFrame {get;private set;}
        static void Capture(Transform model,string name)
        {
            foreach(var child in model.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
            var go=new GameObject("Weapon polish capture camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.028f,.05f,.09f);camera.nearClipPlane=.01f;camera.fieldOfView=40;
            var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float distance=bounds.extents.magnitude/Mathf.Sin(camera.fieldOfView*Mathf.Deg2Rad*.5f)*1.12f;camera.transform.position=bounds.center+(name.StartsWith("BoxingGlove")&&!name.Contains("Front")?new Vector3(-1,.4f,-.8f):new Vector3(-1,.55f,.65f)).normalized*distance;camera.transform.LookAt(bounds.center);
            var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.allowXRRendering=false;
            var lightObject=new GameObject("Polish capture soft key");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.color=new Color(1,.91f,.82f);light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(40,-35,0);
            var target=new RenderTexture(1024,768,24);var previous=RenderTexture.active;camera.targetTexture=target;
            try{camera.Render();RenderTexture.active=target;var pixels=new Texture2D(1024,768,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1024,768),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report(name),pixels.EncodeToPNG());Object.Destroy(pixels);}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(target);Object.Destroy(go);Object.Destroy(lightObject);}
        }
        sealed class Source:IEquipmentPickupSource
        {public EquipmentKind Kind=>EquipmentKind.PoweredArmor;public Vector3 Position{get;set;}public float Duration=>20;}
        public static IEnumerator Run(CastlePlaySession session,Action<bool,string> check)
        {
            var runtime=session.Modes;var player=session.Survivor();var avatar=player.GetComponent<CandyPlayerAvatar>();var controls=player.GetComponent<CandyEquipment>();
            var appearance=avatar.Appearance;var originalPosition=player.transform.position;var originalRotation=player.transform.rotation;bool paused=player.InputSuspended,enabled=controls.enabled;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Suit validation floor";floor.layer=Layers.Surface;floor.transform.position=new Vector3(130,9,100);floor.transform.localScale=new Vector3(24,2,24);
            var visualRoot=new GameObject("Weapon polish fixture");
            try
            {
                var mesh=new CandyWeaponVisual(visualRoot.transform,avatar.Rig.style.material);
                var frame=new CandyAvatarFrame{feet=Vector3.zero,head=Vector3.up*1.6f,headRotation=Quaternion.identity,scale=1,left=new CandyHandPose{rotation=Quaternion.identity},right=new CandyHandPose{rotation=Quaternion.identity}};
                foreach(var kind in new[]{EquipmentKind.BoxingGlove,EquipmentKind.DualRifle,EquipmentKind.Blaster,EquipmentKind.MurderSword,EquipmentKind.SheriffHammer})
                {
                    mesh.Present(new EquipmentState{kind=kind,drawn=true},frame,true);yield return null;
                    var renderers=visualRoot.GetComponentsInChildren<Renderer>();
                    check(renderers.Length>=3&&visualRoot.GetComponentsInChildren<Collider>().Length==0,kind+" detailed shared model is entirely cosmetic");
                    var props=new MaterialPropertyBlock();renderers[0].GetPropertyBlock(props);check(props.GetFloat("_Smoothness")>=.85f,kind+" uses polished candy surface");
                    if(kind==EquipmentKind.DualRifle)
                    {
                        var body=visualRoot.transform.Find("Visual - DualRifle/Receiver").GetComponent<Renderer>();var plate=visualRoot.transform.Find("Visual - DualRifle/Titanium receiver side plate").GetComponent<Renderer>();
                        check(plate.bounds.size.x>body.bounds.size.x,"Rifle chrome side plates extend outside the receiver after FBX size normalization");
                    }
                    Capture(visualRoot.transform,kind+"-CandyPolish.png");if(kind==EquipmentKind.BoxingGlove)Capture(visualRoot.transform,"BoxingGlove-Front-CandyPolish.png");
                }
                player.Place(new Vector3(130,10.05f,100),Quaternion.identity);player.InputSuspended=true;yield return new WaitForSeconds(.25f);Physics.SyncTransforms();
                int pickup=((IEquipmentRuntime)runtime).RegisterPickup(new Source{Position=player.transform.position+Vector3.up*.25f});
                check(pickup!=0&&runtime.RequestPickup(pickup,true),"Powered suit uses the authoritative shared pickup/swap slot");
                controls.enabled=false;
                check(runtime.RequestEquipment(true,false,false),"Suit can activate through equipment command");yield return null;
                check(avatar.EquippedAppearance.poweredArmor&&avatar.Appearance.poweredArmor==appearance.poweredArmor,"Suit overlays armor without changing saved wardrobe");
                check(((IPoweredEquipmentRuntime)runtime).RequestThrust(true)&&runtime.EquipmentFor(runtime.LocalActorId).thrust,"Authority accepts live drawn suit thrust state");
                ThrustFrame=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
                var saved=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());check(ModeFrameValidation.Frame(saved,Services.World.Blocks.Count),"Suit and thrust survive validated snapshot JSON");
                saved.equipment.First(x=>x.actor==runtime.LocalActorId).drawn=false;check(!ModeFrameValidation.Frame(saved,Services.World.Blocks.Count),"Malformed holstered thrust snapshot is rejected");
                yield return null;
                var feedback=session.GetComponent<DeadlyJenga.Presentation.CandyPoweredSuitFeedback>();
                check(feedback&&feedback.ActiveSuits>0,"Accepted suit thrust produces spatial jet observer feedback");
                player.InputSuspended=false;float height=player.transform.position.y;
                var focus=typeof(CastleSurvivor).GetMethod("OnApplicationFocus",BindingFlags.Instance|BindingFlags.NonPublic);
                focus.Invoke(player,new object[]{false});float velocity=player.VerticalSpeed;player.ApplyPoweredFlight(Vector3.up*100,.1f);
                check(Mathf.Abs(player.VerticalSpeed-velocity)<.001f,"Lost input focus rejects held powered flight");focus.Invoke(player,new object[]{true});
                // Batch mode cannot lock the desktop cursor; its session correctly reopens
                // the menu. Advance the real capsule explicitly to isolate 10 Hz flight physics.
                bool motorEnabled=player.enabled;player.enabled=false;player.InputSuspended=false;
                try{for(int tick=0;tick<12;tick++){player.ApplyPoweredFlight(Vector3.up*(player.gravity+9),.1f);player.Simulate(Vector2.zero,false,.1f);}}
                finally{player.enabled=motorEnabled;}
                check(player.transform.position.y>height+.1f&&player.VerticalSpeed>0,"Real collision motor rises against gravity at explicit 10 Hz flight ticks (rise="+(player.transform.position.y-height)+", speed="+player.VerticalSpeed+")");player.InputSuspended=true;
                check(runtime.RequestEquipmentAttack(controls.SuitMuzzle),"Suit palm creates authoritative traveling plasma");
                check(runtime.Frame.equipmentActions.Any(x=>x.kind==EquipmentKind.PoweredArmor&&x.action==EquipmentAction.Shot),"Suit plasma has replicated accepted shot sound history");
                check(runtime.RequestEquipment(false,false,false)&&!runtime.EquipmentFor(runtime.LocalActorId).thrust,"Holstering immediately clears replicated thrust");
                StoppedFrame=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
                check(!((IPoweredEquipmentRuntime)runtime).RequestThrust(true),"Holstered suit cannot start jets");
                runtime.RequestEquipment(true,false,false);((IPoweredEquipmentRuntime)runtime).RequestThrust(true);yield return new WaitForSeconds(1.3f);
                check(!runtime.EquipmentFor(runtime.LocalActorId).thrust,"Lost thrust keepalive times out on authority");
            }
            finally
            {
                controls.enabled=enabled;player.InputSuspended=paused;player.Place(originalPosition,originalRotation);Object.Destroy(floor);Object.Destroy(visualRoot);
            }
        }
    }
}
