using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CandyArt;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad]
    public static class MagneticCandyChecks
    {
        const string Key="MagneticCandyChecks";
        static int errors;
        static string Report=>ProjectPath.Report("MagneticCandy.txt");
        static MagneticCandyChecks()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    errors=0;Application.logMessageReceived+=Log;
                    EditorApplication.delayCall+=()=>{var go=new GameObject("Magnetic candy visual check");Object.DontDestroyOnLoad(go);go.AddComponent<MagneticCandyDriver>().StartCoroutine(Guard(Check()));};
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    var original=SessionState.GetString(Key+"Original","");if(File.Exists(original))EditorSceneManager.OpenScene(original);
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Magnetic Candy Finish")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            File.WriteAllText(Report,"Magnetic art and physics regression\n");
            SessionState.SetString(Key+"Original",SceneManager.GetActiveScene().path);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";
            EditorSceneManager.OpenScene(ProjectPath.Find("MapDrafterEvan2.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n"+trace+"\n");}}
        static void Require(bool ok,string message)
        {File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static IEnumerator Guard(IEnumerator check)
        {
            while(true)
            {
                bool next;object value=null;
                try{next=check.MoveNext();if(next)value=check.Current;}
                catch(Exception e){File.AppendAllText(Report,"FAIL "+e+"\n");break;}
                if(!next)break;yield return value;
            }
            (check as IDisposable)?.Dispose();EditorApplication.ExitPlaymode();
        }
        static IEnumerator Check()
        {
            yield return new WaitForSeconds(1);
            var generator=(CastleGenerator)Services.World;var map=generator.magneticMap;var layout=MagneticDraftMap.Layout();
            var appearance=generator.blockPrefab.GetComponent<CastleBlock>().appearance;
            Require(map&&appearance&&generator.Bodies.Count==layout.Count,"Actual magnetic map builds every authored cube, cantilever and thin catwalk");
            bool art=true,physics=true,bounds=true;
            for(int i=0;i<layout.Count;i++)
            {
                var body=generator.Bodies[i];var block=body.GetComponent<CastleBlock>();var collider=body.GetComponent<BoxCollider>();var magnet=body.GetComponent<MagneticBlock>();var piece=layout[i];
                var visual=block.Visual.GetComponentInChildren<CandyCrateVisual>();
                var material=piece.Strength==3?generator.keepMaterial:piece.Strength==2?generator.cornerMaterial:generator.wallMaterial;
                art&=block.appearance==appearance&&visual&&visual.GetComponent<MeshFilter>().sharedMesh.vertexCount>24&&visual.GetComponent<Renderer>().sharedMaterial==material&&!block.Visual.GetComponent<Renderer>().enabled;
                physics&=collider.size==piece.Size&&body.mass==(piece.Catwalk?25:40)&&magnet.strength==piece.Strength&&magnet.foundationAnchor==piece.Anchor&&magnet.Supported&&body.isKinematic&&block.Visual.GetComponentsInChildren<Collider>().Length==0;
                var mesh=visual.GetComponent<MeshFilter>().sharedMesh;
                foreach(var vertex in mesh.vertices)
                {var point=collider.transform.InverseTransformPoint(visual.transform.TransformPoint(vertex))-collider.center;bounds&=Mathf.Abs(point.x)<=collider.size.x*.5f+.001f&&Mathf.Abs(point.y)<=collider.size.y*.5f+.001f&&Mathf.Abs(point.z)<=collider.size.z*.5f+.001f;}
            }
            Require(art,"All magnetic shapes reuse castle bevel/panel artwork and exact shared glossy red/green/purple materials");
            Require(bounds,"Rounded artwork stays inside each original collider, including 0.3m catwalks");
            Require(physics,"All original dimensions, masses, strengths, anchors and supported states remain intact; artwork adds no colliders");
            Capture("MagneticCandy-Overview.png",generator.transform.position+new Vector3(32,26,-38),generator.transform.position+Vector3.up*13);
            var bridge=map.Structure.Blocks.First(b=>b.strength==1&&b.GetComponent<BoxCollider>().size.y<1);
            Capture("MagneticCandy-Catwalk.png",bridge.transform.position+new Vector3(6,3,-5),bridge.transform.position);
            Vector3 before=bridge.Body.position;bridge.GetComponent<CastleBlock>().Push(Vector3.right*4,null);yield return new WaitForSeconds(.7f);
            Require(bridge.Broken&&!bridge.Body.isKinematic&&bridge.Body.position.y<before.y-.3f,"Finished magnetic bridge still breaks and falls under gravity");
            Require(map.Structure.Blocks.Where(b=>b.strength==3).All(b=>b.Supported),"Bridge break leaves neighboring strong towers supported");
            var prefab=generator.blockPrefab;generator.blockPrefab=null;generator.Rebuild();yield return null;
            Require(generator.Bodies.All(b=>{var block=b.GetComponent<CastleBlock>();return !block.appearance&&block.Visual.GetComponent<Renderer>().enabled;}),"Without castle artwork, magnetic blocks retain visible gray-box fallback");
            generator.blockPrefab=prefab;generator.Rebuild();yield return null;
            Require(generator.Bodies.All(b=>b.GetComponent<CastleBlock>().appearance==appearance),"Rebuild restores shared artwork on every magnetic piece");
            SceneManager.LoadScene("Game");yield return new WaitForSeconds(1);
            Require(Services.Session is CastlePlaySession s&&s.Player!=null&&Services.World.Blocks.Count>100,"Original Game scene still starts with its normal castle and player");
            Require(errors==0,"Runtime finishes without errors or exceptions");File.AppendAllText(Report,"COMPLETE. Editor rendering and real gravity checked; no Quest device used.\n");
        }
        static void Capture(string name,Vector3 position,Vector3 target)
        {
            var batches=Object.FindObjectsByType<CandyCrateBatchRenderer>(FindObjectsSortMode.None).Where(b=>b.enabled).ToArray();foreach(var batch in batches)batch.enabled=false;
            var artwork=Object.FindObjectsByType<CandyCrateVisual>(FindObjectsSortMode.None).Select(v=>v.GetComponent<Renderer>()).ToArray();
            var hidden=artwork.Select(r=>r.forceRenderingOff).ToArray();foreach(var renderer in artwork)renderer.forceRenderingOff=false;
            var go=new GameObject("Magnetic finish screenshot");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.useOcclusionCulling=false;camera.fieldOfView=55;camera.transform.position=position;camera.transform.LookAt(target);
            var rt=RenderTexture.GetTemporary(1280,800,24);var old=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(ProjectPath.Report(name),image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);Object.Destroy(go);for(int i=0;i<artwork.Length;i++)if(artwork[i])artwork[i].forceRenderingOff=hidden[i];foreach(var batch in batches)if(batch)batch.enabled=true;}
        }
    }
    public sealed class MagneticCandyDriver:MonoBehaviour{}
}
