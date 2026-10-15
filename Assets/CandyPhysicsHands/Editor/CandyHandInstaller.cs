using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using FlyingHand;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.CandyPlayers.Editor;
using DeadlyJenga.CastlePrototype;
using Object=UnityEngine.Object;
namespace DeadlyJenga.CandyPhysicsHands.Editor
{
    [InitializeOnLoad]
    public static class CandyHandInstaller
    {
        public const string Root="Assets/CandyPhysicsHands/",Output="ArtDirection/CandyPhysicsHands/";
        const string Request=Output+"install.request",Key="CandyContactHandTest";
        static readonly List<string> report=new List<string>();
        static CandyHandInstaller()
        {
            EditorApplication.update+=()=>
            {
                if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
                string command=File.ReadAllText(Request).Trim();File.Move(Request,Request+".consumed-"+DateTime.UtcNow.Ticks);
                try{if(command=="visual")RebuildGloveVisuals();else if(command=="sweep"){SessionState.SetBool(Key+"Sweep",true);EditorApplication.EnterPlaymode();}else Install();}catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"ERROR.txt",e.ToString());Debug.LogException(e);}
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key+"Sweep",false)){SessionState.SetBool(Key+"Sweep",false);EditorApplication.delayCall+=()=>{try{Sweep();}finally{EditorApplication.isPlaying=false;}};}
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CandyHandLab>().StartCoroutine(Checks());}
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key+"Demo",false)){SessionState.SetBool(Key+"Demo",false);EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(DemoChecks());}
                if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Restore",false))
                {SessionState.SetBool(Key+"Restore",false);EditorApplication.delayCall+=()=>{EditorSceneManager.OpenScene(SessionState.GetString(Key+"Scene","Assets/Scenes/Game.unity"));Export();if(Object.FindFirstObjectByType<CastlePlaySession>()){SessionState.SetBool(Key+"Demo",true);EditorApplication.EnterPlaymode();}};}
            };
        }
        public static CandyHandSettings Profile(string name,bool giant)
        {
            string path=Root+name+".asset";var settings=AssetDatabase.LoadAssetAtPath<CandyHandSettings>(path);
            if(!settings)
            {
                settings=ScriptableObject.CreateInstance<CandyHandSettings>();settings.skin=CandyPlayerBuilder.Style.material;
                if(!giant){settings.size=1;settings.palmMass=.3f;settings.fingerMass=.1f;settings.palmForce=20;settings.fingerForce=3;settings.palmTorque=1;settings.fingerTorque=.01f;settings.positionSpring=200;settings.positionDamping=2;settings.rotationSpring=10;settings.rotationDamping=.02f;settings.staticFriction=1.5f;settings.dynamicFriction=1.2f;settings.maxSpeed=1;settings.maxTargetSpeed=.6f;}
                AssetDatabase.CreateAsset(settings,path);
            }
            return settings;
        }
        static CandyPhysicalHand BuildVisual(CandyHandSettings settings,bool bench=false)
        {
            var go=new GameObject("Candy physical hand");var hand=go.AddComponent<CandyPhysicalHand>();hand.settings=settings;hand.visualBones=new Transform[16];
            var template=new Transform[44];var rest=bench?CandyHandLab.Pose(Vector3.zero,0).skeleton:CandyHandSkeleton.Rest(false);hand.visualReferenceLengths=new float[15];
            for(int i=0;i<44;i++)
            {
                var bone=new GameObject("Bind "+i).transform;bone.SetParent(go.transform,false);template[i]=bone;
                if(i>=29){int k=i-29,f=k/3,j=k%3;bone.localPosition=rest.Joint(f*4+j);bone.localRotation=CandyPhysicalHand.SegmentRotation(rest.Joint(f*4+j+1)-rest.Joint(f*4+j),Vector3.up);hand.visualBones[k+1]=bone;hand.visualReferenceLengths[k]=(rest.Joint(f*4+j+1)-rest.Joint(f*4+j)).magnitude;}
            }
            hand.visualBones[0]=template[7];
            // Keep enough space between digits for the voxel bake: otherwise a hairline web stretches into spikes.
            var mesh=CandySmoothHandMesh.Build(go.transform,template,1,bench?rest:(CandyHandSkeleton?)null,.0085f,.0035f,.03f);var weights=mesh.boneWeights;
            int Remap(int i)=>i==7?0:Mathf.Clamp(i-28,1,15);
            for(int i=0;i<weights.Length;i++){weights[i].boneIndex0=Remap(weights[i].boneIndex0);weights[i].boneIndex1=Remap(weights[i].boneIndex1);weights[i].boneIndex2=Remap(weights[i].boneIndex2);weights[i].boneIndex3=Remap(weights[i].boneIndex3);}mesh.boneWeights=weights;
            mesh.bindposes=hand.visualBones.Select(b=>b.worldToLocalMatrix*go.transform.localToWorldMatrix).ToArray();mesh.RecalculateBounds();
            foreach(var b in template)if(!hand.visualBones.Contains(b))Object.DestroyImmediate(b.gameObject);
            string meshPath=Root+(bench?"Calibrated Bench Hand.asset":"Continuous Hand.asset");var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!saved){AssetDatabase.CreateAsset(mesh,meshPath);saved=mesh;}
            else{saved.Clear();saved.indexFormat=mesh.indexFormat;saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.uv=mesh.uv;saved.boneWeights=mesh.boneWeights;saved.bindposes=mesh.bindposes;saved.triangles=mesh.triangles;saved.bounds=mesh.bounds;EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);}
            var skinGo=new GameObject("Seamless candy skin");skinGo.transform.SetParent(go.transform,false);hand.skin=skinGo.AddComponent<SkinnedMeshRenderer>();hand.skin.sharedMesh=saved;hand.skin.sharedMaterial=settings.skin;hand.skin.bones=hand.visualBones;hand.skin.rootBone=go.transform;hand.skin.updateWhenOffscreen=true;hand.skin.localBounds=new Bounds(Vector3.zero,Vector3.one*20);
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+(bench?"Candy Bench Hand.prefab":"Candy Physical Hand.prefab")).GetComponent<CandyPhysicalHand>();Object.DestroyImmediate(go);return prefab;
        }
        [MenuItem("Tools/Candy Hands/Rebuild Glove Visuals Only")]
        public static void RebuildGloveVisuals()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode before rebuilding visuals.");
            Directory.CreateDirectory(Output);
            CandyPlayerBuilder.RebuildVisual();
            var giant=AssetDatabase.LoadAssetAtPath<CandyHandSettings>(Root+"Flying Hand Settings.asset");
            var bench=AssetDatabase.LoadAssetAtPath<CandyHandSettings>(Root+"Bench Hand Settings.asset");
            if(!giant||!bench)throw new InvalidOperationException("Install the hand profiles before rebuilding their visuals.");
            var prefab=BuildVisual(giant);BuildVisual(bench,true);AssetDatabase.SaveAssets();
            var mesh=prefab.skin.sharedMesh;
            if(mesh.vertexCount==0||mesh.bounds.min.z>-.03f)throw new InvalidOperationException("The glove cuff did not bake completely.");
            foreach(var p in mesh.vertices)if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z))throw new InvalidOperationException("Invalid glove vertex.");
            GlovePreview(prefab,"glove-preview.png",new Vector3(.16f,.21f,-.19f));
            GlovePreview(prefab,"glove-palm.png",new Vector3(-.12f,-.24f,.19f));
            File.WriteAllText(Output+"GloveVisuals.txt",DateTime.Now+"\nPASS: player, giant and bench meshes saved.\nPASS: finite vertices and full cuff bounds.\nVertices: "+mesh.vertexCount+"\nVisual-only bake: tracking, colliders, joints, forces and profiles unchanged.\n");
        }
        static void GlovePreview(CandyPhysicalHand prefab,string filename,Vector3 view)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var baked=new Mesh();var go=Object.Instantiate(prefab.gameObject);preview.AddSingleGO(go);
                var hand=go.GetComponent<CandyPhysicalHand>();hand.skin.BakeMesh(baked);
                // Preview the saved skin without activating the physical rig.
                hand.skin.enabled=false;var visual=new GameObject("Glove preview");preview.AddSingleGO(visual);
                visual.AddComponent<MeshFilter>().sharedMesh=baked;visual.AddComponent<MeshRenderer>().sharedMaterial=prefab.skin.sharedMaterial;
                preview.camera.transform.position=view+new Vector3(0,0,.04f);preview.camera.transform.LookAt(new Vector3(0,0,.04f));
                preview.camera.nearClipPlane=.005f;preview.camera.farClipPlane=3;preview.camera.fieldOfView=36;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.11f,.16f,.24f);
                preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,-35,0);
                preview.lights[1].intensity=1.1f;preview.lights[1].transform.rotation=Quaternion.Euler(330,140,0);preview.ambientColor=new Color(.4f,.45f,.55f);
                preview.BeginStaticPreview(new Rect(0,0,900,900));preview.Render(true);var texture=preview.EndStaticPreview();
                File.WriteAllBytes(Output+filename,texture.EncodeToPNG());Object.DestroyImmediate(texture);Object.DestroyImmediate(baked);
            }
            finally{preview.Cleanup();}
        }
        [MenuItem("Tools/Candy Hands/Install And Test Contact Hand")]
        public static void Install()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
            if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene before installing the lab; it will be restored after tests.");
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            CandyPlayerBuilder.RebuildVisual();
            var giant=Profile("Flying Hand Settings",true);var labSettings=Profile("Bench Hand Settings",false);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"White candy hand.mat");if(!mat){mat=new Material(CandyPlayerBuilder.Style.material){name="White candy hand"};mat.SetColor("_BaseColor",new Color(.92f,.95f,1));AssetDatabase.CreateAsset(mat,Root+"White candy hand.mat");}
            giant.skin=mat;labSettings.skin=mat;EditorUtility.SetDirty(giant);EditorUtility.SetDirty(labSettings);var prefab=BuildVisual(giant);
            string actorPath="Assets/Prefabs/CastleFlyingHand.prefab";var actorGo=PrefabUtility.LoadPrefabContents(actorPath);
            try{var bridge=actorGo.GetComponent<CandyFlyingHandBridge>();if(!bridge)bridge=actorGo.AddComponent<CandyFlyingHandBridge>();bridge.actor=actorGo.GetComponent<CastleHandActor>();bridge.prefab=prefab;bridge.settings=giant;bridge.trackingStyle=CandyPlayerBuilder.Style;PrefabUtility.SaveAsPrefabAsset(actorGo,actorPath);}finally{PrefabUtility.UnloadPrefabContents(actorGo);}
            SessionState.SetString(Key+"Scene",SceneManager.GetActiveScene().path);
            SessionState.SetString(Key+"Hash",Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(SceneManager.GetActiveScene().path))));
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraGo=new GameObject("Bench Camera");var camera=cameraGo.AddComponent<Camera>();camera.transform.position=new Vector3(.23f,.56f,.34f);camera.transform.LookAt(new Vector3(0,.39f,.05f));camera.fieldOfView=48;camera.nearClipPlane=.005f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.22f,.35f);
            var sun=new GameObject("Soft key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(40,-25,0);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.3f,.37f,.45f);RenderSettings.ambientEquatorColor=new Color(.18f,.22f,.3f);RenderSettings.ambientGroundColor=new Color(.08f,.1f,.14f);
            var table=GameObject.CreatePrimitive(PrimitiveType.Cube);table.name="Static test bench";table.transform.position=new Vector3(0,.278f,.05f);table.transform.localScale=new Vector3(.6f,.1f,.6f);table.GetComponent<Renderer>().sharedMaterial=CandyPlayerBuilder.Style.material;
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name="Friction-only test block";block.transform.position=new Vector3(0,.35f-.007f,.072f);block.transform.localScale=new Vector3(.022f,.03f,.022f);block.GetComponent<Renderer>().sharedMaterial=CandyPlayerBuilder.Style.material;
            block.GetComponent<Collider>().contactOffset=.0005f;table.GetComponent<Collider>().contactOffset=.0005f;
            var rb=block.AddComponent<Rigidbody>();rb.mass=.05f;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.solverIterations=16;rb.solverVelocityIterations=8;
            Material BenchMaterial(string name,Color color,float smoothness)
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");if(!material){material=new Material(CandyPlayerBuilder.Style.material){name=name};material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",smoothness);AssetDatabase.CreateAsset(material,Root+name+".mat");}return material;
            }
            table.GetComponent<Renderer>().sharedMaterial=BenchMaterial("Slate bench",new Color(.15f,.23f,.34f),.15f);block.GetComponent<Renderer>().sharedMaterial=BenchMaterial("Pink test block",new Color(1,.08f,.28f),.78f);
            var lab=new GameObject("Candy Hand Lab").AddComponent<CandyHandLab>();lab.prefab=BuildVisual(labSettings,true);lab.trackedPrefab=prefab;lab.settings=labSettings;lab.view=camera;lab.block=rb;
            var trackingRig=new GameObject("Bench XR origin");trackingRig.transform.position=new Vector3(0,-.7f,0);var offset=new GameObject("Tracking offset").transform;offset.SetParent(trackingRig.transform,false);camera.transform.SetParent(offset,true);
            var origin=trackingRig.AddComponent<Unity.XR.CoreUtils.XROrigin>();origin.Camera=camera;origin.Origin=trackingRig;origin.CameraFloorOffsetObject=offset.gameObject;origin.RequestedTrackingOriginMode=Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;origin.CameraYOffset=0;origin.enabled=false;
            var driver=camera.gameObject.AddComponent<TrackedPoseDriver>();driver.positionInput=new InputActionProperty(new InputAction("Bench head position",InputActionType.Value,"<XRHMD>/centerEyePosition",expectedControlType:"Vector3"));driver.rotationInput=new InputActionProperty(new InputAction("Bench head rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation",expectedControlType:"Quaternion"));driver.trackingStateInput=new InputActionProperty(new InputAction("Bench head tracking",InputActionType.Value,"<XRHMD>/trackingState",expectedControlType:"Integer"));driver.updateType=TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;driver.enabled=false;
            lab.trackingOrigin=offset;lab.poseDriver=driver;lab.xrOrigin=origin;
            EditorSceneManager.SaveScene(scene,Root+"CandyHandContactLab.unity");AssetDatabase.SaveAssets();SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Restore",true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool ok,string description){report.Add((ok?"PASS ":"FAIL ")+description);File.WriteAllLines(Output+"Validation.txt",report);Debug.Log(report.Last());}
        static IEnumerator Checks()
        {
            report.Clear();Application.runInBackground=true;var lab=Object.FindFirstObjectByType<CandyHandLab>();lab.automaticTest=true;yield return null;var hand=lab.Hand;var settings=lab.settings;
            Check(hand.bodies.Length==16&&hand.GetComponentsInChildren<ConfigurableJoint>().Length==15,"16 dynamic hand bodies and 15 hand-only joints.");
            Check(lab.block.GetComponents<Joint>().Length==0,"Test block has no grab/attachment joints.");
            Check(hand.skin.sharedMesh&&hand.skin.sharedMesh.vertexCount>2000&&hand.skin.bones.Length==16,"Baked continuous candy skin follows all physical finger bones.");
            var canonical=lab.trackedPrefab.skin.sharedMesh;var cv=canonical.vertices;var cw=canonical.boneWeights;var ct=canonical.triangles;bool web=false;
            for(int t=0;t<ct.Length;t+=3)
            {
                int a=ct[t],b=ct[t+1],c=ct[t+2];
                if(Mathf.Min(cv[a].z,Mathf.Min(cv[b].z,cv[c].z))<.08f)continue;
                int Finger(int v)=>(cw[v].boneIndex0-1)/3;
                if(cw[a].boneIndex0>0&&cw[b].boneIndex0>0&&cw[c].boneIndex0>0&&(Finger(a)!=Finger(b)||Finger(a)!=Finger(c)))web=true;
            }
            Check(!web,"Open hand mesh has no triangles bridging neighbouring digits beyond knuckles.");
            var filter=new CandyHandTransition();var pose=CandyHandLab.Pose(lab.wrist,0);
            for(int i=0;i<40;i++)filter.Step(pose,CandyHandMode.HandTracking,false,.02f,settings);
            Check(filter.Strength>.99f,"Stable acquisition ramps to full bounded drive strength.");
            var jump=pose;jump.position+=Vector3.right*10;var previous=filter.Pose.position;filter.Step(jump,CandyHandMode.HandTracking,false,.02f,settings);
            Check(Vector3.Distance(previous,filter.Pose.position)<=settings.maxTargetSpeed*.02f+.0001f,"Tracking spike target displacement is speed limited.");
            for(int i=0;i<30;i++)filter.Step(default,CandyHandMode.Inactive,false,.02f,settings);
            Check(filter.Mode==CandyHandMode.Inactive&&filter.Strength==0,"Sustained tracking loss releases pressure and becomes inactive.");
            filter.Step(pose,CandyHandMode.HandTracking,false,.02f,settings);Check(filter.Strength==0,"One reacquired frame cannot reactivate contacts.");
            for(int i=0;i<40;i++)filter.Step(pose,CandyHandMode.Controller,false,.02f,settings);
            Check(filter.Mode==CandyHandMode.Controller&&filter.Strength>.99f,"Stable controller fallback returns through a strength ramp.");
            var trackedPose=pose;trackedPose.skeletonTracked=true;
            for(int i=0;i<12;i++)filter.Step(trackedPose,CandyHandMode.HandTracking,false,.02f,settings);
            Check(filter.Mode==CandyHandMode.HandTracking&&Vector3.Distance(filter.Pose.skeleton.Joint(7),trackedPose.skeleton.Joint(7))<.002f,"Controller-to-skeleton acquisition preserves tracked finger proportions.");
            var wrist=lab.wrist;hand.SetTarget(CandyHandLab.Pose(wrist,0),1);hand.SetInteractive(true);
            Check(hand.IsInteractive&&hand.skin.enabled,"Activated contact hand is visibly rendered.");
            File.WriteAllText(Output+"Activation.txt","Interactive="+hand.IsInteractive+" reason="+hand.ActivationReason);
            var probes=hand.bodies.Select(b=>b.gameObject.AddComponent<CandyHandContactProbe>()).ToArray();
            for(int i=0;i<40;i++)yield return new WaitForFixedUpdate();
            File.WriteAllLines(Output+"PhysicalPose.txt",hand.bodies.Select((b,i)=>i+" position "+b.position.ToString("F4")+" rotation "+b.rotation.eulerAngles.ToString("F2")));
            // Reset to the fixture's authored start, before the open hand's contact
            // settling can nudge it. Reusing the settled pose compounds that nudge.
            var blockStart=new Vector3(0,.343f,.072f);
            File.WriteAllText(Output+"Contacts.txt","");
            for(int i=0;i<90;i++)
            {
                hand.SetTarget(CandyHandLab.Pose(wrist,Mathf.Clamp01(i/60f)),1);yield return new WaitForFixedUpdate();
                if(i%10==0)File.AppendAllLines(Output+"Contacts.txt",probes.Select((p,n)=>i+" BODY "+n+" OTHER "+p.LastOther+" normal "+p.Normal.ToString("F3")+" impulse "+p.Impulse.ToString("F5")+" age "+(Time.fixedTime-p.At).ToString("F3")+" block "+lab.block.position.ToString("F4")));
            }
            Capture(lab,"contact-pinch.png");float beforeLift=lab.block.position.y;
            File.AppendAllLines(Output+"PhysicalPose.txt",hand.bodies.Select((b,i)=>"pinch "+i+" position "+b.position.ToString("F4")+" rotation "+b.rotation.eulerAngles.ToString("F2")));
            File.AppendAllText(Output+"PhysicalPose.txt","\nBLOCK PINCH "+lab.block.position.ToString("F4")+"\n");
            for(int i=0;i<120;i++){var lift=wrist+Vector3.up*Mathf.Min(.12f,i*.001f);hand.SetTarget(CandyHandLab.Pose(lift,1),1);yield return new WaitForFixedUpdate();}
            float lifted=lab.block.position.y-beforeLift;
            Check(lifted>.04f,"Contact/friction-only pinch lifts block; observed rise "+lifted.ToString("F4")+"m.");Capture(lab,"contact-lift.png");
            Check(hand.MaximumAppliedForce<=Mathf.Max(settings.palmForce,settings.fingerForce)+.001f,"Drive forces stay within configured caps.");
            Check(lab.block.GetComponents<Joint>().Length==0&&lab.block.transform.parent==null,"Lift leaves object unattached and unparented.");
            float releaseStart=lab.block.position.y;
            for(int i=0;i<90;i++){hand.SetTarget(CandyHandLab.Pose(wrist+Vector3.up*.12f,0),1);yield return new WaitForFixedUpdate();}
            Check(lab.block.position.y<releaseStart-.04f,"Opening fingers releases block back to bench.");Capture(lab,"contact-release.png");
            Check(hand.bodies.All(b=>!float.IsNaN(b.position.x)&&b.linearVelocity.magnitude<settings.maxSpeed+.1f),"Physics remains finite and velocity bounded.");
            // Recreate only the isolated test fixture; object pose writes never occur in the interaction runtime.
            void ResetFixture()
            {
                // Independent trials need fresh constraints, not cached impulses from
                // the previous grip after teleporting the fixture back onto the table.
                hand.SetInteractive(false);hand.RebuildInactive(CandyHandLab.Pose(wrist,0));hand.SetTarget(CandyHandLab.Pose(wrist,0),1);
                var previousBlock=lab.block.gameObject;previousBlock.SetActive(false);
                var freshBlock=Object.Instantiate(previousBlock,blockStart,Quaternion.identity);freshBlock.name="Fresh friction-only test block";
                lab.block=freshBlock.GetComponent<Rigidbody>();freshBlock.SetActive(true);lab.block.linearVelocity=Vector3.zero;lab.block.angularVelocity=Vector3.zero;
                Object.Destroy(previousBlock);Physics.SyncTransforms();hand.SetInteractive(true);
            }
            ResetFixture();for(int i=0;i<40;i++)yield return new WaitForFixedUpdate();
            for(int i=0;i<90;i++){hand.SetTarget(CandyHandLab.Pose(wrist,Mathf.Clamp01(i/60f)),1);yield return new WaitForFixedUpdate();}
            for(int i=0;i<120;i++){hand.SetTarget(CandyHandLab.Pose(wrist+Vector3.up*Mathf.Min(.12f,i*.001f),1),1);yield return new WaitForFixedUpdate();}
            float heldHeight=lab.block.position.y;var lost=new CandyHandTransition();var heldPose=CandyHandLab.Pose(wrist+Vector3.up*.12f,1);
            for(int i=0;i<40;i++)lost.Step(heldPose,CandyHandMode.HandTracking,false,.02f,settings);
            for(int i=0;i<50;i++){lost.Step(default,CandyHandMode.Inactive,false,.02f,settings);hand.SetTarget(lost.Pose,lost.Strength);hand.SetInteractive(lost.Strength>0);yield return new WaitForFixedUpdate();}
            File.WriteAllText(Output+"TrackingLoss.txt","Held height: "+heldHeight+"; released height: "+lab.block.position.y+"; active: "+hand.IsInteractive+"; strength: "+lost.Strength);
            Check(!hand.IsInteractive&&lost.Strength==0&&lab.block.position.y<heldHeight-.04f,"Tracking loss releases an actual held object and disables all contacts.");
            var slippery=Object.Instantiate(settings);slippery.staticFriction=slippery.dynamicFriction=0;hand.settings=slippery;
            var blockCollider=lab.block.GetComponent<Collider>();var oldMaterial=blockCollider.sharedMaterial;var noFriction=new PhysicsMaterial("Zero-friction comparison"){staticFriction=0,dynamicFriction=0,frictionCombine=PhysicsMaterialCombine.Multiply};blockCollider.sharedMaterial=noFriction;
            ResetFixture();blockCollider=lab.block.GetComponent<Collider>();for(int i=0;i<40;i++)yield return new WaitForFixedUpdate();
            for(int i=0;i<90;i++){hand.SetTarget(CandyHandLab.Pose(wrist,Mathf.Clamp01(i/60f)),1);yield return new WaitForFixedUpdate();}
            float slipperyY=lab.block.position.y;
            for(int i=0;i<120;i++){hand.SetTarget(CandyHandLab.Pose(wrist+Vector3.up*Mathf.Min(.12f,i*.001f),1),1);yield return new WaitForFixedUpdate();}
            Check(lab.block.position.y-slipperyY<.02f,"Zero-friction comparison slips instead of silently attaching.");
            hand.SetInteractive(false);hand.settings=settings;blockCollider.sharedMaterial=oldMaterial;Object.Destroy(slippery);Object.Destroy(noFriction);
            hand.ResetInactive(CandyHandLab.Pose(lab.block.position,0));Physics.SyncTransforms();hand.SetInteractive(true);Check(!hand.IsInteractive&&hand.ActivationBlocked,"Reactivation inside an object is blocked.");
            hand.SetInteractive(false);var scaled=Object.Instantiate(settings);scaled.size=2;hand.settings=scaled;hand.RebuildInactive(CandyHandLab.Pose(new Vector3(4,2,4),0));
            Check(Mathf.Abs(hand.bodies[0].GetComponent<BoxCollider>().size.x-.156f)<.001f&&scaled.palmForce==settings.palmForce&&scaled.fingerTorque==settings.fingerTorque,"Inactive resize doubles geometry without changing strength.");Object.Destroy(scaled);
            Check(Mathf.Abs(Time.fixedDeltaTime-.02f)<.00001f,"Project physics timestep remains unchanged at 50 Hz.");
            File.WriteAllText(Output+"COMPLETE.txt",report.Count(x=>x.StartsWith("PASS"))+" passed, "+report.Count(x=>x.StartsWith("FAIL"))+" failed. Synthetic input; real headset validation still required.");EditorApplication.isPlaying=false;
        }
        static void Capture(CandyHandLab lab,string name)
        {
            var property=new MaterialPropertyBlock();property.SetColor("_BaseColor",new Color(.22f,.3f,.4f));Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).First(r=>r.name=="Static test bench").SetPropertyBlock(property);property.SetColor("_BaseColor",new Color(1,.12f,.4f));lab.block.GetComponent<Renderer>().SetPropertyBlock(property);property.SetColor("_BaseColor",new Color(.9f,.92f,1));lab.Hand.skin.SetPropertyBlock(property);
            var target=new RenderTexture(1200,900,24);var previous=RenderTexture.active;lab.view.targetTexture=target;lab.view.Render();RenderTexture.active=target;var texture=new Texture2D(1200,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,900),0,0);texture.Apply();File.WriteAllBytes(Output+name,texture.EncodeToPNG());lab.view.targetTexture=null;RenderTexture.active=previous;Object.Destroy(texture);Object.Destroy(target);
        }
        [MenuItem("Tools/Candy Hands/Export Module")]
        public static void Export(){Directory.CreateDirectory(Output);AssetDatabase.ExportPackage(new[]{Root.TrimEnd('/'),"Assets/Prefabs/CastleFlyingHand.prefab","Assets/Scripts/CastlePlaySession.cs","Assets/CandyPlayers/CandySmoothHandMesh.cs","Assets/CandyPlayers/CandyHandSkeleton.cs","Assets/CandyPlayers/CandyTrackingSource.cs","Assets/CandyPlayers/CandyPlayerRig.cs","Assets/CandyPlayers/Editor/CandyPlayerBuilder.cs","Assets/CandyPlayers/Resources/CandyPlayerVisual.prefab"},Output+"CandyPhysicsHands.unitypackage",ExportPackageOptions.IncludeDependencies|ExportPackageOptions.Recurse);}
        static IEnumerator DemoChecks()
        {
            report.Clear();report.AddRange(File.ReadAllLines(Output+"Validation.txt"));Application.runInBackground=true;yield return new WaitForSeconds(.5f);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetRole(true);session.Hand.motor.ControlModeOverride=ControlMode.Desktop;session.SetMenu(false);yield return new WaitForSeconds(1);
            var bridge=session.Hand.GetComponent<CandyFlyingHandBridge>();var hand=bridge.PhysicalHand;
            Check(bridge.Mode==CandyHandMode.Desktop&&hand&&hand.IsInteractive,"Castle flying-hand role automatically uses the smooth desktop pointer.");
            Check(hand&&session.Hand.follow.target==session.Hand.motor.transform.Find("CameraFollowTarget")&&hand.skin.bounds.Contains(hand.bodies[0].position),"View follows flight independently of wrist rotation; bounds contain physical hand.");
            Check(session.Hand.motor.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Original rigid pointer colliders are disabled while the new rig owns contact.");
            session.SetMenu(true);yield return new WaitForSeconds(.7f);Check(hand&&!hand.IsInteractive,"Opening the demo menu releases physical hand contacts.");
            session.SetMenu(false);yield return new WaitForSeconds(.7f);Check(hand&&hand.IsInteractive,"Closing the menu smoothly reacquires the desktop pointer.");
            CaptureCamera(session.Hand.View,"castle-candy-pointer.png");
            session.Hand.motor.ControlModeOverride=ControlMode.VR;yield return new WaitForSeconds(.7f);Check(bridge.Mode==CandyHandMode.Inactive&&!hand.IsInteractive,"Headset mode with no tracked source has no active unknown-position hand.");
            var device=InputSystem.AddDevice<XRController>();InputSystem.SetDeviceUsage(device,"RightHand");InputSystem.QueueDeltaStateEvent(device.isTracked,(byte)1);InputSystem.QueueDeltaStateEvent(device.devicePosition,new Vector3(.25f,1.1f,.35f));InputSystem.QueueDeltaStateEvent(device.deviceRotation,Quaternion.identity);
            yield return new WaitForSeconds(1);Check(bridge.Mode==CandyHandMode.Controller&&hand.IsInteractive,"A simulated tracked controller activates the pointing fallback.");
            InputSystem.QueueDeltaStateEvent(device.isTracked,(byte)0);yield return new WaitForSeconds(.7f);Check(!hand.IsInteractive,"Controller tracking loss removes contact without preserving a frozen grip.");InputSystem.RemoveDevice(device);
            session.SetRole(false);yield return null;
            Check(session.Player.GetComponentsInChildren<CandyPhysicalHand>().Length==0,"Ordinary player hands remain visual-only and cannot push castle blocks.");
            var isolated=Object.Instantiate(bridge.prefab);isolated.settings=bridge.settings;
            var openPose=new CandyHandPose{tracked=true,skeletonTracked=true,position=new Vector3(0,1000,0),rotation=Quaternion.identity,skeleton=CandyHandSkeleton.Rest(false)};
            isolated.Build(openPose);isolated.SetTarget(openPose,1);isolated.SetInteractive(true);
            var bent=openPose;bent.rotation=Quaternion.Euler(15,35,12);
            for(int f=0;f<5;f++)
            {var start=bent.skeleton.Joint(f*4);for(int j=1;j<4;j++)bent.skeleton.SetJoint(f*4+j,start+Quaternion.Euler(35,0,0)*(openPose.skeleton.Joint(f*4+j)-start));}
            isolated.SetTarget(bent,1);yield return new WaitForSeconds(2);
            float worstPosition=0,worstAngle=0;
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)
            {var rb=isolated.bodies[1+f*3+j];var a=bent.skeleton.Joint(f*4+j);var b=bent.skeleton.Joint(f*4+j+1);worstPosition=Mathf.Max(worstPosition,Vector3.Distance(rb.position,bent.position+bent.rotation*a*isolated.GeometrySize));worstAngle=Mathf.Max(worstAngle,Quaternion.Angle(rb.rotation,CandyPhysicalHand.SegmentRotation(bent.rotation*(b-a),bent.rotation*Vector3.up)));}
            File.WriteAllText(Output+"GiantTracking.txt","Worst joint position error: "+worstPosition+" m; angle: "+worstAngle+" deg. Synthetic 20x articulated hand, clear space.");
            Check(worstPosition<.005f*isolated.GeometrySize&&worstAngle<3,"Giant hand follows rotated articulated skeleton in clear space (20x scale).");
            var proofCamera=new GameObject("Temporary hand proof camera").AddComponent<Camera>();proofCamera.fieldOfView=40;proofCamera.nearClipPlane=.01f;proofCamera.clearFlags=CameraClearFlags.SolidColor;proofCamera.backgroundColor=new Color(.08f,.15f,.24f);
            var look=bent.position+bent.rotation*(Vector3.forward*.06f*isolated.GeometrySize);proofCamera.transform.position=look+bent.rotation*(new Vector3(.18f,.22f,.20f)*isolated.GeometrySize);proofCamera.transform.LookAt(look);CaptureCamera(proofCamera,"giant-tracked-pose.png");Object.Destroy(proofCamera.gameObject);
            float movingPosition=0,movingAngle=0;
            for(int i=0;i<50;i++)
            {
                var moving=bent;float phase=i*.02f;
                moving.position+=Vector3.right*(.7f*Mathf.Sin(phase*2));moving.rotation=Quaternion.Euler(15,35+20*Mathf.Sin(phase*2),12);
                isolated.SetTarget(moving,1);yield return new WaitForFixedUpdate();
                movingPosition=Mathf.Max(movingPosition,Vector3.Distance(isolated.bodies[0].position,moving.position));movingAngle=Mathf.Max(movingAngle,Quaternion.Angle(isolated.bodies[0].rotation,moving.rotation));
            }
            File.AppendAllText(Output+"GiantTracking.txt","\nMoving wrist worst position error: "+movingPosition+" m; angle: "+movingAngle+" deg.");
            Check(movingPosition<.02f*isolated.GeometrySize&&movingAngle<12,"Giant wrist follows continuous translation and rotation without excessive lag.");
            Object.Destroy(isolated.gameObject);
            string scenePath=SessionState.GetString(Key+"Scene","");Check(File.Exists(scenePath)&&Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(scenePath)))==SessionState.GetString(Key+"Hash",""),"Saved castle scene and block configuration are byte-for-byte unchanged.");
            File.WriteAllText(Output+"COMPLETE.txt",report.Count(x=>x.StartsWith("PASS"))+" passed, "+report.Count(x=>x.StartsWith("FAIL"))+" failed. Synthetic input and native physics; real headset validation still required.");EditorApplication.isPlaying=false;
        }
        static void CaptureCamera(Camera camera,string name)
        {
            var target=new RenderTexture(1400,1000,24);var previous=RenderTexture.active;camera.targetTexture=target;camera.Render();RenderTexture.active=target;var texture=new Texture2D(1400,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1400,1000),0,0);texture.Apply();File.WriteAllBytes(Output+name,texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=previous;Object.Destroy(texture);Object.Destroy(target);
        }
        static void Sweep()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"Sweep.csv","torque,fingerMass,blockZ,rise,blockEndX,blockEndZ\n");
            var scene=SceneManager.CreateScene("Isolated hand calibration",new CreateSceneParameters(LocalPhysicsMode.Physics3D));var physics=scene.GetPhysicsScene();
            try
            {
                foreach(float torque in new[]{.001f,.002f,.005f,.01f,.02f})foreach(float mass in new[]{.015f,.1f})foreach(float z in new[]{.069f,.072f,.075f})
                {
                    var profile=Object.Instantiate(Profile("Bench Hand Settings",false));profile.fingerTorque=torque;profile.fingerMass=mass;
                    var go=new GameObject("Sweep hand");SceneManager.MoveGameObjectToScene(go,scene);var hand=go.AddComponent<CandyPhysicalHand>();hand.settings=profile;var wrist=new Vector3(0,.35f,0);hand.Build(CandyHandLab.Pose(wrist,0));hand.SetTarget(CandyHandLab.Pose(wrist,0),1);hand.SetInteractive(true);
                    var table=new GameObject("Sweep table");SceneManager.MoveGameObjectToScene(table,scene);var tableCol=table.AddComponent<BoxCollider>();tableCol.size=new Vector3(.6f,.1f,.6f);table.transform.position=new Vector3(0,.278f,.05f);tableCol.contactOffset=.0005f;
                    var block=new GameObject("Sweep block");SceneManager.MoveGameObjectToScene(block,scene);var col=block.AddComponent<BoxCollider>();col.size=new Vector3(.022f,.03f,.022f);col.contactOffset=.0005f;block.transform.position=new Vector3(0,.343f,z);var rb=block.AddComponent<Rigidbody>();rb.mass=.05f;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.solverIterations=16;rb.solverVelocityIterations=8;
                    hand.SetInteractive(false);Physics.SyncTransforms();hand.SetInteractive(true);
                    for(int i=0;i<40;i++){hand.StepDrives(.02f);physics.Simulate(.02f);}
                    for(int i=0;i<90;i++){hand.SetTarget(CandyHandLab.Pose(wrist,Mathf.Clamp01(i/60f)),1);hand.StepDrives(.02f);physics.Simulate(.02f);}
                    float y=rb.position.y;
                    for(int i=0;i<120;i++){hand.SetTarget(CandyHandLab.Pose(wrist+Vector3.up*Mathf.Min(.12f,i*.001f),1),1);hand.StepDrives(.02f);physics.Simulate(.02f);}
                    File.AppendAllText(Output+"Sweep.csv",torque+","+mass+","+z+","+(rb.position.y-y)+","+rb.position.x+","+rb.position.z+"\n");
                    Object.DestroyImmediate(go);Object.DestroyImmediate(table);Object.DestroyImmediate(block);Object.DestroyImmediate(profile);
                }
            }
            finally{SceneManager.UnloadSceneAsync(scene);}
            Debug.Log("Isolated native hand sweep complete: "+Output+"Sweep.csv");
        }
    }
}
