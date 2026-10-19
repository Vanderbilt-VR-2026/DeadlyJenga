using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using CommonUsages=UnityEngine.XR.CommonUsages;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        Camera modeCamera;
        bool wasOverride;
        Pose deathPose;
        Quaternion headNeutral;
        Vector3 headOrigin;
        bool spectatorStickArmed=true;
        void TickSpectatorInput()
        {
            if(session==null||session.MenuOpen||frame.players==null)return;
            MatchPlayerFrame self=null;foreach(var row in frame.players)if(row.id==LocalActorId){self=row;break;}
            if(self==null||self.role!="spectator")return;
            int step=0;var keyboard=Keyboard.current;
            if(!session.IsVR&&keyboard!=null){if(keyboard.leftArrowKey.wasPressedThisFrame)step=-1;if(keyboard.rightArrowKey.wasPressedThisFrame)step=1;}
            if(session.IsVR)
            {
                var device=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if(device.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 axis))
                {if(Mathf.Abs(axis.x)<.3f)spectatorStickArmed=true;else if(spectatorStickArmed&&Mathf.Abs(axis.x)>.7f){step=axis.x<0?-1:1;spectatorStickArmed=false;}}
            }
            if(step!=0)NextSpectator(step);
        }
        void LateUpdate(){TickCamera();}
        void TickCamera()
        {
            if(session==null||!NetworkActors.Alive(session.Player)||frame.players==null)return;
            MatchPlayerFrame self=null;foreach(var row in frame.players)if(row.id==LocalActorId){self=row;break;}
            var source=session.Player.ViewCamera;
            bool waterOccluded=!session.PlayingHand&&self!=null&&self.alive&&self.role!="hand"&&frame.phase==RoundPhase.Playing&&source&&Services.World!=null&&source.transform.position.y<=Services.World.WaterHeight(source.transform.position)+.25f;
            bool use=!session.PlayingHand&&self!=null&&(self.dying||self.role=="spectator"||waterOccluded);
            if(!use)
            {
                OverrideCamera=null;
                if(modeCamera)modeCamera.enabled=false;
                if(wasOverride)
                {
                    var active=session.PlayingHand?session.Hand:session.Player;
                    if(NetworkActors.Alive(active)&&active.ViewCamera)active.ViewCamera.enabled=true;
                }
                wasOverride=false;return;
            }
            if(!modeCamera)
            {
                var root=new GameObject("Mode spectator view");root.transform.SetParent(transform,false);
                modeCamera=root.AddComponent<Camera>();if(source)modeCamera.CopyFrom(source);
                modeCamera.stereoTargetEye=StereoTargetEyeMask.Both;
            }
            if(!wasOverride)
            {
                deathPose=source?new Pose(source.transform.position,source.transform.rotation):new Pose(self.viewPosition,self.viewRotation);
                float water=Services.World?.WaterHeight(deathPose.position)??-1000;
                deathPose.position.y=Mathf.Max(deathPose.position.y,water+.12f);
                var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if(!device.TryGetFeatureValue(CommonUsages.deviceRotation,out headNeutral))headNeutral=Quaternion.identity;
                device.TryGetFeatureValue(CommonUsages.devicePosition,out headOrigin);
            }
            Pose pose=deathPose;
            if(!self.dying&&!waterOccluded)
            {
                MatchPlayerFrame watched=null;foreach(var row in frame.players)if(row.id==self.spectating&&row.alive){watched=row;break;}
                if(watched!=null)
                {
                    var live=FindActor(watched.id);
                    pose=live!=null?live.ViewPose:new Pose(watched.viewPosition,watched.viewRotation);
                    if(live==null&&wasOverride){float blend=1-Mathf.Exp(-18*Time.unscaledDeltaTime);pose.position=Vector3.Lerp(modeCamera.transform.position,pose.position,blend);pose.rotation=Quaternion.Slerp(modeCamera.transform.rotation,pose.rotation,blend);}
                }
                else if(Services.World!=null)
                {
                    var world=Services.World;Vector3 position=world.Center+new Vector3(0,world.TopHeight-world.Center.y+world.Radius*.3f,-world.Radius*2.5f);
                    pose=new Pose(position,Quaternion.LookRotation(world.Center+Vector3.up*(world.TopHeight-world.Center.y)*.5f-position));
                }
            }
            if(session.IsVR)
            {
                var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if(!device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion head))head=headNeutral;
                Quaternion heading=Quaternion.Euler(0,pose.rotation.eulerAngles.y,0);
                pose.rotation=heading*Quaternion.Inverse(headNeutral)*head;
                if(!device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 headPosition))headPosition=headOrigin;
                Vector3 lean=headPosition-headOrigin;
                pose.position+=heading*Vector3.ClampMagnitude(lean,.35f);
            }
            if(self.dying||waterOccluded)pose.position.y=Mathf.Max(pose.position.y,(Services.World?.WaterHeight(pose.position)??-1000)+.12f);
            modeCamera.transform.SetPositionAndRotation(pose.position,pose.rotation);modeCamera.enabled=true;
            if(source)source.enabled=false;
            if(session.Hand?.ViewCamera)session.Hand.ViewCamera.enabled=false;
            OverrideCamera=modeCamera;wasOverride=true;
        }
    }
}
