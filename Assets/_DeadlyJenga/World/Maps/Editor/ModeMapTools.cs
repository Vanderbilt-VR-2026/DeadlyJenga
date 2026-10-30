using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // Explicit authoring only. Never runs on import or changes a team scene.
    public static class ModeMapTools
    {
        [MenuItem("Tools/Deadly Jenga/Maps/Build Mode Map Prefabs")]
        public static void BuildPrefabs()
        { Build(ModeMapLayout.Presets); }
        [MenuItem("Tools/Deadly Jenga/Maps/Rebuild CTF Map Prefab")]
        public static void BuildCtfPrefab()
        { Build(new[]{"ctf"}); }
        [MenuItem("Tools/Deadly Jenga/Maps/Rebuild King Of Hill Map Prefab")]
        public static void BuildKohPrefab()
        { Build(new[]{"koth"}); }
        static void Build(IEnumerable<string> presets)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before building map prefabs.");
            string maps=Path.GetDirectoryName(ProjectPath.FolderOf("ModeMapLayout.cs").TrimEnd('/')).Replace('\\','/');
            string resources=Folder(maps,"Resources"),output=Folder(resources,"ModeMaps");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPath.Find("CastleBlock.prefab"));
            if(!source||!source.GetComponent<Rigidbody>())throw new InvalidOperationException("The shared physical block prefab is missing.");
            var previous=Services.World;
            try
            {
                foreach(string preset in presets)
                {
                    var root=new GameObject("Mode map - "+preset);
                    try
                    {
                        var generator=root.AddComponent<CastleGenerator>();generator.settings=new CastleSettings{layoutPreset=preset};generator.blockPrefab=source;generator.generateOnStart=false;
                        generator.cornerMaterial=FindMaterial("Apple Green.mat");generator.wallMaterial=FindMaterial("Cherry Red.mat");generator.keepMaterial=FindMaterial("Lavender.mat");
                        var foundation=new GameObject("Fixed Foundation");foundation.transform.SetParent(root.transform,false);foundation.layer=0;foundation.transform.localPosition=Vector3.down*.5f;var size=CastleLayout.FootprintSize(generator.settings)+Vector2.one*4;foundation.transform.localScale=new Vector3(size.x,1,size.y);foundation.AddComponent<BoxCollider>();generator.foundation=foundation.transform;
                        generator.waterHeight=.08f;generator.oceanKillHeight=-6;generator.mass=40;
                        PrefabUtility.SaveAsPrefabAsset(root,output+"/Mode_"+preset+".prefab");
                    }
                    finally {UnityEngine.Object.DestroyImmediate(root);}
                }
                AssetDatabase.SaveAssets();ValidateLayouts();
            }
            finally {Services.World=previous;}
        }
        static Material FindMaterial(string name){try{return AssetDatabase.LoadAssetAtPath<Material>(ProjectPath.Find(name));}catch(FileNotFoundException){return null;}}
        static string Folder(string parent,string name){string path=parent+"/"+name;if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,name);return path;}
        [MenuItem("Tools/Deadly Jenga/Maps/Validate Mode Layouts")]
        public static void ValidateLayouts()
        {
            var report=new System.Text.StringBuilder("Deterministic mode map geometry checks\n");int checks=0;
            void Check(bool passed,string message){checks++;report.Append(passed?"PASS ":"FAIL ").AppendLine(message);if(!passed){File.WriteAllText(ProjectPath.Report("ModeMapLayouts.txt"),report.ToString());throw new InvalidOperationException(message);}}
            foreach(string preset in ModeMapLayout.Presets)
            {
                var settings=new CastleSettings{layoutPreset=preset};var layout=CastleLayout.Build(settings);var twice=CastleLayout.Build(settings);
                Check(layout.Count>=400&&layout.Count<=800,preset+" has a bounded block budget: "+layout.Count);
                Check(layout.Count==twice.Count&&layout.Where((p,i)=>p.Position!=twice[i].Position||p.Rotation!=twice[i].Rotation).Count()==0,preset+" is deterministic");
                var bounds=layout.Select(p=>Box(p,settings.blockSize)).ToArray();bool overlaps=false,supported=true,symmetric=true;
                for(int i=0;i<layout.Count;i++)
                {
                    var current=bounds[i];bool hasMirror=false;var contacts=new List<Vector2>();
                    for(int j=0;j<layout.Count;j++)
                    {
                        if((layout[j].Position-new Vector3(-layout[i].Position.x,layout[i].Position.y,-layout[i].Position.z)).sqrMagnitude<.0001f&&Mathf.Abs(Quaternion.Dot(layout[j].Rotation,layout[i].Rotation))>.999f)hasMirror=true;
                        if(i==j)continue;var other=bounds[j];
                        if(layout[i].Course==layout[j].Course&&Overlap(current.min.x,current.max.x,other.min.x,other.max.x)>.001f&&Overlap(current.min.z,current.max.z,other.min.z,other.max.z)>.001f)overlaps=true;
                        if(layout[j].Course!=layout[i].Course-1)continue;
                        float loX=Mathf.Max(current.min.x,other.min.x),hiX=Mathf.Min(current.max.x,other.max.x),loZ=Mathf.Max(current.min.z,other.min.z),hiZ=Mathf.Min(current.max.z,other.max.z);
                        if(hiX-loX>.001f&&hiZ-loZ>.001f){contacts.Add(new Vector2(loX,loZ));contacts.Add(new Vector2(loX,hiZ));contacts.Add(new Vector2(hiX,loZ));contacts.Add(new Vector2(hiX,hiZ));}
                    }
                    if(layout[i].Course>0&&!InsideSupport(contacts,new Vector2(current.center.x,current.center.z)))supported=false;
                    if(!hasMirror)symmetric=false;
                }
                Check(!overlaps,preset+" has no penetrating blocks in any course");
                Check(supported,preset+" every raised block center lies inside its bearing polygon");
                if(preset=="ctf"||preset=="tickets")Check(symmetric,preset+" has rotationally balanced team ends");
                var size=CastleLayout.FootprintSize(settings);Check(bounds.All(b=>b.min.x>=-size.x*.5f-.001f&&b.max.x<=size.x*.5f+.001f&&b.min.z>=-size.y*.5f-.001f&&b.max.z<=size.y*.5f+.001f),preset+" is contained in its reported footprint");
                if(preset=="ctf")
                {
                    Check(layout.Count==788,"ctf keeps a 788-block budget including grounded stairs and supported roof and midlevel links");
                    bool stairs=true;float pitch=2*settings.blockSize.x-settings.blockSize.z;
                    for(int side=-1;side<=1;side+=2)for(int step=0;step<16;step++)
                    {
                        Vector3 tread=new Vector3(side*(4*pitch-15*settings.blockSize.z+step*settings.blockSize.z),(step+1)*settings.blockSize.y,side*(pitch+settings.blockSize.x));
                        stairs&=bounds.Count(b=>Mathf.Abs(b.center.x-tread.x)<.001f&&Mathf.Abs(b.center.z-tread.z)<.001f&&b.max.y<=tread.y+.001f)==step+1;
                        var freeTread=tread+Vector3.forward*(side*settings.blockSize.z);
                        stairs&=!bounds.Any(b=>b.min.y>=freeTread.y-.001f&&b.max.y>freeTread.y+.001f&&freeTread.x>b.min.x+.001f&&freeTread.x<b.max.x-.001f&&freeTread.z>b.min.z+.001f&&freeTread.z<b.max.z-.001f);
                    }
                    Check(stairs,"ctf both sixteen-step carrier approaches retain grounded open outer treads beside the midlevel landings");
                    Check(layout.Count(p=>p.Section==CastleSection.Keep&&(p.Course==8||p.Course==9)&&Mathf.Abs(p.Position.z)>pitch)==6,"ctf has six mirrored midlevel landing blocks joining the inner roofs to the stairs");
                }
                if(preset=="koth")
                    Check(layout.Count==628&&layout.Count(p=>p.Course==18&&Mathf.Abs(p.Position.x)>settings.blockSize.x*.5f)==6,"koth has six supported upper approach bridges within its 628-block budget");
                string json=JsonUtility.ToJson(settings);var copy=JsonUtility.FromJson<CastleSettings>(json);Check(copy.layoutPreset==preset&&CastleLayout.Build(copy).Count==layout.Count,preset+" survives serialized layout round-trip");
            }
            report.AppendLine("Checks: "+checks+". Geometry checks do not replace real-gravity or headset tests.");File.WriteAllText(ProjectPath.Report("ModeMapLayouts.txt"),report.ToString());Debug.Log(report.ToString());
        }
        static Bounds Box(CastlePlacement p,Vector3 size)
        {var right=p.Rotation*Vector3.right;var forward=p.Rotation*Vector3.forward;return new Bounds(p.Position,new Vector3(Mathf.Abs(right.x)*size.x+Mathf.Abs(forward.x)*size.z,size.y,Mathf.Abs(right.z)*size.x+Mathf.Abs(forward.z)*size.z));}
        static float Overlap(float a,float b,float c,float d)=>Mathf.Min(b,d)-Mathf.Max(a,c);
        static bool InsideSupport(List<Vector2> points,Vector2 center)
        {
            if(points.Count<3)return false;points.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));var hull=new List<Vector2>();
            foreach(var p in points){while(hull.Count>=2&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}int lower=hull.Count;
            for(int i=points.Count-2;i>=0;i--){var p=points[i];while(hull.Count>lower&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}if(hull.Count<4)return false;
            for(int i=0;i<hull.Count-1;i++)if(Cross(hull[i+1]-hull[i],center-hull[i])<-.001f)return false;return true;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    }
}
