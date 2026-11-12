using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadlyJenga.CandyArt
{
    internal sealed class CandyCatwalksCoast : IDisposable
    {
        static readonly Vector2[] ReefDirections = { new(.80f,.60f), new(.82f,-.57f), new(-.84f,-.54f), new(-.48f,.88f) };
        static readonly Vector4[] Islets =
        {
            new(93,48,8.7f,5.6f),
            new(-104,67,13.6f,8.8f),
            new(-54,-115,9.2f,6.3f)
        };
        static readonly Vector3 SunEuler = new(42,49,0);
        internal static readonly Vector3 SunDirection = Quaternion.Euler(SunEuler)*Vector3.back;
        static readonly Color Sand = new(.87f,.66f,.36f);
        static readonly Color Rock = new(.51f,.39f,.28f);
        static readonly Color[] Coral = { new(1f,.12f,.27f), new(1f,.54f,.055f), new(.60f,.25f,.88f) };
        static readonly Color Grass = new(.28f,.61f,.29f);
        readonly GameObject root;
        readonly Material material;
        readonly Texture2D waterSpectrum;
        readonly Texture2D panorama, caustics, sandGrain;
        readonly CandyCoastReflection reflection;
        readonly CandyReefLight reefLight;
        Material daySky, daySkySource;
        readonly List<Mesh> meshes = new();
        readonly Vector4[] reefs = new Vector4[ReefDirections.Length];
        readonly Transform[] schools = new Transform[2];
        readonly Vector3[] schoolCenters = new Vector3[2];
        Transform ray;
        Vector3 rayCenter;
        readonly float water;
        bool disposed;
        internal bool Alive => root && !disposed;

        public CandyCatwalksCoast(Transform owner, CandySandbar terrain, Vector3 origin, float waterLevel, Shader shader, IWorldQuery world, Renderer waterRenderer, Renderer sandRenderer)
        {
            water = waterLevel;
            root = new GameObject("Catwalks coast · visual only");
            root.transform.SetParent(owner,false);
            root.transform.position = new Vector3(origin.x,water,origin.z);
            material = new Material(shader) { name="Catwalks coast shared art" };
            waterSpectrum=Resources.Load<Texture2D>("CandyScenery/CatwalksWaterSpectrum");
            panorama=Resources.Load<Texture2D>("CandyScenery/CatwalksDayPanorama");
            caustics=Resources.Load<Texture2D>("CandyScenery/CatwalksRefractedLight");
            sandGrain=Resources.Load<Texture2D>("CandyScenery/CatwalksSandGrain");
            if(caustics) material.SetTexture("_CoastCaustics",caustics);
            reflection=new CandyCoastReflection(world,waterRenderer,sandRenderer,material,waterLevel);
            reefLight=new CandyReefLight(world,waterRenderer,caustics,waterLevel);
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
                BuildReef(i,center,terrain,random,i<schools.Length);
            }
            for(int i=0;i<Islets.Length;i++) BuildIslet(i,random);
            BuildHorizon(random);
            BuildRay();
        }

        internal void SetWaterProperties(MaterialPropertyBlock properties)
        {
            properties.SetFloat("_CatwalksCoast",Alive?1:0);
            properties.SetFloat("_CoastDayWater",CandySceneSkins.Selected==CandySceneSkin.TropicalDay?1:0);
            properties.SetVector("_CoastSun",SunDirection);
            if(waterSpectrum) properties.SetTexture("_CoastSpectrum",waterSpectrum);
            if(panorama) properties.SetTexture("_CoastPanorama",panorama);
            if(caustics) properties.SetTexture("_CoastCaustics",caustics);
            if(sandGrain) properties.SetTexture("_CoastSandGrain",sandGrain);
            properties.SetFloat("_CoastHasGrain",sandGrain?1:0);
            properties.SetFloat("_CoastHasPanorama",panorama?1:0);
            properties.SetFloat("_CoastReflectionReady",0);
            properties.SetFloat("_CoastShadowReady",0);
            properties.SetVector("_ReefA",reefs[0]);
            properties.SetVector("_ReefB",reefs[1]);
            properties.SetVector("_ReefC",reefs[2]);
            properties.SetVector("_ReefD",reefs[3]);
            properties.SetVector("_CoastIslet",WorldIslet(0));
            properties.SetVector("_CoastIsletB",WorldIslet(1));
            properties.SetVector("_CoastIsletC",WorldIslet(2));
        }

        Vector4 WorldIslet(int index)
        {
            Vector4 island=Islets[index];
            return new Vector4(root.transform.position.x+island.x,root.transform.position.z+island.y,island.z,island.w);
        }

        internal Material DaySky(Material source)
        {
            if(!source || !source.HasProperty("_CoastDay")) return source;
            if(daySkySource==source && daySky) return daySky;
            if(daySky) UnityEngine.Object.Destroy(daySky);
            daySkySource=source;
            daySky=new Material(source) { name="Catwalks warm island sky" };
            daySky.SetFloat("_CoastDay",1);
            daySky.SetColor("_Zenith",new Color(.018f,.25f,.82f));
            daySky.SetColor("_Horizon",new Color(.27f,.67f,1));
            daySky.SetVector("_SunDirection",SunDirection);
            daySky.SetColor("_SunColor",new Color(1f,.90f,.74f));
            daySky.SetFloat("_CoastWaterLevel",water);
            if(panorama) daySky.SetTexture("_CoastPanorama",panorama);
            daySky.SetFloat("_CoastHasPanorama",panorama?1:0);
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

        void BuildReef(int index, Vector2 center, CandySandbar terrain, System.Random random, bool hasSchool)
        {
            var garden=new ArtMesh();
            for (int patch=0;patch<5;patch++)
            {
                float angle=(patch*83+index*29)*Mathf.Deg2Rad;
                float radius=patch==0?.17f:.36f+patch*.07f;
                Vector2 point=center+new Vector2(Mathf.Cos(angle)*8*radius,Mathf.Sin(angle)*7*radius);
                float seabed=terrain.transform.position.y-water+terrain.SampleHeight(point);
                Vector3 position=new(point.x,seabed,point.y);
                float scale=patch==0?1.55f:Range(random,.62f,1.12f);
                scale=Mathf.Min(scale,Mathf.Max(.30f,-seabed*1.1f));
                garden.Stone(position,new Vector3(1.2f,.43f,.92f)*scale,Rock,patch*.6f);
                garden.Stone(position+new Vector3(-.62f,.12f,.48f)*scale,new Vector3(.64f,.32f,.55f)*scale,Rock*.83f,patch*.88f+.6f);
                garden.Stone(position+new Vector3(.71f,.02f,-.28f)*scale,new Vector3(.66f,.31f,.55f)*scale,Rock*.77f,patch*.55f-.8f);
                int stems=patch==0?8:5;
                for (int stem=0;stem<stems;stem++)
                {
                    float stemAngle=stem*2.4f;
                    float spread=stem<4?.41f:.65f;
                    var foot=position+new Vector3(Mathf.Cos(stemAngle)*spread,.23f,Mathf.Sin(stemAngle)*spread)*scale;
                    var up=new Vector3(Mathf.Cos(stemAngle)*.26f,1,Mathf.Sin(stemAngle)*.22f);
                    Color tint=Color.Lerp(Coral[0],new Color(1f,.42f,.45f),Range(random,.02f,.67f));
                    Branch(garden,foot,up,scale*Range(random,.40f,.69f),.080f*scale,tint,stem<3&&patch==0?4:3,random);
                }
                for (int fan=0;fan<7;fan++)
                {
                    float phi=fan*2.39996f;
                    float spread=.34f*Mathf.Sqrt(fan/7f);
                    var foot=position+new Vector3(.54f+Mathf.Cos(phi)*spread,.18f,-.46f+Mathf.Sin(phi)*spread)*scale;
                    garden.Cup(foot,Range(random,.135f,.26f)*scale,Range(random,.27f,.52f)*scale,Coral[1],fan);
                }
                for (int pebble=0;pebble<24;pebble++)
                {
                    float a=pebble*2.37f, spread=.41f*Mathf.Sqrt(pebble/24f);
                    var foot=position+new Vector3(Mathf.Cos(a)*spread-.72f,.23f-spread*.35f,Mathf.Sin(a)*spread-.32f)*scale;
                    float size=Range(random,.077f,.142f)*scale;
                    garden.Ellipsoid(foot,new Vector3(size,size*1.37f,size),Coral[2]*Range(random,.74f,1.16f),8,4);
                }
                for (int blade=0;blade<17;blade++)
                {
                    float a=blade*2.39f;
                    var foot=position+new Vector3(Mathf.Cos(a)*1.24f,-.02f,Mathf.Sin(a)*.85f)*scale;
                    garden.Leaf(foot,Vector3.up, new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),Range(random,.28f,.79f)*scale,
                        Range(random,.04f,.095f)*scale,Grass*Range(random,.78f,1.14f),false);
                }
            }
            for(int i=0;i<21;i++)
            {
                float angle=i*2.39996f;
                float radius=Range(random,2.2f,10.6f);
                var p=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                float y=terrain.transform.position.y-water+terrain.SampleHeight(p);
                float size=Range(random,.045f,.18f);
                var foot=new Vector3(p.x,y+.015f,p.y);
                if(i%4==0)
                    garden.Shell(foot,size*1.62f,angle,Color.Lerp(new Color(.93f,.53f,.35f),Sand,i/21f));
                else
                    garden.Ellipsoid(foot,new Vector3(size,.40f*size,.68f*size),
                        Color.Lerp(Rock,Sand,Range(random,.2f,.77f)),7,4);
            }
            Draw("Coral and seagrass "+index,garden,1);
            if(!hasSchool) return;
            var school=new ArtMesh();
            for (int i=0;i<11;i++)
            {
                var point=new Vector3(Range(random,-1.4f,1.4f),Range(random,-.12f,.14f),i*.22f-1.1f);
                var tint=index==0?new Color(1f,.69f,.12f):new Color(.18f,.82f,.89f);
                float scale=Range(random,.72f,1.16f);
                school.Ellipsoid(point,new Vector3(.105f,.14f,.32f)*scale,tint,12,6);
                school.Fin(point+Vector3.back*.22f*scale,scale,tint*.9f);
                school.Ellipsoid(point+new Vector3(.092f,.043f,.16f)*scale,Vector3.one*.022f*scale,new Color(.06f,.11f,.15f),5,3);
                school.Ellipsoid(point+new Vector3(-.092f,.043f,.16f)*scale,Vector3.one*.022f*scale,new Color(.06f,.11f,.15f),5,3);
            }
            schools[index]=Draw("Looping reef fish school "+index,school,1,true).transform;
            schoolCenters[index]=new Vector3(center.x,-.62f,center.y);
        }


        static void Branch(ArtMesh mesh, Vector3 start, Vector3 direction, float length, float width,
            Color color, int depth, System.Random random)
        {
            Vector3 end=start+direction.normalized*length;
            Vector3 bend=Vector3.Cross(direction,Vector3.right)*length*.12f;
            Vector3 middle=(start+end)*.5f+bend;
            mesh.CoralTube(start,middle,end,width,color);
            if(depth<=1) return;
            for(int i=0;i<2;i++)
            {
                float azimuth=Range(random,0,Mathf.PI)+i*Mathf.PI;
                var branch=new Vector3(Mathf.Cos(azimuth)*.94f,Range(random,.18f,.48f),Mathf.Sin(azimuth)*.94f);
                Branch(mesh,Vector3.Lerp(middle,end,.30f+i*.66f),(direction*.54f+branch).normalized,length*.69f,width*.65f,color,depth-1,random);
            }
        }

        void BuildIslet(int index,System.Random random)
        {
            Vector4 island=Islets[index];
            var mesh=new ArtMesh();
            Vector3 center=new(island.x,-.11f,island.y);
            float height=index==1?3.4f:1.65f;
            float seed=index*1.3f+1;
            mesh.Land(center,new Vector2(island.z,island.w),height,seed,1,32);
            Vector3 Ground(float x,float z)
            {
                float y=ArtMesh.LandHeight(x/island.z,z/island.w,seed,1)*height;
                return center+new Vector3(x,y-.055f,z);
            }
            int rocks=index==1?13:7;
            for(int i=0;i<rocks;i++)
            {
                float angle=i*2.39996f;
                Vector3 foot=Ground(Mathf.Cos(angle)*Range(random,.8f,island.z*.63f),
                    Mathf.Sin(angle)*Range(random,.6f,island.w*.64f));
                mesh.Stone(foot,new Vector3(Range(random,.46f,1.5f),Range(random,.30f,1.35f),Range(random,.41f,1.22f)),
                    Rock*Range(random,.84f,1.18f),i*.64f);
            }
            int palms=index==1?5:index==0?2:3;
            for(int i=0;i<palms;i++)
            {
                float side=i%2==0?-1:1;
                var foot=Ground(side*(1.55f+i*.57f),Mathf.Sin(i*2.4f)*island.w*.42f);
                Palm(mesh,foot,Range(random,3.2f,5.8f),new Vector3(side*Range(random,.45f,1.6f),0,Range(random,-.4f,.65f)));
            }
            for(int i=0;i<18;i++)
            {
                float angle=i*2.39996f, spread=Range(random,.16f,.66f);
                var foot=Ground(Mathf.Cos(angle)*island.z*spread,Mathf.Sin(angle)*island.w*spread);
                mesh.Ellipsoid(foot,new Vector3(.54f,.34f,.52f)*Range(random,.65f,1.35f),Grass*Range(random,.70f,1.2f),8,4);
            }
            Draw("Palm and rock islet "+(index+1),mesh,0);
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

        void BuildHorizon(System.Random random)
        {
            // Every bearing gets its own coastline and relief rather than a view-facing sprite.
            AddLand("Lighthouse coast",35,415,new Vector2(136,59),31,1.3f,3,true,random);
            AddLand("Outer northeast ridge",88,535,new Vector2(142,71),29,6.2f,3,false,random);
            AddLand("Twin limestone headlands",143,406,new Vector2(95,54),41,4.7f,2,false,random);
            AddLand("Western green ridge",194,498,new Vector2(157,65),34,8.6f,4,false,random);
            AddLand("Southwest wooded islands",242,415,new Vector2(118,57),26,2.4f,3,false,random);
            AddLand("Southern rock stack",287,362,new Vector2(62,46),41,5.4f,2,false,random);
            AddLand("Southeast volcanic shore",331,493,new Vector2(145,72),38,7.8f,3,false,random);
        }

        void AddLand(string name,float bearing,float distance,Vector2 size,float height,float seed,int peaks,
            bool lighthouse,System.Random random)
        {
            float angle=bearing*Mathf.Deg2Rad;
            Vector3 center=new(Mathf.Cos(angle)*distance,-1.35f,Mathf.Sin(angle)*distance);
            Vector3 inland=new(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var mesh=new ArtMesh();
            Vector3 side=Vector3.Cross(Vector3.up,inland);
            float ridgeOffset=seed%2<1?-1:1;
            Vector3 outerRidge=center+inland*61+side*(size.x*.34f*ridgeOffset);
            mesh.Land(outerRidge,new Vector2(size.x*.67f,size.y*.87f),height*1.15f,seed+2.7f,3,28,inland);
            mesh.Land(center,size,height,seed,peaks,46,inland);
            const int canopySamples=236;
            for(int i=0;i<canopySamples;i++)
            {
                float azimuth=i*2.39996f, radius=.78f*Mathf.Sqrt((i+.5f)/canopySamples);
                float x=Mathf.Cos(azimuth)*radius,z=Mathf.Sin(azimuth)*radius;
                float ground=ArtMesh.LandHeight(x,z,seed,peaks)*height;
                if(ground<2.6f||Mathf.PerlinNoise(x*5+seed,z*4.3f+9)<.34f) continue;
                float canopy=Range(random,2.1f,5.2f);
                Vector3 point=center+side*(x*size.x)+inland*(z*size.y)+Vector3.up*(ground-canopy*.26f);
                Color foliage=Color.Lerp(new Color(.025f,.12f,.052f),new Color(.15f,.32f,.069f),Range(random,0,1));
                mesh.Ellipsoid(point,new Vector3(canopy,canopy*.71f,canopy*.95f),foliage,6,3);
            }
            if(lighthouse)
            {
                const float x=.55f,z=-.29f;
                float baseHeight=ArtMesh.LandHeight(x,z,seed,peaks)*height;
                Vector3 foot=center+side*(x*size.x)+inland*(z*size.y)+Vector3.up*(baseHeight-.35f);
                mesh.Tube(foot,foot+Vector3.up*12,3.1f,2.2f,new Color(.92f,.86f,.72f),12);
                mesh.Tube(foot+Vector3.up*12,foot+Vector3.up*15,2.6f,2.6f,new Color(.32f,.51f,.60f),12);
                mesh.Tube(foot+Vector3.up*15,foot+Vector3.up*18,3.65f,.06f,new Color(.83f,.21f,.13f),12);
            }
            Draw(name,mesh,2);
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
            reflection.Dispose();
            reefLight.Dispose();
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
            internal static float LandHeight(float x,float z,float seed,int peaks)
            {
                float coast=Mathf.Sqrt(x*x+z*z);
                if(peaks>1)
                    coast+=(Mathf.PerlinNoise(x*3.9f+seed,z*5.2f+5)-.5f)*.17f;
                float shore=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.56f,.98f,coast));
                float profile=0;
                for(int i=0;i<peaks;i++)
                {
                    float offset=peaks==1?0:Mathf.Lerp(-.51f,.51f,i/(float)(peaks-1));
                    float side=Mathf.Sin(seed*2.14f+i*1.7f)*.22f;
                    float dx=(x-offset)*(2.35f+i*.12f),dz=(z-side)*(2.25f-i*.10f);
                    float peak=Mathf.Exp(-(dx*dx+dz*dz))*(.65f+.32f*Mathf.Sin(seed+i*1.72f)*.5f+.16f);
                    profile=Mathf.Max(profile,peak);
                }
                float folds=Mathf.PerlinNoise(x*6.1f+seed*2,z*6.8f+11.2f)-.5f;
                float strata=Mathf.Sin(x*24+z*13+folds*2)*.048f;
                float terrain=profile*.92f+folds*.20f+strata+.105f;
                if(peaks>1)
                {
                    float ridge=1-Mathf.Abs(Mathf.PerlinNoise(x*8.2f+seed,z*9.1f+5)*2-1);
                    float cliff=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.43f,.72f,terrain));
                    terrain+=cliff*(ridge*ridge*.19f+Mathf.Sin(terrain*32)*.027f);
                }
                return Mathf.Max(0,terrain)*shore;
            }

            public void Land(Vector3 center,Vector2 radii,float height,float seed,int peaks,int resolution,
                Vector3 inland=default)
            {
                if(inland==Vector3.zero) inland=Vector3.forward;
                Vector3 side=Vector3.Cross(Vector3.up,inland);
                int first=vertices.Count, stride=resolution+1;
                for(int row=0;row<=resolution;row++)
                for(int column=0;column<=resolution;column++)
                {
                    float x=column*2f/resolution-1,z=row*2f/resolution-1;
                    float y=LandHeight(x,z,seed,peaks)*height;
                    float slopeX=(LandHeight(x+.012f,z,seed,peaks)-LandHeight(x-.012f,z,seed,peaks))*height/(.024f*radii.x);
                    float slopeZ=(LandHeight(x,z+.012f,seed,peaks)-LandHeight(x,z-.012f,seed,peaks))*height/(.024f*radii.y);
                    float steep=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,1.09f,Mathf.Sqrt(slopeX*slopeX+slopeZ*slopeZ)));
                    float patch=Mathf.PerlinNoise(x*13+seed,z*11+7);
                    Color canopy=Color.Lerp(new Color(.12f,.32f,.10f),new Color(.48f,.62f,.18f),patch);
                    Color cliff=Color.Lerp(new Color(.43f,.32f,.27f),new Color(.82f,.62f,.42f),patch);
                    Color earth=Color.Lerp(canopy,cliff,steep);
                    Color tint=Color.Lerp(Sand,earth,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,height*.29f,y)));
                    if(peaks>1)
                    {
                        canopy=Color.Lerp(new Color(.038f,.12f,.039f),new Color(.16f,.35f,.073f),patch);
                        cliff=Color.Lerp(new Color(.16f,.099f,.069f),new Color(.43f,.255f,.14f),patch);
                        float exposure=steep*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.23f,.62f,patch));
                        earth=Color.Lerp(canopy,cliff,exposure);
                        tint=Color.Lerp(new Color(.48f,.31f,.15f),earth,
                            Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,4.2f,y)));
                    }
                    Point(center+side*(x*radii.x)+inland*(z*radii.y)+Vector3.up*y,tint,Vector2.zero);
                }
                for(int row=0;row<resolution;row++)
                for(int column=0;column<resolution;column++)
                {
                    int a=first+row*stride+column;
                    Triangle(a,a+stride,a+1);
                    Triangle(a+1,a+stride,a+stride+1);
                }
            }

            public void Stone(Vector3 center,Vector3 scale,Color color,float seed)
            {
                const int segments=16, rings=9;
                int start=vertices.Count;
                for(int r=0;r<=rings;r++)
                for(int s=0;s<segments;s++)
                {
                    float lat=r*Mathf.PI/rings, angle=s*Mathf.PI*2/segments;
                    Vector3 n=new(Mathf.Sin(lat)*Mathf.Cos(angle),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(angle));
                    float profile=Mathf.Sin(n.x*5.4f+n.z*4.2f+seed)*Mathf.Cos(n.y*4.1f-n.z*3.1f);
                    float radius=1+profile*.17f;
                    float stripes=Mathf.Sin(n.y*13+profile*1.6f+seed);
                    Color tint=color*Mathf.Lerp(.66f,1.22f,Mathf.Clamp01(.53f+stripes*.21f+profile*.22f));
                    Point(center+Vector3.Scale(n,scale)*radius,tint,Vector2.zero);
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
            public void CoralTube(Vector3 start,Vector3 control,Vector3 end,float width,Color color)
            {
                const int segments=9, rings=6;
                Vector3 seed=Vector3.Cross((end-start).normalized,Vector3.forward).normalized;
                if(seed.sqrMagnitude<.1f) seed=Vector3.right;
                int first=vertices.Count;
                for(int r=0;r<=rings;r++)
                {
                    float t=r/(float)rings;
                    Vector3 center=Vector3.Lerp(Vector3.Lerp(start,control,t),Vector3.Lerp(control,end,t),t);
                    Vector3 tangent=Vector3.Lerp(control-start,end-control,t).normalized;
                    Vector3 right=Vector3.ProjectOnPlane(seed,tangent).normalized;
                    Vector3 side=Vector3.Cross(tangent,right);
                    float radius=width*Mathf.Lerp(1,.54f,t)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1,t)));
                    Color tint=Color.Lerp(color*.83f,Color.Lerp(color,new Color(1,.58f,.52f),.28f),t);
                    for(int s=0;s<segments;s++)
                    {
                        float theta=s*Mathf.PI*2/segments;
                        Vector3 normal=right*Mathf.Cos(theta)+side*Mathf.Sin(theta);
                        Point(center+normal*radius,tint,new Vector2(0,1));
                    }
                }
                Grid(first,segments,rings);
            }

            public void Shell(Vector3 foot,float width,float bearing,Color color)
            {
                const int ribs=14, rings=5, stride=ribs+1;
                int first=vertices.Count;
                Vector3 forward=new(Mathf.Sin(bearing),0,Mathf.Cos(bearing));
                Vector3 right=Vector3.Cross(Vector3.up,forward);
                for(int row=0;row<=rings;row++)
                for(int rib=0;rib<=ribs;rib++)
                {
                    float t=row/(float)rings, angle=Mathf.Lerp(-1.12f,1.12f,rib/(float)ribs);
                    float ridge=rib%2==0?1:.74f;
                    Vector3 center=foot+(forward*Mathf.Cos(angle)+right*Mathf.Sin(angle))*width*t;
                    center.y+=width*.31f*Mathf.Sin(t*Mathf.PI*.78f)*ridge;
                    Point(center,color*Mathf.Lerp(.72f,1.24f,ridge*t),Vector2.zero);
                }
                for(int row=0;row<rings;row++)
                for(int rib=0;rib<ribs;rib++)
                {
                    int a=first+row*stride+rib;
                    Triangle(a,a+stride,a+1);
                    Triangle(a+1,a+stride,a+stride+1);
                }
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
                    float rim=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,1,t));
                    float lips=Mathf.Sin(a*5+offset)*.085f*rim;
                    Color tint=Color.Lerp(color*.73f,Color.Lerp(color,new Color(1,.85f,.36f),.42f),rim);
                    Point(foot+new Vector3(Mathf.Cos(a)*radius,height*(Mathf.Sin(t*Mathf.PI*.78f)+lips),Mathf.Sin(a)*radius),tint,new Vector2(0,1));
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
