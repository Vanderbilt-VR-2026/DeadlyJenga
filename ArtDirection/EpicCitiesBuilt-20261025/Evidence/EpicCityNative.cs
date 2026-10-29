using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using Object=UnityEngine.Object;
[InitializeOnLoad] public static class EpicCityNative
{
 static string Out {get{var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-epicOutput");return i>=0?a[i+1]:"EpicResults";}}
 static EpicCityNative(){EditorApplication.update+=()=>{if(EditorApplication.isPlaying&&SessionState.GetBool("EpicNative",false)){SessionState.SetBool("EpicNative",false);Measure();}};}
 public static void Run(){Directory.CreateDirectory(Out);SessionState.SetBool("EpicNative",true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
 static void Measure()
 {
  var text=new StringBuilder("Actual native Unity 6000.3.23f1 isolated physical-map checks. Not production Game, FPS or Quest.\n");int failures=0;
  var colors=new[]{new Color(.1f,.75f,.58f),new Color(.96f,.31f,.29f),new Color(.55f,.36f,.92f),new Color(1,.82f,.48f)};
  var palette=colors.Select(c=>{var m=new Material(Shader.Find("Standard"));m.color=c;m.SetFloat("_Glossiness",.65f);return m;}).ToArray();
  try
  {
   for(int design=0;design<10;design++)
   {
    var scene=SceneManager.CreateScene("Epic native "+design,new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
    var root=new GameObject("Epic "+design);var map=root.AddComponent<EpicCityMap>();map.generateOnStart=false;map.design=design;map.palette=palette;
    map.friction=new PhysicsMaterial("Structural friction"){staticFriction=.65f,dynamicFriction=.55f,bounciness=0};
    map.Rebuild();var physics=scene.GetPhysicsScene();var shapes=root.GetComponentsInChildren<Collider>().Where(c=>c.attachedRigidbody).ToArray();var bodies=map.Blocks;
    text.AppendLine("\nMAP "+(design+1)+" "+EpicCityLayout.Names[design]+" bodies="+bodies.Count+" shapes="+shapes.Length+" sites="+map.Plan.sites.Count+" routes="+map.Plan.routes.Count+" elevated="+map.Plan.routes.Count(r=>!r.name.StartsWith("Ground street"))+" joints="+root.GetComponentsInChildren<Joint>().Length);
    int overlaps=0;
    for(int i=0;i<shapes.Length;i++)for(int j=i+1;j<shapes.Length;j++)
    {
     var a=shapes[i];var b=shapes[j];if(a.attachedRigidbody==b.attachedRigidbody||!a.bounds.Intersects(b.bounds))continue;
     if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>.04f){if(overlaps++<18)text.AppendLine("OVERLAP "+a.attachedRigidbody.name+" / "+b.attachedRigidbody.name+" depth="+depth.ToString("F3"));}
    }
    text.AppendLine("Initial overlaps>.04m="+overlaps);if(overlaps>0)failures++;
    var start=bodies.Select(b=>b.position).ToArray();var rotation=bodies.Select(b=>b.rotation).ToArray();
    for(int n=0;n<750;n++)physics.Simulate(.02f);
    foreach(var rb in bodies){rb.transform.SetPositionAndRotation(rb.position,rb.rotation);}Physics.SyncTransforms();
    float drift=bodies.Select((b,i)=>Vector3.Distance(b.position,start[i])).Max(),tilt=bodies.Select((b,i)=>Quaternion.Angle(b.rotation,rotation[i])).Max();
    text.AppendLine("15s settle drift="+drift.ToString("F3")+" tilt="+tilt.ToString("F2")+" awake="+bodies.Count(b=>!b.IsSleeping()));
    if(drift>.6f||tilt>5){failures++;text.AppendLine("MOVED "+string.Join("; ",bodies.Select((b,i)=>new{b,d=Vector3.Distance(b.position,start[i])}).OrderByDescending(x=>x.d).Take(10).Select(x=>x.b.name+" "+x.d.ToString("F2"))));}
    var buffer=new Collider[32];int checkedSamples=0,blocked=0;var bad=new List<string>();
    for(int i=0;i<map.Authored.Count;i++)
    {
     var m=map.Authored[i];var inv=Quaternion.Inverse(m.shapes[0].rotation);var rb=bodies[i];
     foreach(var s in m.shapes)
     {
      if(s.vertices==null||Vector3.Distance(s.vertices[0],s.vertices[4])>.001f||s.vertices[6].y-s.vertices[4].y<.7f||s.vertices[6].y-s.vertices[4].y>5)continue;
      Vector3 Pt(int v)=>rb.transform.TransformPoint(inv*(s.center+s.rotation*s.vertices[v]));var a=(Pt(4)+Pt(5))*.5f;var b=(Pt(6)+Pt(7))*.5f;
      for(int n=1;n<=10;n++){var p=Vector3.Lerp(a,b,n/10f);checkedSamples++;if(physics.OverlapCapsule(p+Vector3.up*.3f,p+Vector3.up*1.55f,.24f,buffer,(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore)>0){blocked++;if(bad.Count<12)bad.Add(m.purpose+" at "+p+" HIT "+buffer[0].attachedRigidbody?.name);}}
     }
    }
    text.AppendLine("Full-size ascent capsule samples="+checkedSamples+" blocked="+blocked+" "+string.Join("; ",bad));if(blocked>0)failures++;
    string saved=map.SaveLayout();int revision=map.Revision;bool reject=!map.LoadLayout(saved,bodies.Count+1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&map.Revision==revision;map.SetReplica(true);bool replica=bodies.All(b=>b.isKinematic);map.SetReplica(false);text.AppendLine("Contracts wrongCountRejected="+reject+" replica="+replica+" originalColliders="+map.UsesOriginalColliders);if(!reject||!replica)failures++;
    var ground=map.foundation.gameObject;var mf=ground.AddComponent<MeshFilter>();mf.sharedMesh=ground.GetComponent<MeshCollider>().sharedMesh;ground.AddComponent<MeshRenderer>().sharedMaterial=palette[3];
    Capture(map,scene,design,"intact");
    var before=bodies.Select(b=>b.position).ToArray();
    for(int d=0;d<map.Plan.sites.Count;d++){var hit=bodies.FirstOrDefault(b=>b.GetComponent<MayhemStructuralMember>().District==d&&b.name.Contains("inhabited section"));if(hit)hit.AddForceAtPosition(new Vector3(d%2==0?14:-14,0,5),hit.worldCenterOfMass+Vector3.up*3,ForceMode.VelocityChange);}
    for(int n=0;n<600;n++)physics.Simulate(.02f);foreach(var rb in bodies)rb.transform.SetPositionAndRotation(rb.position,rb.rotation);Physics.SyncTransforms();
    text.AppendLine("Separate 14m/s X + 5m/s Z off-center demolition moved>.5m="+bodies.Where((b,i)=>Vector3.Distance(b.position,before[i])>.5f).Count()+"/"+bodies.Count);Capture(map,scene,design,"impact");
    File.WriteAllText(Path.Combine(Out,"native.txt"),text.ToString());root.SetActive(false);Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(scene);
   }
  }
  catch(Exception e){failures++;text.AppendLine("EXCEPTION "+e);}
  text.AppendLine("COMPLETE issues="+failures);File.WriteAllText(Path.Combine(Out,"native.txt"),text.ToString());EditorApplication.Exit(failures==0?0:2);
 }
 static void Capture(EpicCityMap map,Scene scene,int design,string state)
 {
  var go=new GameObject("Evidence camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();camera.scene=scene;camera.enabled=false;camera.backgroundColor=new Color(.3f,.65f,.86f);camera.clearFlags=CameraClearFlags.SolidColor;camera.farClipPlane=1600;camera.fieldOfView=48;
  camera.transform.position=new Vector3(310,260,-430);camera.transform.LookAt(new Vector3(0,16,0));var sun=new GameObject("Evidence sun");SceneManager.MoveGameObjectToScene(sun,scene);var light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(48,-35,0);
  RenderSettings.ambientLight=new Color(.6f,.65f,.75f);var rt=RenderTexture.GetTemporary(1600,1050,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,1050,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1050),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Out,"Epic-"+(design+1).ToString("D2")+"-"+state+".png"),image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);Object.DestroyImmediate(sun);
 }
}
