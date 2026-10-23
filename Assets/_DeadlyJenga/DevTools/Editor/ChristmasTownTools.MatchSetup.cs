using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class ChristmasTownTools
    {
        static MapMatchLayout matchLayout;
        static void AuthorMatchFoundation(ChristmasTownMap map)
        {
            Physics.SyncTransforms();
            string modes=ProjectPath.FolderOf("MapMatchLayout.cs").TrimEnd('/');
            string folder=Folder(Folder(modes,"Resources"),"MapMatchLayouts");
            string path=folder+"/Christmas Candy Town Match Layout.asset";
            matchLayout=AssetDatabase.LoadAssetAtPath<MapMatchLayout>(path);
            if(!matchLayout){matchLayout=ScriptableObject.CreateInstance<MapMatchLayout>();AssetDatabase.CreateAsset(matchLayout,path);}
            matchLayout.layoutSignature=map.SaveLayout();matchLayout.hill=new Vector3(0,.23f,7);
            var free=new List<Vector3>();
            for(int side=-1;side<=1;side+=2)
            {
                for(int x=-42;x<=42;x+=3)AddOpen(free,new Vector3(x,.08f,side*15));
                for(int z=-30;z<=30;z+=3)AddOpen(free,new Vector3(side*17,.08f,z));
            }
            matchLayout.freeSpawns=free.ToArray();
            matchLayout.teams=new MapMatchLayout.TeamArea[8];
            for(int team=0;team<8;team++)
            {
                var points=new List<Vector3>();Vector3 home;string name;
                if(team<2)
                {
                    int side=team==0?-1:1;home=new Vector3(side*43,.16f,0);name=side<0?"West cocoa gate":"East mint gate";
                    for(int x=40;x<=46;x+=2)for(int z=-14;z<=14;z+=2)
                    {var p=new Vector3(side*x,.08f,z);if(Vector3.Distance(p,home)>2)AddOpen(points,p);}
                }
                else if(team<4)
                {
                    int side=team==2?1:-1;home=new Vector3(0,.16f,side*33);name=side>0?"North post lane":"South diner lane";
                    for(int x=-6;x<=6;x+=2)for(int z=32;z<=36;z+=2)
                    {var p=new Vector3(x,.08f,side*z);if(Vector3.Distance(p,home)>1.4f)AddOpen(points,p);}
                }
                else
                {
                    int side=team%2==0?-1:1,end=team<6?1:-1;
                    home=new Vector3(side*32,.16f,end*15);name=(side<0?"West":"East")+(end>0?" north garden":" south garden");
                    for(int x=30;x<=38;x+=2)for(int z=12;z<=16;z++)
                    {var p=new Vector3(side*x,.08f,end*z);if(Vector3.Distance(p,home)>1.4f)AddOpen(points,p);}
                }
                if(points.Count<12)throw new Exception("Too few safe spawns in "+name+": "+points.Count);
                if(!OpenSpawn(home))throw new Exception("Flag home is blocked: "+name);
                matchLayout.teams[team]=new MapMatchLayout.TeamArea{name=name,flagHome=home,spawns=points.ToArray()};
            }
            if(free.Count<50)throw new Exception("Need at least fifty distributed free spawns.");
            var markers=Group(map.transform,"Match foundations - positions only");
            var neutral=Group(markers,"Free for all spawns");
            for(int i=0;i<free.Count;i++)Group(neutral,"Spawn "+i,free[i]);
            foreach(var team in matchLayout.teams)
            {
                var root=Group(markers,team.name);Group(root,"Flag home",team.flagHome);
                for(int i=0;i<team.spawns.Length;i++)Group(root,"Spawn "+i,team.spawns[i]);
            }
            Group(markers,"Hill objective",matchLayout.hill);EditorUtility.SetDirty(matchLayout);
        }
        static void AddOpen(List<Vector3> points,Vector3 p){if(OpenSpawn(p)&&!points.Contains(p))points.Add(p);}
        static bool OpenSpawn(Vector3 p)
        {
            int mask=(1<<Layers.Surface)|(1<<Layers.Block)|1;
            return Physics.Raycast(p+Vector3.up*.2f,Vector3.down,.4f,mask,QueryTriggerInteraction.Ignore)&&
                !Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.65f,.32f,mask,QueryTriggerInteraction.Ignore);
        }
        static IEnumerable<Vector3> FoundationPositions()
        {
            foreach(var p in matchLayout.freeSpawns)yield return p;
            foreach(var team in matchLayout.teams)
            {yield return team.flagHome;foreach(var p in team.spawns)yield return p;}
            yield return matchLayout.hill;
        }
        static void ValidateMatchFoundation()
        {
            foreach(var p in FoundationPositions())if(!OpenSpawn(p))throw new Exception("Authored match position blocked: "+p);
            checks.Add("PASS "+matchLayout.freeSpawns.Length+" ground-level free spawns; eight team areas ("+string.Join(", ",matchLayout.teams.Select(t=>t.spawns.Length))+") with separate flag homes; hill anchor.");
        }
    }
}
