using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DeadlyJenga.CastlePrototype;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit authored-geometry inspection. Does not open scenes, simulate physics or create assets.
    public static class CuratedMapRouteChecks
    {
        [Serializable] sealed class SourceToken { public string path,sha256; }
        [Serializable] sealed class PrefabToken
        {
            public string file,assetGuid,sourceSha256,dependencyHash,component,selection,savedLayout;
            public int serializedBodies,serializedColliders;
        }
        [Serializable] sealed class TeamIntent
        {
            public string name;
            public Vector3 flagHome;
            public Vector3[] spawns;
        }
        [Serializable] sealed class MatchLayoutIntent
        {
            public string mapId,prefabFile,assetGuid,dependencyHash,selection,savedLayout,pointIntent;
            public bool authoredPointsComplete;
            public Vector3[] freeSpawns;
            public TeamIntent[] teams;
            public Vector3 hill;
        }
        [Serializable] sealed class Result
        {
            public string evidence="Authored geometry and imported prefab inspection only; NOT native physics, actor/carrier traversal, Game, bots, collapse, peers or Quest validation";
            public string utc,project,worldAssemblyMvid,editorAssemblyMvid;
            public bool passed;
            public int ctfBodies,ctfShapes,harborBodies,harborShapes,harborSites,groundEdges,groundCycles,upperBridges;
            public Vector2 ctfFootprint,harborFootprint;
            public Vector3 ctfMin,ctfMax,harborMin,harborMax;
            public List<string> checks=new(),failedAssertions=new();
            public List<SourceToken> sources=new();
            public List<PrefabToken> prefabs=new();
            public List<MatchLayoutIntent> mapMatchLayouts=new();
        }
        [MenuItem("Tools/Deadly Jenga/Maps/Inspect Two Curated Map Routes")]
        public static void Inspect()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Wait for imports and compilation to finish.");
            var r=new Result{utc=DateTime.UtcNow.ToString("O"),project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")),worldAssemblyMvid=typeof(CastleLayout).Assembly.ManifestModule.ModuleVersionId.ToString(),editorAssemblyMvid=typeof(CuratedMapRouteChecks).Assembly.ManifestModule.ModuleVersionId.ToString()};
            try{Ctf(r);}catch(Exception e){r.failedAssertions.Add("CTF inspection: "+e.Message);}
            try{Harbor(r);}catch(Exception e){r.failedAssertions.Add("Harbor inspection: "+e.Message);}
            foreach(string file in new[]{"Mode_ctf.prefab","Mode_epic-double-loop.prefab"})
                try{Prefab(r,file);}catch(Exception e){r.failedAssertions.Add(file+": "+e.Message);}
            foreach(string file in new[]{"ModeMapLayout.cs","CastleLayout.cs","CastleGenerator.cs","EpicCityLayout.cs","EpicCityLayout.Compositions.cs","EpicCityLayout.Streets.cs","EpicCityMap.cs","CuratedMapRouteChecks.cs"})
                try{string path=ProjectPath.Find(file);r.sources.Add(new SourceToken{path=path,sha256=Hash(path)});}catch(Exception e){r.failedAssertions.Add(file+": "+e.Message);}
            r.passed=r.failedAssertions.Count==0;
            string output=ProjectPath.Report("CuratedMaps/Authoring-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(r,true));
            Debug.Log("Curated map authoring inspection passed="+r.passed+"; "+output+". Native gameplay validation remains pending.");
            if(!r.passed)throw new InvalidOperationException(string.Join("; ",r.failedAssertions));
        }
        static void Check(Result r,bool ok,string message){r.checks.Add((ok?"PASS ":"FAIL ")+message);if(!ok)r.failedAssertions.Add(message);}
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        static bool Positive(Vector3 p)=>Finite(p)&&p.x>0&&p.y>0&&p.z>0;
        static Vector3[] Corners(CastlePlacement p,CastleSettings settings)=>MayhemStructureLayout.Cube(p.BlockSize(settings)).Select(v=>p.Position+p.Rotation*v).ToArray();
        static bool Covers(CastlePlacement p,CastleSettings settings,Vector3 point)
        {
            Vector3 local=Quaternion.Inverse(p.Rotation)*(point-p.Position),half=p.BlockSize(settings)*.5f;
            return Mathf.Abs(local.x)<=half.x+.001f&&Mathf.Abs(local.z)<=half.z+.001f&&Mathf.Abs(local.y-half.y)<.01f;
        }
        static bool Contains(CastlePlacement p,CastleSettings settings,Vector3 point)
        {
            Vector3 local=Quaternion.Inverse(p.Rotation)*(point-p.Position),half=p.BlockSize(settings)*.5f;
            return Mathf.Abs(local.x)<half.x&&Mathf.Abs(local.y)<half.y&&Mathf.Abs(local.z)<half.z;
        }
        static void Ctf(Result r)
        {
            var settings=new CastleSettings{layoutPreset="ctf"};var pieces=CastleLayout.Build(settings);
            r.ctfBodies=pieces.Count;r.ctfShapes=pieces.Count;r.ctfFootprint=CastleLayout.FootprintSize(settings);
            Check(r,pieces.Count>0&&pieces.Count<=80,"CTF has at most 80 authored independent box bodies");
            Check(r,pieces.All(p=>Finite(p.Position)&&Positive(p.BlockSize(settings))&&Finite(Quaternion.Dot(p.Rotation,p.Rotation))),"CTF placements have finite poses and positive finite box dimensions");
            var vertices=pieces.SelectMany(p=>Corners(p,settings)).ToArray();var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
            r.ctfMin=bounds.min;r.ctfMax=bounds.max;
            Check(r,(r.ctfFootprint-new Vector2(88,64)).sqrMagnitude<.001f,"CTF footprint is 88 by 64 metres");
            Check(r,Mathf.Abs(bounds.max.y-4.4f)<.01f&&bounds.min.x>=-44.01f&&bounds.max.x<=44.01f&&bounds.min.z>=-32.01f&&bounds.max.z<=32.01f,"CTF actual box corners stay inside footprint with top 4.4 metres");
            bool mirrored=pieces.All(p=>pieces.Any(other=>Corners(p,settings).All(v=>Corners(other,settings).Any(w=>(new Vector3(-v.x,v.y,-v.z)-w).sqrMagnitude<.0001f))));
            Check(r,mirrored,"CTF full oriented box geometry has a 180-degree counterpart");
            for(int team=0;team<2;team++)foreach(var point in new[]{ModeMapLayout.CtfHome(team),ModeMapLayout.CtfSpawn(team)})
            {
                Check(r,Finite(point)&&pieces.Any(p=>Covers(p,settings,point)),"CTF team "+team+" authored home/spawn has box-top coverage at "+point);
                Check(r,!pieces.Any(p=>Contains(p,settings,point+Vector3.up*.6f)),"CTF team "+team+" authored home/spawn head sample is unoccupied at "+point);
            }
            r.checks.Add("LIMIT: Coverage and one head sample do not establish capsule clearance, motor/carrier access or objective completion.");
        }
        static void Harbor(Result r)
        {
            var plan=EpicCityLayout.Build(1);r.harborBodies=plan.modules.Count;r.harborShapes=plan.modules.Sum(m=>m.shapes.Count);r.harborSites=plan.sites.Count;r.harborFootprint=plan.halfSize*2;
            var bounds=MayhemStructureLayout.Bounds(plan.modules);r.harborMin=bounds.min;r.harborMax=bounds.max;
            Check(r,plan.modules.Count>0&&plan.modules.Count<=64&&plan.modules.All(m=>Finite(m.position)&&Finite(Quaternion.Dot(m.rotation,m.rotation))&&m.shapes.Count>0&&m.shapes.All(s=>Finite(s.center)&&Positive(s.size)&&Finite(Quaternion.Dot(s.rotation,s.rotation))&&(s.vertices==null||s.vertices.All(Finite)))),"Harbor has at most 64 modules with positive finite authored shapes");
            Check(r,plan.sites.Count==6,"Harbor has six authored destinations");
            Check(r,Finite(r.harborFootprint.x)&&Finite(r.harborFootprint.y)&&r.harborFootprint.x>0&&r.harborFootprint.y>0&&r.harborFootprint.x<=140&&r.harborFootprint.y<=120,"Harbor footprint is compact, at most 140 by 120 metres");
            var ground=plan.routes.Where(x=>x.name.StartsWith("Ground street:",StringComparison.Ordinal)).ToArray();var edges=new HashSet<string>();var reached=new HashSet<int>{0};
            foreach(var route in ground){Check(r,route.from>=0&&route.from<6&&route.to>=0&&route.to<6&&route.from!=route.to&&Finite(route.a)&&Finite(route.b),"Harbor ground-route endpoints are finite site references");edges.Add(Math.Min(route.from,route.to)+":"+Math.Max(route.from,route.to));}
            for(int pass=0;pass<6;pass++)foreach(var edge in ground){if(reached.Contains(edge.from))reached.Add(edge.to);if(reached.Contains(edge.to))reached.Add(edge.from);}
            r.groundEdges=edges.Count;r.groundCycles=edges.Count-6+1;r.upperBridges=plan.routes.Count(x=>x.name.StartsWith("Upper crossing:",StringComparison.Ordinal));
            Check(r,reached.Count==6&&r.groundCycles==2,"Harbor declared ground graph is connected with two independent loops");
            Check(r,r.upperBridges==2&&plan.modules.Count(m=>m.purpose=="Optional exchange upper crossing")==2,"Harbor has two separately authored optional upper bridges");
            Check(r,plan.spawnPoints.Count==6&&plan.spawnPoints.All(p=>Finite(p)&&Mathf.Abs(p.x)<plan.halfSize.x&&Mathf.Abs(p.z)<plan.halfSize.y&&Mathf.Abs(p.y-EpicCityLayout.GroundHeight(plan,p)-.1f)<.02f),"Harbor has six finite terrain-relative spawn candidates within footprint");
            r.checks.Add("LIMIT: Ground edges are declared site relationships, not extracted navigation. Terrain-relative spawn candidates are not native collision checks.");
        }
        static void Prefab(Result r,string file)
        {
            string path=ProjectPath.Find(file);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!prefab)throw new InvalidOperationException("Imported prefab unavailable");
            var token=new PrefabToken{file=file,assetGuid=AssetDatabase.AssetPathToGUID(path),sourceSha256=Hash(path),dependencyHash=AssetDatabase.GetAssetDependencyHash(path).ToString(),serializedBodies=prefab.GetComponentsInChildren<Rigidbody>(true).Length,serializedColliders=prefab.GetComponentsInChildren<Collider>(true).Length};
            if(file=="Mode_ctf.prefab"){var map=prefab.GetComponent<CastleGenerator>();token.component="CastleGenerator";token.selection=map&&map.settings!=null?map.settings.layoutPreset:"missing";if(map&&map.settings!=null)token.savedLayout=map.SaveLayout();Check(r,map&&map.settings!=null&&map.settings.layoutPreset=="ctf","Imported CTF prefab selects ctf generator");}
            else{var map=prefab.GetComponent<EpicCityMap>();token.component="EpicCityMap";token.selection=map?map.design.ToString():"missing";if(map)token.savedLayout=map.SaveLayout();Check(r,map&&map.design==1,"Imported Harbor prefab selects design 1");}
            r.prefabs.Add(token);
            if(!string.IsNullOrEmpty(token.savedLayout))MatchIntent(r,token);
        }
        static void MatchIntent(Result r,PrefabToken prefab)
        {
            bool ctf=prefab.file=="Mode_ctf.prefab";
            var intent=new MatchLayoutIntent{mapId=ctf?"mode-ctf":"epic-double-loop",prefabFile=prefab.file,assetGuid=prefab.assetGuid,dependencyHash=prefab.dependencyHash,selection=prefab.selection,savedLayout=prefab.savedLayout};
            if(ctf)
            {
                intent.pointIntent="MapLab explicit courtyard intent: ten separate ground free spawns, five protected-side spawns per team with 180-degree counterparts, existing ground court flag homes and central ground hill. Coordinates are local authored surface points, not motor/capsule clearance certification.";
                intent.freeSpawns=new[]{new Vector3(-20,.4f,-28),new Vector3(20,.4f,28),new Vector3(20,.4f,-28),new Vector3(-20,.4f,28),new Vector3(-10,.4f,-12),new Vector3(10,.4f,12),new Vector3(-20,.4f,-12),new Vector3(20,.4f,12),new Vector3(0,.4f,-28),new Vector3(0,.4f,28)};
                intent.teams=new[]{
                    new TeamIntent{name="West Court",flagHome=ModeMapLayout.CtfHome(0),spawns=new[]{new Vector3(-36,.4f,-12),new Vector3(-40,.4f,-12),new Vector3(-40,.4f,0),new Vector3(-40,.4f,12),new Vector3(-36,.4f,12)}},
                    new TeamIntent{name="East Court",flagHome=ModeMapLayout.CtfHome(1),spawns=new[]{new Vector3(36,.4f,12),new Vector3(40,.4f,12),new Vector3(40,.4f,0),new Vector3(40,.4f,-12),new Vector3(36,.4f,-12)}}};
                intent.hill=new Vector3(0,ModeMapLayout.CuratedCtfGroundY,0);
            }
            else
            {
                var plan=EpicCityLayout.Build(1);
                intent.pointIntent="MapLab explicit exchange intent: six current plan spawnPoints plus four perimeter ground candidates; five outdoor west/east forecourt spawns per team, mirrored in X; flag homes on ground outside the south pavilions; central ground hill under the optional crossing. No upper route is required to carry a flag. Native ground/head clearance, carrier, bot and side-swap fairness remain unverified.";
                intent.freeSpawns=plan.spawnPoints.Concat(new[]{new Vector3(-44,.45f,-34),new Vector3(44,.45f,-34),new Vector3(-44,.45f,34),new Vector3(44,.45f,34)}).ToArray();
                intent.teams=new[]{
                    new TeamIntent{name="West Forecourt",flagHome=new Vector3(-48,.35f,-20),spawns=new[]{new Vector3(-50,.45f,-30),new Vector3(-46,.45f,-30),new Vector3(-50,.45f,-20),new Vector3(-50,.45f,-10),new Vector3(-46,.45f,-10)}},
                    new TeamIntent{name="East Forecourt",flagHome=new Vector3(48,.35f,-20),spawns=new[]{new Vector3(50,.45f,-30),new Vector3(46,.45f,-30),new Vector3(50,.45f,-20),new Vector3(50,.45f,-10),new Vector3(46,.45f,-10)}}};
                intent.hill=new Vector3(0,.35f,0);
            }
            int before=r.failedAssertions.Count;
            Check(r,intent.teams.Length==2&&intent.teams.All(t=>!string.IsNullOrWhiteSpace(t.name)&&t.spawns.Length==5)&&intent.teams.Select(t=>t.name).Distinct().Count()==2,"Two distinct teams each have five explicit authored spawns for "+intent.mapId);
            var allSpawns=intent.freeSpawns.Concat(intent.teams.SelectMany(t=>t.spawns)).ToArray();
            Check(r,intent.freeSpawns.Length==10&&allSpawns.All(Finite)&&allSpawns.SelectMany((p,i)=>allSpawns.Take(i).Select(q=>(p-q).sqrMagnitude)).All(distance=>distance>=1.44f),"Twenty finite, separate free/team candidates retain at least 1.2m authored spacing for "+intent.mapId);
            var points=allSpawns.Concat(intent.teams.Select(t=>t.flagHome)).Append(intent.hill).ToArray();
            var footprint=ctf?r.ctfFootprint:r.harborFootprint;
            Check(r,points.All(p=>Finite(p)&&Mathf.Abs(p.x)<footprint.x*.5f&&Mathf.Abs(p.z)<footprint.y*.5f),"All authored objective/spawn points are finite and inside "+intent.mapId+" footprint");
            if(ctf)
            {
                var settings=new CastleSettings{layoutPreset="ctf"};var pieces=CastleLayout.Build(settings);
                Check(r,points.All(p=>pieces.Any(piece=>Covers(piece,settings,p))&&!pieces.Any(piece=>Contains(piece,settings,p+Vector3.up*.6f))),"All courtyard intents have authored box-top coverage and an unoccupied head sample");
            }
            else
            {
                var plan=EpicCityLayout.Build(1);
                Check(r,points.All(p=>p.y>=EpicCityLayout.GroundHeight(plan,p)&&p.y<=EpicCityLayout.GroundHeight(plan,p)+.11f&&Mathf.Pow(p.x/(plan.halfSize.x*1.08f),2)+Mathf.Pow(p.z/(plan.halfSize.y*1.08f),2)<.95f),"All exchange intents lie over the authored terrain envelope with shore margin");
            }
            intent.authoredPointsComplete=r.failedAssertions.Count==before;
            r.mapMatchLayouts.Add(intent);
            r.checks.Add("LIMIT: Authored point intent and spacing do not establish native ClearSpawn, full head clearance, legal objective interaction, carrier/bot routes or post-collapse safety.");
        }
        static string Hash(string path){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    }
}
