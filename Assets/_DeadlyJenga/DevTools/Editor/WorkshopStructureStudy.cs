using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    public static class WorkshopStructureStudy
    {
        const string Pending="WorkshopStructureStudy.Native";
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
                SessionState.SetBool(Pending,false);EditorApplication.delayCall+=RunNative;
            };
        }
        static void RunNative()
        {
            int result=0;var report=new StringBuilder(File.ReadAllText(ProjectPath.Report("SparseStructures.txt")));
            try
            {
                int failures=0;var scene=SceneManager.GetActiveScene();
                for(int i=0;i<Ids.Length;i++)
                {
                    report.AppendLine("\nNATIVE MAP "+WorkshopStructureLayout.Names[i]);
                    if(!Measure(Resources.Load<GameObject>("ModeMaps/Mode_"+Ids[i]),report,scene))failures++;
                    File.WriteAllText(ProjectPath.Report("SparseStructures.txt"),report.ToString());
                }
                report.AppendLine("NATIVE COMPLETE failedMaps="+failures);File.WriteAllText(ProjectPath.Report("SparseStructures.txt"),report.ToString());
                if(failures>0)throw new InvalidOperationException(failures+" maps failed native checks.");
            }
            catch(Exception e){UnityEngine.Debug.LogException(e);File.AppendAllText(ProjectPath.Report("SparseStructures.txt"),"FAIL "+e+"\n");result=1;}
            if(Application.isBatchMode)EditorApplication.Exit(result);else EditorApplication.ExitPlaymode();
        }
        public static readonly string[] Ids={"workshop-crown","workshop-terraces","workshop-viaduct","workshop-megakeeps","workshop-crown-grand","workshop-terraces-grand","workshop-viaduct-grand","workshop-megakeeps-grand","workshop-archipelago"};
        [MenuItem("Tools/Deadly Jenga/Maps/Build and Measure Workshop Structure Suite")]
        public static void BuildAndMeasure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var report=new StringBuilder("Nine loose-structure maps; actual isolated PhysX settling and support-removal tests. Native solver timings are NOT FPS.\n");
            var catalog=Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog");if(!catalog)throw new InvalidOperationException("Missing lobby catalog.");
            var original=Services.World;var active=SceneManager.GetActiveScene();int failures=0;
            string maps=Path.GetDirectoryName(ProjectPath.FolderOf("WorkshopStructureMap.cs").TrimEnd('/')).Replace('\\','/');
            string resources=Folder(maps,"Resources"),target=Folder(resources,"ModeMaps");
            var block=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleBlock.prefab"));
            var entries=catalog.maps.ToList();
            try
            {
                for(int index=0;index<9;index++)
                {
                    var kind=(WorkshopStructureKind)index;var layout=WorkshopStructureLayout.Build(kind);var bounds=WorkshopStructureLayout.Bounds(layout);
                    report.AppendLine($"\nMAP {kind} | {WorkshopStructureLayout.Names[index]} | members={layout.Count} size={bounds.size} top={bounds.max.y:F1}m");
                    bool geometry=CheckGeometry(layout,report);
                    if(!geometry)failures++;
                    var root=new GameObject(WorkshopStructureLayout.Names[index]);
                    var map=root.AddComponent<WorkshopStructureMap>();map.kind=kind;map.blockPrefab=block;
                    map.pylonMaterial=Material("Apple Green.mat");map.capMaterial=Material("Cherry Red.mat");map.deckMaterial=Material("Lavender.mat");
                    var floor=new GameObject("Fixed Foundation");floor.transform.SetParent(root.transform,false);floor.AddComponent<BoxCollider>();
                    floor.transform.localPosition=new Vector3(bounds.center.x,-.5f,bounds.center.z);floor.transform.localScale=new Vector3(bounds.size.x+12,1,bounds.size.z+12);map.foundation=floor.transform;
                    string path=target+"/Mode_"+Ids[index]+".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
                    var entry=entries.Find(m=>m.id==Ids[index]);if(entry==null){entry=new IslandLobbyCatalog.Map{id=Ids[index]};entries.Add(entry);}
                    entry.title=WorkshopStructureLayout.Names[index];entry.mapPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);entry.scenePath=ProjectPath.Find("Game.unity");
                    entry.recommendedMode="demo";entry.recommendedPlayers=index<4?12:24;entry.walkableIsland=true;
                    if(!geometry){report.AppendLine("SKIP native physics until geometric overlap/support failures are corrected.");continue;}
                    File.WriteAllText(ProjectPath.Report("SparseStructures.txt"),report.ToString());
                }
                catalog.maps=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
                report.AppendLine($"\nCOMPLETE failedMaps={failures}; no magnets, joints, forced sleeping or hidden anchors. All authored map members remain dynamic until replica mode or submerged cleanup.");
                report.AppendLine("Not verified here: live controls/bot route completion, actual tsunami, render/GPU FPS, live multiplayer or Quest. Support polygon tests are only an initial geometric filter; native gravity is measured separately.");
                File.WriteAllText(ProjectPath.Report("SparseStructures.txt"),report.ToString());
                UnityEngine.Debug.Log(report.ToString());
                if(failures>0)throw new InvalidOperationException(failures+" sparse maps failed; inspect SparseStructures.txt.");
                SessionState.SetBool(Pending,true);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            finally{Services.World=original;if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
        }
        static Material Material(string name)=>AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find(name));
        static string Folder(string parent,string name){string path=parent+"/"+name;if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,name);return path;}
        static bool CheckGeometry(List<WorkshopMember> members,StringBuilder report)
        {
            bool good=true;int overlaps=0,unsupported=0;
            for(int i=0;i<members.Count;i++)
            {
                var a=members[i].Bounds;var points=new List<Vector2>();
                for(int j=0;j<members.Count;j++)
                {
                    if(i==j)continue;var b=members[j].Bounds;
                    float x=Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x),z=Mathf.Min(a.max.z,b.max.z)-Mathf.Max(a.min.z,b.min.z);
                    float y=Mathf.Min(a.max.y,b.max.y)-Mathf.Max(a.min.y,b.min.y);
                    if(j>i&&x>.001f&&y>.001f&&z>.001f){if(overlaps++<8)report.AppendLine($"OVERLAP {i} {members[i].Purpose} / {j} {members[j].Purpose}: {x:F2},{y:F2},{z:F2}");}
                    if(a.min.y>.001f&&Mathf.Abs(a.min.y-b.max.y)<.002f&&x>.001f&&z>.001f)
                    {float loX=Mathf.Max(a.min.x,b.min.x),hiX=Mathf.Min(a.max.x,b.max.x),loZ=Mathf.Max(a.min.z,b.min.z),hiZ=Mathf.Min(a.max.z,b.max.z);points.Add(new Vector2(loX,loZ));points.Add(new Vector2(loX,hiZ));points.Add(new Vector2(hiX,loZ));points.Add(new Vector2(hiX,hiZ));}
                }
                if(a.min.y>.001f&&!Supported(points,new Vector2(a.center.x,a.center.z)))
                {if(unsupported++<8)report.AppendLine($"UNSUPPORTED {i} {members[i].Purpose} center={a.center} bottom={a.min.y:F2}");}
            }
            good=overlaps==0&&unsupported==0;report.AppendLine($"GEOMETRY overlaps={overlaps} unsupportedCenters={unsupported} {(good?"PASS":"FAIL")}");return good;
        }
        static bool Supported(List<Vector2> points,Vector2 p)
        {
            if(points.Count<3)return false;points.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));var hull=new List<Vector2>();
            foreach(var q in points){while(hull.Count>1&&Cross(hull[hull.Count-1]-hull[hull.Count-2],q-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(q);}int lower=hull.Count;
            for(int i=points.Count-2;i>=0;i--){var q=points[i];while(hull.Count>lower&&Cross(hull[hull.Count-1]-hull[hull.Count-2],q-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(q);}
            if(hull.Count<4)return false;for(int i=0;i<hull.Count-1;i++)if(Cross(hull[i+1]-hull[i],p-hull[i])<-.001f)return false;return true;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool Measure(GameObject mapPrefab,StringBuilder report,Scene prior)
        {
            var oldWorld=Services.World;var scene=SceneManager.CreateScene("Sparse isolated physics "+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            GameObject root=null;
            try
            {
                SceneManager.SetActiveScene(scene);root=Object.Instantiate(mapPrefab);var map=root.GetComponent<WorkshopStructureMap>();map.Rebuild();var physics=scene.GetPhysicsScene();
                var start=map.Bodies.Select(b=>b.position).ToArray();var rotation=map.Bodies.Select(b=>b.rotation).ToArray();
                var timer=Stopwatch.StartNew();for(int i=0;i<500;i++)physics.Simulate(.02f);timer.Stop();
                float drift=0,tilt=0,speed=0;int awake=0;
                for(int i=0;i<map.Bodies.Count;i++){var b=map.Bodies[i];drift=Mathf.Max(drift,Vector3.Distance(start[i],b.position));tilt=Mathf.Max(tilt,Quaternion.Angle(rotation[i],b.rotation));speed+=b.linearVelocity.sqrMagnitude;if(!b.IsSleeping())awake++;}
                speed=Mathf.Sqrt(speed/map.Bodies.Count);bool stable=drift<.6f&&tilt<5&&speed<.08f;
                report.AppendLine($"NATIVE 10s settle: maxMove={drift:F4}m maxTilt={tilt:F3}deg rmsSpeed={speed:F4}m/s awake={awake}/{map.Bodies.Count} meanStep={timer.Elapsed.TotalMilliseconds/500:F3}ms {(stable?"PASS":"FAIL")}");
                bool native=map.Bodies.All(b=>!b.isKinematic&&b.useGravity&&b.detectCollisions)&&root.GetComponentsInChildren<Joint>().Length==0;
                string state=map.SaveLayout();int revision=map.Revision;
                bool rejected=!map.LoadLayout(state,map.Bodies.Count+1,new Pose(Vector3.zero,Quaternion.identity),Vector3.one)&&map.Revision==revision;
                int removed=-1;float highest=float.NegativeInfinity;
                for(int i=0;i<map.Authored.Count;i++)if((map.Authored[i].Purpose.Contains("pylon")||map.Authored[i].Purpose.Contains("corner pier")||map.Authored[i].Purpose=="Solid terrace mass")&&map.Authored[i].Center.y>highest){highest=map.Authored[i].Center.y;removed=i;}
                bool collapse=false;
                if(removed>=0)
                {
                    var positions=map.Bodies.Select(b=>b.position).ToArray();map.Bodies[removed].gameObject.SetActive(false);
                    for(int i=0;i<180;i++)physics.Simulate(.02f);
                    int moved=0;for(int i=0;i<map.Bodies.Count;i++)if(i!=removed&&Vector3.Distance(positions[i],map.Bodies[i].position)>1)moved++;
                    collapse=moved>0;report.AppendLine($"SUPPORT REMOVAL member={removed} {map.Authored[removed].Purpose} otherMembersMovedOver1m={moved} {(collapse?"PASS":"FAIL")}");
                }
                map.SetReplica(true);bool replica=map.Bodies.All(b=>b.isKinematic);map.SetReplica(false);replica&=map.Bodies.All(b=>!b.isKinematic);
                map.Rebuild();bool rebuild=map.Bodies.Count==start.Length&&map.Revision>revision;
                report.AppendLine($"CONTRACTS dynamic/noJoints={native} rejectWrongCountWithoutMutation={rejected} authorityRestore={replica} rebuildStableCount={rebuild}");
                return stable&&native&&rejected&&replica&&rebuild&&collapse;
            }
            finally{if(root)Object.DestroyImmediate(root);if(prior.IsValid()&&prior.isLoaded)SceneManager.SetActiveScene(prior);SceneManager.UnloadSceneAsync(scene);Services.World=oldWorld;}
        }
    }
}
