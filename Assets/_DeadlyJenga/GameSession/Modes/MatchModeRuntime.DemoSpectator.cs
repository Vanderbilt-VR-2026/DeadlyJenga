using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.XR;
using CommonUsages=UnityEngine.XR.CommonUsages;

namespace DeadlyJenga.Modes
{
    public sealed partial class MatchModeRuntime
    {
        public bool DemoSpectating { get; private set; }
        public bool DemoFourViews { get; private set; }
        readonly Camera[] demoViews=new Camera[4];
        readonly string[] demoTargets=new string[4];
        readonly List<MatchPlayerFrame> demoCandidates=new List<MatchPlayerFrame>();
        Quaternion demoHeadNeutral;
        Vector3 demoHeadOrigin;
        int demoOffset;
        static readonly Rect[] Quadrants={new Rect(0,.5f,.5f,.5f),new Rect(.5f,.5f,.5f,.5f),new Rect(0,0,.5f,.5f),new Rect(.5f,0,.5f,.5f)};

        public void SetDemoSpectating(bool active)
        {
            if(active&&(session==null||Rules.mode!=GameModeKind.Demo&&!session.CanEditWorld))return;
            if(DemoSpectating==active)return;
            DemoSpectating=active;
            if(active)
            {
                var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if(!device.TryGetFeatureValue(CommonUsages.deviceRotation,out demoHeadNeutral))demoHeadNeutral=Quaternion.identity;
                device.TryGetFeatureValue(CommonUsages.devicePosition,out demoHeadOrigin);
                demoOffset=0;for(int i=0;i<4;i++)demoTargets[i]=null;
                if(modeCamera)modeCamera.enabled=false;
                TickDemoSpectator();
            }
            else
            {
                ClearDemoCameras();OverrideCamera=null;
                if(session!=null)
                {var original=session.PlayingHand?session.Hand:session.Player;if(NetworkActors.Alive(original)&&original.ViewCamera)original.ViewCamera.enabled=true;}
                TickCamera();
            }
            if(session!=null&&NetworkActors.Alive(session.Player)&&NetworkActors.Alive(session.Hand))session.SetMenu(session.MenuOpen);
        }
        public void SetDemoFourViews(bool active)
        {
            if(active&&session!=null&&session.IsVR)return;
            DemoFourViews=active;
            if(DemoSpectating)TickDemoSpectator();
        }
        public string DemoWatchedName(int slot)
        {
            if(!DemoSpectating||slot<0||slot>=4||!DemoFourViews&&slot>0)return "";
            foreach(var row in demoCandidates)if(row.id==demoTargets[slot])return row.name;
            return "Build overview · no living target";
        }
        public Transform SpectatorRoot(Camera camera)
        {
            if(!camera)return null;
            if(DemoSpectating)
            {for(int i=0;i<4;i++)if(camera==demoViews[i])return FindParticipantRoot(demoTargets[i]);}
            else if(camera==modeCamera&&frame?.players!=null)
            {foreach(var row in frame.players)if(row.id==LocalActorId&&row.role=="spectator")return FindParticipantRoot(row.spectating);}
            return null;
        }
        void GatherDemoCandidates()
        {
            demoCandidates.Clear();
            if(frame?.players==null)return;
            foreach(var row in frame.players)
                if(row.id!=LocalActorId&&row.alive&&!row.dying&&row.role!="spectator"&&row.role!="hand")demoCandidates.Add(row);
        }
        public void CycleDemoWatched(int step)
        {
            if(!DemoSpectating||step==0)return;
            GatherDemoCandidates();if(demoCandidates.Count==0)return;
            int current=demoCandidates.FindIndex(row=>row.id==demoTargets[0]);
            demoOffset=((Mathf.Max(0,current)+step)%demoCandidates.Count+demoCandidates.Count)%demoCandidates.Count;
            for(int i=0;i<4;i++)demoTargets[i]=null;
            TickDemoSpectator();
        }
        void TickDemoSpectator()
        {
            if(session==null)return;
            if(session.IsVR)DemoFourViews=false;
            GatherDemoCandidates();int count=DemoFourViews?4:1;
            var source=(session.PlayingHand?session.Hand:session.Player)?.ViewCamera;
            for(int i=0;i<count;i++)
            {
                bool valid=demoCandidates.Exists(row=>row.id==demoTargets[i]);
                if(!valid)
                {
                    demoTargets[i]=null;
                    for(int n=0;n<demoCandidates.Count;n++)
                    {
                        string id=demoCandidates[(demoOffset+n)%demoCandidates.Count].id;
                        bool used=false;for(int j=0;j<count;j++)if(j!=i&&demoTargets[j]==id)used=true;
                        if(!used){demoTargets[i]=id;break;}
                    }
                }
                var actor=FindActor(demoTargets[i]);var template=actor?.gameObject.GetComponent<IPlayableCharacter>()?.ViewCamera;
                var camera=demoViews[i];
                if(!camera)
                {var root=new GameObject("Demo spectator POV "+(i+1));root.transform.SetParent(transform,false);demoViews[i]=camera=root.AddComponent<Camera>();camera.enabled=false;}
                if(template||source)camera.CopyFrom(template?template:source);
                if(source){camera.clearFlags=source.clearFlags;camera.backgroundColor=source.backgroundColor;}
                camera.targetTexture=null;camera.targetDisplay=0;camera.rect=DemoFourViews?Quadrants[i]:new Rect(0,0,1,1);
                if(!UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline)camera.stereoTargetEye=session.IsVR?StereoTargetEyeMask.Both:StereoTargetEyeMask.None;camera.depth=20+i;
                Pose pose=DemoOverview();
                if(actor!=null)pose=actor.ViewPose;
                else foreach(var row in demoCandidates)if(row.id==demoTargets[i]){pose=new Pose(row.viewPosition,row.viewRotation);break;}
                if(session.IsVR)
                {
                    var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                    if(device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion rotation))pose.rotation=pose.rotation*Quaternion.Inverse(demoHeadNeutral)*rotation;
                    if(device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 position))pose.position+=Quaternion.Euler(0,pose.rotation.eulerAngles.y,0)*Vector3.ClampMagnitude(position-demoHeadOrigin,.35f);
                }
                camera.transform.SetPositionAndRotation(pose.position,pose.rotation);camera.enabled=true;
            }
            for(int i=count;i<4;i++)if(demoViews[i]){demoViews[i].enabled=false;Destroy(demoViews[i].gameObject);demoViews[i]=null;demoTargets[i]=null;}
            if(NetworkActors.Alive(session.Player)){session.Player.InputSuspended=true;if(session.Player.ViewCamera)session.Player.ViewCamera.enabled=false;}
            if(NetworkActors.Alive(session.Hand)){session.Hand.InputSuspended=true;if(session.Hand.ViewCamera)session.Hand.ViewCamera.enabled=false;}
            if(modeCamera)modeCamera.enabled=false;
            OverrideCamera=demoViews[0];
        }
        Pose DemoOverview()
        {
            var world=Services.World;
            if(world==null)return new Pose(new Vector3(0,5,-10),Quaternion.identity);
            var focus=new Vector3(world.Center.x,world.TopHeight,world.Center.z);
            var position=focus+new Vector3(0,world.Radius*.3f,-Mathf.Max(10,world.Radius*2.5f));
            return new Pose(position,Quaternion.LookRotation(focus-position));
        }
        void ClearDemoCameras()
        {
            for(int i=0;i<4;i++)
            {if(demoViews[i]){demoViews[i].enabled=false;Destroy(demoViews[i].gameObject);demoViews[i]=null;}demoTargets[i]=null;}
            demoCandidates.Clear();
        }
    }
}
