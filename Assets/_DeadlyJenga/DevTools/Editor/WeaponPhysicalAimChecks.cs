using System;
using System.Reflection;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadlyJenga.DevTools.Editor
{
    // QA calls these after its normal input pump; this never moves a camera or injects a pose.
    public static class WeaponPhysicalAimChecks
    {
        public static void CheckEyeBox(Action<bool,string> check)
        {
            foreach(float scale in new[]{.5f,1f,1.5f})
            {
                var forward=Vector3.forward;
                float naturalRelief=.35f*scale+.10f;
                check(CandyGunSights.EyeBoxContains(Vector3.zero,forward,forward*naturalRelief,forward,scale),"Natural forward wrist can raise the physical scope, scale "+scale);
                foreach(float side in new[]{-1f,1f})
                    check(CandyGunSights.EyeBoxContains(Vector3.zero,forward,new Vector3(side*.035f,0,naturalRelief),forward,scale),"Human eye spacing remains usable, side "+side+" scale "+scale);
                check(!CandyGunSights.EyeBoxContains(Vector3.zero,forward,new Vector3(0,-.25f,naturalRelief),forward,scale),"Hip height never activates magnification, scale "+scale);
                check(!CandyGunSights.EyeBoxContains(Vector3.zero,forward,forward*-.1f,forward,scale),"Rearward scope never activates, scale "+scale);
                check(!CandyGunSights.EyeBoxContains(Vector3.zero,forward,forward*.01f,forward,scale),"Scope inside the face never activates, scale "+scale);
                check(!CandyGunSights.EyeBoxContains(Vector3.zero,forward,forward*2,forward,scale),"Distant scope never activates, scale "+scale);
                check(!CandyGunSights.EyeBoxContains(Vector3.zero,forward,forward*naturalRelief,Quaternion.Euler(0,25,0)*forward,scale),"Mispointed weapon never activates, scale "+scale);
                check(!CandyGunSights.EyeBoxContains(Vector3.zero,forward,new Vector3(.2f,0,naturalRelief),forward,scale),"Sideways weapon never activates, scale "+scale);
            }
        }

        [Serializable] public sealed class Sample
        {
            public int frame;
            public string kind;
            public bool left,shotgun,dominantGrip,supportGrip,physicalSightAligned;
            public float scale,trackedGripError,renderedGripError,modelGripError,opticCenterError;
            public Pose grip,muzzle,trackedGrip;
            public Vector3 eye,optic,renderedWrist,modelRoot;
            public Quaternion eyeRotation,opticRotation;
        }

        public static string Observe(CandyEquipment equipment,bool expectedSight,Action<bool,string> check)
        {
            if(!Application.isPlaying||!equipment)throw new InvalidOperationException("Requires the live ready player, after a real rendered frame.");
            var motor=equipment.GetComponent<CastleSurvivor>();var avatar=equipment.GetComponent<CandyPlayerAvatar>();
            check(motor&&motor.IsVR&&motor.Alive&&avatar&&avatar.tracking,"Actual living VR survivor and tracking source");
            var state=equipment.Equipment;bool left=state.leftHanded;
            check(state.drawn&&HybridXRInput.ControllerTracked(left),"Weapon drawn with actual tracked dominant controller");
            check(HybridXRInput.GripHeld(left)==expectedSight,"Actual dominant AIM grip matches pressed/released test phase");
            check(avatar.tracking.TryControllerAim(left,out var tracked),"Actual controller pointer pose available");
            var grip=equipment.GripPose;var rendered=avatar.LastFrame;
            var wrist=left?rendered.left:rendered.right;
            var supportAction=(InputAction)typeof(CandyEquipment).GetField("grip",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(equipment);
            Transform root=null,optic=null;
            string opticName=state.kind==EquipmentKind.DualRifle?(state.shotgun?"Shotgun optic":"Rifle optic"):state.kind==EquipmentKind.Blaster?"Blaster optic":state.kind==EquipmentKind.SheriffHammer?"Revolver optic":null;
            foreach(var t in equipment.GetComponentsInChildren<Transform>())
            {
                if(t.name=="Visual - "+state.kind)root=t;
                if(opticName!=null&&t.name==opticName)optic=t;
            }
            check(root,"Actual weapon model is present");
            var camera=avatar.viewCamera;check(camera,"Actual player eye camera exists");
            var sample=new Sample{frame=Time.frameCount,kind=state.kind.ToString(),left=left,shotgun=state.shotgun,scale=motor.playerScale,
                dominantGrip=HybridXRInput.GripHeld(left),supportGrip=supportAction?.IsPressed()==true,grip=grip,muzzle=equipment.MuzzlePose,trackedGrip=tracked,
                eye=camera.transform.position,eyeRotation=camera.transform.rotation,renderedWrist=wrist.position,modelRoot=root.position,
                trackedGripError=Vector3.Distance(grip.position,tracked.position),renderedGripError=Vector3.Distance(wrist.position,grip.position),modelGripError=Vector3.Distance(root.position,grip.position)};
            if(!expectedSight)check(sample.trackedGripError<.001f,"Released aim restores the actual tracked hip position");
            else check(sample.trackedGripError>.01f,"AIM button physically moves the weapon away from its unchanged tracked hip position");
            check(sample.renderedGripError<.001f&&sample.modelGripError<.001f,"Rendered wrist and weapon use the actual grip position");
            if(optic)
            {
                sample.optic=optic.position;sample.opticRotation=optic.rotation;
                sample.opticCenterError=Vector3.Distance(optic.position,sample.eye+camera.transform.forward*.18f);
                if(expectedSight)
                {
                    check(sample.opticCenterError<.002f,"Actual model optic reaches the centered binocular eye line");
                    check(Vector3.Angle(optic.forward,camera.transform.forward)<.5f,"Actual optic points along the eye aim line");
                }
                sample.physicalSightAligned=CandyGunSights.EyeBoxContains(sample.eye,camera.transform.forward,optic.position,optic.forward,Mathf.Abs(optic.lossyScale.z));
            }
            else sample.physicalSightAligned=CandyGunSights.OpenSightAligned(sample.eye,camera.transform.forward,sample.muzzle,state,sample.scale);
            check(sample.physicalSightAligned==expectedSight,"Actual physical sight alignment matches raised/lowered state");
            var sights=UnityEngine.Object.FindFirstObjectByType<CandyGunSights>();
            check(sights&&sights.Aiming==expectedSight,"Live binocular/open-sight presenter agrees with physical alignment");
            return JsonUtility.ToJson(sample,true);
        }
    }
}
