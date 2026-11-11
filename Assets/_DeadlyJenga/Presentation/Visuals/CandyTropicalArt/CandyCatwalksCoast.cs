using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    internal sealed class CandyCatwalksCoast : IDisposable
    {
        static readonly Vector2[] ReefDirections = { new(.80f,.60f), new(.82f,-.57f) };
        static readonly Color Sand = new(.87f,.66f,.36f);
        static readonly Color Rock = new(.43f,.37f,.31f);
        static readonly Color[] Coral = { new(.94f,.25f,.43f), new(1f,.60f,.12f), new(.60f,.38f,.77f) };
        static readonly Color Grass = new(.28f,.61f,.29f);
        readonly GameObject root;
        readonly Material material;
        Material daySky, daySkySource;
        readonly List<Mesh> meshes = new();
        readonly Vector4[] reefs = new Vector4[2];
        readonly Transform[] schools = new Transform[2];
        readonly Vector3[] schoolCenters = new Vector3[2];
        Transform ray;
        Vector3 rayCenter;
        readonly float water;
        bool disposed;
        internal bool Alive => root && !disposed;

        public CandyCatwalksCoast(Transform owner, CandySandbar terrain, Vector3 origin, float waterLevel, Shader shader)
        {
            water = waterLevel;
            root = new GameObject("Catwalks coast · visual only");
            root.transform.SetParent(owner,false);
            root.transform.position = new Vector3(origin.x,water,origin.z);
            material = new Material(shader) { name="Catwalks coast shared art" };
            var random = new System.Random(61011);
            for (int i=0;i<reefs.Length;i++)
            {
                Vector2 direction=ReefDirections[i].normalized;
                float low=0,high=terrain.HalfSize.magnitude;
                for (int step=0;step<14;step++)
                {
                    float middle=(low+high)*.5f;
                    if (terrain.ShapeDistance(direction*middle)<0) low=middle;
                    else high=middle;
                }
                Vector2 center=direction*(high+5.5f);
                reefs[i]=new Vector4(origin.x+center.x,origin.z+center.y,13f,9f);
                BuildReef(i,center,terrain,random);
            }
            BuildIslet(random);
            BuildHorizon();
            BuildRay();
        }

        internal void SetWaterProperties(MaterialPropertyBlock properties)
        {
            properties.SetFloat("_CatwalksCoast",Alive?1:0);
            properties.SetVector("_ReefA",reefs[0]);
            properties.SetVector("_ReefB",reefs[1]);
            Vector3 islet=root.transform.position+new Vector3(93,0,48);
            properties.SetVector("_CoastIslet",new Vector4(islet.x,islet.z,8.7f,5.6f));
        }

        internal Material DaySky(Material source)
        {
            if(!source || !source.HasProperty("_CoastDay")) return source;
            if(daySkySource==source && daySky) return daySky;
            if(daySky) UnityEngine.Object.Destroy(daySky);
            daySkySource=source;
            daySky=new Material(source) { name="Catwalks warm island sky" };
            daySky.SetFloat("_CoastDay",1);
            daySky.SetColor("_Zenith",new Color(.018f,.34f,.92f));
            daySky.SetColor("_Horizon",new Color(.34f,.72f,1));
            return daySky;
        }

        internal void Animate()
        {
            float time=Time.time;
            for (int i=0;i<schools.Length;i++)
            {
                float phase=time*.22f+i*2.8f;
                var offset=new Vector3(Mathf.Sin(phase)*2.2f,Mathf.Sin(phase*1.5f)*.12f,Mathf.Cos(phase)*1.45f);
                schools[i].localPosition=schoolCenters[i]+offset;
                schools[i].localRotation=Quaternion.LookRotation(new Vector3(Mathf.Cos(phase)*2.2f,0,-Mathf.Sin(phase)*1.45f));
            }
            float course=time*.12f;
            ray.localPosition=rayCenter+new Vector3(Mathf.Sin(course)*2.7f,Mathf.Sin(course*2)*.09f,Mathf.Cos(course)*2.7f);
            ray.localRotation=Quaternion.LookRotation(new Vector3(Mathf.Cos(course)*2.7f,0,-Mathf.Sin(course)*2.7f));
        }

        void BuildReef(int index, Vector2 center, CandySandbar terrain, System.Random random)
        {
            var garden=new ArtMesh();
            for (int patch=0;patch<4;patch++)
            {
                float angle=(patch*83+index*29)*Mathf.Deg2Rad;
                float radius=patch==0?.17f:.36f+patch*.07f;
                Vector2 point=center+new Vector2(Mathf.Cos(angle)*8*radius,Mathf.Sin(angle)*7*radius);
                float seabed=terrain.transform.position.y-water+terrain.SampleHeight(point);
                Vector3 position=new(point.x,seabed,point.y);
                float scale=patch==0?1.23f:Range(random,.66f,.97f);
                scale=Mathf.Min(scale,Mathf.Max(.35f,-seabed*.70f));
                garden.Ellipsoid(position,new Vector3(1.2f,.43f,.92f)*scale,Rock,11,5);
                garden.Ellipsoid(position+new Vector3(-.62f,.12f,.48f)*scale,new Vector3(.64f,.32f,.55f)*scale,Rock*.83f,9,4);
                for (int stem=0;stem<5;stem++)
                {
                    float stemAngle=stem*2.4f;
                    var foot=position+new Vector3(Mathf.Cos(stemAngle)*.37f,.24f,Mathf.Sin(stemAngle)*.37f)*scale;
                    var up=new Vector3(Mathf.Cos(stemAngle)*.16f,1,Mathf.Sin(stemAngle)*.16f);
                    Branch(garden,foot,up,scale*Range(random,.38f,.62f),.075f*scale,Coral[index%2],3,random);
                }
                for (int fan=0;fan<4;fan++)
                {
                    var foot=position+new Vector3(.3f+fan*.14f,.12f,-.42f+fan*.12f)*scale;
                    garden.Cup(foot,Range(random,.17f,.32f)*scale,Range(random,.22f,.46f)*scale,Coral[(index+1)%2],fan);
                }
                for (int pebble=0;pebble<6;pebble++)
                {
                    float a=pebble*2.37f;
                    var foot=position+new Vector3(Mathf.Cos(a)*.21f-.72f,.19f,Mathf.Sin(a)*.24f-.32f)*scale;
                    garden.Ellipsoid(foot,new Vector3(.14f,.19f,.14f)*scale,Coral[2],7,4);
                }
                for (int blade=0;blade<11;blade++)
                {
                    float a=blade*2.39f;
                    var foot=position+new Vector3(Mathf.Cos(a)*1.24f,-.02f,Mathf.Sin(a)*.85f)*scale;
                    garden.Leaf(foot,Vector3.up, new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),Range(random,.28f,.79f)*scale,
                        Range(random,.04f,.095f)*scale,Grass*Range(random,.78f,1.14f),false);
                }
            }
            Draw("Coral and seagrass "+index,garden,1);
            var school=new ArtMesh();
            for (int i=0;i<11;i++)
            {
                var point=new Vector3(Range(random,-1.4f,1.4f),Range(random,-.12f,.14f),i*.22f-1.1f);
                var tint=index==0?new Color(1f,.69f,.12f):new Color(.18f,.82f,.89f);
                float scale=Range(random,.72f,1.16f);
                school.Ellipsoid(point,new Vector3(.12f,.15f,.29f)*scale,tint,8,4);
                school.Fin(point+Vector3.back*.22f*scale,scale,tint*.9f);
                school.Ellipsoid(point+new Vector3(.092f,.043f,.16f)*scale,Vector3.one*.022f*scale,new Color(.06f,.11f,.15f),5,3);
                school.Ellipsoid(point+new Vector3(-.092f,.043f,.16f)*scale,Vector3.one*.022f*scale,new Color(.06f,.11f,.15f),5,3);
            }
            schools[index]=Draw("Looping reef fish school "+index,school,1,true).transform;
            schoolCenters[index]=new Vector3(center.x,-.82f,center.y);
        }


        static void Branch(ArtMesh mesh, Vector3 start, Vector3 direction, float length, float width,
            Color color, int depth, System.Random random)
        {
            Vector3 end=start+direction.normalized*length;
            mesh.Tube(start,end,width,width*.58f,color,6);
            mesh.Ellipsoid(end,Vector3.one*width*.65f,color,6,3);
            if(depth<=1) return;
            for(int i=0;i<2;i++)
            {
                var bend=new Vector3(Range(random,-.62f,.62f),Range(random,.3f,.66f),Range(random,-.62f,.62f));
                Branch(mesh,Vector3.Lerp(start,end,.7f+i*.26f),(direction*.7f+bend).normalized,length*.65f,width*.63f,color,depth-1,random);
            }
        }

        void BuildIslet(System.Random random)
        {
            var mesh=new ArtMesh();
            // The outer sand skirts meet the water gradually so the islet reads as a separate landmark.
            Vector3 center=new(93,-.09f,48);
            mesh.Ellipsoid(center,new Vector3(8.7f,1.19f,5.6f),Sand,30,8);
            for(int i=0;i<8;i++)
            {
                float angle=i*2.4f;
                var foot=center+new Vector3(Mathf.Cos(angle)*Range(random,2,6),.85f,Mathf.Sin(angle)*Range(random,1.7f,3.5f));
                mesh.Ellipsoid(foot,new Vector3(Range(random,.52f,1.45f),Range(random,.28f,1.16f),Range(random,.5f,1.15f)),Rock,9,5);
            }
            for(int i=0;i<12;i++)
            {
                float angle=i*2.39f;
                Vector3 bush=center+new Vector3(Mathf.Cos(angle)*Range(random,1.7f,3.4f),1.08f,Mathf.Sin(angle)*Range(random,1.3f,2.7f));
                mesh.Ellipsoid(bush,new Vector3(.49f,.34f,.51f)*Range(random,.6f,1.45f),Grass*Range(random,.72f,1.2f),9,5);
            }
            Palm(mesh,center+new Vector3(-1.7f,1.45f,.35f),5.8f,new Vector3(1.6f,0,.3f));
            Palm(mesh,center+new Vector3(2.1f,1.2f,1.45f),3.65f,new Vector3(1.15f,0,-.48f));
            Draw("Twin palm islet",mesh,0);
            var foam=new ArtMesh();
            for(int i=0;i<52;i++)
            {
                float a=i*2*Mathf.PI/52, b=(i+1)*2*Mathf.PI/52;
                Vector3 Outer(float angle,float width) => new(center.x+Mathf.Cos(angle)*(8.7f+width),.04f,center.z+Mathf.Sin(angle)*(5.6f+width));
                float band=.12f+.10f*Mathf.Sin(a*6);
                foam.Quad(Outer(a,0),Outer(b,0),Outer(b,band),Outer(a,band),new Color(.80f,.96f,.88f));
            }
            Draw("Palm islet surf line",foam,0);
        }

        static void Palm(ArtMesh mesh,Vector3 foot,float height,Vector3 bend)
        {
            Vector3 previous=foot;
            for(int i=1;i<=8;i++)
            {
                float t=i/8f;
                Vector3 next=foot+Vector3.up*(height*t)+bend*t*t;
                float width=Mathf.Lerp(.24f,.09f,t);
                mesh.Tube(previous,next,width+.022f,width,new Color(.56f,.35f,.16f)*Mathf.Lerp(.78f,1.12f,(i%3)/2f),8);
                previous=next;
            }
            for(int i=0;i<10;i++)
            {
                float angle=i*2.4f;
                Vector3 direction=new(Mathf.Cos(angle),0,Mathf.Sin(angle));
                mesh.Leaf(previous,direction,Vector3.up,height*(i%3==0?.53f:.43f),height*.115f,
                    Color.Lerp(new Color(.30f,.58f,.17f),new Color(.61f,.72f,.22f),i/9f),true);
            }
        }

        void BuildHorizon()
        {
            var artwork=Resources.Load<Texture2D>("CandyScenery/CatwalksHorizon");
            if(!artwork) return;
            material.SetTexture("_CoastHorizon",artwork);
            var strip=new ArtMesh();
            const int spans=28;
            for(int i=0;i<=spans;i++)
            {
                float t=i/(float)spans;
                float bearing=Mathf.Lerp(99,-7,t)*Mathf.Deg2Rad;
                Vector3 point=new(Mathf.Cos(bearing)*415,-.95f,Mathf.Sin(bearing)*415);
                strip.Point(point,Color.white,new Vector2(t,.24f));
                strip.Point(point+Vector3.up*44,Color.white,new Vector2(t,.52f));
                if(i==0) continue;
                int a=(i-1)*2;
                strip.Triangle(a,a+1,a+2);
                strip.Triangle(a+1,a+3,a+2);
            }
            Draw("Painted distant island chain and lighthouse",strip,2);
        }

        void BuildRay()
        {
            var mesh=new ArtMesh();
            Color tint=new(.27f,.56f,.65f);
            mesh.Ellipsoid(new Vector3(0,.04f,0),new Vector3(.24f,.085f,.58f),tint,10,5);
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 front=new(0,.01f,.54f),tip=new(side*1.05f,-.04f,-.08f),tail=new(0,-.01f,-.48f);
                int index=mesh.Point(front,tint,new Vector2(0,0));
                mesh.Point(tip,tint*.78f,new Vector2(1,0));
                mesh.Point(tail,tint,new Vector2(0,0));
                mesh.Point(new Vector3(side*.45f,.06f,-.04f),tint*1.08f,new Vector2(.47f,0));
                mesh.Triangle(index,index+1,index+3);mesh.Triangle(index+1,index+2,index+3);mesh.Triangle(index+2,index,index+3);
            }
            mesh.Tube(new Vector3(0,0,-.42f),new Vector3(.12f,-.05f,-1.32f),.038f,.007f,tint*.68f,5);
            ray=Draw("Gliding reef ray",mesh,1,true).transform;
            rayCenter=new Vector3(reefs[0].x-root.transform.position.x+6.3f,-1.14f,reefs[0].y-root.transform.position.z);
        }

        GameObject Draw(string name, ArtMesh source, float style, bool animated=false)
        {
            var mesh=source.Build(name);
            meshes.Add(mesh);
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            obj.transform.SetParent(root.transform,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var properties=new MaterialPropertyBlock();
            properties.SetFloat("_CoastStyle",style);
            properties.SetFloat("_CoastWater",water);
            properties.SetFloat("_CoastMotion",animated?1:0);
            renderer.SetPropertyBlock(properties);
            return obj;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed=true;
            if(root)root.SetActive(false);
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(material);
            if(daySky) UnityEngine.Object.Destroy(daySky);
            foreach(var mesh in meshes)UnityEngine.Object.Destroy(mesh);
        }

        static float Range(System.Random random,float min,float max) => Mathf.Lerp(min,max,(float)random.NextDouble());

        sealed class ArtMesh
        {
            readonly List<Vector3> vertices=new();
            readonly List<Color> colors=new();
            readonly List<Vector2> uv=new();
            readonly List<int> indices=new();
            public int Point(Vector3 point,Color color,Vector2 texcoord)
            {
                int index=vertices.Count;
                vertices.Add(point);colors.Add(color);uv.Add(texcoord);
                return index;
            }
            public void Triangle(int a,int b,int c) { indices.Add(a);indices.Add(b);indices.Add(c); }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
            {
                int start=Point(a,color,Vector2.zero);
                Point(b,color,Vector2.zero);Point(c,color,Vector2.zero);Point(d,color,Vector2.zero);
                Triangle(start,start+1,start+2);Triangle(start,start+2,start+3);
            }
            public void Ellipsoid(Vector3 center,Vector3 scale,Color color,int segments,int rings)
            {
                int start=vertices.Count;
                for(int r=0;r<=rings;r++)
                for(int s=0;s<segments;s++)
                {
                    float latitude=r*Mathf.PI/rings,angle=s*Mathf.PI*2/segments;
                    Vector3 normal=new(Mathf.Sin(latitude)*Mathf.Cos(angle),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(angle));
                    Point(center+Vector3.Scale(normal,scale),color,Vector2.zero);
                }
                Grid(start,segments,rings);
            }
            void Grid(int start,int segments,int rings)
            {
                for(int r=0;r<rings;r++)
                for(int s=0;s<segments;s++)
                {
                    int a=start+r*segments+s,b=start+r*segments+(s+1)%segments;
                    Triangle(a,b,a+segments);Triangle(b,b+segments,a+segments);
                }
            }
            public void Tube(Vector3 from,Vector3 to,float first,float last,Color color,int segments)
            {
                Vector3 axis=(to-from).normalized;
                Vector3 right=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized;
                Vector3 forward=Vector3.Cross(right,axis);
                int start=vertices.Count;
                for(int r=0;r<2;r++)
                for(int s=0;s<segments;s++)
                {
                    float a=s*Mathf.PI*2/segments;
                    Vector3 normal=right*Mathf.Cos(a)+forward*Mathf.Sin(a);
                    Point((r==0?to:from)+normal*(r==0?last:first),color,Vector2.zero);
                }
                Grid(start,segments,1);
            }
            public void Cup(Vector3 foot,float width,float height,Color color,int offset)
            {
                int start=vertices.Count;
                const int segments=11,rings=5;
                for(int r=0;r<=rings;r++)
                for(int s=0;s<segments;s++)
                {
                    float t=r/(float)rings,a=s*Mathf.PI*2/segments;
                    float radius=width*Mathf.Sin(t*Mathf.PI*.83f)*(.86f+.14f*Mathf.Cos(a*4+offset));
                    Point(foot+new Vector3(Mathf.Cos(a)*radius,height*Mathf.Sin(t*Mathf.PI*.78f),Mathf.Sin(a)*radius),color,Vector2.zero);
                }
                Grid(start,segments,rings);
            }
            public void Leaf(Vector3 origin,Vector3 direction,Vector3 arc,float length,float width,Color color,bool palm)
            {
                Vector3 side=Vector3.Cross(direction,arc).normalized;
                int start=vertices.Count;
                const int segments=9;
                for(int i=0;i<=segments;i++)
                {
                    float t=i/(float)segments;
                    float curve=palm?Mathf.Sin(t*Mathf.PI)*.26f-t*t*.23f:t*t*.38f;
                    var center=origin+direction*length*t+arc*length*curve;
                    float edge=width*Mathf.Sin(t*Mathf.PI)*(.77f+(i%2)*.23f);
                    Point(center-side*edge,color*.85f,Vector2.zero);
                    Point(center+arc*edge*.27f,color,Vector2.zero);
                    Point(center+side*edge,color*.85f,Vector2.zero);
                }
                for(int i=0;i<segments;i++)
                {
                    int a=start+i*3;
                    for(int s=0;s<2;s++)
                    { Triangle(a+s,a+s+3,a+s+1);Triangle(a+s+1,a+s+3,a+s+4); }
                }
            }
            public void Fin(Vector3 center,float scale,Color color)
            {
                int a=Point(center,color,Vector2.zero);
                Point(center+new Vector3(0,.18f,-.19f)*scale,color,new Vector2(1,0));
                Point(center+new Vector3(0,0,-.11f)*scale,color,new Vector2(.7f,0));
                Point(center+new Vector3(0,-.14f,-.18f)*scale,color,new Vector2(1,0));
                Triangle(a,a+1,a+2);Triangle(a,a+2,a+3);
            }
            public Mesh Build(string name)
            {
                var mesh=new Mesh { name=name };
                mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
