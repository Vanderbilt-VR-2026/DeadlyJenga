using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyArt
{
    // Shared static topology. Waves run entirely in the vertex shader; no mesh uploads or physics simulation per frame.
    [ExecuteAlways,RequireComponent(typeof(MeshFilter))]
    public sealed class CandyOceanSurface : MonoBehaviour
    {
        public CandyArtProfile profile;
        MeshFilter filter;
        int lastResolution;
        float lastSize,lastExtent,stormExtent;
        static readonly Dictionary<string,Mesh> Cache=new Dictionary<string,Mesh>();
        static readonly HashSet<CandyOceanSurface> Live=new HashSet<CandyOceanSurface>();
        public float ActiveExtent{get;private set;}
        void OnEnable(){Live.Add(this);Refresh();}
        void OnDisable(){Live.Remove(this);PruneUnused();}
        void Update(){Refresh();}
        public void Refresh()
        {
            if(!profile)return;if(!filter)filter=GetComponent<MeshFilter>();
            int resolution=Mathf.Clamp(profile.waterResolution,24,128);
            float size=Mathf.Max(100,profile.oceanSize),extent=Mathf.Min(Mathf.Max(16,profile.waveExtent),size*.4f);
            bool storm=false;
            foreach(var source in Tracked.Waves)
            {
                var wave=source.Wave;if(!wave.active)continue;storm=true;
                Vector3 delta=wave.center-transform.position;
                float reach=Mathf.Max(Mathf.Abs(delta.x),Mathf.Abs(delta.z))+wave.width*.65f+wave.depth*2+70;
                stormExtent=Mathf.Max(stormExtent,Mathf.Ceil(reach/64)*64);
            }
            if(storm){extent=Mathf.Min(size*.4f,Mathf.Max(extent,stormExtent));resolution=128;}
            else stormExtent=0;
            ActiveExtent=extent;
            if(resolution==lastResolution&&size==lastSize&&extent==lastExtent&&filter.sharedMesh)return;
            string key=resolution+":"+size.ToString("F2")+":"+extent.ToString("F2");
            if(!Cache.TryGetValue(key,out var mesh)||!mesh)
            {mesh=Create(resolution,size,extent);mesh.hideFlags=HideFlags.HideAndDontSave;Cache[key]=mesh;}
            filter.sharedMesh=mesh;lastResolution=resolution;lastSize=size;lastExtent=extent;
            if(Cache.Count>8)PruneUnused();
        }
        static void PruneUnused()
        {
            var unused=new List<string>();
            foreach(var entry in Cache)
            {
                bool used=false;foreach(var surface in Live)if(surface&&surface.filter&&surface.filter.sharedMesh==entry.Value){used=true;break;}
                if(used)continue;
                if(entry.Value){if(Application.isPlaying)Destroy(entry.Value);else DestroyImmediate(entry.Value);}unused.Add(entry.Key);
            }
            foreach(var key in unused)Cache.Remove(key);
        }
        public static Mesh Create(int resolution,float size,float extent)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            float inner=extent/size;
            for(int z=0;z<=resolution;z++)for(int x=0;x<=resolution;x++)vertices.Add(new Vector3(Mathf.Lerp(-inner,inner,(float)x/resolution),0,Mathf.Lerp(-inner,inner,(float)z/resolution)));
            for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
            {int a=z*(resolution+1)+x,b=a+1,c=a+resolution+1,d=c+1;indices.Add(a);indices.Add(c);indices.Add(b);indices.Add(b);indices.Add(c);indices.Add(d);}
            // A cheap outer ring reaches the horizon. Waves fade to zero before the shared boundary.
            for(int edge=0;edge<4;edge++)
            {
                int start=vertices.Count;
                for(int i=0;i<=resolution;i++)
                {
                    float t=(float)i/resolution,u=Mathf.Lerp(-inner,inner,t),far=Mathf.Lerp(-.5f,.5f,t);
                    Vector3 near=edge switch{0=>new Vector3(u,0,-inner),1=>new Vector3(inner,0,u),2=>new Vector3(-u,0,inner),_=>new Vector3(-inner,0,-u)};
                    Vector3 outer=edge switch{0=>new Vector3(far,0,-.5f),1=>new Vector3(.5f,0,far),2=>new Vector3(-far,0,.5f),_=>new Vector3(-.5f,0,-far)};
                    vertices.Add(near);vertices.Add(outer);
                }
                for(int i=0;i<resolution;i++)
                {int a=start+i*2,b=a+1,c=a+2,d=a+3;indices.Add(a);indices.Add(c);indices.Add(b);indices.Add(b);indices.Add(c);indices.Add(d);}
            }
            var mesh=new Mesh{name="Candy ocean static grid "+resolution};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(1,4,1));return mesh;
        }
    }
}
