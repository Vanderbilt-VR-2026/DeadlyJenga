using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastlePrototype.Editor;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CandyArt.Editor
{
    [InitializeOnLoad]
    public static class CandyArtVerification
    {
        const string Request="ArtDirection/CandyTropical/run-art-pass.request";
        const string Output="ArtDirection/CandyTropical/Results/";
        const string PlayKey="CandyArtPlayVerification";
        static CandyArtVerification()
        {
            EditorApplication.update+=CheckRequest;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(PlayKey,false))
                {SessionState.SetBool(PlayKey,false);EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(PlayChecks());}
            };
        }
        static void CheckRequest()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            string command=File.ReadAllText(Request).Trim();
            File.Move(Request,Request+".consumed-"+DateTime.UtcNow.Ticks);
            try
            {
                if(command=="apply-and-verify")ApplyAndVerify();
                else if(command=="capture-and-export"){Capture();CandyArtBuilder.Export();}
                else if(command=="finish-art-pass")
                {
                    var p=CandyArtBuilder.Profile;p.water.SetColor("_Deep",new Color(.015f,.4f,.68f,1));p.water.SetFloat("_Glints",.28f);
                    EditorUtility.SetDirty(p.water);AssetDatabase.SaveAssets();Capture();CandyArtBuilder.Export();
                }
                else throw new InvalidOperationException("Unknown art-pass request.");
            }
            catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"ERROR.txt",e.ToString());Debug.LogException(e);}
        }
        sealed class PhysicsState
        {
            public CastleBlock block;public Vector3 position,scale,center,size,velocity;public Quaternion rotation;
            public float mass;public PhysicsMaterial material;public bool gravity,kinematic;public int layer;
            public PhysicsState(CastleBlock b)
            {
                block=b;var t=b.transform;var rb=b.GetComponent<Rigidbody>();var box=b.GetComponent<BoxCollider>();
                position=t.position;rotation=t.rotation;scale=t.localScale;center=box.center;size=box.size;mass=rb.mass;material=box.sharedMaterial;
                gravity=rb.useGravity;kinematic=rb.isKinematic;layer=b.gameObject.layer;velocity=rb.linearVelocity;
            }
            public bool Matches()
            {
                var rb=block.GetComponent<Rigidbody>();var box=block.GetComponent<BoxCollider>();var t=block.transform;
                return position==t.position&&rotation==t.rotation&&scale==t.localScale&&center==box.center&&size==box.size&&mass==rb.mass&&material==box.sharedMaterial&&gravity==rb.useGravity&&kinematic==rb.isKinematic&&layer==block.gameObject.layer&&velocity==rb.linearVelocity;
            }
        }
        [MenuItem("Tools/Candy Tropical/5 Apply And Verify Art Pass")]
        public static void ApplyAndVerify()
        {
            if(SceneManager.GetActiveScene().path!="Assets/Scenes/Game.unity")
                throw new InvalidOperationException("Open the Game scene. Verification never opens or replaces a different scene.");
            Directory.CreateDirectory(Output);
            var generator=Object.FindFirstObjectByType<CastleGenerator>();
            var before=generator.GetComponentsInChildren<CastleBlock>(true).Select(b=>new PhysicsState(b)).ToArray();
            // Restore/reapply is tested before the final application. The baseline retains current author edits.
            CandyArtBuilder.Apply();
            var results=new List<string>{(before.All(s=>s.Matches())?"PASS":"FAIL")+" Apply preserved all "+before.Length+" crate transforms, Rigidbody settings, velocities, collider shapes and physics materials."};
            CandyArtBuilder.Apply();
            results.Add((Object.FindObjectsByType<CandyArtBaseline>(FindObjectsSortMode.None).Length==1?"PASS":"FAIL")+" Repeated Apply creates one installation.");
            var baseline=Object.FindFirstObjectByType<CandyArtBaseline>();var originalPrefab=baseline.blockPrefab;var originalSky=baseline.sky;
            var originalMaterials=new[]{baseline.red,baseline.green,baseline.purple};
            CandyArtBuilder.Restore();
            results.Add((generator.blockPrefab==originalPrefab&&RenderSettings.skybox==originalSky&&generator.wallMaterial==originalMaterials[0]&&generator.cornerMaterial==originalMaterials[1]&&generator.keepMaterial==originalMaterials[2]&&before.All(s=>s.Matches())?"PASS":"FAIL")+" Restore recovered original assignments and preserved physics.");
            CandyArtBuilder.Apply();
            results.Add((before.All(s=>s.Matches())?"PASS":"FAIL")+" Final application preserved physics.");
            results.Add((Object.FindObjectsByType<CandyCrateVisual>(FindObjectsSortMode.None).All(v=>v.GetComponentsInChildren<Collider>(true).Length==0&&v.GetComponentsInChildren<Rigidbody>(true).Length==0)?"PASS":"FAIL")+" Candy artwork contains no physics components.");
            var variant=AssetDatabase.LoadAssetAtPath<GameObject>(CandyArtBuilder.Root+"Prefabs/Candy Physics Block.prefab");
            var original=originalPrefab.GetComponent<Rigidbody>();var replacement=variant.GetComponent<Rigidbody>();
            results.Add((original.mass==replacement.mass&&original.useGravity==replacement.useGravity&&original.isKinematic==replacement.isKinematic&&originalPrefab.GetComponent<BoxCollider>().size==variant.GetComponent<BoxCollider>().size?"PASS":"FAIL")+" Generated physics prefab retains the source mass and collider.");
            File.WriteAllLines(Output+"ArtValidation.txt",results);
            Capture();CandyArtBuilder.Export();
            SessionState.SetBool(PlayKey,true);EditorApplication.EnterPlaymode();
        }
        static IEnumerator PlayChecks()
        {
            yield return new WaitForSeconds(2);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            File.WriteAllText(Output+"SpawnValidation.txt",(session.Player.Grounded?"PASS":"FAIL")+" Player remains grounded after art application.\n");
            yield return CastleReferenceValidation.Validate(session);
            yield return CastleTraversalValidation.Validate(session);
            CastlePrototypeBuilder.ValidatePhysics();
            foreach(string name in new[]{"ReferenceValidation.txt","TraversalValidation.txt","Validation.txt"})
                File.Copy("Assets/CastlePrototype/"+name,Output+name,true);
            // Tests rebuild interpolated bodies and swap artwork. Let their render poses settle before evidence capture.
            yield return new WaitForSeconds(.5f);
            foreach(var block in session.generator.GetComponentsInChildren<CastleBlock>())block.RefreshAppearance();
            yield return new WaitForFixedUpdate();
            Capture();
            File.WriteAllText(Output+"COMPLETE.txt","Art apply/restore and Unity Play Mode verification finished at "+DateTime.UtcNow.ToString("O")+". Inspect reports for failures. Hardware performance remains unverified.");
            EditorApplication.isPlaying=false;
        }
        [MenuItem("Tools/Candy Tropical/6 Capture Art Views")]
        public static void Capture()
        {
            Directory.CreateDirectory(Output);
            foreach(var visual in Object.FindObjectsByType<CandyCrateVisual>(FindObjectsSortMode.None))visual.Refresh();
            var generator=Object.FindFirstObjectByType<CastleGenerator>();
            var go=new GameObject("Temporary candy evidence camera");var camera=go.AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=52;camera.farClipPlane=1200;camera.nearClipPlane=.05f;
            var texture=new RenderTexture(1600,1100,24);camera.targetTexture=texture;var old=RenderTexture.active;
            try
            {
                void Shot(string name,Vector3 position,Vector3 target)
                {
                    camera.transform.position=position;camera.transform.LookAt(target);camera.Render();RenderTexture.active=texture;
                    var png=new Texture2D(1600,1100,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1600,1100),0,0);png.Apply();
                    File.WriteAllBytes(Output+name+".png",png.EncodeToPNG());Object.DestroyImmediate(png);
                }
                var origin=generator.transform.position;
                Shot("candy-castle",origin+new Vector3(72,64,-78),origin+new Vector3(0,26,0));
                Shot("candy-interior",origin+new Vector3(0,61,0),origin+new Vector3(0,32,7));
                var corner=generator.GetComponentsInChildren<CastleBlock>().Where(b=>b.name.StartsWith("Corner")).OrderByDescending(b=>b.transform.position.y).ThenBy(b=>b.transform.position.x+b.transform.position.z).First();
                var center=corner.transform.position;
                Shot("candy-crate-closeup",center+new Vector3(8,5,-9),center);
                Shot("candy-shallows",origin+new Vector3(29,9,-32),origin+new Vector3(5,0,-5));
                var swatch=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CandyArtBuilder.Root+"Prefabs/Candy Crate Visual.prefab"));
                try
                {
                    swatch.transform.position=origin+new Vector3(0,85,0);swatch.transform.localScale=new Vector3(6,2,2);
                    swatch.GetComponent<CandyCrateVisual>().Refresh();swatch.GetComponent<Renderer>().sharedMaterial=CandyArtBuilder.Profile.green;
                    Shot("candy-material-swatch",swatch.transform.position+new Vector3(6,3,-6),swatch.transform.position);
                }
                finally{Object.DestroyImmediate(swatch);}
            }
            finally{RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(go);Object.DestroyImmediate(texture);}
        }
    }
}
