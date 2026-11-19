using System;
using System.IO;
using System.Reflection;
using DeadlyJenga.CandyPlayers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class WristSeamChecks
    {
        static string Output=>Path.GetFullPath("Logs/Validation/WristSeams");
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static double pollAt;
        static int assertions;
        const string SmokeKey="WristSeamSmoke";
        static double smokeStarted;
        [Serializable] sealed class SceneList { public SceneSetup[] scenes; }
        static WristSeamChecks(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=State;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<pollAt)return;
            pollAt=EditorApplication.timeSinceStartup+1;
            if(EditorApplication.isPlaying&&SessionState.GetBool(SmokeKey,false))
            {
                if(smokeStarted<=0||EditorApplication.timeSinceStartup-smokeStarted<8)return;
                try
                {
                    var avatar=Object.FindFirstObjectByType<CandyPlayerAvatar>();
                    Check(avatar&&avatar.Rig&&avatar.Rig.bodyRenderer,"Game.unity creates the production avatar in Play Mode");
                    Check(avatar.Rig.bodyRenderer.sharedMesh.name.Contains("blended wrists"),"Live avatar uses corrected wrist mesh");
                    if(avatar.viewCamera)Capture(avatar.viewCamera,"Game-first-person.png");
                }
                catch(Exception error){SessionState.SetBool(SmokeKey+"Failed",true);File.AppendAllText(Path.Combine(Output,"report.txt"),error+"\n");}
                finally{EditorApplication.ExitPlaymode();}
                return;
            }
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string request=Path.GetFullPath("Logs/WristSeams.request");
            if(!File.Exists(request))return;
            string text=File.ReadAllText(request).Trim(),pid=System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
            if(text!=pid&&text!=pid+" smoke")return;
            File.Delete(request);
            if(text.EndsWith(" smoke"))RunSmoke();else Run();
        }
        static void RunSmoke()
        {
            foreach(var scene in EditorSceneManager.GetSceneManagerSetup())
                if(EditorSceneManager.GetSceneByPath(scene.path).isDirty)throw new InvalidOperationException("Save scene edits first.");
            SessionState.SetString(SmokeKey+"Scenes",JsonUtility.ToJson(new SceneList{scenes=EditorSceneManager.GetSceneManagerSetup()}));
            SessionState.SetBool(SmokeKey,true);SessionState.SetBool(SmokeKey+"Failed",false);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(SmokeKey,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){smokeStarted=EditorApplication.timeSinceStartup;Application.logMessageReceived+=OnLog;}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived-=OnLog;SessionState.SetBool(SmokeKey,false);
                File.AppendAllText(Path.Combine(Output,"report.txt"),"GAME SMOKE "+(SessionState.GetBool(SmokeKey+"Failed",false)?"FAIL":"PASS")+"; returned to Edit Mode\n");
                var saved=JsonUtility.FromJson<SceneList>(SessionState.GetString(SmokeKey+"Scenes",""));
                if(saved?.scenes!=null)EditorSceneManager.RestoreSceneManagerSetup(saved.scenes);
            }
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            SessionState.SetBool(SmokeKey+"Failed",true);File.AppendAllText(Path.Combine(Output,"report.txt"),"RUNTIME ERROR "+message+"\n"+stack+"\n");
        }
        static void Check(bool pass,string label)
        {
            File.AppendAllText(Path.Combine(Output,"report.txt"),(pass?"PASS ":"FAIL ")+label+"\n");
            if(!pass)throw new InvalidOperationException(label);
            assertions++;
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check player wrist seams")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Wrist checks need Edit Mode.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene edits first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(Output);assertions=0;
            File.WriteAllText(Path.Combine(Output,"report.txt"),"Native Unity production rig and mesh, controlled poses. Not gameplay input or headset evidence.\n");
            var scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            try
            {
                var prefab=Resources.Load<CandyPlayerRig>("CandyPlayerVisual");
                var original=prefab.bodyRenderer.sharedMesh;
                var rig=Object.Instantiate(prefab);rig.SetColor(2);
                var prepare=typeof(CandyPlayerRig).GetMethod("BlendWristMesh",Private);
                var fixedMesh=(Mesh)prepare.Invoke(null,new object[]{original});
                rig.bodyRenderer.sharedMesh=fixedMesh;
                Check(fixedMesh.vertexCount==original.vertexCount&&fixedMesh.triangles.Length==original.triangles.Length&&fixedMesh.bindposes.Length==original.bindposes.Length,"No added vertices, triangles or bone bindings");
                Check(ReferenceEquals(fixedMesh,prepare.Invoke(null,new object[]{original})),"Wrist mesh is shared across avatars");
                var originalPoints=original.vertices;var fixedPoints=fixedMesh.vertices;
                var oldWeights=original.boneWeights;var newWeights=fixedMesh.boneWeights;
                int changed=0;
                for(int i=0;i<fixedPoints.Length;i++)
                {
                    if(originalPoints[i]!=fixedPoints[i])changed++;
                    var w=oldWeights[i];
                    bool digit=w.weight0>0&&w.boneIndex0>=14||w.weight1>0&&w.boneIndex1>=14||w.weight2>0&&w.boneIndex2>=14||w.weight3>0&&w.boneIndex3>=14;
                    if(digit&&(originalPoints[i]!=fixedPoints[i]||!w.Equals(newWeights[i])))throw new InvalidOperationException("Finger changed during wrist repair");
                }
                Check(changed>0&&changed<700,"Only distal forearm geometry changed: "+changed+" vertices; fingers preserved");
                var light=new GameObject("Wrist studio sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.1f;
                light.transform.rotation=Quaternion.Euler(30,-35,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.50f,.58f);
                var camera=new GameObject("Wrist studio eye").AddComponent<Camera>();camera.enabled=false;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.11f,.17f);
                camera.transform.position=new Vector3(0,1.6f,0);camera.transform.rotation=Quaternion.identity;
                camera.nearClipPlane=.03f;camera.farClipPlane=20;camera.fieldOfView=66;
                camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
                rig.ownerCamera=camera;
                var baked=new Mesh();
                try
                {
                    foreach(string name in new[]{"Desktop-relaxed","Desktop-fist","VR-forward","VR-bent","VR-rolled"})
                    {
                        var frame=Pose(name);
                        camera.transform.LookAt((frame.left.position+frame.right.position)*.5f);
                        rig.bodyRenderer.sharedMesh=fixedMesh;rig.ApplyFrame(frame,true,0);
                        Check(Vector3.Distance(rig.LeftPalm.position,frame.left.position)<.00001f&&Vector3.Distance(rig.RightPalm.position,frame.right.position)<.00001f,
                            name+" both hand anchors preserved");
                        Check(Quaternion.Angle(rig.LeftPalm.rotation,frame.left.rotation)<.01f&&Quaternion.Angle(rig.RightPalm.rotation,frame.right.rotation)<.01f,
                            name+" both hand rotations preserved");
                        rig.bodyRenderer.BakeMesh(baked);
                        foreach(var p in baked.vertices)if(!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z))throw new InvalidOperationException("Invalid skin vertex");
                        Check(true,name+" finite native skin deformation");
                        CapturePose(rig,camera,name+"-fixed.png");
                        if(name=="Desktop-relaxed"||name=="VR-bent")
                        {
                            rig.bodyRenderer.sharedMesh=original;LegacyArms(rig,frame);
                            CapturePose(rig,camera,name+"-original.png");
                        }
                    }
                }
                finally{Object.DestroyImmediate(baked);}
                File.AppendAllText(Path.Combine(Output,"report.txt"),"COMPLETE PASS "+assertions+" assertions; Characters MVID "+typeof(CandyPlayerRig).Assembly.ManifestModule.ModuleVersionId+"\n");
            }
            catch(Exception error){File.AppendAllText(Path.Combine(Output,"report.txt"),"FAIL "+error+"\n");throw;}
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
        static CandyAvatarFrame Pose(string name)
        {
            var frame=new CandyAvatarFrame{head=Vector3.up*1.6f,headRotation=Quaternion.identity,scale=1};
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;
                bool desktop=name.StartsWith("Desktop");
                var hand=new CandyHandPose{tracked=!desktop,skeletonTracked=true,
                    position=frame.head+new Vector3(sign*(desktop?.23f:.25f),desktop?-.22f:-.30f,desktop?.50f:.44f),
                    rotation=desktop?Quaternion.Euler(38,-sign*5,-sign*67):Quaternion.Euler(name=="VR-bent"?65:0,sign*8,name=="VR-rolled"?sign*115:0),
                    skeleton=CandyHandSkeleton.Rest(side==0,name=="Desktop-fist"?.95f:.38f)};
                if(side==0)frame.left=hand;else frame.right=hand;
            }
            return frame;
        }
        static void LegacyArms(CandyPlayerRig rig,CandyAvatarFrame frame)
        {
            var joint=typeof(CandyPlayerRig).GetMethod("SolveJoint",Private);
            var segment=typeof(CandyPlayerRig).GetMethod("Segment",Private);
            for(int side=0;side<2;side++)
            {
                float sign=side==0?-1:1;int upper=side==0?2:5;
                var hand=side==0?frame.left:frame.right;
                var shoulder=new Vector3(sign*.21f,1.255f,0);
                var pole=Vector3.right*sign+Vector3.forward*Mathf.Clamp(hand.position.z*2,-.45f,.75f)+Vector3.down*.25f;
                var elbow=(Vector3)joint.Invoke(null,new object[]{shoulder,hand.position,.29f,.27f,pole});
                segment.Invoke(rig,new object[]{upper,shoulder,elbow,null});
                segment.Invoke(rig,new object[]{upper+1,elbow,hand.position,null});
            }
        }
        static void Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1400,900,24);var prior=RenderTexture.active;camera.targetTexture=target;
            try
            {
                camera.Render();RenderTexture.active=target;
                var image=new Texture2D(1400,900,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1400,900),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally{camera.targetTexture=null;RenderTexture.active=prior;target.Release();Object.DestroyImmediate(target);}
        }
        static void CapturePose(CandyPlayerRig rig,Camera camera,string name)
        {
            // Multiple poses in one editor tick otherwise reuse the previous GPU skinning result.
            var mesh=new Mesh();rig.bodyRenderer.BakeMesh(mesh);
            var frozen=new GameObject("Native baked pose",typeof(MeshFilter),typeof(MeshRenderer));
            frozen.transform.SetPositionAndRotation(rig.bodyRenderer.transform.position,rig.bodyRenderer.transform.rotation);
            frozen.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=frozen.GetComponent<MeshRenderer>();renderer.sharedMaterial=rig.bodyRenderer.sharedMaterial;
            var properties=new MaterialPropertyBlock();rig.bodyRenderer.GetPropertyBlock(properties);renderer.SetPropertyBlock(properties);
            rig.gameObject.SetActive(false);
            try{Capture(camera,name);}
            finally{Object.DestroyImmediate(frozen);Object.DestroyImmediate(mesh);rig.gameObject.SetActive(true);}
        }
    }
}
