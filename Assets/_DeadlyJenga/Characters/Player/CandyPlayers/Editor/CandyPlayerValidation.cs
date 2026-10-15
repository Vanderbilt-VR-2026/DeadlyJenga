using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastlePrototype.Editor;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CandyPlayers.Editor
{
    [InitializeOnLoad]
    public static class CandyPlayerValidation
    {
        const string Request="ArtDirection/CandyPlayers/install.request",Key="CandyPlayerValidation";
        public const string Results="ArtDirection/CandyPlayers/Results/";
        static CandyPlayerValidation()
        {
            EditorApplication.update+=()=>
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
                File.Delete(Request);
                try{Run();}catch(System.Exception e){Directory.CreateDirectory(Results);File.WriteAllText(Results+"ERROR.txt",e.ToString());Debug.LogException(e);}
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
                SessionState.SetBool(Key,false);EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Validate());
            };
        }
        [MenuItem("Tools/Candy Players/Install And Verify")]
        public static void Run()
        {
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Game.unity")throw new System.InvalidOperationException("Open the Game scene first.");
            CandyPlayerBuilder.Install();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static IEnumerator Validate()
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            Directory.CreateDirectory(Results);yield return null;
            var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetRole(false);session.SetMenu(true);yield return new WaitForSeconds(2);
            var player=session.Player;var avatar=player.GetComponent<CandyPlayerAvatar>();var report=new StringBuilder();
            void Check(string label,bool pass,string details=""){report.AppendLine((pass?"PASS ":"FAIL ")+label+" "+details);File.WriteAllText(Results+"AvatarValidation.txt",report.ToString());}
            Check("Candy body and head instantiated",avatar&&avatar.Rig&&avatar.Rig.bodyRenderer&&avatar.Rig.headRenderer);
            Check("Rig has 44 articulated bones",avatar.Rig.BoneCount==44);
            Check("Visual body cannot push physics blocks",avatar.Rig.GetComponentsInChildren<Collider>().Length==0&&avatar.Rig.GetComponentsInChildren<Rigidbody>().Length==0);
            Check("First-person and outside views share one hand rig",avatar.Rig.ownerCamera==player.viewCamera&&avatar.GetComponentsInChildren<CandyPlayerRig>().Length==1);
            Check("Random spawn color comes from bright palette",avatar.ColorIndex>=0&&avatar.ColorIndex<avatar.Rig.style.colors.Length);
            Check("Input payload fits 512 bytes",Marshal.SizeOf<HandPoseInput>()<=512,Marshal.SizeOf<HandPoseInput>().ToString());
            var joints=CandyHandSkeleton.Rest(true);Vector3 expected=new Vector3(-.041f,.012f,.097f);joints.SetJoint(7,expected);
            Check("Network finger positions preserve 1mm precision",Vector3.Distance(joints.Joint(7),expected)<.001f);
            var frame=avatar.ReadFrame();frame.left=new CandyHandPose{tracked=true,skeletonTracked=true,position=frame.head+new Vector3(-.18f,-.11f,.23f),rotation=Quaternion.Euler(10,20,15),skeleton=joints};
            frame.right=new CandyHandPose{tracked=true,skeletonTracked=true,position=frame.head+new Vector3(.16f,-.12f,.22f),rotation=Quaternion.Euler(5,-10,0),skeleton=CandyHandSkeleton.Rest(false)};
            avatar.OverridePose(frame);avatar.RefreshNow();
            Check("Tracked left wrist rendered at actual world pose",Vector3.Distance(avatar.Rig.LeftPalm.position,frame.left.position)<.001f&&Quaternion.Angle(avatar.Rig.LeftPalm.rotation,frame.left.rotation)<.1f);
            Check("Tracked right wrist rendered at actual world pose",Vector3.Distance(avatar.Rig.RightPalm.position,frame.right.position)<.001f);
            var finger=avatar.Rig.transform.Find("Left finger 1 segment 2");Vector3 world=frame.left.position+frame.left.rotation*(joints.Joint(6)*frame.scale);
            Check("Tracked finger geometry uses actual joint position",Vector3.Distance(finger.position,world)<.001f);
            avatar.ClearPoseOverride();
            // Exercise actual controller actions, rather than only the pose-injection hook.
            var left=InputSystem.AddDevice<XRController>();InputSystem.SetDeviceUsage(left,UnityEngine.InputSystem.CommonUsages.LeftHand);
            var right=InputSystem.AddDevice<XRController>();InputSystem.SetDeviceUsage(right,UnityEngine.InputSystem.CommonUsages.RightHand);
            var oldMode=player.controlMode;
            try
            {
                player.controlMode=FlyingHand.ControlMode.VR;player.RefreshMode();
                InputSystem.QueueDeltaStateEvent(left.devicePosition,new Vector3(-.25f,1.2f,.3f));InputSystem.QueueDeltaStateEvent(left.deviceRotation,Quaternion.identity);InputSystem.QueueDeltaStateEvent(left.isTracked,(byte)1);
                InputSystem.QueueDeltaStateEvent(right.devicePosition,new Vector3(.25f,1.2f,.3f));InputSystem.QueueDeltaStateEvent(right.deviceRotation,Quaternion.identity);InputSystem.QueueDeltaStateEvent(right.isTracked,(byte)1);
                yield return null;yield return null;avatar.RefreshNow();
                Vector3 controller=player.cameraOffset.TransformPoint(new Vector3(-.25f,1.2f,.3f)+avatar.Rig.style.controllerPalmOffset);
                Check("Controller device position drives visible hand",Vector3.Distance(avatar.Rig.LeftPalm.position,controller)<.003f,$"actual={avatar.Rig.LeftPalm.position}, expected={controller}");
            }
            finally{InputSystem.RemoveDevice(left);InputSystem.RemoveDevice(right);player.controlMode=oldMode;player.RefreshMode();}
            // Contact animation on a real collider, with no hand colliders or forces.
            var fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.name="Candy vault contact test";fixture.transform.position=new Vector3(120,11,120);fixture.transform.localScale=new Vector3(6,2,2);
            var savedPosition=player.transform.position;var savedRotation=player.transform.rotation;player.enabled=false;
            player.Teleport(new Vector3(120,11.65f,118.73f),Quaternion.identity);Physics.SyncTransforms();avatar.RefreshNow();
            bool started=player.TryStartVault();Check("Vault reaches actual crate ledge",started);
            if(started)
            {
                player.Simulate(Vector2.zero,false,player.vaultDuration*.3f);avatar.RefreshNow();
                player.TryGetVaultGrip(true,out var leftGrip);player.TryGetVaultGrip(false,out var rightGrip);
                Check("Both palms lock to direct ledge contacts",Vector3.Distance(avatar.Rig.LeftPalm.position,leftGrip.position)<.002f&&Vector3.Distance(avatar.Rig.RightPalm.position,rightGrip.position)<.002f);
                Check("Palm underside touches crate top",Mathf.Abs(leftGrip.position.y-.025f*player.playerScale-fixture.GetComponent<Collider>().bounds.max.y)<.003f);
                fixture.transform.position+=Vector3.right*.08f;Physics.SyncTransforms();avatar.RefreshNow();
                Check("Hand contact follows moving ledge",Vector3.Distance(avatar.Rig.LeftPalm.position,leftGrip.position+Vector3.right*.08f)<.003f);
                CaptureVault(player,avatar);
                for(int i=0;i<50;i++)player.Simulate(Vector2.zero,false,.02f);
                avatar.RefreshNow();Check("Vault completes and tracking is released",!player.Vaulting&&player.transform.position.y>11.98f);
            }
            Object.Destroy(fixture);player.Teleport(savedPosition,savedRotation);player.enabled=true;session.SetRole(false);session.SetMenu(true);
            CapturePortraits(session);
            yield return CastleReferenceValidation.Validate(session);yield return CastleTraversalValidation.Validate(session);yield return CastleInputValidation.Validate(session);
            foreach(var name in new[]{"ReferenceValidation.txt","TraversalValidation.txt","InputValidation.txt"})File.Copy("Assets/CastlePrototype/"+name,Results+name,true);
            File.WriteAllText(Results+"COMPLETE.txt","Candy avatar, tracking, vault and input verification finished. Hardware and two-headset network tests remain unverified.");
            Application.runInBackground=background;
            EditorApplication.isPlaying=false;
        }
        static void Render(Camera camera,string filename)
        {
            var texture=new RenderTexture(1500,1100,24);var old=RenderTexture.active;camera.targetTexture=texture;
            try{camera.Render();RenderTexture.active=texture;var png=new Texture2D(1500,1100,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1500,1100),0,0);png.Apply();File.WriteAllBytes(Results+filename+".png",png.EncodeToPNG());Object.DestroyImmediate(png);}
            finally{camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(texture);}
        }
        static void CaptureVault(CastleSurvivor player,CandyPlayerAvatar avatar)
        {
            var go=new GameObject("Temporary avatar evidence camera");var camera=go.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=55;
            try{camera.transform.position=player.transform.position+new Vector3(1.5f,1.2f,-1.7f);camera.transform.LookAt(avatar.Rig.LeftPalm.position+Vector3.down*.12f);Render(camera,"vault-contact-outside");Render(player.viewCamera,"vault-contact-first-person");}
            finally{Object.DestroyImmediate(go);}
        }
        static void CapturePortraits(CastlePlaySession session)
        {
            var root=new GameObject("Temporary candy portraits");var rig=Object.Instantiate(session.Player.GetComponent<CandyPlayerAvatar>().visualPrefab,root.transform);rig.SetColor(1);
            var basis=new Vector3(80,70,80);float s=.5f;
            var frame=new CandyAvatarFrame{feet=basis,head=basis+Vector3.up*1.6f*s,headRotation=Quaternion.identity,scale=s,
                left=new CandyHandPose{position=basis+new Vector3(-.3f,.95f,.12f)*s,rotation=Quaternion.Euler(0,0,-20),skeleton=CandyHandSkeleton.Rest(true,.55f)},
                right=new CandyHandPose{position=basis+new Vector3(.48f,1.64f,.06f)*s,rotation=Quaternion.Euler(-70,0,-15),skeleton=CandyHandSkeleton.Rest(false,.65f)}};
            rig.ApplyFrame(frame);var camera=new GameObject("Portrait camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=40;camera.nearClipPlane=.01f;
            try
            {
                camera.transform.position=basis+new Vector3(1.05f,.9f,1.7f);camera.transform.LookAt(basis+Vector3.up*.44f);Render(camera,"candy-player-character");
                var clones=new List<CandyPlayerRig>();
                for(int i=0;i<6;i++)
                {var clone=Object.Instantiate(rig,root.transform);clone.SetColor(i);var pose=frame;Vector3 offset=Vector3.right*(i-2.5f)*.62f;pose.feet+=offset;pose.head+=offset;pose.left.position+=offset;pose.right.position+=offset;clone.ApplyFrame(pose);clones.Add(clone);}
                rig.gameObject.SetActive(false);camera.transform.position=basis+new Vector3(0,1.4f,4.1f);camera.transform.LookAt(basis+Vector3.up*.44f);Render(camera,"candy-player-colors");
                Render(session.Player.viewCamera,"candy-player-first-person");
                var player=session.Player;var avatar=player.GetComponent<CandyPlayerAvatar>();var view=player.viewCamera;
                var saved=view.transform.localRotation;
                view.transform.localRotation=Quaternion.Euler(82,0,0);avatar.RefreshNow();Render(view,"candy-player-looking-down");view.transform.localRotation=saved;avatar.RefreshNow();
                // Isolated posed hand avoids the nearby crate obscuring the close-up.
                foreach(var clone in clones)clone.gameObject.SetActive(false);
                rig.gameObject.SetActive(true);var hand=rig.LeftPalm;camera.nearClipPlane=.003f;
                camera.transform.position=hand.position+hand.rotation*new Vector3(.19f,.23f,.20f)*s;camera.transform.LookAt(hand.position+hand.forward*.06f*s);camera.fieldOfView=38;Render(camera,"candy-player-smooth-hand");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(camera.gameObject);}
        }
    }
}
