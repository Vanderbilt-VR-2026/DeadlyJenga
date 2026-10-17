using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.CastlePrototype;

namespace DeadlyJenga.CandyArt
{
    [ExecuteAlways,RequireComponent(typeof(MeshFilter))]
    public sealed class CandySandbar:MonoBehaviour
    {
        public CandyArtProfile profile;
        public CastleGenerator generator;
        Mesh owned;Texture2D heights;int signature;
        public Texture2D HeightMap=>heights;
        public Vector2 HalfSize{get;private set;}
        public float SampleHeight(Vector2 p)=>SurfaceHeight(p);
        struct Foot {public Vector2 center,half;public Quaternion rotation;}
        readonly List<Foot> feet=new List<Foot>();
        void OnEnable(){Refresh();}
        void OnDisable(){Release();}
        void Dispose(Object item){if(!item)return;if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        void Release(){Dispose(owned);Dispose(heights);owned=null;heights=null;}
        public void Refresh()
        {
            if(!profile)return;var settings=generator?generator.settings:null;
            int hash=profile.terrainResolution;
            void Hash(float value){hash=hash*31+value.GetHashCode();}
            Hash(profile.sandMargin);Hash(profile.sandRelief);Hash(profile.sandDepth);Hash(profile.sandShoreWidth);Hash(profile.islandRadius);
            if(settings!=null){Hash(settings.blockSize.x);Hash(settings.blockSize.z);Hash(settings.modulesPerSide);Hash(settings.moduleGap);Hash(settings.singleTower?1:0);Hash(CastleLayout.Footprint(settings));}
            if(owned&&heights&&hash==signature)return;
            Release();feet.Clear();float half=settings!=null?CastleLayout.Footprint(settings)*.5f:profile.islandRadius*.65f;
            // A softly oval natural island has room at its shoulders while covering the castle's corners.
            HalfSize=new Vector2(half+profile.sandMargin+2.7f,half+profile.sandMargin+2.3f)+Vector2.one*profile.sandShoreWidth;
            if(settings!=null)foreach(var placement in CastleLayout.Build(settings))if(placement.Course==0)
                feet.Add(new Foot{center=new Vector2(placement.Position.x,placement.Position.z),half=new Vector2(settings.blockSize.x,settings.blockSize.z)*.5f,rotation=Quaternion.Inverse(placement.Rotation)});
            BuildMesh();signature=hash;
        }
        float SupportDistance(Vector2 p)
        {
            float distance=float.PositiveInfinity;
            foreach(var foot in feet)
            {
                Vector3 local=foot.rotation*new Vector3(p.x-foot.center.x,0,p.y-foot.center.y);
                Vector2 q=new Vector2(Mathf.Abs(local.x),Mathf.Abs(local.z))-foot.half;
                float d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0);
                distance=Mathf.Min(distance,d);
            }
            return distance;
        }
        public float ShapeDistance(Vector2 p)
        {
            Vector2 half=HalfSize-Vector2.one*profile.sandShoreWidth;
            Vector2 q=new Vector2(Mathf.Abs(p.x)/half.x,Mathf.Abs(p.y)/half.y);
            float coast=(Mathf.Pow(q.x*q.x*q.x+q.y*q.y*q.y,1f/3)-1)*Mathf.Min(half.x,half.y);
            float a=Mathf.Atan2(p.y,p.x);
            float shoulder=.22f*Mathf.Sin(a*3+.7f)+.15f*Mathf.Sin(a*5-1.2f)+.09f*Mathf.Sin(a*7+.4f);
            return coast-shoulder;
        }
        float SurfaceHeight(Vector2 p)
        {
            float edge=ShapeDistance(p);
            float bank=-profile.sandDepth*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.2f,profile.sandShoreWidth,edge));
            float support=SupportDistance(p);
            float reliefMask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,1.4f,support));
            float n=Mathf.PerlinNoise(p.x*.32f+21.7f,p.y*.32f+9.1f)*.72f+Mathf.PerlinNoise(p.x*.67f+8,p.y*.67f+31)*.28f;
            // Rounded dips leave raised molded ridges at support level; terrain never intrudes into a player or block.
            float relief=-profile.sandRelief*(.12f+.88f*n)*reliefMask;
            return bank+relief*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,profile.sandShoreWidth,edge)));
        }
        void BuildMesh()
        {
            int resolution=Mathf.Clamp(profile.terrainResolution,48,160),row=resolution+1;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();var samples=new float[row*row];
            for(int z=0;z<=resolution;z++)for(int x=0;x<=resolution;x++)
            {
                Vector2 p=new Vector2(Mathf.Lerp(-HalfSize.x,HalfSize.x,(float)x/resolution),Mathf.Lerp(-HalfSize.y,HalfSize.y,(float)z/resolution));float h=SurfaceHeight(p);
                vertices.Add(new Vector3(p.x,h,p.y));uv.Add(new Vector2((float)x/resolution,(float)z/resolution));samples[z*row+x]=h;
            }
            for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
            {int a=z*row+x,b=a+1,c=a+row,d=c+1;triangles.Add(a);triangles.Add(c);triangles.Add(b);triangles.Add(b);triangles.Add(c);triangles.Add(d);}
            var border=new List<int>();for(int x=0;x<resolution;x++)border.Add(x);for(int z=0;z<resolution;z++)border.Add(z*row+resolution);for(int x=resolution;x>0;x--)border.Add(resolution*row+x);for(int z=resolution;z>0;z--)border.Add(z*row);
            int bottom=vertices.Count;float floor=-profile.sandDepth-1.2f;
            foreach(int id in border){var v=vertices[id];v.y=floor;vertices.Add(v);uv.Add(Vector2.zero);}
            int center=vertices.Count;vertices.Add(new Vector3(0,floor,0));uv.Add(Vector2.zero);
            for(int i=0;i<border.Count;i++)
            {int j=(i+1)%border.Count,a=border[i],b=border[j],c=bottom+i,d=bottom+j;triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(b);triangles.Add(d);triangles.Add(c);triangles.Add(center);triangles.Add(c);triangles.Add(d);}
            owned=new Mesh{name="Tight molded sand terrain",hideFlags=HideFlags.HideAndDontSave};owned.SetVertices(vertices);owned.SetUVs(0,uv);owned.SetTriangles(triangles,0);owned.RecalculateNormals();owned.RecalculateBounds();GetComponent<MeshFilter>().sharedMesh=owned;
            heights=new Texture2D(row,row,TextureFormat.RFloat,false,true){name="Static sand terrain heights",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};heights.SetPixelData(samples,0);heights.Apply(false,true);
        }
    }
}
